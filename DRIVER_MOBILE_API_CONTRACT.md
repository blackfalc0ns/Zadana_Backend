# Driver Mobile API Contract

This document explains how the driver mobile app should integrate with the current Zadana backend delivery APIs.

It is focused on the driver app flow only:

- driver registration
- zone selection
- availability updates
- GPS location updates
- current assignment and assignment history
- proof of delivery
- driver order status updates

This document matches the current backend implementation under:

- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs`
- `src/Zadana.Application/Modules/Delivery`
- `src/Zadana.Application/Modules/Orders/Commands/DriverUpdateOrderStatus`

## Scope

The driver app can integrate these APIs today:

1. Public driver registration
2. Authenticated driver operational APIs
3. Delivery proof upload metadata
4. Driver delivery status transitions

Important:

- The admin approval flow exists in the backend, but it is handled by the superadmin APIs, not by the driver app.
- A driver can only become available after admin approval.
- The driver must submit photo proof before marking an order as delivered.
- The driver must provide a failure note before marking delivery as failed.

## Base Requirements

All authenticated endpoints in this guide require:

- a valid authenticated driver account
- `Authorization: Bearer <access_token>`

These APIs are protected by the `DriverOnly` policy unless noted otherwise.

## Driver Verification Lifecycle

The driver app should treat the account as moving through these states:

- `NeedsDocuments`: one or more required documents were not provided
- `UnderReview`: all required documents were uploaded and the driver is waiting for admin review
- `Approved`: driver can be activated and can go available
- `Rejected`: driver was rejected and cannot enter dispatch

Operational rule:

- `PUT /api/drivers/me/availability` with `isAvailable = true` only succeeds when the driver is both `Approved` and `Active`

## Required Driver Documents

The current registration flow supports these document URLs:

- `nationalIdImageUrl`
- `licenseImageUrl`
- `vehicleImageUrl`
- `personalPhotoUrl`

If all four are present at registration time, the backend starts the driver in `UnderReview`.

If one or more are missing, the backend starts the driver in `NeedsDocuments`.

## Main Mobile Flow

Recommended app flow:

1. Register the driver
2. Save tokens returned by registration
3. Fetch active zones
4. Let the driver choose one active zone from a dropdown
5. Keep the app in waiting mode until the superadmin approves the driver
6. Once approved, allow the driver to toggle availability on
7. Send periodic GPS updates while online or on an active assignment
8. Fetch current assignment
9. Submit photo proof before calling delivered
10. Update order status through pickup, on-the-way, and delivery completion or failure

## Auth Response Shape

Driver registration currently returns `AuthResponseDto`.

Example shape:

```json
{
  "tokens": {
    "accessToken": "jwt-access-token",
    "refreshToken": "jwt-refresh-token"
  },
  "user": {
    "id": "11111111-1111-1111-1111-111111111111",
    "fullName": "Driver Name",
    "email": "driver@example.com",
    "phone": "+201000000000",
    "role": "Driver",
    "favoritesCount": 0
  },
  "isVerified": true,
  "message": null
}
```

## Endpoints

### 1. Register Driver

```http
POST /api/drivers/register
```

Authentication:

- public

Request body:

```json
{
  "fullName": "Ahmed Driver",
  "email": "ahmed.driver@example.com",
  "phone": "+201001112233",
  "password": "StrongPassword123!",
  "vehicleType": "Motorcycle",
  "nationalId": "29801011234567",
  "licenseNumber": "CAI-DRV-4421",
  "address": "Nasr City, Cairo",
  "nationalIdImageUrl": "https://cdn.example.com/driver/national-id.jpg",
  "licenseImageUrl": "https://cdn.example.com/driver/license.jpg",
  "vehicleImageUrl": "https://cdn.example.com/driver/vehicle.jpg",
  "personalPhotoUrl": "https://cdn.example.com/driver/photo.jpg"
}
```

Notes:

- `vehicleType`, `nationalId`, `licenseNumber`, `address`, and document URLs are nullable in the current API contract
- mobile should still send all available data to avoid landing in `NeedsDocuments`

Success response:

- `200 OK`
- returns `AuthResponseDto`

### 2. Get Active Zones

```http
GET /api/drivers/zones
```

Authentication:

- driver only

Success response:

```json
[
  {
    "id": "22222222-2222-2222-2222-222222222222",
    "city": "Cairo",
    "name": "Nasr City East",
    "centerLat": 30.0626,
    "centerLng": 31.2497,
    "radiusKm": 8,
    "isActive": true
  }
]
```

Notes:

- only active zones are returned
- mobile should use this list to populate the driver zone dropdown

### 3. Set Driver Zone

```http
PUT /api/drivers/me/zone
```

Authentication:

- driver only

Request body:

```json
{
  "zoneId": "22222222-2222-2222-2222-222222222222"
}
```

Success response:

```json
{
  "message": "Zone updated successfully"
}
```

Notes:

- only an active zone can be assigned
- v1 supports one primary zone per driver

### 4. Set Driver Availability

```http
PUT /api/drivers/me/availability
```

Authentication:

- driver only

Request body:

```json
{
  "isAvailable": true
}
```

Success response:

```json
{
  "message": "Availability set to True"
}
```

Business rule:

- enabling availability fails unless the driver is already `Approved` and `Active`

Common failure:

```json
{
  "code": "DRIVER_NOT_READY_FOR_DISPATCH",
  "message": "Only approved active drivers can enable availability."
}
```

### 5. Update Driver Location

```http
POST /api/drivers/location
```

Authentication:

- driver only

Request body:

```json
{
  "latitude": 30.0444,
  "longitude": 31.2357
}
```

Success response:

```json
{
  "message": "Location updated"
}
```

Validation rules:

- latitude must be between `-90` and `90`
- longitude must be between `-180` and `180`

Recommended mobile behavior:

- send location periodically while available
- send location more frequently while the driver has an active assignment

### 6. Get Current Assignment

```http
GET /api/drivers/assignments/current
```

Authentication:

- driver only

No active assignment response:

```json
{
  "hasAssignment": false
}
```

Active assignment response:

```json
{
  "hasAssignment": true,
  "assignment": {
    "id": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "orderNumber": "ORD-10245",
    "status": "Accepted",
    "codAmount": 120.50,
    "createdAtUtc": "2026-04-23T10:15:00Z"
  }
}
```

Notes:

- current implementation returns the latest non-terminal assignment
- terminal statuses are excluded from this endpoint

### 7. Get Assignment History

```http
GET /api/drivers/assignments/history
```

Authentication:

- driver only

Success response:

```json
[
  {
    "id": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "orderNumber": "ORD-10245",
    "status": "Delivered",
    "acceptedAtUtc": "2026-04-23T10:18:00Z",
    "deliveredAtUtc": "2026-04-23T10:52:00Z",
    "failedAtUtc": null,
    "failureReason": null,
    "codAmount": 120.50
  }
]
```

Notes:

- the backend currently returns the latest 50 assignments for the authenticated driver

### 8. Submit Delivery Proof

```http
POST /api/drivers/assignments/{assignmentId}/proof
```

Authentication:

- driver only

Request body for photo proof:

```json
{
  "proofType": "photo",
  "imageUrl": "https://cdn.example.com/proofs/order-10245.jpg",
  "otpCode": null,
  "recipientName": "Mohamed Ali",
  "note": "Delivered to building reception"
}
```

Request body for OTP proof:

```json
{
  "proofType": "otp",
  "imageUrl": null,
  "otpCode": "481920",
  "recipientName": "Mohamed Ali",
  "note": "Customer requested OTP verification"
}
```

Success response:

```json
{
  "id": "55555555-5555-5555-5555-555555555555",
  "message": "Proof submitted successfully"
}
```

Validation rules:

- allowed `proofType` values: `image`, `photo`, `otp`
- `imageUrl` is required for `image` and `photo`
- `otpCode` is required for `otp`

Business rules:

- the assignment must belong to the authenticated driver
- proof can only be submitted for active assignments
- current backend allows proof submission when assignment status is `Accepted`, `PickedUp`, or `ArrivedAtCustomer`

Mobile recommendation:

- use `photo` as the default proof type in v1
- submit proof before calling the delivered endpoint

### 9. Mark Order Picked Up

```http
POST /api/drivers/orders/{orderId}/picked-up
```

Authentication:

- driver only

Success response:

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "PickedUp",
  "message": "Order status updated successfully"
}
```

