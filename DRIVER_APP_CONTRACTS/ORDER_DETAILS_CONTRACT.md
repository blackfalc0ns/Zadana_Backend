# Driver Order Details Contract

## Status

- `implemented`

## Purpose

This file defines the driver app contract for the active assignment screen.

The source of truth on the driver task screen is:

- `assignmentStatus`
- `driverArrivalState`
- `allowedActions`

The mobile app must not infer workflow steps from a local enum when the backend payload already states the current action set.

## Main Endpoints

### 1. Get Assignment Detail

- `GET /api/drivers/assignments/{assignmentId}`

Returns the full operational snapshot for the active driver assignment.

Example response:

```json
{
  "assignmentId": "11111111-1111-1111-1111-111111111111",
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "ORD-10025",
  "assignmentStatus": "Accepted",
  "homeState": "OnMission",
  "allowedActions": [
    "arrived_at_vendor"
  ],
  "vendorName": "Driver Read Vendor",
  "pickupAddress": "Olaya Street",
  "pickupLatitude": 24.7136,
  "pickupLongitude": 46.6753,
  "storePhone": "01000000060",
  "customerName": "Ahmed Customer",
  "deliveryAddress": "Yasmin District",
  "deliveryLatitude": 24.7821,
  "deliveryLongitude": 46.6520,
  "customerPhone": "01000000061",
  "paymentMethod": "CashOnDelivery",
  "codAmount": 60.0,
  "pickupOtpRequired": true,
  "pickupOtpStatus": "pending",
  "deliveryOtpRequired": false,
  "deliveryOtpStatus": "not_required",
  "driverArrivalState": "en_route",
  "orderItems": [
    {
      "name": "Fresh Item",
      "quantity": 2,
      "unitPrice": 50.0,
      "lineTotal": 100.0
    }
  ]
}
```

### 2. Get Current Assignment Envelope

- `GET /api/drivers/assignments/current`

This remains a lightweight check for:

- whether the driver currently has an assignment
- whether the driver is operational or blocked

The detail screen itself should still be built from:

- `GET /api/drivers/assignments/{assignmentId}`

### 3. Driver Actions

Driver lifecycle actions remain separate endpoints:

- `POST /api/drivers/offers/{assignmentId}/accept`
- `POST /api/drivers/offers/{assignmentId}/reject`
- `POST /api/drivers/orders/{orderId}/arrived-at-vendor`
- `POST /api/drivers/orders/{orderId}/picked-up`
- `POST /api/drivers/orders/{orderId}/on-the-way`
- `POST /api/drivers/orders/{orderId}/arrived-at-customer`
- `POST /api/drivers/orders/{orderId}/delivered`
- `POST /api/drivers/orders/{orderId}/delivery-failed`
- `POST /api/drivers/assignments/{assignmentId}/verify-otp`

Primary happy path:

- driver accepts the offer
- driver marks `arrived-at-vendor`
- vendor confirms pickup OTP through `POST /api/vendor/orders/{orderId}/confirm-pickup`
- driver marks `on-the-way`
- driver marks `arrived-at-customer`
- driver verifies delivery OTP through `POST /api/drivers/assignments/{assignmentId}/verify-otp`

## Allowed Actions

Current values that may be returned in `allowedActions`:

- `accept_offer`
- `reject_offer`
- `arrived_at_vendor`
- `mark_on_the_way`
- `arrived_at_customer`
- `verify_delivery_otp`

## Mapping Rules

The mobile app must render the current step from `allowedActions`, not from local assumptions.

Happy-path expectations:

- `OfferSent` -> `accept_offer`, `reject_offer`
- `Accepted` -> `arrived_at_vendor`
- `ArrivedAtVendor` with pending pickup OTP -> no actions; wait for the vendor to confirm pickup OTP
- `PickedUp` before `OnTheWay` -> `mark_on_the_way`
- `PickedUp` after `OnTheWay` -> `arrived_at_customer`
- `ArrivedAtCustomer` -> `verify_delivery_otp`

Examples:

- `["accept_offer", "reject_offer"]` -> show offer controls only
- `["arrived_at_vendor"]` -> show the arrival CTA for the vendor
- `[]` with `assignmentStatus = "ArrivedAtVendor"` -> show waiting state for vendor pickup OTP confirmation
- `["mark_on_the_way"]` -> show the start-delivery CTA
- `["verify_delivery_otp"]` -> show delivery OTP entry

## OTP Notes

Pickup OTP handoff endpoint for the vendor:

- `POST /api/vendor/orders/{orderId}/confirm-pickup`

Request body:

```json
{
  "otpCode": "1234"
}
```

Successful pickup handoff response:

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "assignmentId": "11111111-1111-1111-1111-111111111111",
  "status": "picked_up",
  "message": "Pickup OTP confirmed and order handed off to the driver."
}
```

Delivery OTP verification endpoint:

- `POST /api/drivers/assignments/{assignmentId}/verify-otp`

Request body:

```json
{
  "otpType": "delivery",
  "otpCode": "5678"
}
```

Example success response:

```json
{
  "assignmentId": "11111111-1111-1111-1111-111111111111",
  "orderId": "22222222-2222-2222-2222-222222222222",
  "otpType": "delivery",
  "status": "delivered",
  "message": "Delivery OTP verified and order marked as delivered."
}
```

Legacy compatibility:

- `POST /api/drivers/orders/{orderId}/picked-up`
- `POST /api/drivers/orders/{orderId}/delivered`

These endpoints still exist for backward compatibility, but they are not part of the primary mobile happy path and they are not surfaced through `allowedActions`.

## Realtime Refresh

- hub: `/hubs/notifications`
- event: `ReceiveOrderStatusChanged`
- treat the event as a refresh signal only
- when `payload['orderId']` matches the opened assignment order, refresh:
  - `GET /api/drivers/assignments/{assignmentId}`
  - or `GET /api/drivers/assignments/current`
- keep polling every `10s` as backup while the assignment is active

## Important Mobile Notes

- `assignmentId` is the primary identifier for the assignment detail screen
- do not rely on `orderId` alone for accept and reject actions
- `homeState` can be:
  - `IncomingOffer`
  - `OnMission`
- `pickupOtpRequired` and `deliveryOtpRequired` are the official flags for OTP UI
- the driver app should use realtime as a refresh trigger and keep `GET /api/drivers/assignments/{assignmentId}` as the final source of truth
