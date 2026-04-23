# Driver Mobile API Contract

This document explains how the driver mobile app should integrate with the current Zadana backend delivery APIs.

It is focused on the driver app flow only:

- file upload
- auth and session lifecycle
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
- A driver can log in before operational approval, but cannot enter dispatch until approved.
- A driver can only become available after admin approval.
- Operational access means the driver profile is both `Approved` and `Active`.
- Before operational access, the app must not show delivery jobs, delivery CTAs, or the online toggle as usable.
- The driver must submit photo proof before marking an order as delivered.
- The driver must provide a failure note before marking delivery as failed.

## Base Requirements

All authenticated endpoints in this guide require:

- a valid authenticated driver account
- `Authorization: Bearer <access_token>`

These APIs are protected by the `DriverOnly` policy unless noted otherwise.

## Environment Setup

The mobile app should keep the API base URL configurable per environment.

Recommended environment variables:

- `API_BASE_URL`
- `WS_BASE_URL` if real-time features are introduced later

Suggested values:

- local: `https://localhost:<api-port>`
- staging: provided by backend/devops
- production: provided by backend/devops

Important:

- do not hardcode the full host inside the app
- all paths in this document are relative to `API_BASE_URL`

## Request Conventions

Use these defaults in the mobile networking layer:

- JSON endpoints: `Content-Type: application/json`
- file upload endpoint: `Content-Type: multipart/form-data`
- authenticated endpoints: `Authorization: Bearer <access_token>`
- optional localization header: `Accept-Language: ar` or `Accept-Language: en`

## Standard Error Contract

The backend uses a global exception middleware and returns `ProblemDetails`-style responses.

Typical business error example:

```json
{
  "status": 409,
  "title": "Business rule violation",
  "detail": "Photo proof is required before marking an order as delivered.",
  "instance": "/api/drivers/orders/44444444-4444-4444-4444-444444444444/delivered",
  "errorCode": "DELIVERY_PROOF_REQUIRED",
  "traceId": "00-abc123-def456"
}
```

Driver not operational example:

```json
{
  "status": 409,
  "title": "Business rule violation",
  "detail": "Driver must be reviewed and approved by admin before handling delivery orders.",
  "instance": "/api/drivers/orders/44444444-4444-4444-4444-444444444444/picked-up",
  "errorCode": "DRIVER_NOT_READY_FOR_DISPATCH",
  "traceId": "00-abc123-def456"
}
```

Typical validation error example:

```json
{
  "status": 400,
  "title": "Validation error",
  "detail": "The Identifier field is required.",
  "instance": "/api/drivers/auth/login",
  "errors": {
    "Identifier": [
      "The Identifier field is required."
    ]
  },
  "errorCode": "VALIDATION_ERROR",
  "traceId": "00-abc123-def456"
}
```

Mobile should always read:

- HTTP status code
- `errorCode`
- `detail`
- `errors` for validation failures

## Driver Verification Lifecycle

The driver app should treat the account as moving through these states:

- `NeedsDocuments`: one or more required documents were not provided
- `UnderReview`: all required documents were uploaded and the driver is waiting for admin review
- `Approved`: driver can be activated and can go available
- `Rejected`: driver was rejected and cannot enter dispatch

Operational rule:

- `PUT /api/drivers/me/availability` with `isAvailable = true` only succeeds when the driver is both `Approved` and `Active`
- `GET /api/drivers/assignments/current` never exposes an active assignment until the driver is both `Approved` and `Active`
- `GET /api/drivers/assignments/history` returns an empty list until the driver is both `Approved` and `Active`
- proof submission and order status updates also fail with `DRIVER_NOT_READY_FOR_DISPATCH` until admin approval is completed

Current implementation note:

- the canonical mobile endpoint for operational state is `GET /api/drivers/me/status`
- mobile should use this endpoint to decide whether the driver is `NeedsDocuments`, `UnderReview`, `Approved`, `Rejected`, or blocked by account state such as `Suspended`
- `GET /api/drivers/assignments/current` remains useful for job polling, but it is no longer the primary status source

