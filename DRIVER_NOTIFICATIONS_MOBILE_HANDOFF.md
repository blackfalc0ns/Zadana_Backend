# Driver Notifications Mobile Handoff

## Goal

This document is the implementation handoff for the mobile developer to connect the driver app notifications end-to-end.

The driver notification flow now matches the customer path in all three app states:

- `foreground`
- `background`
- `killed / terminated`

The backend is already ready.
The mobile app now needs to connect to the contracts below.

## Delivery Model

There are now two delivery channels for the driver app:

1. `SignalR`
   - used while the app is open in `foreground`
   - used for instant UI refresh and realtime state sync

2. `OneSignal displayable push`
   - used for `background`
   - used for `killed / terminated`
   - used for opening the app through notification tap with deep linking

Important:

- do not rely on `SignalR` for `background` or `killed`
- do not rely on system notification tray as source of truth
- after opening from push, always refetch from REST

## OneSignal App

The driver app has its own separate OneSignal application.
It is not the same OneSignal app used by customers.

Mobile must make sure the driver app is initialized with the driver OneSignal app configuration, not the customer configuration.

## Android Channels

The backend uses two Android channels for driver pushes:

- `zadana_heads_up_notifications`
  - for urgent notifications
  - examples: new offer, suspend, active order cancelled, support evidence request

- `zadana_driver_general_notifications`
  - for normal operational notifications
  - examples: wallet updates, account updates, assignment updates, support updates

The mobile app must ensure both channels are created before the first notification is shown.

## Realtime Events

Connect to:

- `/hubs/notifications`

Handle these events:

- `ReceiveNotification`
- `ReceiveDeliveryOffer`
- `ReceiveAssignmentUpdated`
- `ReceiveOrderStatusChanged`
- `ReceiveOrderSupportCaseChanged`
- `ReceiveDriverHomeUpdated`
- `ReceiveDriverWalletUpdated`

## Event Usage

### `ReceiveNotification`

Use this to update:

- notifications list
- unread count
- in-app banners if needed

### `ReceiveDeliveryOffer`

Use this to:

- show a live offer card/modal
- refresh any current dispatch screen

### `ReceiveAssignmentUpdated`

Use this to:

- refresh assignment detail state
- update current mission/task UI

### `ReceiveOrderStatusChanged`

Use this to:

- refresh the visible order/assignment state if the current screen depends on order status

### `ReceiveOrderSupportCaseChanged`

Use this to:

- refresh support case detail
- refresh support lists or badges

### `ReceiveDriverHomeUpdated`

Payload shape matches:

- `GET /api/drivers/home`

Use this to refresh:

- home dashboard
- availability/operational status
- account status banners
- performance restriction banners

### `ReceiveDriverWalletUpdated`

Payload includes:

- `currentBalance`
- `pendingBalance`
- `withdrawalSummary`
- `recentTransactions`

Use this to refresh:

- wallet summary
- withdrawal state
- recent wallet activity

## Push Payload Contract

All driver mobile pushes are displayable and include:

- root `headings`
- root `contents`
- `content_available = true`
- `mutable_content = true`
- `data.click_action = FLUTTER_NOTIFICATION_CLICK`

Driver push payloads include business keys inside `data`.

Minimum keys to support:

- `screen`
- `event`
- `orderId`
- `assignmentId`
- `supportCaseId`
- `withdrawalId`
- `notificationId`
- `type`
- `referenceId`

Treat any additional keys as optional extras.

## Screen Routing Contract

Current `screen` values used by backend:

- `home`
- `assignment_detail`
- `support_case_detail`
- `wallet`
- `account_status`
- `notifications_center`

Recommended mapping:

- `home`
  - open the driver home screen
- `assignment_detail`
  - open assignment detail using `assignmentId`
- `support_case_detail`
  - open support case detail using `supportCaseId`
- `wallet`
  - open wallet screen
- `account_status`
  - open home or account status screen depending on app structure
- `notifications_center`
  - open notifications inbox

## Event Values

Examples of `event` values currently sent by backend:

- `dispatch.offer_new`
- `dispatch.offer_expired`
- `assignment.driver_assigned`
- `assignment.pickup_ready`
- `assignment.active_order_cancelled`
- `support.created`
- `support.request_evidence`
- `support.admin_message`
- `support.approved`
- `support.rejected`
- `support.resolved`
- `wallet.withdrawal_submitted`
- `wallet.withdrawal_paid`
- `wallet.withdrawal_rejected`
- `wallet.admin_adjustment`
- `account.approve`
- `account.request_docs`
- `account.reject`
- `account.suspend`
- `account.reactivate`
- `account.location_blocked`
- `account.location_unblocked`
- `performance.soft_blocked`
- `performance.suspension_candidate`
- `performance.forced_offline`

Use `event` for analytics, conditional UX, or logging, but do not build brittle logic that depends only on event names when a REST refetch can be used.

## App Lifecycle Requirements

### At app start

Must happen before first route render:

1. initialize OneSignal
2. register foreground notification listener
3. register notification opened/tap listener
4. restore initial notification-open payload for cold start

### After login

Must do all of the following:

1. call the driver notification device register endpoint
2. open `SignalR` connection
3. subscribe to all events listed above

### On token refresh

Must:

1. re-register the device token with the backend

### On logout

Must:

1. call the device unregister endpoint
2. disconnect `SignalR`
3. clear any notification-specific transient state

## Required API Endpoints

### Device registration

Use the driver notification device endpoints:

