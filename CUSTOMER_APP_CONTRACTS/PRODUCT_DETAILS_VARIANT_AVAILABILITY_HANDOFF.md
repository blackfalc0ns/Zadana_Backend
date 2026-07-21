# Product Details — Independent Variant Availability Handoff

## Status

- `implemented` on backend
- Mobile must update product details screen behavior

## Problem

When a product has multiple sizes/types in the same `variant_group`, disabling one size used to make the whole product non-purchasable in the app.

Example:

- Size `1L` = active
- Size `2L` = inactive

Expected:

- `1L` can be purchased
- `2L` is shown but not purchasable

## Endpoint

- `GET /api/products/{productId}`
- Auth: optional (`AllowAnonymous`)
- `productId` can be:
  - `master_product_id`
  - `vendor_product_id`

## What Changed

Availability is now **per variant**, not per whole product group.

Each item in `variant_options[]` now includes:

- `is_online_now`
- `is_available_for_purchase`
- `unavailable_reason`

Root-level fields:

- `is_available_for_purchase`
- `is_online_now`
- `unavailable_reason`

These root fields describe **only the currently selected variant** (`is_current: true`), not the whole group.

## رسالة للموبايل — ليه زر «أضف للسلة» بيظهر مع حجم غير نشط؟

### المشكلة الحالية في التطبيق

لما المستخدم يفتح منتج فيه أكثر من حجم:

1. الصفحة بتفتح على **حجم نشط** (مثلاً 1L).
2. الـ API بيرجع `is_available_for_purchase: true` على مستوى **root**.
3. المستخدم يختار **حجم غير نشط** (مثلاً 2L) من الـ selector.
4. الحجم **يظهر** في الاختيارات (ده صح — مش المفروض يتخفى).
5. لكن زر «أضف للسلة» **يفضل مفعّل** — وده **غلط**.

### السبب

التطبيق حالياً غالباً بيعمل كده:

```text
فتح الصفحة على 1L  →  root.is_available_for_purchase = true  →  الزر مفعّل
اختيار 2L (غير نشط)  →  التطبيق ما قرأش variant_options[2L]  →  الزر فضل مفعّل ❌
```

يعني: بعد تغيير الحجم، التطبيق **لسه بيعتمد على** `is_available_for_purchase` من **root** (اللي جاي من الحجم الأول)، **مش** من الحجم المختار داخل `variant_options[]`.

### إيه اللي الـ Backend بيرسل للحجم غير النشط؟

```json
{
  "id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
  "display_size_ar": "2 لتر",
  "is_available_for_purchase": false,
  "unavailable_reason": "product_inactive",
  "default_vendor_product_id": null,
  "price": null,
  "vendor_prices": []
}
```

الـ API بيقول بوضوح: الحجم **غير قابل للشراء**. المشكلة في **قراءة التطبيق** للبيانات بعد اختيار الحجم.

### لو المستخدم ضغط «أضف للسلة» فعلاً؟

| السيناريو | النتيجة |
|---|---|
| بعت `master_product_id` للحجم **غير النشط** | الـ Backend **يرفض** — المنتج لازم يكون `Active` |
| لسه بيبعت id الحجم **النشط** رغم إن الشاشة تعرض حجم تاني | ممكن يضيف للسلة **الحجم النشط** — bug في الـ UI/state |

### المطلوب تنفيذه (إلزامي)

عند **كل** تغيير للحجم، حدّث حالة الشاشة من **الحجم المختار** في `variant_options[]`:

```dart
final selected = variantOptions.firstWhere((v) => v.id == selectedVariantId);

final canAddToCart =
    selected.isAvailableForPurchase;

// استخدم selected.price / selected.vendorPrices للعرض
// لا تستخدم root.isAvailableForPurchase بعد تغيير الحجم
```

**قاعدة بسيطة:**

- `selected.is_available_for_purchase == true` → زر «أضف للسلة» **مفعّل**
- `selected.is_available_for_purchase == false` → الزر **معطّل** + اعرض سبب من `unavailable_reason` إن أمكن

### مهم: المعرف المرسل إلى السلة

Endpoint السلة:

```http
POST /api/cart/items
```

يقبل **Master Product ID فقط**. عند الإضافة، أرسل `id` للحجم المختار من
`variant_options[]`، ولا ترسل `default_vendor_product_id`.

