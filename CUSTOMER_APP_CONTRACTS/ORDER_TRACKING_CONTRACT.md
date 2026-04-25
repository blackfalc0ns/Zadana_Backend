# Customer Order Tracking Contract

## Status

- `implemented`

## Purpose

This contract explains the current customer tracking behavior after the driver assignment, arrival state, and delivery OTP updates.

The backend now returns:

- `assigned_driver`
- `driver_arrival_state`
- `driver_arrival_updated_at_utc`
- `delivery_otp`
- `show_delivery_otp`

## Main Endpoint

### Get Order Tracking

- `GET /api/orders/{orderId}/tracking`

Example response:

```json
{
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "on_the_way"
  },
  "estimated_delivery": {
    "datetime": "2026-04-25T12:15:00Z",
    "formatted": "Today, 3:15 PM"
  },
  "driver": {
    "id": "55555555-5555-5555-5555-555555555555",
    "name": "Ahmed Driver",
    "phone_number": "01289078938",
    "subtitle": "Motorcycle"
  },
  "assigned_driver": {
    "id": "55555555-5555-5555-5555-555555555555",
    "name": "Ahmed Driver",
    "phone_number": "01289078938",
    "vehicle_type": "Motorcycle",
    "plate_number": "XYZ-1234"
  },
  "driver_arrival_state": "arrived_at_customer",
  "driver_arrival_updated_at_utc": "2026-04-25T11:58:00Z",
  "delivery_otp": "4821",
  "show_delivery_otp": true,
  "timeline": [
    {
      "id": "placed",
      "title": "Order placed",
      "time": "11:00 AM",
      "is_active": false,
      "is_completed": true
    },
    {
      "id": "on_the_way",
      "title": "Driver is on the way",
      "time": "11:40 AM",
      "is_active": true,
      "is_completed": true
    }
  ]
}
```

## Field Meaning

### `driver`

Use for the lightweight tracking card:

- name
- phone number
- subtitle

### `assigned_driver`

Use when the UI needs richer assignment details:

- name
- phone number
- vehicle type
- plate number

### `driver_arrival_state`

Current values from backend behavior:

- `en_route`
- `arrived_at_vendor`
- `arrived_at_customer`

### `delivery_otp` and `show_delivery_otp`

Rules:

- only show the OTP to the customer when `show_delivery_otp = true`
- if `show_delivery_otp = false`, the mobile UI must hide the OTP block
- `delivery_otp` is intended to be shared with the driver on arrival

## UI Guidance

Suggested order tracking sections:

- order status
- estimated delivery
- driver card
- delivery OTP card
- timeline

OTP card display rule:

- visible only when `show_delivery_otp = true`

Arrival state hint:

- `en_route` -> driver on the way
- `arrived_at_vendor` -> driver reached merchant
- `arrived_at_customer` -> driver reached customer

## Important Mobile Notes

- Treat backend tracking response as the source of truth
- Do not infer OTP visibility from order status alone
- Do not infer driver arrival state locally
- Use `assigned_driver` for detailed driver identity
- Use `driver` for compact tracking widgets if needed
