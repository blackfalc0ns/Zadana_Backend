# اختيار فرع الاستلام حسب المدينة — هاندوف الموبايل (تحديث فقط)

تاريخ التحديث: 2026-07-25  
الحالة: مطبّق في الباك إند  
الجمهور: مبرمج تطبيق العميل  
النطاق: **هذا الملف يغطي التعديل الجديد فقط** — فروع الاستلام حسب المدينة + توفر منتجات السلة.

Base URL إنتاج: `https://api.zadna0.com`  
Auth: `Authorization: Bearer <access_token>` (دور Customer)

Aliases مقبولة:  
`vendor_id` / `vendorId` ، `address_id` / `addressId` ، `vendor_branch_id` / `vendorBranchId` ، `fulfillment_type` / `fulfillmentType` ، `payment_method` / `paymentMethod` ، `city` / `City`

---

## 1) ماذا تغيّر؟

1. Endpoint جديد يجيب فروع التاجر **في مدينة العميل فقط**.
2. كل فرع عليه علم `can_fulfill_cart` هل منتجات السلة الحالية متوفرة فيه.
3. لازم تبعت `vendor_branch_id` بعد الاختيار في:
   - `GET /api/checkout/summary`
   - `POST /api/orders`
4. من غير `vendor_branch_id` هترجع:
   - `delivery_check.status = "pickup_branch_required"`
   - `pickup_branch = null`
   - `can_proceed_to_checkout = false`

---

## 2) Endpoint الجديد

```http
GET /api/checkout/pickup-branches?vendor_id={vendorId}&city={city}
```

أو بدل المدينة:

```http
GET /api/checkout/pickup-branches?vendor_id={vendorId}&address_id={addressId}
```

| Query | مطلوب؟ | المعنى |
|---|---|---|
| `vendor_id` | نعم | التاجر |
| `city` | واحد من الاتنين | مدينة العميل (مثال: `الرياض`) |
| `address_id` | واحد من الاتنين | عنوان العميل — المدينة تُقرأ منه |

لو مفيش `city` ولا `address_id` → خطأ `PICKUP_CITY_REQUIRED`.

### Response مثال

```json
{
  "vendor_id": "0f42e51e-5252-4aa5-ae79-704478ae9b24",
  "city": "الرياض",
  "branches": [
    {
      "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "name": "فرع العليا",
      "address_line": "طريق الملك فهد",
      "city": "الرياض",
      "address": "طريق الملك فهد, الرياض",
      "hours_today": "10:00 - 22:00",
      "is_primary": true,
      "can_fulfill_cart": true,
      "missing_items_count": 0
    },
    {
      "id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      "name": "فرع النسيم",
      "address_line": "شارع النسيم",
      "city": "الرياض",
      "address": "شارع النسيم, الرياض",
      "hours_today": "09:00 - 23:00",
      "is_primary": false,
      "can_fulfill_cart": false,
      "missing_items_count": 1
    }
  ]
}
```

### معنى الحقول

| الحقل | الاستخدام في الواجهة |
|---|---|
| `id` | قيمة `vendor_branch_id` بعد الاختيار |
| `name` | اسم الفرع |
| `address` | عنوان المتجر للعرض (مفضّل) |
| `address_line` + `city` | بديل لو حابب تركّب العنوان بنفسك |
| `hours_today` | مواعيد اليوم (قد تكون `null`) |
| `is_primary` | فرع أساسي (للاختيار التلقائي عند التعادل) |
| `can_fulfill_cart` | `true` = كل منتجات السلة متوفرة في الفرع |
| `missing_items_count` | عدد المنتجات الناقصة لو `can_fulfill_cart = false` |

الترتيب من السيرفر: المتاح أولًا (`can_fulfill_cart`) ثم الأساسي ثم الاسم.

---

## 3) قواعد واجهة الموبايل

1. لما العميل يختار **استلام من الفرع**:
   - خد مدينة العميل من عنوانه (أو ابعت `address_id`).
   - نادِ `GET /api/checkout/pickup-branches`.
2. اعرض الفروع.
   - **موصى به:** امنع اختيار فرع `can_fulfill_cart = false` أو وضّح «غير متوفر لكل المنتجات».
3. لو فرع واحد فقط `can_fulfill_cart = true` → اختَره تلقائيًا بدون شاشة اختيار.
4. لو مفيش فروع في المدينة أو مفيش فرع يوفّر السلة → رسالة واضحة ومنع إتمام الطلب.
5. بعد الاختيار نادِ الـ summary **مع** الفرع:

