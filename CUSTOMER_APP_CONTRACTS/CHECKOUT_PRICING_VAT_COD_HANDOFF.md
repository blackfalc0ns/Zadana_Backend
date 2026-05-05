# Checkout Pricing VAT and COD Handoff

## Status

- `implemented`

## Purpose

This file documents the current backend behavior for checkout pricing after enabling:

- `vat_amount`
- `cod_fee`
- payment-method-aware checkout summary

This is the pricing source of truth for the customer mobile app.

## Scope

Implemented backend coverage in this handoff:

- `GET /api/checkout/summary`
- `POST /api/checkout/promo-code`
- `DELETE /api/checkout/promo-code`
- `POST /api/orders`

Current backend result:

- checkout summary now changes based on selected `payment_method`
- `vat_amount` and `cod_fee` are returned in checkout totals
- the same values are persisted into the created order total during placement

## Canonical Payment Method Codes

Mobile should send these canonical values:

- `card`
- `cash`
- `bank`
- `apple_pay`

Notes:

- `apple_pay` is still returned in payment methods but is currently unavailable
- backend also normalizes aliases like `cod`, `cash_on_delivery`, `credit_card`, and `bank_transfer`
- mobile should still prefer canonical values above in both query params and request bodies

## Main Rule for Mobile

Whenever the user changes the selected payment method, mobile should refresh checkout totals from backend.

Do not keep an old summary when switching from:

- `card` to `cash`
- `cash` to `card`
- `cash` to `bank`

because `cod_fee` may change immediately.

## Get Checkout Summary

### Endpoint

- `GET /api/checkout/summary?vendor_id={vendorId}&address_id={addressId}&delivery_slot_id={slotId}&payment_method={paymentMethod}`

Accepted camelCase aliases:

- `vendorId`
- `addressId`
- `deliverySlotId`
- `paymentMethod`

If `address_id` is omitted, backend uses the default customer address.

### Important Query Param

- `payment_method` is now important for totals
- if you want accurate COD pricing, send the selected payment method every time

Examples:

- card summary:
  - `GET /api/checkout/summary?...&payment_method=card`
- COD summary:
  - `GET /api/checkout/summary?...&payment_method=cash`

## Summary Response Shape

The `summary` object now contains:

- `subtotal`
- `shipping_cost`
- `discount`
- `vat_amount`
- `cod_fee`
- `total`
- `currency`

Example:

```json
{
  "summary": {
    "subtotal": 100.0,
    "shipping_cost": 15.0,
    "discount": 10.0,
    "vat_amount": 15.75,
    "cod_fee": 10.0,
    "total": 130.75,
    "currency": "EGP"
  }
}
```

## Shipping Breakdown Response Shape

`shipping_breakdown` still contains delivery lines, and may now also include:

- `vat`
- `cod_fee`

Possible codes:

- `base_delivery`
- `distance_surcharge`
- `peak_surcharge`
- `vat`
- `cod_fee`

Example COD response:

```json
{
  "shipping_breakdown": [
    {
      "code": "base_delivery",
      "label_ar": "رسوم التوصيل الأساسية",
      "label_en": "Base delivery",
      "amount": 12.0
    },
    {
      "code": "distance_surcharge",
      "label_ar": "رسوم المسافة",
      "label_en": "Distance surcharge",
      "amount": 17.87
    },
    {
      "code": "peak_surcharge",
      "label_ar": "رسوم الذروة",
      "label_en": "Peak surcharge",
      "amount": 0.0
    },
    {
      "code": "vat",
      "label_ar": "ضريبة القيمة المضافة",
      "label_en": "VAT",
      "amount": 6.72
    },
    {
      "code": "cod_fee",
      "label_ar": "رسوم الدفع عند الاستلام",
      "label_en": "Cash on delivery fee",
      "amount": 10.0
    }
  ]
}
```

## Pricing Formula Used by Backend

Current backend formula:

1. `taxable_base = max(0, subtotal + shipping_cost - discount)`
2. `vat_amount = taxable_base * vat_percent / 100` when zone VAT is active
3. `cod_fee` applies only when `payment_method = cash` and COD fee is active for the zone
4. `total = subtotal + shipping_cost - discount + vat_amount + cod_fee`

COD fee logic:

- if zone setting `CodFeeType = flat`, backend uses `CodFlatFee`
- if zone setting `CodFeeType = percent`, backend uses `taxable_base * CodPercent / 100`

## Zone Resolution

VAT and COD settings are resolved from the selected or default delivery address:

- backend first tries geographic zone matching if coordinates exist
- then falls back to a same-city active delivery zone
- if no zone finance settings exist, backend uses the current default finance snapshot

This means mobile should never try to rebuild VAT or COD locally from hardcoded values.

## Promo Code Endpoints

Promo endpoints now also need the current `payment_method` in query when you want accurate totals.

### Apply Promo

- `POST /api/checkout/promo-code?vendor_id={vendorId}&payment_method={paymentMethod}`

Body:

```json
{
  "code": "SAVE10"
}
```

### Remove Promo

- `DELETE /api/checkout/promo-code?vendor_id={vendorId}&payment_method={paymentMethod}`

Important rule:

- after promo apply/remove, use backend `summary` as the new pricing source of truth

## Place Order

### Endpoint

- `POST /api/orders`

Body example:

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "address_id": "22222222-2222-2222-2222-222222222222",
  "delivery_slot_id": "standard-30-45",
  "payment_method": "cash",
  "promo_code": "SAVE10",
  "notes": "Leave at the door"
}
```

Important placement notes:

- backend now persists `vat_amount` and `cod_fee` into the created order pricing
- mobile should still place the order using the same selected `payment_method` used for the final summary refresh

Recommended mobile sequence:

1. user selects address
2. user selects payment method
3. mobile requests checkout summary with that `payment_method`
4. user applies or removes promo code
5. mobile refreshes totals from backend
6. mobile sends `POST /api/orders` with the same `payment_method`

## Rendering Guidance for Mobile

Recommended checkout pricing block:

- subtotal
- base delivery
- distance surcharge
- peak surcharge
- discount
- VAT
- COD fee
- total

UI rules:

- hide `VAT` line if `vat_amount = 0`
- hide `COD fee` line if `cod_fee = 0`
- never hardcode VAT percent or COD amount on device
- always display backend `summary.total` as the final payable amount

## Card vs Cash Behavior

Typical card behavior:

- `vat_amount` may be greater than `0`
- `cod_fee = 0`

Typical cash behavior:

- `vat_amount` may be greater than `0`
- `cod_fee` may be greater than `0`

This is why refreshing summary after payment-method switch is mandatory.

## Important Scope Note

This handoff covers checkout pricing and order placement totals.

Current limitation outside checkout:

- customer order details contract still mainly exposes:
  - `subtotal`
  - `shipping_cost`
  - `total`
- it does not yet expose separate customer-facing `vat_amount` and `cod_fee` lines in the order details response

So for the checkout screen, use this contract.
For post-order details screens, use the current order details contract unless that endpoint is expanded later.
