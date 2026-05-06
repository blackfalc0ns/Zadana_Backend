# Driver Notifications Mobile Handoff

## Summary

Driver notifications now support the same three app states as the customer path:

- `foreground`: SignalR + normal API responses
- `background`: displayable OneSignal push
- `killed/terminated`: displayable OneSignal push with cold-start deep linking

## New SignalR Events

Connect to `/hubs/notifications` and handle:

- `ReceiveNotification`
- `ReceiveDeliveryOffer`
- `ReceiveAssignmentUpdated`
- `ReceiveOrderStatusChanged`
- `ReceiveOrderSupportCaseChanged`
- `ReceiveDriverHomeUpdated`
- `ReceiveDriverWalletUpdated`

## Push Profiles

The backend now uses two mobile push profiles for drivers:

- `MobileHeadsUp`
  - Android channel: `zadana_heads_up_notifications`
  - used for urgent events like new offers, suspend, active order cancelled, support evidence requests
- `MobileStandard`
  - Android channel: `zadana_driver_general_notifications`
  - used for non-urgent account, wallet, assignment, and support updates

All driver mobile pushes are displayable and include:

- root `headings`
- root `contents`
- `content_available = true`
- `mutable_content = true`
- `data.click_action = FLUTTER_NOTIFICATION_CLICK`
- flattened business payload fields inside `data`

## Payload Contract

The minimum deep-link payload keys now used by backend driver notifications are:

- `screen`
- `event`
- `orderId`
- `assignmentId`
- `supportCaseId`
- `withdrawalId`

Current `screen` values:

- `home`
- `assignment_detail`
- `support_case_detail`
- `wallet`
- `account_status`
- `notifications_center`

Examples of `event` values:

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

## App Lifecycle Requirements

At app start:

- initialize OneSignal before routing
- register foreground and opened/tap listeners before first screen render
- restore any initial notification-open payload on cold start

After login:

- call device register endpoint
- open `SignalR` connection

On token refresh:

- re-register the device token

On logout:

- call device unregister endpoint
- disconnect `SignalR`

## Device Preferences API

`GET/POST/PUT /api/drivers/notifications/devices*` now includes:

- `dispatchPushEnabled`
- `assignmentPushEnabled`
- `supportPushEnabled`
- `walletPushEnabled`
- `accountPushEnabled`

These flags mute push delivery only.
Inbox persistence and SignalR updates still happen.

## Driver Notifications API

`GET /api/drivers/notifications` now accepts:

- `type`
- `category`
- `priority`
- `is_read`
- `from_utc`
- `to_utc`

Every notification item now includes:

- `category`
- `priority`

## Reconciliation Rules

After a push tap from `background` or `killed`:

1. parse `data.screen`
2. navigate to the target route
3. fetch the REST source of truth for that screen
4. refresh unread count

Recommended source-of-truth mapping:

- `home` -> `GET /api/drivers/home`
- `assignment_detail` -> `GET /api/drivers/assignments/{assignmentId}`
- `support_case_detail` -> your support case detail endpoint
- `wallet` -> `GET /api/drivers/wallet`
- `account_status` -> `GET /api/drivers/home` or `GET /api/drivers/me/status`

## New Realtime Payloads

`ReceiveDriverHomeUpdated`

- same payload shape as `GET /api/drivers/home`

`ReceiveDriverWalletUpdated`

- `currentBalance`
- `pendingBalance`
- `withdrawalSummary`
- `recentTransactions` with max 3 items
