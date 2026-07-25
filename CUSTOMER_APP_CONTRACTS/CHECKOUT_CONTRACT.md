# Customer Checkout Contract

## Status

- `implemented`

## Purpose

This contract explains the current checkout summary behavior after the delivery pricing upgrade and pickup fulfillment support.

For the latest implemented pricing additions related to:

- `payment_method` aware totals
- `vat_amount`
- `cod_fee`

see:

- `CHECKOUT_PRICING_VAT_COD_HANDOFF.md`

The backend now returns:

- `delivery_check`
- `estimated_delivery_window`
- `delivery_quote`
- `shipping_breakdown`
- `pricing_mode`
- `summary.shipping_cost`

Mobile should render the delivery fee from these backend values, not from local calculations.

For pickup checkout, the backend also returns:

- `fulfillment_type`
- `pickup_branch`
- pickup-only payment methods from checkout summary
- platform pickup/delivery toggles from `GET /api/checkout/config`

## Checkout Config Endpoint

### Get Checkout Config

- `GET /api/checkout/config`
- Auth: optional (`AllowAnonymous`)
- Use on checkout entry to decide whether delivery and/or pickup tiles should appear

Example response:

```json
{
  "delivery_enabled": true,
  "pickup_enabled": true,
  "pickup_cash_on_pickup_enabled": false,
  "allowed_payments_for_pickup": ["card", "apple_pay"]
}
```

Field meaning:

- `delivery_enabled`: when `false`, hide the delivery fulfillment tile entirely
- `pickup_enabled`: when `false`, hide the pickup fulfillment tile entirely
- `pickup_cash_on_pickup_enabled`: when `true`, cash is allowed for pickup (also reflected in `allowed_payments_for_pickup`)
- `allowed_payments_for_pickup`: authoritative payment methods for pickup checkout (`card`, `apple_pay`, and optionally `cash`)

Mobile rules:

- Offer only the methods listed in `allowed_payments_for_pickup`
- When cash is enabled: show “Pay on Pickup” / الدفع عند الاستلام
- Bank transfer remains unsupported for pickup
- If `delivery_enabled = false`, do not show delivery as a selectable fulfillment option even if the cart previously used delivery pricing checks

## Fulfillment Selection

Checkout and place-order now support:

- `fulfillment_type`: `"delivery"` or `"pickup"`
- `vendor_branch_id`: required when `fulfillment_type = "pickup"`

Accepted request/query aliases:

- `fulfillmentType`
- `vendorBranchId`

Pickup summary query example:

- `GET /api/checkout/summary?vendor_id={vendorId}&fulfillment_type=pickup&vendor_branch_id={branchId}&payment_method=card`