### 10. Mark Order On The Way

```http
POST /api/drivers/orders/{orderId}/on-the-way
```

Authentication:

- driver only

Success response:

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "OnTheWay",
  "message": "Order status updated successfully"
}
```

### 11. Mark Order Delivered

```http
POST /api/drivers/orders/{orderId}/delivered
```

Authentication:

- driver only

Success response:

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "Delivered",
  "message": "Order status updated successfully"
}
```

Business rule:

- a photo proof must already exist for the assignment before this call succeeds

Common failure:

```json
{
  "code": "DELIVERY_PROOF_REQUIRED",
  "message": "Photo proof is required before marking an order as delivered."
}
```

### 12. Mark Order Delivery Failed

```http
POST /api/drivers/orders/{orderId}/delivery-failed
```

Authentication:

- driver only

Request body:

```json
{
  "note": "Customer did not answer the phone"
}
```

Success response:

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "DeliveryFailed",
  "message": "Order status updated successfully"
}
```

Business rule:

- a failure note is required

Common failure:

```json
{
  "code": "DELIVERY_FAILURE_NOTE_REQUIRED",
  "message": "A failure note is required before marking delivery as failed."
}
```

## Driver Order Transition Rules

The backend currently allows these driver-side order transitions:

- `DriverAssigned -> PickedUp`
- `PickedUp -> OnTheWay`
- `OnTheWay -> Delivered`
- `OnTheWay -> DeliveryFailed`
- `DriverAssigned -> DeliveryFailed`

Anything outside these transitions is rejected.

## Common Business Errors

The mobile app should handle these error codes explicitly:

- `DRIVER_NOT_AUTHENTICATED`
- `DRIVER_NOT_FOUND`
- `DRIVER_NOT_READY_FOR_DISPATCH`
- `DRIVER_NOT_ASSIGNED`
- `ASSIGNMENT_NOT_OWNED`
- `INVALID_PROOF_STATE`
- `DELIVERY_PROOF_REQUIRED`
- `DELIVERY_FAILURE_NOTE_REQUIRED`
- `INVALID_ORDER_STATUS_TRANSITION`
- `DeliveryZone` not found

## Mobile UX Recommendations

- After registration, always prompt the driver to choose a zone if one is not set
- Do not show the online toggle as enabled until the account is approved
- Before showing the delivered CTA, make sure photo proof was uploaded successfully
- Require a mandatory reason textarea before sending `delivery-failed`
- Keep a lightweight polling or refresh strategy for `assignments/current`
- Continue GPS updates during active deliveries even if the driver toggles unavailable later

## APIDog Reference

The APIDog export was updated to include these driver mobile endpoints:

- `/api/drivers/register`
- `/api/drivers/zones`
- `/api/drivers/me/zone`
- `/api/drivers/me/availability`
- `/api/drivers/location`
- `/api/drivers/assignments/current`
- `/api/drivers/assignments/history`
- `/api/drivers/assignments/{assignmentId}/proof`
- `/api/drivers/orders/{orderId}/picked-up`
- `/api/drivers/orders/{orderId}/on-the-way`
- `/api/drivers/orders/{orderId}/delivered`
- `/api/drivers/orders/{orderId}/delivery-failed`

Reference file:

- `Zadana_APIDog_Folders.json`
