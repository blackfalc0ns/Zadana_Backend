# Customer Order Tracking Contract

## Status

- `implemented`

## Purpose

This contract explains the current customer tracking behavior after the driver assignment, arrival state, delivery OTP, and pickup fulfillment updates.

The backend now returns:

- `fulfillment_type`
- `assigned_driver`
- `driver_arrival_state`
- `driver_arrival_updated_at_utc`
- `delivery_otp`
- `show_delivery_otp`
- `pickup_otp_code`
- `pickup_otp_expires_at_utc`
- `pickup_no_show_deadline_utc`
- `pickup_branch`

## Main Endpoint

### Get Order Tracking

- `GET /api/orders/{orderId}/tracking`

Example response (delivery):

```json
{
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "order_number": "ORD-10245",
    "status": "on_the_way"
  },
  "fulfillment_type": "delivery",
  "estimated_delivery": {
    "datetime": "2026-04-25T12:15:00Z",
    "formatted": "Today, 3:15 PM",
    "window": {
      "min_minutes": 15,
      "max_minutes": 25,
      "label": "15-25 minutes",
      "confidence": "high",
      "source": "live_tracking_refined",
      "is_approximate": false
    }
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
  "pickup_otp_code": null,
  "pickup_otp_expires_at_utc": null,
  "pickup_no_show_deadline_utc": null,
  "pickup_branch": null,
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

Example response (pickup):

```json
{
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "order_number": "ORD-10246",
    "status": "ready_for_pickup"
  },
  "fulfillment_type": "pickup",
  "estimated_delivery": null,
  "driver": null,
  "assigned_driver": null,
  "driver_arrival_state": "",
  "driver_arrival_updated_at_utc": null,
  "delivery_otp": null,
  "show_delivery_otp": false,
  "pickup_otp_code": "4821",
  "pickup_otp_expires_at_utc": "2026-04-25T14:00:00Z",
  "pickup_no_show_deadline_utc": "2026-04-25T18:00:00Z",
  "pickup_branch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
  },
  "timeline": [
    {
      "id": "placed",
      "title": "Order placed",
      "time": "11:00 AM",
      "is_active": false,
      "is_completed": true
    },
    {
      "id": "ready_for_pickup",
      "title": "Ready for pickup",
      "time": "12:10 PM",
      "is_active": true,
      "is_completed": true
    }
  ]
}
```

## Fulfillment-Specific UI Rules

### Delivery orders

Suggested tracking sections:

- order status
- estimated delivery
- estimated delivery window
- driver card
- delivery OTP card
- timeline

### Pickup orders

When `fulfillment_type = "pickup"`:

- show branch address from `pickup_branch.address`
- show branch hours from `pickup_branch.hours_today`
- show pickup OTP card when `pickup_otp_code` is not null
- show pickup deadline from `pickup_no_show_deadline_utc` when present

Do not show for pickup:

- map / live driver location UI
- driver card
- assigned driver card
- call-driver button
- delivery OTP card
- driver arrival state UI

Pickup tracking is branch-and-OTP focused, not driver focused.

## Field Meaning

### `estimated_delivery.window`

Use for the moving delivery estimate window on delivery orders only:

- `min_minutes`
- `max_minutes`
- `label`
- `confidence`
- `source`

If the order is already delivered, backend may return the actual `datetime`/`formatted` value with `window = null`.

For pickup orders, `estimated_delivery` is usually `null`.

### `driver`

Use for the lightweight tracking card on delivery orders:

- name
- phone number
- subtitle

Hidden for pickup.

### `assigned_driver`

Use when the UI needs richer assignment details on delivery orders:

- name
- phone number
- vehicle type
- plate number

Hidden for pickup.

### `driver_arrival_state`

Current values from backend behavior on delivery orders:

- `en_route`
- `arrived_at_vendor`
- `arrived_at_customer`

Not used for pickup tracking UI.

### `delivery_otp` and `show_delivery_otp`

Rules:

- delivery orders only
- only show the OTP to the customer when `show_delivery_otp = true`
- if `show_delivery_otp = false`, the mobile UI must hide the OTP block
- `delivery_otp` is intended to be shared with the driver on arrival

### `pickup_otp_code`, `pickup_otp_expires_at_utc`, `pickup_no_show_deadline_utc`

Rules:

- pickup orders only
- `pickup_otp_code` is returned only while the order is ready for pickup and OTP verification is still pending
- share the OTP with the branch staff on arrival
- use `pickup_no_show_deadline_utc` to show the remaining pickup window

### `pickup_branch`

Use for pickup tracking header/card:

- `name`
- `address`
- `hours_today`

No map coordinates are returned in this contract.

## Important Mobile Notes

- Treat backend tracking response as the source of truth
- Branch on `fulfillment_type` before rendering driver or map sections
- Do not recalculate ETA locally from coordinates or timeline state
- Tracking ETA is progressively refined by stage using live order state plus historical branch/vendor calibration on delivery orders only
- Do not infer OTP visibility from order status alone
- Do not infer driver arrival state locally
- Use `assigned_driver` for detailed driver identity on delivery orders only