## Operational Access Gate

The backend treats a driver as operational only when:

```text
driver.verificationStatus == Approved
driver.status == Active
```

Until both conditions are true:

- login can succeed, but the response now includes `driverStatus` so the app can route away from the home screen
- file upload can be used
- profile and zone setup can continue
- GPS updates can be accepted, but they do not make the driver eligible for dispatch
- the online toggle cannot be enabled
- current assignment does not expose order details
- assignment history returns `[]`
- proof submission is rejected
- order status changes are rejected

Mobile UI recommendation:

- after registration, move the driver into a waiting/review screen
- keep zone selection available
- disable the online toggle
- do not poll for jobs aggressively while `isOperational = false`
- show a clear message such as `Waiting for admin review`
- start normal assignment polling only after the driver becomes operational

Recommended screen mapping from `driverStatus.gateStatus`:

- `Operational`: open the normal app home and allow dispatch-related polling
- `UnderReview`: open the waiting-for-review screen
- `NeedsDocuments`: open a missing-documents or blocked onboarding screen
- `Rejected`: open the rejected-account screen
- `Suspended`: open the suspended-account screen
- `Inactive`, `PendingActivation`, or `Banned`: open a blocked-account screen

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

1. Load active delivery zones using `GET /api/public/delivery-zones`
2. Let the driver choose one active zone from a dropdown
3. Upload required images or documents using the common files endpoint
4. Register the driver with the returned file URLs and the selected `primaryZoneId`
5. Save `accessToken` and `refreshToken` securely
6. Read `driverStatus` from the login or registration response and route immediately to the correct screen
7. Call `GET /api/drivers/auth/me` to hydrate the session on app launch
8. Call `GET /api/drivers/me/status` to know whether the driver is operational on later launches
9. Keep the app in waiting mode until the superadmin approves the driver
10. Once approved, allow the driver to toggle availability on
11. Send periodic GPS updates while online or on an active assignment
12. Fetch current assignment
13. Submit photo proof before calling delivered
14. Update order status through pickup, on-the-way, and delivery completion or failure

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
  "driverStatus": {
    "driverId": "11111111-1111-1111-1111-111111111111",
    "gateStatus": "UnderReview",
    "isOperational": false,
    "canReceiveOrders": false,
    "canGoAvailable": false,
    "isAvailable": false,
    "verificationStatus": "UnderReview",
    "accountStatus": "Pending",
    "reviewedAtUtc": null,
    "reviewNote": null,
    "suspensionReason": null,
    "primaryZoneId": "22222222-2222-2222-2222-222222222222",
    "zoneName": "Cairo - Nasr City East",
    "message": "Driver profile is currently under admin review."
  },
  "isVerified": false,
  "message": null
}
```

Notes:

- `favoritesCount` is a legacy customer-oriented field in the shared auth DTO.
- the driver app should ignore `favoritesCount` if it appears in auth or `me` responses.
- for driver login and driver registration, `driverStatus` is the field the app should use for routing.
- for driver auth responses, `isVerified` is `false` until the driver becomes operationally approved.
- use `driverStatus.gateStatus` or `GET /api/drivers/me/status` to know the exact blocked state and choose the correct screen.

## Endpoints

### Common: Upload File

```http
POST /api/files/upload
```

Authentication:

- public

Content type:

- `multipart/form-data`

Form fields:

- `file`: binary file
- `directory`: optional target directory

Suggested directories for the driver app:

- `drivers/national-id`
- `drivers/license`
- `drivers/vehicle`
- `drivers/profile`
- `drivers/proofs`

Example response:

```json
{
  "url": "https://cdn.example.com/drivers/proofs/order-10245.jpg"
}
```

Notes:

- allowed file extensions currently are `.jpg`, `.jpeg`, `.png`, `.pdf`
- registration and proof endpoints expect URL strings, not raw file binaries
- mobile should upload first, then pass the returned `url` into driver APIs

### Auth: Driver Login

```http
POST /api/drivers/auth/login
```

Authentication:

- public

Request body:

```json
{
  "identifier": "driver@example.com",
  "password": "StrongPassword123!"
}
```

Notes:

- `identifier` can be either email or phone number
- login is allowed for driver accounts, but home-screen access must be decided from `driverStatus.gateStatus`
- if `driverStatus.gateStatus` is not `Operational`, do not open the normal app home

Success response:

- `200 OK`
- returns `AuthResponseDto`

### Auth: Refresh Token

```http
POST /api/drivers/auth/refresh-token
```

Authentication:

- public

Request body:

```json
{
  "refreshToken": "stored-refresh-token"
}
```

Success response:

```json
{
  "accessToken": "new-jwt-access-token",
  "refreshToken": "new-jwt-refresh-token"
}
```

Notes:

- store the new refresh token returned by this endpoint
- old refresh tokens are revoked during rotation

### Auth: Logout

```http
POST /api/drivers/auth/logout
```

Authentication:

- driver only

Request body:

```json
{
  "refreshToken": "stored-refresh-token"
}
```

Success response:

- `204 No Content`

### Auth: Get Current Driver Session

```http
GET /api/drivers/auth/me
```

Authentication:

- driver only

Success response:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "fullName": "Ahmed Driver",
  "email": "ahmed.driver@example.com",
  "phone": "+201001112233",
  "role": "Driver",
  "favoritesCount": 0
}
```

