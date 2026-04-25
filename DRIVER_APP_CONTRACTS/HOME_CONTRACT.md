# Driver Home Contract

## Status

- `implemented`

## Purpose

This file explains how the driver mobile home screen should integrate with the current backend.

The home screen is backend-first. The app should not invent its own home mode from local assumptions. The server is the source of truth for:

- `homeState`
- `operationalStatus`
- `currentOffer`
- `currentAssignment`
- `earningsSummaryToday`
- `unreadAlerts`
- `commitment`
- `profileReadiness`

## Home Endpoints In One Place

All endpoints that the mobile home screen depends on are grouped here:

- `GET /api/drivers/home`
- `GET /api/drivers/me/status`
- `PUT /api/drivers/me/availability`
- `PUT /api/drivers/me/zone`
- `POST /api/drivers/location`
- `GET /api/drivers/assignments/current`
- `POST /api/drivers/offers/{assignmentId}/accept`
- `POST /api/drivers/offers/{assignmentId}/reject`
- `GET /api/drivers/notifications/unread-count`

Authentication for all home endpoints:

- `Authorization: Bearer <access_token>`
- policy: `DriverOnly`

## Main Home Endpoint

### `GET /api/drivers/home`

This endpoint returns the complete home snapshot for the authenticated driver.

Important runtime note:

- the backend processes expired offers before building the response
- this means an offer can disappear from the home response if it has already expired server-side

## Response Shape

Example response:

```json
{
  "operationalStatus": {
    "driverId": "11111111-1111-1111-1111-111111111111",
    "gateStatus": "Operational",
    "isOperational": true,
    "canReceiveOrders": true,
    "canGoAvailable": true,
    "isAvailable": true,
    "verificationStatus": "Approved",
    "accountStatus": "Active",
    "reviewedAtUtc": "2026-04-24T10:30:00Z",
    "reviewNote": null,
    "suspensionReason": null,
    "primaryZoneId": "22222222-2222-2222-2222-222222222222",
    "zoneName": "Cairo - Nasr City East",
    "commitmentScore": 96.5,
    "dailyRejections": 0,
    "weeklyRejections": 1,
    "enforcementLevel": "Healthy",
    "canReceiveOffers": true,
    "restrictionMessage": null,
    "message": "Driver is approved and can receive orders."
  },
  "homeState": "WaitingForOffer",
  "currentOffer": null,
  "currentAssignment": null,
  "earningsSummaryToday": {
    "earningsAmount": 180.0,
    "completedTrips": 3
  },
  "unreadAlerts": 2,
  "commitment": {
    "acceptedOffers": 12,
    "rejectedOffers": 1,
    "timedOutOffers": 0,
    "dailyRejections": 0,
    "weeklyRejections": 1,
    "commitmentScore": 96.5,
    "enforcementLevel": "Healthy",
    "canReceiveOffers": true,
    "restrictionMessage": null,
    "lastOfferResponseAtUtc": "2026-04-25T08:40:00Z"
  },
  "profileReadiness": {
    "isProfileComplete": true,
    "completionPercent": 100,
    "missingRequirements": [],
    "canSubmitForReview": true,
    "checklist": [
      {
        "code": "personal_info",
        "completed": true,
        "note": null,
        "critical": false
      },
      {
        "code": "vehicle_info",
        "completed": true,
        "note": null,
        "critical": true
      },
      {
        "code": "national_id_document",
        "completed": true,
        "note": null,
        "critical": true
      }
    ]
  }
}
```

## Home State Rules

The backend currently resolves `homeState` in this order:

1. If `currentAssignment != null` => `OnMission`
2. Else if `operationalStatus.isOperational == false` => `operationalStatus.gateStatus`
3. Else if `currentOffer != null` => `IncomingOffer`
4. Else if `operationalStatus.isAvailable == true` => `WaitingForOffer`
5. Else => `Offline`

Current possible values from the backend:

- `OnMission`
- `IncomingOffer`
- `WaitingForOffer`
- `Offline`
- `NeedsDocuments`
- `UnderReview`
- `Rejected`
- `Suspended`
- `Banned`
- `PendingActivation`
- `Inactive`
- `Unavailable`

Mobile must treat `homeState` as the primary screen mode.

## Operational Status Block

`operationalStatus` is the source of truth for driver eligibility and top-of-home badges.

Important fields:

- `gateStatus`: normalized server gate such as `Operational`, `UnderReview`, `Suspended`
- `isOperational`: whether the driver can actively participate in dispatch
- `canGoAvailable`: whether the app may enable the online toggle
- `isAvailable`: current online toggle state stored in the backend
- `verificationStatus`: current verification lifecycle state
- `accountStatus`: current account lifecycle state
- `primaryZoneId`, `zoneName`: currently selected zone
- `commitmentScore`, `dailyRejections`, `weeklyRejections`, `enforcementLevel`
- `canReceiveOffers`, `restrictionMessage`, `message`