Place-order pickup body fields:

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "fulfillment_type": "pickup",
  "vendor_branch_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "payment_method": "card",
  "promo_code": null,
  "notes": null
}
```

Pickup mobile notes:

- `address_id` is not required for pickup checkout
- `delivery_slot_id` is ignored for pickup
- `summary.shipping_cost` is `0` for pickup
- `delivery_check.status` can be `pickup_ready` or `pickup_branch_required`
- Checkout summary includes `pickup_branch` when a branch is selected

## Cart Gate Endpoint

### Get Cart Delivery Check

- `GET /api/cart/delivery-check?vendor_id={vendorId}&address_id={addressId}`
- Mobile camelCase query aliases are also accepted: `vendorId`, `addressId`
- This endpoint is intended for the cart screen before navigation to checkout

Example response:

```json
{
  "address_id": "33333333-3333-3333-3333-333333333333",
  "selected_address": {
    "id": "33333333-3333-3333-3333-333333333333",
    "label": "Home",
    "address_line": "12 Lebanon Sq, Mohandessin",
    "is_default": true
  },
  "delivery_check": {
    "status": "deliverable",
    "is_deliverable": true,
    "can_proceed_to_checkout": true,
    "message": "Delivery is available for this address.",
    "message_ar": "التوصيل متاح لهذا العنوان.",
    "message_en": "Delivery is available for this address.",
    "delivery_fee": 28.5,
    "distance_km": 6.4
  },
  "delivery_quote": {
    "distance_km": 6.4,
    "base_fee": 18.0,
    "distance_fee": 7.5,
    "surge_fee": 3.0,
    "total_fee": 28.5,
    "pricing_mode": "exact-distance",
    "rule_label": "Giza Standard"
  }
}
```

## Main Endpoint

### Get Checkout Summary

- `GET /api/checkout/summary?vendor_id={vendorId}&address_id={addressId}&delivery_slot_id={slotId}`
- Mobile camelCase query aliases are also accepted: `vendorId`, `addressId`, `deliverySlotId`
- If `address_id`/`addressId` is not sent, backend selects the customer's default address and returns its id in `address_id` and `selected_address.id`

Example response:

```json
{
  "cart": {
    "items_count": 2,
    "total_quantity": 3,
    "items": [
      {
        "id": "11111111-1111-1111-1111-111111111111",
        "product_id": "22222222-2222-2222-2222-222222222222",
        "name": "Olive Oil 1L",
        "image_url": "https://cdn.example.com/products/olive-oil.jpg",
        "unit": "piece",
        "quantity": 2,
        "price": 52.5,
        "total_price": 105.0
      }
    ]
  },
  "address_id": "33333333-3333-3333-3333-333333333333",
  "selected_address": {
    "id": "33333333-3333-3333-3333-333333333333",
    "label": "Home",
    "address_line": "12 Lebanon Sq, Mohandessin",
    "is_default": true
  },
  "delivery_slots": [
    {
      "id": "today-6pm",
      "label": "Today 6:00 PM - 7:00 PM",
      "start_at": "2026-04-25T18:00:00Z",
      "end_at": "2026-04-25T19:00:00Z",
      "is_available": true,
      "is_selected": true
    }
  ],
  "payment_methods": [
    {
      "code": "cash",
      "label": "Cash on delivery",
      "is_available": true,
      "is_default": true
    }
  ],
  "promo_code": null,
  "estimated_delivery_window": {
    "min_minutes": 45,
    "max_minutes": 60,
    "title": "Estimated delivery time",
    "label": "45-60 minutes",
    "subtitle": "This estimate will be updated as the order progresses.",
    "confidence": "high",
    "source": "hybrid_operational",
    "is_approximate": false
  },
  "delivery_check": {
    "status": "deliverable",
    "is_deliverable": true,
    "can_proceed_to_checkout": true,
    "message": "Delivery is available for this address.",
    "message_ar": "التوصيل متاح لهذا العنوان.",
    "message_en": "Delivery is available for this address.",
    "delivery_fee": 28.5,
    "distance_km": 6.4
  },
  "delivery_quote": {
    "distance_km": 6.4,
    "base_fee": 18.0,
    "distance_fee": 7.5,
    "surge_fee": 3.0,
    "total_fee": 28.5,
    "pricing_mode": "exact-distance",
    "rule_label": "Giza Standard"
  },
  "shipping_breakdown": [
    {
      "code": "base_delivery",
      "label": "Base delivery",
      "amount": 18.0
    },
    {
      "code": "distance_surcharge",
      "label": "Distance surcharge",
      "amount": 7.5
    },
    {
      "code": "peak_surcharge",
      "label": "Peak surcharge",
      "amount": 3.0
    }
  ],
  "pricing_mode": "exact-distance",
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 28.5,
    "discount": 0.0,
    "total": 133.5,
    "currency": "EGP"
  },
  "fulfillment_type": "delivery",
  "pickup_branch": null
}
```

Pickup summary example:

```json
{
  "fulfillment_type": "pickup",
  "pickup_branch": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "Mohandessin Branch",
    "address_line": "12 Lebanon Sq",
    "city": "Giza",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
  },
  "delivery_check": {
    "status": "pickup_ready",
    "is_deliverable": true,
    "can_proceed_to_checkout": true,
    "message": "Pickup checkout is ready.",
    "message_ar": "يمكن متابعة الطلب للاستلام من الفرع.",
    "message_en": "Pickup checkout is ready.",
    "delivery_fee": 0.0,
    "distance_km": 0.0
  },
  "payment_methods": [
    {
      "code": "card",
      "label": "Credit / Debit Card",
      "is_available": true,
      "is_default": true
    },
    {
      "code": "apple_pay",
      "label": "Apple Pay",
      "is_available": true,
      "is_default": false
    }
  ],
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 0.0,
    "discount": 0.0,
    "total": 105.0,
    "currency": "EGP"
  }
}
```

## Delivery Pricing Rules

- `delivery_check.status` can be:
  - `deliverable`
  - `undeliverable`
  - `address_required`
  - `pricing_unavailable`
- The cart screen must block navigation to checkout when `delivery_check.can_proceed_to_checkout = false`
- If delivery is not allowed, checkout summary still returns cart and address context, but `summary.shipping_cost` becomes `0` and the UI should use `delivery_check` as the source of truth for the blocked state
- `estimated_delivery_window` is the checkout ETA source of truth for the user-facing delivery time
- The backend calibrates checkout ETA from recent delivered orders at the branch level when enough data exists, then falls back to vendor-level history and finally to default policy
- Mobile should render checkout ETA directly from:
  - `estimated_delivery_window.title`
  - `estimated_delivery_window.label`
  - `estimated_delivery_window.subtitle`
- Mobile should not convert `delivery_quote` distance into time on device
- `estimated_delivery_window.confidence` can be:
  - `low`
  - `medium`
  - `high`
- `estimated_delivery_window.source` can be:
  - `hybrid_operational`
  - `historical_fallback`
  - `live_tracking_refined`

- `delivery_quote.total_fee` is the official shipping total before discount
- `summary.shipping_cost` is the shipping number the UI should display in totals
- `shipping_breakdown` is the recommended UI breakdown
- `pricing_mode` can currently be:
  - `exact-distance`
  - `zone-fallback`
- If no active delivery pricing rule matches the selected/default address, checkout returns a `zone-fallback` quote with zero delivery fee instead of failing the summary response

## Rendering Guidance

Recommended checkout UI lines:

- subtotal
- estimated delivery window
- shipping breakdown lines
- discount
- final total

Suggested mapping:

- `base_delivery` -> base delivery line
- `distance_surcharge` -> distance surcharge line
- `peak_surcharge` -> peak surcharge line

## Pickup Payment Rules

- Pickup supports `card` and `apple_pay` always
- Pickup supports `cash` only when `pickup_cash_on_pickup_enabled = true`
- Selecting cash while cash-on-pickup is disabled → `PICKUP_CASH_DISABLED`
- Selecting bank transfer (or any other unsupported method) → `PICKUP_ONLY_ONLINE_PAYMENT`
- For cash pickup: payment is collected by the merchant at handoff (OTP verify); no online gateway session

## Important Mobile Notes

- Hide the delivery fulfillment tile when `delivery_enabled = false` from checkout config
- Hide the pickup fulfillment tile when `pickup_enabled = false` from checkout config
- Do not calculate shipping on device
- Do not calculate ETA on device
- Do not rebuild delivery quote from address coordinates on mobile
- Always trust:
  - `estimated_delivery_window`
  - `delivery_quote`
  - `shipping_breakdown`
  - `summary`
- When a promo code is applied or removed, refresh totals from backend responses