Notes:

- `favoritesCount` is not used by the driver app.
- this endpoint validates the JWT/session only; it does not mean the driver is approved for dispatch.

### Auth: Update Current Driver Profile

```http
PUT /api/drivers/auth/me
```

Authentication:

- driver only

Request body:

```json
{
  "fullName": "Ahmed Driver",
  "email": "ahmed.driver@example.com",
  "phone": "+201001112233"
}
```

Success response:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "fullName": "Ahmed Driver",
  "email": "ahmed.driver@example.com",
  "phone": "+201001112233",
  "role": "Driver",
  "favoritesCount": 0
}
```

Notes:

- `favoritesCount` is not used by the driver app.
- updating profile data does not activate the driver for dispatch.

### Auth: Forgot Password

```http
POST /api/drivers/auth/forgot-password
```

Authentication:

- public

Request body:

```json
{
  "identifier": "driver@example.com"
}
```

Success response:

```json
{
  "message": "Password reset OTP sent successfully"
}
```

Notes:

- the backend may still return success even when the identifier does not map to a user
- OTP delivery depends on the configured SMS or email channels

### Auth: Reset Password

```http
POST /api/drivers/auth/reset-password
```

Authentication:

- public

Request body:

```json
{
  "identifier": "driver@example.com",
  "otpCode": "481920",
  "newPassword": "NewStrongPassword123!"
}
```

Success response:

```json
{
  "message": "Password reset successful"
}
```

### Delivery: Register Driver

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
  "primaryZoneId": "22222222-2222-2222-2222-222222222222",
  "nationalIdImageUrl": "https://cdn.example.com/driver/national-id.jpg",
  "licenseImageUrl": "https://cdn.example.com/driver/license.jpg",
  "vehicleImageUrl": "https://cdn.example.com/driver/vehicle.jpg",
  "personalPhotoUrl": "https://cdn.example.com/driver/photo.jpg"
}
```

Notes:

- `primaryZoneId` is required during registration
- call `GET /api/public/delivery-zones` before registration and send one active zone id here
- `vehicleType`, `nationalId`, `licenseNumber`, `address`, and document URLs are nullable in the current API contract
- `vehicleType` is an enum string. Allowed values: `Car`, `Motorcycle`, `Scooter`, `Van`, `Bicycle`, `Truck`
- send exactly one of the enum values above; do not send localized labels like Arabic vehicle names
- mobile should still send all available data to avoid landing in `NeedsDocuments`