Mobile notes:

- disable the online toggle when `canGoAvailable == false`
- if `canReceiveOffers == false`, show the restriction message from the server
- use `message` as the default blocked-state explanation

## Current Offer Block

When `currentOffer` is not null, the home should render the incoming offer UI.

Example shape:

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-10245",
  "vendorName": "Driver Read Vendor",
  "pickupAddress": "Olaya Street",
  "pickupLatitude": 24.7136,
  "pickupLongitude": 46.6753,
  "customerName": "Ahmed Customer",
  "deliveryAddress": "Yasmin District",
  "deliveryLatitude": 24.7821,
  "deliveryLongitude": 46.6520,
  "estimatedDistanceKm": 6.2,
  "estimatedEta": "25-30 min",
  "payout": 22.5,
  "vendorInitials": "DR",
  "customerInitials": "AC",
  "packageNote": "Call before arrival",
  "countdownSeconds": 38,
  "orderItems": [
    {
      "name": "Fresh Item",
      "quantity": 2,
      "note": "Call before arrival"
    }
  ]
}
```

Mobile rules:

- use `assignmentId` for accept and reject actions
- render the countdown from `countdownSeconds`
- do not cache expired offers locally after the server removes them
- `orderItems` here are offer preview items, not the full order-detail contract

Related actions:

- `POST /api/drivers/offers/{assignmentId}/accept`
- `POST /api/drivers/offers/{assignmentId}/reject`

## Current Assignment Block

When `currentAssignment` is not null, the home should render the active mission card and route into order details.

Example shape:

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-10245",
  "status": "Accepted",
  "vendorName": "Driver Read Vendor",
  "pickupAddress": "Olaya Street",
  "deliveryAddress": "Yasmin District",
  "pickupLatitude": 24.7136,
  "pickupLongitude": 46.6753,
  "deliveryLatitude": 24.7821,
  "deliveryLongitude": 46.6520,
  "codAmount": 60.0,
  "createdAtUtc": "2026-04-25T08:35:00Z",
  "merchantContact": "01000000060",
  "vehicleType": "Motorcycle",
  "plateNumber": "CAI-DRV-4421",
  "pickupOtpRequired": true,
  "deliveryOtpRequired": false
}
```

Current assignment statuses returned by home can be:

- `Accepted`
- `ArrivedAtVendor`
- `PickedUp`
- `ArrivedAtCustomer`

Mobile rules:

- if `currentAssignment` exists, the app should enter the mission-first home state
- use `assignmentId` to open `ORDER_DETAILS_CONTRACT.md`
- use `orderId` for order status transition endpoints
- use `pickupOtpRequired` and `deliveryOtpRequired` only as indicators; the full action matrix comes from the order details endpoint

## Earnings And Alerts

`earningsSummaryToday` contains:

- `earningsAmount`
- `completedTrips`

`unreadAlerts` is the unread notifications count for the driver.

Mobile notes:

- use this block for the home earnings card only
- for full wallet history and payout methods, use `WALLET_CONTRACT.md`
- for the notifications inbox, use `NOTIFICATIONS_CONTRACT.md`

## Commitment Block

`commitment` is a compact operational-performance summary used on the home screen.

Fields:

- `acceptedOffers`
- `rejectedOffers`
- `timedOutOffers`
- `dailyRejections`
- `weeklyRejections`
- `commitmentScore`
- `enforcementLevel`
- `canReceiveOffers`
- `restrictionMessage`
- `lastOfferResponseAtUtc`

Mobile notes:

- do not calculate commitment locally
- if `canReceiveOffers == false`, the home should clearly show that the driver is temporarily restricted from offers
- `restrictionMessage` should be displayed exactly as sent by the backend

## Profile Readiness Block

`profileReadiness` is included so the home screen can explain blocked states such as `NeedsDocuments` without requiring an immediate second API call.

Fields:

- `isProfileComplete`
- `completionPercent`
- `missingRequirements[]`
- `canSubmitForReview`
- `checklist[]`

Checklist item fields:

- `code`
- `completed`
- `note`
- `critical`

Current checklist codes returned by backend:

- `personal_info`
- `vehicle_info`
- `national_id_document`
- `license_document`
- `vehicle_document`
- `personal_photo`
- `zone_selection`

Current missing requirement codes returned by backend:

- `missing_personal_info`
- `missing_vehicle_info`
- `missing_documents`
- `missing_zone_selection`

Mobile notes:

- this block is especially important when `homeState` is `NeedsDocuments` or `UnderReview`
- use `missingRequirements` for routing decisions
- use `checklist` for rendering a more explicit blocked-state UI similar to admin verification
- `profileReadiness` is a home summary, not a replacement for the full profile endpoint

## Recommended Mobile Rendering Logic

