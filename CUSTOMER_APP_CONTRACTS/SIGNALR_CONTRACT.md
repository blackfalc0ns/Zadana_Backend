# Customer SignalR Contract

## Status

- `implemented`

## Purpose

This file is the source of truth for real-time mobile integration over SignalR.

It covers:

- hub URL
- authentication
- event names
- payload for each event
- order realtime updates
- driver arrival realtime updates

Important scope note:

- the backend currently exposes customer-facing realtime events on the notifications hub
- there is no separate driver mobile SignalR hub in the current backend
- order and driver realtime updates for customer mobile are both delivered through the same notifications hub

## Implemented Hubs

### 1. Notifications Hub

- route: `/hubs/notifications`
- backend file: `src/Zadana.Api/Realtime/NotificationHub.cs`

This is the main hub for customer mobile realtime:

- inbox notification delivery
- direct order status changes
- direct driver arrival state changes
- customer broadcast messages

### 2. Customer Presence Hub

- route: `/hubs/customer-presence`
- backend file: `src/Zadana.Api/Realtime/CustomerPresenceHub.cs`

This hub exists mainly for customer presence tracking.

For customer mobile, it is used to tell the backend:

- app moved to foreground
- app moved to background
- app heartbeat is still alive

It does not deliver order-status events to the customer app.

## Hub URL Rule

If your API base URL is:

```text
https://api.example.com/api
```

Then the SignalR hub URLs are:

```text
https://api.example.com/hubs/notifications
https://api.example.com/hubs/customer-presence
```

Rule:

- remove `/api` from `API_BASE_URL`
- then append the hub route

Example for local development:

- API base URL: `http://localhost:5298/api`
- notifications hub: `http://localhost:5298/hubs/notifications`
- presence hub: `http://localhost:5298/hubs/customer-presence`

## Authentication

Both hubs are protected with:

- `[Authorize]`

Mobile must connect using the authenticated user access token.

Recommended SignalR authentication:

- use `accessTokenFactory` in SignalR client
- SignalR will send the token using the `access_token` flow supported by ASP.NET Core SignalR

Pseudo example:

```text
connection = HubConnectionBuilder()
  .withUrl(HUB_URL, {
    accessTokenFactory: () => accessToken
  })
  .withAutomaticReconnect()
  .build()
```

Important notes:

- the token must belong to the authenticated customer
- the backend automatically resolves the current user from the token
- the client does not subscribe to a manual room or user group
- the backend adds the connection to the correct internal group automatically

## Notifications Hub Events

Hub:

- `/hubs/notifications`

Exact event names implemented in backend:

- `ReceiveNotification`
- `ReceiveOrderStatusChanged`
- `ReceiveDriverArrivalStateChanged`
- `ReceiveBroadcast`

## Event: `ReceiveNotification`

Purpose:

- general realtime inbox notification
- should be used to refresh in-app notifications state

Backend source:

- `NotificationService.SendToUserAsync(...)`

Payload shape:

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "تم تحديث حالة الطلب",
  "titleEn": "Order update",
  "bodyAr": "تم تحديث حالة طلبك رقم 12345",
  "bodyEn": "Your order #12345 status has been updated",
  "type": "order_status_changed",
  "referenceId": "22222222-2222-2222-2222-222222222222",
  "data": "{\"orderId\":\"22222222-2222-2222-2222-222222222222\",\"orderNumber\":\"12345\",\"vendorId\":\"33333333-3333-3333-3333-333333333333\",\"oldStatus\":\"Accepted\",\"newStatus\":\"Preparing\",\"actorRole\":\"vendor\",\"action\":\"status_changed\",\"targetUrl\":\"/orders/22222222-2222-2222-2222-222222222222\"}",
  "dataObject": {
    "orderId": "22222222-2222-2222-2222-222222222222",
    "orderNumber": "12345",
    "vendorId": "33333333-3333-3333-3333-333333333333",
    "oldStatus": "Accepted",
    "newStatus": "Preparing",
    "actorRole": "vendor",
    "action": "status_changed",
    "targetUrl": "/orders/22222222-2222-2222-2222-222222222222"
  },
  "isRead": false,
  "createdAtUtc": "2026-04-22T10:00:00Z"
}
```

Fields:

- `id`: notification record ID
- `titleAr`, `titleEn`: localized title
- `bodyAr`, `bodyEn`: localized body
- `type`: notification type
- `referenceId`: usually the main related entity, often `orderId`
- `data`: raw JSON string payload
- `dataObject`: parsed payload when available
- `isRead`: always `false` for freshly pushed realtime payload
- `createdAtUtc`: server creation time

Mobile guidance:

- use this event to update inbox list and unread badge
- use `dataObject` when available instead of parsing `data` yourself
- if `type` is order-related, `referenceId` or `dataObject.orderId` can be used for navigation

## Event: `ReceiveOrderStatusChanged`

Purpose:

- direct order realtime update
- should update the open order details/tracking screen immediately

Backend source:

- `NotificationService.SendOrderStatusChangedToUserAsync(...)`

Payload shape:

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "PendingVendorAcceptance",
  "newStatus": "Accepted",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-22T10:05:00Z"
}
```

Fields:

- `orderId`: target order
- `orderNumber`: customer-facing order number
- `vendorId`: related vendor
- `oldStatus`: previous order status
- `newStatus`: current order status
- `actorRole`: who triggered the change, if provided
- `action`: backend action label
- `targetUrl`: backend navigation hint
- `changedAtUtc`: event time