Success response:

- `200 OK`
- returns `AuthResponseDto`

Notes:

- this endpoint also returns tokens, so the mobile app can continue without an immediate login call
- this response also includes `driverStatus`, so the app can route immediately to `UnderReview`, `Rejected`, `Suspended`, or the normal home
- the returned `driverStatus.primaryZoneId` and `zoneName` reflect the zone selected during registration
- for later app launches, use the driver auth endpoints above

### Delivery: Get Public Active Zones

```http
GET /api/public/delivery-zones
```

Authentication:

- public

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
- mobile should use this list to populate the registration zone dropdown before the driver has a token

### Delivery: Get Active Zones After Login

```http
GET /api/drivers/zones
```

Authentication:

- driver only

Notes:

- returns the same active zone list after login
- use it when the authenticated driver wants to change the current zone later from the app

### Delivery: Set Driver Zone

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

### Delivery: Get Driver Operational Status

```http
GET /api/drivers/me/status
```

Authentication:

- driver only

Success response:

```json
{
  "driverId": "11111111-1111-1111-1111-111111111111",
  "gateStatus": "UnderReview",
  "isOperational": false,
  "canReceiveOrders": false,
  "canGoAvailable": false,
  "isAvailable": false,
  "verificationStatus": "UnderReview",
  "accountStatus": "Pending",
  "reviewedAtUtc": null,
  "reviewNote": null,
  "suspensionReason": null,
  "primaryZoneId": "22222222-2222-2222-2222-222222222222",
  "zoneName": "Cairo - Nasr City East",
  "message": "Driver profile is currently under admin review."
}
```

Field meanings:

- `gateStatus`: normalized routing state for the app such as `Operational`, `UnderReview`, `NeedsDocuments`, `Rejected`, or `Suspended`
- `isOperational`: true only when the driver is both `Approved` and `Active`
- `canReceiveOrders`: whether the driver is eligible for dispatch
- `canGoAvailable`: whether the app may enable the online toggle
- `isAvailable`: current online/offline toggle state saved in the backend
- `verificationStatus`: `NeedsDocuments`, `UnderReview`, `Approved`, or `Rejected`
- `accountStatus`: backend account state such as `Pending`, `Active`, `Inactive`, `Suspended`, or `Banned`
- `reviewNote`: latest admin review note if one exists
- `suspensionReason`: current suspension reason if the account is suspended
- `primaryZoneId` and `zoneName`: currently selected delivery zone
- `message`: human-readable status message for waiting or blocked states

Mobile recommendation:

- call this endpoint after login if you need a fresh server read, and on every app startup after restoring tokens
- use it as the single source of truth for waiting, rejected, suspended, or approved screens
- `isVerified` in driver auth responses is a quick boolean gate, but `driverStatus.gateStatus` is the exact routing state
- keep `assignments/current` only for assignment polling

