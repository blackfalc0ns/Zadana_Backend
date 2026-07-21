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

## Mobile Rules

### 1. When user selects a size/type

Use the selected item from `variant_options[]` as the source of truth:

```dart
final selected = variantOptions.firstWhere((v) => v.id == selectedVariantId);

final canBuy = selected.isAvailableForPurchase;
final reason = selected.unavailableReason;
final vendorProductId = selected.defaultVendorProductId;
final price = selected.price;
```

Do **not** keep using only root `is_available_for_purchase` after the user changes size.

### 2. Add to cart button

Enable only when:

```text
selectedVariant.is_available_for_purchase == true
AND selectedVariant.default_vendor_product_id != null
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
4. Add to cart uses `default_vendor_product_id` of the selected available variant

## Backend Source

- `src/Zadana.Application/Modules/Catalog/DTOs/ProductDetailsDto.cs`
- `src/Zadana.Application/Modules/Catalog/Queries/Products/GetProductDetails/GetProductDetailsQueryHandler.cs`
- `tests/Zadana.UnitTests/Modules/Catalog/GetProductDetailsQueryHandlerTests.cs`

Test name:

- `Handle_WhenVariantGroupHasInactiveMember_EachVariantHasIndependentAvailability`