### 1. Mission Mode

Use when:

- `homeState == "OnMission"`

Render:

- active mission card
- CTA to open order details
- mission-specific quick actions only if the app already supports them

### 2. Incoming Offer Mode

Use when:

- `homeState == "IncomingOffer"`

Render:

- offer preview
- countdown timer
- accept and reject buttons

### 3. Waiting Mode

Use when:

- `homeState == "WaitingForOffer"`

Render:

- online and waiting state
- today earnings
- unread alerts
- commitment summary

### 4. Offline Mode

Use when:

- `homeState == "Offline"`

Render:

- offline state
- online toggle if `operationalStatus.canGoAvailable == true`

### 5. Blocked Or Review Mode

Use when `homeState` is one of:

- `NeedsDocuments`
- `UnderReview`
- `Rejected`
- `Suspended`
- `Banned`
- `PendingActivation`
- `Inactive`
- `Unavailable`

Render:

- blocked-state illustration or card
- `operationalStatus.message`
- `operationalStatus.reviewNote` if useful
- `operationalStatus.suspensionReason` when present

## Supporting Home Endpoints

### `GET /api/drivers/me/status`

Purpose:

- lightweight refresh for driver gate and readiness without loading the full home payload

Response:

- `DriverOperationalStatusDto`

Use it when:

- the app resumes from background
- the app needs to know if the online toggle should be enabled
- the app only needs gate status, approval state, and availability

### `PUT /api/drivers/me/availability`

Purpose:

- turn the driver online or offline

Request:

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

Important notes:

- only approved active drivers can go online
- blocked or under-review drivers can receive `DRIVER_NOT_READY_FOR_DISPATCH`

### `PUT /api/drivers/me/zone`

Purpose:

- change the primary operating zone used by the driver

Request:

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

### `POST /api/drivers/location`

Purpose:

- send live GPS updates for dispatch ranking and active-mission tracking

Request:

```json
{
  "latitude": 30.0444,
  "longitude": 31.2357,
  "accuracyMeters": 18.5
}
```

Success response:

```json
{
  "message": "Location updated"
}
```

Mobile notes:

- send location while available
- continue sending location during active mission
- the backend stores the official timestamp

### `GET /api/drivers/assignments/current`

Purpose:

- quick endpoint for checking whether the driver currently has a non-terminal assignment

Possible responses:

```json
{
  "hasAssignment": false
}
```

```json
{
  "hasAssignment": true,
  "assignment": {
    "id": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "orderNumber": "ORD-10245",
    "status": "Accepted",
    "codAmount": 120.5,
    "createdAtUtc": "2026-04-23T10:15:00Z"
  }
}
```

Blocked-state response can also include:

- `gateStatus`
- `isOperational`
- `verificationStatus`
- `accountStatus`
- `commitmentScore`
- `dailyRejections`
- `weeklyRejections`
- `enforcementLevel`
- `canReceiveOffers`
- `restrictionMessage`
- `message`

### `POST /api/drivers/offers/{assignmentId}/accept`

Purpose:

- accept the incoming offer shown on home

Path parameter:

- `assignmentId`

Success response:

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "Accepted",
  "message": "Offer accepted successfully."
}
```

Important notes:

- use `assignmentId` from `currentOffer.assignmentId`
- do not use `orderId` for offer acceptance

### `POST /api/drivers/offers/{assignmentId}/reject`

Purpose:

- reject the incoming offer shown on home

Path parameter:

- `assignmentId`

Optional request:

```json
{
  "reason": "too_far"
}
```

Success response:

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "Rejected",
  "message": "Offer rejected successfully."
}
```

Important notes:

- `reason` is optional in the current backend
- if the offer already expired server-side, the app should refresh home immediately

### `GET /api/drivers/notifications/unread-count`

Purpose:

- fetch the unread badge count shown on the home header

Response:

```json
{
  "count": 3
}
```

## Integration Notes

- `GET /api/drivers/home` should be the main source for home hydration
- the app should not derive home mode from local enums when the backend already returned `homeState`
- if both an offer and a mission are theoretically present, the backend prioritizes `OnMission`
- use `GET /api/drivers/me/status` for a lightweight refresh when the app only needs gate and availability information
- use `POST /api/drivers/location` while available and while on an active mission
- use `GET /api/drivers/assignments/current` when the app needs a quick boot check before loading full mission details
- for the full mission action matrix, use `ORDER_DETAILS_CONTRACT.md`
- if `homeState` is blocked, the app can render the primary reason directly from `profileReadiness` plus `operationalStatus.reviewNote`

## Related Contracts

- `ORDER_DETAILS_CONTRACT.md`
- `COMPLETED_ORDERS_CONTRACT.md`
- `WALLET_CONTRACT.md`
- `PROFILE_CONTRACT.md`
- `NOTIFICATIONS_CONTRACT.md`
