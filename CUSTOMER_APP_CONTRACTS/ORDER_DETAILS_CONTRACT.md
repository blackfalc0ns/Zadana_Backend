# Customer Order Details Contract

## Status

- `implemented`

## Purpose

This contract describes the current customer order details endpoint used for order summary screens outside the live tracking view.

## Main Endpoint

### Get Order Detail

- `GET /api/orders/{orderId}`

Example response:

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "created_at": "2026-04-25T11:00:00Z",
  "total_price": 133.5,
  "status": "on_the_way",
  "payment_status": "paid",
  "payment_method": "card",
  "can_retry_payment": false,
  "can_delete": false,
  "can_cancel": false,
  "items_count": 2,
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 28.5,
    "total": 133.5
  },
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Olive Oil 1L",
      "quantity": 2,
      "price": 52.5
    }
  ]
}
```

## UI Notes

- Use this endpoint for the order details screen
- Use `/api/orders/{orderId}/tracking` for live delivery movement, OTP, and driver arrival state
- `summary.shipping_cost` is the customer-facing shipping amount

## Action Flags

- `can_retry_payment`
- `can_delete`
- `can_cancel`

Mobile should trust these flags directly instead of deriving behavior from status alone.
