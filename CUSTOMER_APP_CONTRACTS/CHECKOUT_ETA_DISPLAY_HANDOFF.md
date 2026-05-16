# Checkout ETA Display Handoff

## Purpose

This file documents only the delivery-time display block used in the checkout screen.

The goal is to let mobile render the ETA section directly from backend response fields without rebuilding text locally.

## Source Field

Use:

- `estimated_delivery_window`

From:

- `GET /api/checkout/summary`

## ETA Display Fields

`estimated_delivery_window` now returns:

- `min_minutes`
- `max_minutes`
- `title`
- `label`
- `subtitle`
- `confidence`
- `source`
- `is_approximate`

## Recommended Checkout Rendering

Render the ETA block in this order:

- `estimated_delivery_window.title`
- `estimated_delivery_window.label`
- `estimated_delivery_window.subtitle`

## Example

```json
{
  "estimated_delivery_window": {
    "min_minutes": 30,
    "max_minutes": 45,
    "title": "وقت التوصيل المتوقع",
    "label": "حوالي 30-45 دقيقة",
    "subtitle": "سيتم تحديث الوقت حسب تقدم الطلب.",
    "confidence": "medium",
    "source": "hybrid_operational",
    "is_approximate": true
  }
}
```

## UI Meaning

- `title`
  - section heading shown above the ETA
- `label`
  - main user-facing delivery-time text
- `subtitle`
  - helper text under the ETA

## Mobile Rules

- Do not calculate ETA locally
- Do not generate alternative text from `min_minutes` and `max_minutes` unless explicitly needed for fallback
- Do not derive delivery time from `distance_km`
- Backend text is the source of truth for checkout ETA display

## Suggested Fallback Behavior

If the mobile client needs a fallback:

- show `label` as the main text
- hide `subtitle` only if it is empty

## Notes

- `label` may already contain approximate wording like `حوالي`
- `subtitle` may vary depending on ETA confidence and approximation mode
- The same ETA display approach can also be reused later in order tracking if needed
