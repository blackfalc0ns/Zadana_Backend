# Customer Checkout Contract

## Status

- `implemented`

## Purpose

This contract explains the current checkout summary behavior after the delivery pricing upgrade.

The backend now returns:

- `delivery_quote`
- `shipping_breakdown`
- `pricing_mode`
- `summary.shipping_cost`

Mobile should render the delivery fee from these backend values, not from local calculations.

## Main Endpoint

### Get Checkout Summary

- `GET /api/checkout/summary?vendor_id={vendorId}&address_id={addressId}&delivery_slot_id={slotId}`

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
  }
}
```

## Delivery Pricing Rules

- `delivery_quote.total_fee` is the official shipping total before discount
- `summary.shipping_cost` is the shipping number the UI should display in totals
- `shipping_breakdown` is the recommended UI breakdown
- `pricing_mode` can currently be:
  - `exact-distance`
  - `zone-fallback`

## Rendering Guidance

Recommended checkout UI lines:

- subtotal
- shipping breakdown lines
- discount
- final total

Suggested mapping:

- `base_delivery` -> base delivery line
- `distance_surcharge` -> distance surcharge line
- `peak_surcharge` -> peak surcharge line

## Important Mobile Notes

- Do not calculate shipping on device
- Do not rebuild delivery quote from address coordinates on mobile
- Always trust:
  - `delivery_quote`
  - `shipping_breakdown`
  - `summary`
- When a promo code is applied or removed, refresh totals from backend responses