Current backend behavior:

- `action` is resolved from the new order status
- `targetUrl` defaults to `/orders/{orderId}`

Mobile guidance:

- if `payload.orderId == currentlyOpenedOrderId`, update the screen immediately
- do not wait for manual refresh
- this event is more direct than inbox notifications and should drive live order UI

## Event: `ReceiveDriverArrivalStateChanged`

Purpose:

- direct realtime update when driver arrival state changes
- used for driver-arrival UX in order tracking

Backend source:

- `NotificationService.SendDriverArrivalStateChangedToUserAsync(...)`

Payload shape:

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "arrivalState": "arrived_at_customer",
  "driverName": "Ahmed Ali",
  "actorRole": "driver",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-22T10:08:00Z"
}
```

Fields:

- `orderId`: target order
- `orderNumber`: customer-facing order number
- `arrivalState`: realtime driver arrival state
- `driverName`: current driver display name
- `actorRole`: usually `driver`
- `targetUrl`: backend navigation hint
- `changedAtUtc`: event time

Current arrival states sent by backend:

- `arrived_at_vendor`
- `arrived_at_customer`

Mobile guidance:

- use this event to update order tracking immediately
- if `arrivalState == "arrived_at_customer"`, the app can reveal delivery OTP guidance instantly
- use this event as the realtime source for driver arrival, not local timers

## Event: `ReceiveBroadcast`

Purpose:

- realtime broadcast to connected customers
- mainly for banners or general customer-wide app messaging

Backend source:

- `NotificationService.BroadcastToAllCustomersAsync(...)`

Payload shape:

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "titleAr": "عرض جديد",
  "titleEn": "New offer",
  "bodyAr": "اكتشف أحدث العروض الآن",
  "bodyEn": "Discover the latest offers now",
  "type": "new_banner",
  "referenceId": null,
  "data": "{\"bannerId\":\"55555555-5555-5555-5555-555555555555\",\"imageUrl\":\"https://example.com/banner.jpg\"}",
  "dataObject": {
    "bannerId": "55555555-5555-5555-5555-555555555555",
    "imageUrl": "https://example.com/banner.jpg"
  },
  "isRead": false,
  "createdAtUtc": "2026-04-22T10:10:00Z"
}
```

Mobile guidance:

- this is realtime only
- do not assume every broadcast is persisted as inbox history
- use `type` and `dataObject` to decide whether to show a banner, modal, or silent refresh

## Presence Hub Methods

Hub:

- `/hubs/customer-presence`

This hub supports client-to-server invokes from customer mobile:

- `AppForeground()`
- `AppBackground()`
- `Heartbeat()`

Purpose:

- keep customer presence state accurate
- support admin visibility of online/offline customer presence

### `AppForeground()`

Call when:

- app becomes active in foreground

Expected effect:

- backend marks customer as online

### `AppBackground()`

Call when:

- app goes to background

Expected effect:

- backend starts offline grace handling

### `Heartbeat()`

Call when:

- app stays active in foreground

Recommended mobile behavior:

- call periodically while the app is active and connected

Important backend timing:

- heartbeat timeout: `75 seconds`
- offline grace period after background/disconnect: `20 seconds`

## Presence Realtime Event

Event name:

- `customerPresenceUpdated`

Payload shape:

```json
{
  "customerId": "11111111-1111-1111-1111-111111111111",
  "isOnlineNow": true,
  "lastSeenAtUtc": "2026-04-22T10:12:00Z"
}
```

Important note:

- this event is sent to admin presence listeners
- customer mobile should not depend on this event for orders or notifications

## Recommended Mobile Subscription Set

For customer mobile, the recommended listeners are:

- `ReceiveNotification`
- `ReceiveOrderStatusChanged`
- `ReceiveDriverArrivalStateChanged`
- `ReceiveBroadcast`

Recommended optional invokes on presence hub:

- `AppForeground`
- `AppBackground`
- `Heartbeat`

## Order And Driver Realtime Strategy

Use this rule in mobile:

1. `ReceiveOrderStatusChanged` is the direct source for live order status changes.
2. `ReceiveDriverArrivalStateChanged` is the direct source for live driver-arrival changes.
3. `ReceiveNotification` updates inbox and badge state.
4. `ReceiveBroadcast` handles app-wide customer broadcasts.

## Flutter-style Pseudo Flow

```text
1. Login and get customer access token
2. Connect to /hubs/notifications using accessTokenFactory
3. Listen to:
   - ReceiveNotification
   - ReceiveOrderStatusChanged
   - ReceiveDriverArrivalStateChanged
   - ReceiveBroadcast
4. Connect to /hubs/customer-presence
5. Invoke AppForeground when app becomes active
6. Invoke Heartbeat while app is active
7. Invoke AppBackground when app goes to background
```

## Related Backend Files

- `src/Zadana.Api/Program.cs`
- `src/Zadana.Api/Realtime/NotificationHub.cs`
- `src/Zadana.Api/Realtime/NotificationService.cs`
- `src/Zadana.Api/Realtime/CustomerPresenceHub.cs`
- `src/Zadana.Api/Realtime/CustomerPresenceService.cs`
- `src/Zadana.Api/Realtime/Contracts/OrderStatusChangedRealtimePayload.cs`
- `src/Zadana.Api/Realtime/Contracts/DriverArrivalStateChangedRealtimePayload.cs`
- `src/Zadana.Api/Realtime/Contracts/CustomerPresenceUpdatedDto.cs`