- `GET /api/drivers/notifications/devices*`
- `POST /api/drivers/notifications/devices*`
- `PUT /api/drivers/notifications/devices*`

These endpoints now include:

- `dispatchPushEnabled`
- `assignmentPushEnabled`
- `supportPushEnabled`
- `walletPushEnabled`
- `accountPushEnabled`

Important:

- these flags mute push only
- inbox still persists
- SignalR still works while app is open

### Notification list

Use:

- `GET /api/drivers/notifications`

Supported filters:

- `type`
- `category`
- `priority`
- `is_read`
- `from_utc`
- `to_utc`

Notification items now include:

- `category`
- `priority`

### Reconciliation sources of truth

After a notification tap or realtime event, use these APIs as source of truth:

- `home` -> `GET /api/drivers/home`
- `assignment_detail` -> `GET /api/drivers/assignments/{assignmentId}`
- `support_case_detail` -> support case detail endpoint used by the app
- `wallet` -> `GET /api/drivers/wallet`
- `account_status` -> `GET /api/drivers/home` or `GET /api/drivers/me/status`

## Foreground Behavior

When the app is open:

- use `SignalR` as the primary update channel
- update UI/store immediately
- optionally show an in-app banner/snackbar
- do not depend on push tray notification for state updates

Recommended behavior:

- `ReceiveNotification` updates inbox and unread count
- `ReceiveDeliveryOffer` updates dispatch UI instantly
- `ReceiveDriverHomeUpdated` patches or refetches home data
- `ReceiveDriverWalletUpdated` patches or refetches wallet data

## Background Behavior

When the app is in background:

- OneSignal push is the primary delivery mechanism
- tapping the notification should:
  1. open the app
  2. parse `data.screen`
  3. navigate to the target route
  4. fetch REST source of truth
  5. refresh unread count

## Killed State Behavior

When the app is fully terminated:

- OneSignal push is the only guaranteed delivery mechanism
- the opened/tap listener must work during cold start
- routing must happen only after initial app services are ready

Recommended flow:

1. app starts from notification tap
2. initialize OneSignal and app services
3. restore the opening payload
4. login gate if needed
5. resume pending navigation
6. fetch source of truth for the destination screen

## Dedup and Safety

The mobile app should protect against duplicate processing.

Recommended:

- deduplicate by `notificationId` when available
- ignore empty or malformed payloads safely
- log unknown `screen` values instead of crashing
- log unknown `event` values without blocking navigation

## Suggested Flutter Integration Shape

Recommended service split:

### `driver_notification_bootstrap_service`

Responsibilities:

- initialize OneSignal
- wire foreground and opened listeners
- capture cold-start notification payload

### `driver_notification_device_service`

Responsibilities:

- register device after login
- update token on refresh
- unregister on logout
- manage push preferences

### `driver_realtime_service`

Responsibilities:

- open `SignalR`
- subscribe to notification hub events
- dispatch events to app stores/blocs/notifiers

### `driver_notification_router_service`

Responsibilities:

- parse push payload
- map `screen` to app route
- defer navigation until app/session is ready

## Suggested Processing Rules

### On foreground SignalR event

1. patch local store or trigger fetch
2. update unread count if needed
3. show in-app UX only if helpful

### On push received in foreground

1. do not create conflicting duplicate UI if SignalR already handled it
2. prefer app-level banner over duplicated modal behavior

### On push tap from background or killed

1. parse payload
2. navigate
3. fetch source of truth
4. refresh unread count

## Example Push Payload

Example structure the mobile app should expect:

```json
{
  "notificationId": "8b6f2a7e-1111-2222-3333-44c8b9e71abc",
  "type": "driver_assignment_update",
  "referenceId": "6d3638e1-1111-2222-3333-7a14f0f41abc",
  "screen": "assignment_detail",
  "event": "assignment.pickup_ready",
  "assignmentId": "6d3638e1-1111-2222-3333-7a14f0f41abc",
  "orderId": "04b7fd51-1111-2222-3333-24535df61abc",
  "click_action": "FLUTTER_NOTIFICATION_CLICK"
}
```

Not every payload will contain every ID.
The app must handle missing optional values safely.

## Example State Handling

### Case A: new delivery offer while app is open

Expected:

- `ReceiveDeliveryOffer` arrives
- dispatch UI updates instantly
- inbox/unread updates through `ReceiveNotification`

### Case B: support evidence request while app is in background

Expected:

- displayable push appears
- tap opens support case route
- app refetches support case detail

### Case C: driver suspended while app is killed

Expected:

- displayable push appears
- tap cold-starts the app
- app restores payload
- app navigates to `account_status` or home
- app refetches `GET /api/drivers/home`
- UI reflects suspended state

### Case D: wallet withdrawal paid

Expected:

- wallet push appears if wallet push is enabled
- wallet screen opens on tap
- app refetches `GET /api/drivers/wallet`
- latest balances and transactions are shown

## Mobile Acceptance Checklist

- OneSignal initializes before routing
- cold-start notification payload is captured correctly
- app registers driver device after login
- app unregisters device on logout
- SignalR connects only for authenticated driver session
- all listed SignalR events are handled
- push tap works in `background`
- push tap works in `killed`
- route navigation uses `screen`
- data reconciliation uses REST, not stale local assumptions
- unread count refreshes after push tap
- both Android channels exist
- duplicate processing is prevented

## Final Notes

- backend delivery is ready
- customer and driver OneSignal apps are separate
- the driver mobile app must register into the driver OneSignal app only
- if raw payloads ever look inconsistent, log them and verify against backend contracts before adding client-side workarounds