```dart
await dio.post(
  '/api/cart/items',
  data: {
    'productId': selected.id, // Master Product ID
    'quantity': quantity,
  },
);
```

`default_vendor_product_id` هو معرّف عرض السعر/المتجر، وليس معرّف إضافة المنتج
إلى السلة. إرساله في `productId` ينتج عنه `404 MASTERPRODUCT_NOT_FOUND` حتى لو
كان المنتج ظاهراً في شاشة التفاصيل.

### Acceptance (للاختبار)

1. منتج فيه 1L نشط + 2L غير نشط.
2. اختيار 1L → الزر مفعّل.
3. اختيار 2L → الزر **معطّل فوراً** (حتى لو root لسه `true` من أول response).
4. 2L يفضل **ظاهر** في الـ selector لكن disabled/greyed out.
5. عند إضافة 1L للسلة، body يرسل `productId: selected.id` وليس
   `selected.defaultVendorProductId`.

## Mobile Rules

### 1. When user selects a size/type

Use the selected item from `variant_options[]` as the source of truth:

```dart
final selected = variantOptions.firstWhere((v) => v.id == selectedVariantId);

final canBuy = selected.isAvailableForPurchase;
final reason = selected.unavailableReason;
final price = selected.price;
```

Do **not** keep using only root `is_available_for_purchase` after the user changes size.

### 2. Add to cart button

Enable only when:

```text
selectedVariant.is_available_for_purchase == true
```

### 3. Show all sizes in the selector

Do not hide inactive/unavailable sizes.

Visual state per size chip/button:

| State | UI |
|---|---|
| `is_available_for_purchase = true` | normal + selectable |
| `is_available_for_purchase = false` | visible but disabled / greyed out |

Optional label by `unavailable_reason`:

| Value | Suggested Arabic label |
|---|---|
| `product_inactive` | غير متاح حالياً |
| `out_of_stock` | غير متوفر |
| `vendor_offline` | المتجر مغلق حالياً |
| `unavailable` | غير متاح |

### 4. Price and vendor prices

When selected variant is unavailable:

- `price` may be `null`
- `default_vendor_product_id` may be `null`
- `vendor_prices` may be empty

Do not show an enabled buy button in this case.

### 5. Re-fetch behavior

If mobile navigates to another variant by calling:

```http
GET /api/products/{master_product_id_of_selected_variant}
```

root fields will match that variant. Still prefer `variant_options[]` when switching locally without a new request.

## Example Response

```json
{
  "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "master_product_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "is_available_for_purchase": true,
  "is_online_now": true,
  "unavailable_reason": null,
  "price": 50,
  "variant_options": [
    {
      "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "display_size_ar": "1 لتر",
      "display_size_en": "1 L",
      "is_current": true,
      "default_vendor_product_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      "price": 50,
      "is_online_now": true,
      "is_available_for_purchase": true,
      "unavailable_reason": null,
      "vendor_prices": [
        {
          "id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          "name": "Green Valley Market",
          "price": 50
        }
      ]
    },
    {
      "id": "cccccccc-cccc-cccc-cccc-cccccccccccc",
      "display_size_ar": "2 لتر",
      "display_size_en": "2 L",
      "is_current": false,
      "default_vendor_product_id": null,
      "price": null,
      "is_online_now": false,
      "is_available_for_purchase": false,
      "unavailable_reason": "product_inactive",
      "vendor_prices": []
    }
  ]
}
```

## Acceptance Criteria

1. Product with one inactive size and one active size:
   - active size → buy button enabled
   - inactive size → buy button disabled
2. Switching between sizes updates buy button state immediately
3. Inactive size remains visible in size selector
4. Add to cart uses `id` (the selected available variant's `master_product_id`)

## Backend Source

- `src/Zadana.Application/Modules/Catalog/DTOs/ProductDetailsDto.cs`
- `src/Zadana.Application/Modules/Catalog/Queries/Products/GetProductDetails/GetProductDetailsQueryHandler.cs`
- `tests/Zadana.UnitTests/Modules/Catalog/GetProductDetailsQueryHandlerTests.cs`

Test name:

- `Handle_WhenVariantGroupHasInactiveMember_EachVariantHasIndependentAvailability`