```http
GET /api/checkout/summary?vendor_id={vendorId}&fulfillment_type=pickup&vendor_branch_id={branchId}&payment_method=card
```

6. عند إنشاء الطلب ابعت نفس الفرع:

```json
{
  "vendor_id": "...",
  "fulfillment_type": "pickup",
  "vendor_branch_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "payment_method": "card"
}
```

---

## 4) شكل `pickup_branch` بعد الاختيار (Summary)

لو الفرع متبعت صح:

```json
{
  "fulfillment_type": "pickup",
  "delivery_check": {
    "status": "pickup_ready",
    "can_proceed_to_checkout": true,
    "message_ar": "يمكن متابعة الطلب للاستلام من الفرع."
  },
  "pickup_branch": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "فرع العليا",
    "address_line": "طريق الملك فهد",
    "city": "الرياض",
    "address": "طريق الملك فهد, الرياض",
    "hours_today": "10:00 - 22:00"
  },
  "summary": {
    "subtotal": 60.0,
    "shipping_cost": 0,
    "discount": 0,
    "vat_amount": 9.0,
    "cod_fee": 0,
    "total": 69.0,
    "currency": "SAR"
  }
}
```

اعرض بطاقة الفرع من `pickup_branch.address` (+ `hours_today` إن وُجد).  
`summary.shipping_cost` دائمًا `0` في الـ pickup.

### لو نسيت `vendor_branch_id` (غلط شائع)

```json
{
  "fulfillment_type": "pickup",
  "delivery_check": {
    "status": "pickup_branch_required",
    "can_proceed_to_checkout": false,
    "message_ar": "يرجى اختيار فرع الاستلام."
  },
  "pickup_branch": null
}
```

---

## 5) Place Order — `pickup_branch` في الرد

رد `POST /api/orders` بقى يشمل الفرع المختار:

```json
{
  "message": "...",
  "order": {
    "id": "...",
    "created_at": "2026-07-25T16:00:00Z",
    "status": "pending",
    "payment_method": "card",
    "payment_status": "pending",
    "total_price": 69.0,
    "fulfillment_type": "pickup",
    "pickup_branch": {
      "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "name": "فرع العليا",
      "address_line": "طريق الملك فهد",
      "city": "الرياض",
      "address": "طريق الملك فهد, الرياض",
      "hours_today": "10:00 - 22:00"
    }
  },
  "payment": { }
}
```

---

## 6) أخطاء مهمة

| Code | متى | ماذا تفعل |
|---|---|---|
| `PICKUP_CITY_REQUIRED` | مفيش city ولا address_id | اطلب مدينة/عنوان |
| `PICKUP_BRANCH_REQUIRED` | place من غير فرع | ارجع لشاشة اختيار الفرع |
| `PICKUP_BRANCH_INACTIVE` / `PICKUP_BRANCH_INVALID` | فرع غير صالح | أعد تحميل `pickup-branches` |
| `CART_ITEMS_UNAVAILABLE_AT_ADDRESS_BRANCH` | الفرع المختار مش بيغطي السلة | اختَر فرع `can_fulfill_cart=true` |

---

## 7) تدفق سريع

```text
Checkout → استلام من الفرع
  → GET /api/checkout/pickup-branches?vendor_id=...&city=...
  → عرض الفروع (can_fulfill_cart)
  → اختيار فرع (أو auto لو واحد)
  → GET /api/checkout/summary?...&fulfillment_type=pickup&vendor_branch_id=...
  → عرض عنوان الفرع من pickup_branch.address
  → POST /api/orders بنفس vendor_branch_id
```

---

## 8) Checklist

- [ ] استدعاء `pickup-branches` عند اختيار وضع الاستلام
- [ ] تمرير `city` أو `address_id`
- [ ] منع/تحذير فروع `can_fulfill_cart = false`
- [ ] auto-select لو فرع متاح واحد
- [ ] تمرير `vendor_branch_id` في summary و place
- [ ] عرض `pickup_branch.address` (+ `hours_today`) في شاشة الملخص
- [ ] التعامل مع `pickup_branch_required` لو الفرع ناقص

---

## 9) Endpoints هذا التحديث

| Method | Path | الغرض |
|---|---|---|
| GET | `/api/checkout/pickup-branches` | فروع المدينة + توفر السلة **(جديد)** |
| GET | `/api/checkout/summary` | لازم `vendor_branch_id` للـ pickup عشان يرجع العنوان |
| POST | `/api/orders` | لازم `vendor_branch_id` + يرجع `order.pickup_branch` |
