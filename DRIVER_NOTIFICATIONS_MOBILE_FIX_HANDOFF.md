# Driver Notifications Mobile Fix Handoff

## Current Problem

Driver test notifications are not appearing on the mobile app.

Backend investigation is already complete.

The current blocker is on the mobile side:

- the driver OneSignal app is valid
- the driver REST API key is valid
- backend test notification dispatch reaches OneSignal successfully
- but the driver OneSignal app currently has:
  - `players = 0`
  - `messageable_players = 0`

This means the driver app is not registering any active OneSignal device/player yet.

Without a registered player/device in the driver OneSignal app, push notifications cannot appear.

## What Backend Already Confirms

The backend side is already prepared and working:

- driver notifications endpoint exists
- admin test notification endpoint exists
- driver notifications use the dedicated driver OneSignal app
- the driver REST API key was tested directly against OneSignal and returned `200 OK`
- fallback logic was added for admin driver test notifications

So the remaining issue is not backend delivery logic.
It is mobile registration and OneSignal connection inside the driver app.

## Driver OneSignal App

The driver app must use the dedicated driver OneSignal app, not the customer app.

Driver OneSignal App ID:

- `1eead1ea-3d6f-4f2a-8bc5-c681d71b55f6`

Important:

- do not initialize OneSignal with the customer app id
- do not mix customer and driver OneSignal setup

## Required Mobile Fix

The mobile app must do all of the following:

1. initialize OneSignal using the driver app id
2. request notification permission
3. create/restore the OneSignal player/subscription
4. log in to OneSignal using the backend driver `UserId` as external user id
5. call the backend driver device registration endpoint after login

If any of these are missing, the backend may be correct while push still never appears.

## Required Identity Mapping

The backend sends driver notifications using the driver `UserId`, not the `DriverId`.

Example from backend investigation:

- `DriverId`: `226239b5-03fd-4645-935f-19e3622c6a6a`
- actual backend `UserId`: `5523e050-ea68-475b-814b-e85231bb08cb`

The mobile app must use the backend driver `UserId` when logging in to OneSignal as the external user id.

Do not use:

- `DriverId`
- internal screen id
- phone number
- email

Use only the authenticated backend driver `UserId`.

## Required Mobile Lifecycle

### At app startup

Must happen before routing:

1. initialize OneSignal with the driver app id
2. attach foreground notification listener
3. attach notification opened listener
4. request permission if needed
5. inspect whether the device has a valid OneSignal subscription/player

### After driver login

Must happen after successful authentication:

1. get the authenticated driver backend `UserId`
2. call OneSignal login/external-id binding with that `UserId`
3. collect device/subscription identifiers from OneSignal
4. call backend register device endpoint
5. open SignalR connection

### On token/subscription change

Must:

1. update backend device registration again

### On logout

Must:

1. unregister device from backend
2. disconnect SignalR
3. clear OneSignal login identity if the app architecture requires that

## Required Backend Endpoint Usage

The driver app must register its device through the driver notifications device endpoints:

- `GET /api/drivers/notifications/devices*`
- `POST /api/drivers/notifications/devices*`
- `PUT /api/drivers/notifications/devices*`

The goal is that the backend should eventually have an active `UserPushDevices` row for the authenticated driver `UserId`.

## Expected Success Criteria

After the mobile fix is implemented:

1. the driver app appears in OneSignal dashboard with at least:
   - `players >= 1`
   - `messageable_players >= 1`
2. backend `UserPushDevices` contains an active row for the driver `UserId`
3. admin test notification for driver succeeds
4. notification appears on the real device

## How To Verify On Mobile

### Verification A: OneSignal dashboard

After opening the driver app on a real device:

- the driver OneSignal app should no longer show `players = 0`

### Verification B: backend device registration

After login:

- backend should receive device registration for the driver user

### Verification C: admin test notification

From superadmin driver detail screen:

- click the test notification button
- expected result:
  - success response
  - notification appears on the device

## Recommended Flutter Implementation Checklist

- use the driver OneSignal app id only
- ensure OneSignal initialization runs once at app bootstrap
- request notification permission on supported platforms
- bind OneSignal identity using backend driver `UserId`
- listen for subscription/player changes
- register device with backend after login
- refresh registration after token/subscription changes
- verify on a real device, not emulator only

## Common Failure Cases To Avoid

- initializing OneSignal with the customer app id
- using `DriverId` instead of backend `UserId`
- never calling OneSignal login/external-id binding
- never calling backend device registration endpoint
- testing on a device where notification permission is denied
- assuming SignalR alone can replace push delivery

## What Mobile Developer Should Send Back

After implementing the fix, please provide:

1. the exact place where OneSignal is initialized
2. the exact place where driver `UserId` is bound to OneSignal
3. the exact place where backend device registration is called
4. confirmation that the driver OneSignal app now shows active players
5. result of an admin test notification on a real device