### Delivery: Set Driver Availability

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
  "status": 409,
  "title": "Business rule violation",
  "detail": "Only approved active drivers can enable availability.",
  "instance": "/api/drivers/me/availability",
  "errorCode": "DRIVER_NOT_READY_FOR_DISPATCH",
  "traceId": "00-abc123-def456"
}
```

### Delivery: Update Driver Location

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

### Delivery: Get Current Assignment

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

Not approved yet response:

```json
{
  "hasAssignment": false,
  "gateStatus": "UnderReview",
  "isOperational": false,
  "verificationStatus": "UnderReview",
  "accountStatus": "Pending",
  "message": "Driver profile is currently under admin review."
}
```

Mobile behavior for this response:

- keep the driver in the waiting/review screen
- keep the online toggle disabled
- do not show order cards or delivery action buttons
- `gateStatus` can be used directly to choose the blocked screen variant
- optionally show `verificationStatus` and `accountStatus` as support/debug info

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
- if the driver is not approved and active, this endpoint returns `hasAssignment = false` and does not expose order details

### Delivery: Get Assignment History

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
- if the driver is not approved and active, the backend returns an empty list

### Delivery: Submit Delivery Proof

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

- the driver must be reviewed and approved by admin first
- the assignment must belong to the authenticated driver
- proof can only be submitted for active assignments
- current backend allows proof submission when assignment status is `Accepted`, `PickedUp`, or `ArrivedAtCustomer`

Common failures:

```json
{
  "status": 409,
  "title": "Business rule violation",
  "detail": "Driver must be reviewed and approved by admin before submitting delivery proof.",
  "instance": "/api/drivers/assignments/33333333-3333-3333-3333-333333333333/proof",
  "errorCode": "DRIVER_NOT_READY_FOR_DISPATCH",
  "traceId": "00-abc123-def456"
}
```

```json
{
  "status": 409,
  "title": "Business rule violation",
  "detail": "You can only submit proof for your assigned deliveries.",
  "instance": "/api/drivers/assignments/33333333-3333-3333-3333-333333333333/proof",
  "errorCode": "ASSIGNMENT_NOT_OWNED",
  "traceId": "00-abc123-def456"
}
```

Mobile recommendation:

- use `photo` as the default proof type in v1
- submit proof before calling the delivered endpoint

### Delivery: Mark Order Picked Up

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

### Delivery: Mark Order On The Way

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

Shared business rules for all driver order status endpoints:

- the driver must be reviewed and approved by admin first
- the driver account must be `Active`
- the order must be assigned to the authenticated driver
- the requested transition must be one of the allowed driver transitions listed below

Common not-approved failure:

```json
{
  "status": 409,
  "title": "Business rule violation",
  "detail": "Driver must be reviewed and approved by admin before handling delivery orders.",
  "instance": "/api/drivers/orders/44444444-4444-4444-4444-444444444444/picked-up",
  "errorCode": "DRIVER_NOT_READY_FOR_DISPATCH",
  "traceId": "00-abc123-def456"
}
```

### Delivery: Mark Order Delivered

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
  "status": 409,
  "title": "Business rule violation",
  "detail": "Photo proof is required before marking an order as delivered.",
  "instance": "/api/drivers/orders/44444444-4444-4444-4444-444444444444/delivered",
  "errorCode": "DELIVERY_PROOF_REQUIRED",
  "traceId": "00-abc123-def456"
}
```

### Delivery: Mark Order Delivery Failed

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
  "status": 409,
  "title": "Business rule violation",
  "detail": "A failure note is required before marking delivery as failed.",
  "instance": "/api/drivers/orders/44444444-4444-4444-4444-444444444444/delivery-failed",
  "errorCode": "DELIVERY_FAILURE_NOTE_REQUIRED",
  "traceId": "00-abc123-def456"
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

- Build one shared auth interceptor that injects the bearer token and retries once after `refresh-token`
- On app startup, prefer `GET /api/drivers/auth/me` to validate the stored session
- After session validation, call `GET /api/drivers/me/status` to decide whether to show waiting, rejected, suspended, or active operational UI
- Upload all images first, cache their returned URLs, then send those URLs to registration and proof APIs
- After registration, always prompt the driver to choose a zone if one is not set
- Do not show the online toggle as enabled until the account is approved
- Before showing the delivered CTA, make sure photo proof was uploaded successfully
- Require a mandatory reason textarea before sending `delivery-failed`
- Keep a lightweight polling or refresh strategy for `assignments/current`
- Continue GPS updates during active deliveries even if the driver toggles unavailable later

## APIDog Reference

The APIDog export was updated to include these driver mobile endpoints:

- `/api/files/upload`
- `/api/drivers/auth/login`
- `/api/drivers/auth/forgot-password`
- `/api/drivers/auth/reset-password`
- `/api/drivers/auth/refresh-token`
- `/api/drivers/auth/logout`
- `/api/drivers/auth/me`
- `/api/drivers/me/status`
- `/api/drivers/register`
- `/api/public/delivery-zones`
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
