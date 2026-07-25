# Customer Order Details Contract

## Status

- `implemented`

## Purpose

This contract describes the current customer order details endpoint used for order summary screens outside the live tracking view.

Pickup orders expose branch context and customer pickup OTP fields only while the order is `ready_for_pickup` and the OTP has not yet been verified.

## Main Endpoint

### Get Order Detail

- `GET /api/orders/{orderId}`

Example response (delivery):

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "order_number": "ORD-10245",
  "created_at": "2026-04-25T11:00:00Z",
  "total_price": 133.5,
  "status": "on_the_way",
  "payment_status": "paid",
  "payment_method": "card",
  "fulfillment_type": "delivery",
  "can_retry_payment": false,
  "can_delete": false,
  "can_cancel": false,
  "items_count": 2,
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 28.5,
    "total": 133.5
  },
  "pickup_otp_code": null,
  "pickup_otp_expires_at_utc": null,
  "pickup_no_show_deadline_utc": null,
  "pickup_branch": null,
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

Example response (pickup, ready for customer collection):

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "order_number": "ORD-10246",
  "created_at": "2026-04-25T11:00:00Z",
  "total_price": 105.0,
  "status": "ready_for_pickup",
  "payment_status": "paid",
  "payment_method": "card",
  "fulfillment_type": "pickup",
  "can_retry_payment": false,
  "can_delete": false,
  "can_cancel": true,
  "items_count": 2,
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 0.0,
    "total": 105.0
  },
  "pickup_otp_code": "4821",
  "pickup_otp_expires_at_utc": "2026-04-25T14:00:00Z",
  "pickup_no_show_deadline_utc": "2026-04-25T18:00:00Z",
  "pickup_branch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
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

## Pickup Field Rules

### `fulfillment_type`

- `delivery`
- `pickup`

Use this to switch the details layout between delivery summary and pickup summary.

### `pickup_otp_code`, `pickup_otp_expires_at_utc`, `pickup_no_show_deadline_utc`

Rules:

- returned only to the authenticated customer who owns the order
- `pickup_otp_code` is populated only when:
  - `fulfillment_type = "pickup"`
  - `status = "ready_for_pickup"`
  - pickup OTP has not yet been verified
- after OTP verification or order completion, these fields become `null`
- do not infer OTP visibility from status alone; trust the nullable response fields

### `pickup_branch`

Rules:

- populated for pickup orders when branch context is available
- intended for customer display while the order is ready for pickup
- shape:
  - `name`
  - `address`
  - `hours_today`

Resend OTP endpoint:

- `POST /api/orders/{orderId}/resend-pickup-otp`
- regenerates OTP server-side
- customer must refresh order details or wait for realtime update to see the new code

## UI Notes

- Use this endpoint for the order details screen
- Use `/api/orders/{orderId}/tracking` for live delivery movement, OTP, and driver arrival state
- For pickup orders, order details is the primary screen for branch info and pickup OTP
- `summary.shipping_cost` is the customer-facing shipping amount and is `0` for pickup

## Action Flags

- `can_retry_payment`
- `can_delete`
- `can_cancel`

Mobile should trust these flags directly instead of deriving behavior from status alone.

For pickup cancellation behavior, see `ORDER_CANCELLATION_CONTRACT.md`.
