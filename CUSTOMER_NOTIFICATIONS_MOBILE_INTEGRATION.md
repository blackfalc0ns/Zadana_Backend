# Customer Notifications Mobile Integration Guide

This document explains how the customer notifications APIs should be integrated from the mobile app, especially Flutter.

## Scope

The current backend supports two notification-related areas:

1. Inbox notifications stored in the database and shown inside the app.
2. Push device registration for the authenticated customer.
3. Real-time notification delivery over SignalR for authenticated users.

Important:

- The backend currently supports storing notifications, listing them, unread counts, mark-as-read flows, and registering customer push devices.
- The backend currently supports real-time notification events on `/hubs/notifications` for authenticated users.
- The backend does **not** currently send real push notifications through Firebase or APNs yet.
- Mobile can safely integrate the notification inbox and device registration today.

## Base Requirements

All endpoints in this guide require:

- An authenticated customer user
- `Authorization: Bearer <access_token>`

These APIs are protected by the `CustomerOnly` policy.

## Main Concepts

### 1. Inbox Notifications

These are the notifications stored in the backend and displayed in the app inbox.

Examples:

- Order status updated
- Order cancelled
- Marketing/banner style app notifications

### 2. Push Device Registration

These endpoints register the customer device and its push token.

Examples:

- Store the FCM token after login
- Update the token when Firebase refreshes it
- Disable notifications for this device
- Unregister the device on logout

## Backend Files

Key backend files involved:

- `src/Zadana.Api/Modules/Social/Controllers/NotificationsController.cs`
- `src/Zadana.Api/Modules/Social/Controllers/NotificationDevicesController.cs`
- `src/Zadana.Api/Modules/Social/Requests/NotificationDeviceRequests.cs`
- `src/Zadana.Application/Modules/Social/Queries/NotificationQueries.cs`
- `src/Zadana.Application/Modules/Social/Commands/NotificationDeviceCommands.cs`
- `src/Zadana.Api/Realtime/NotificationService.cs`
- `src/Zadana.Api/Realtime/NotificationHub.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`

## Real-time Notifications (SignalR)

The backend exposes a real-time notifications hub at:

```http
/hubs/notifications
```

The mobile app should connect with the same customer `Bearer token` using the SignalR `access_token` query string flow.

### Recommended SignalR Usage

- Connect after login using the current customer access token.
- Listen for `ReceiveNotification`.
- Treat the received payload as the same logical notification contract used by `GET /api/notifications`.
- Refresh `GET /api/notifications/unread-count` when needed for badge reconciliation, but do not require polling for every notification.

### Real-time Event Name

```text
ReceiveNotification
```

### Real-time Payload Shape

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "titleAr": "تم تحديث حالة الطلب",
  "titleEn": "Order update",
  "bodyAr": "تم تحديث حالة طلبك رقم 12345",
  "bodyEn": "Your order #12345 status has been updated",
  "type": "order_status_changed",
  "referenceId": "22222222-2222-2222-2222-222222222222",
  "data": "{\"orderId\":\"22222222-2222-2222-2222-222222222222\",\"action\":\"status_changed\",\"targetUrl\":\"/orders/22222222-2222-2222-2222-222222222222\"}",
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
  "createdAtUtc": "2026-04-18T12:34:56Z"
}
```

### Mobile Recommendation

- Use `referenceId` or `dataObject.orderId` for navigation to order details.
- Prefer `dataObject.targetUrl` when the app wants to preserve backend routing intent.
- Use SignalR as the primary real-time channel.
- Use `GET /api/notifications` as the source for inbox history and pagination.

## Inbox APIs

### GET `/api/notifications`

Returns a paginated list of customer notifications.

#### Query Parameters

- `page`: page number, default `1`
- `per_page`: page size, default `20`
- `type`: optional notification type filter
- `is_read`: optional read/unread filter
- `from_utc`: optional start date-time filter
- `to_utc`: optional end date-time filter

#### Example Request

```http
GET /api/notifications?page=1&per_page=20&type=order_status_changed&is_read=false
Authorization: Bearer <token>
```

#### Example Response

```json
{
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "titleAr": "تم تحديث حالة الطلب",
      "titleEn": "Order update",
      "bodyAr": "تم تحديث حالة طلبك رقم 12345",
      "bodyEn": "Your order #12345 status has been updated",
      "type": "order_status_changed",
      "referenceId": "22222222-2222-2222-2222-222222222222",
      "data": "{\"orderId\":\"22222222-2222-2222-2222-222222222222\",\"action\":\"placed\"}",
      "dataObject": {
        "orderId": "22222222-2222-2222-2222-222222222222",
        "orderNumber": "12345",
        "vendorId": "33333333-3333-3333-3333-333333333333",
        "oldStatus": "PendingPayment",
        "newStatus": "PendingVendorAcceptance",
        "actorRole": "system",
        "action": "placed",
        "targetUrl": "/orders/22222222-2222-2222-2222-222222222222"
      },
      "isRead": false,
      "createdAtUtc": "2026-04-18T12:34:56Z"
    }
  ],
  "page": 1,
  "perPage": 20,
  "total": 57,
  "unreadCount": 5,
  "hasMore": true
}
```

#### Notes

- `type` describes the notification kind.
- `referenceId` usually points to the main related entity, such as an `orderId`.
- `data` is the raw string payload.
- `dataObject` is the parsed JSON payload and should be preferred by mobile when available.
- `dataObject.targetUrl` is included for order-related notifications and can be used as the preferred navigation target.
- `unreadCount` is returned with the list to reduce extra calls.
- `hasMore` should be used for pagination.

### GET `/api/notifications/unread-count`

Returns only the unread count.

#### Example Request

```http
GET /api/notifications/unread-count
Authorization: Bearer <token>
```

#### Example Response

```json
{
  "count": 5
}
```

#### Recommended Use

- Notification badge
- Home screen indicator
- Lightweight refresh

### POST `/api/notifications/{id}/read`

Marks one notification as read.

#### Example Request

```http
POST /api/notifications/11111111-1111-1111-1111-111111111111/read
Authorization: Bearer <token>
```

#### Example Response

```json
{
  "message": "notification marked as read"
}
```

#### Recommended Use

- When the user opens a notification
- When the user opens the related screen from the notification

### POST `/api/notifications/read-all`

Marks all customer notifications as read.

#### Example Request

```http
POST /api/notifications/read-all
Authorization: Bearer <token>
```

#### Example Response

```json
{
  "message": "all notifications marked as read",
  "count": 12
}
```

#### Recommended Use

- `Mark all as read` action in the inbox screen

## Push Device APIs

These APIs register the customer's mobile device and push token.

### GET `/api/notifications/devices`

Returns all devices currently registered for the authenticated customer.

#### Example Request

```http
GET /api/notifications/devices
Authorization: Bearer <token>
```

#### Example Response

```json
{
  "items": [
    {
      "id": "44444444-4444-4444-4444-444444444444",
      "deviceToken": "fcm-token-here",
      "platform": "fcm",
      "deviceId": "android-unique-id",
      "deviceName": "Samsung A55",
      "appVersion": "1.0.0",
      "locale": "ar",
      "notificationsEnabled": true,
      "isActive": true,
      "lastRegisteredAtUtc": "2026-04-18T10:00:00Z",
      "lastSeenAtUtc": "2026-04-18T10:00:00Z"
    }
  ]
}
```

#### Recommended Use

- Debugging
- Optional device management screen

### POST `/api/notifications/devices/register`

Registers or updates a customer device.

#### Request Body

```json
{
  "deviceToken": "fcm-token-here",
  "platform": "fcm",
  "deviceId": "android-unique-id",
  "deviceName": "Samsung A55",
  "appVersion": "1.0.0",
  "locale": "ar",
  "notificationsEnabled": true
}
```

#### Required Fields

- `deviceToken`
- `platform`

#### Supported Platforms

- `fcm`
- `apns`

#### Recommended Use

Call this endpoint:

- After login
- On app startup after login
- After `FirebaseMessaging.onTokenRefresh`
- After reinstall or token change

#### Backend Behavior

The backend will:

- Update the device if the same `deviceToken` already exists
- Or update the device if the same `deviceId` already exists for the same user
- Otherwise create a new device record

### PUT `/api/notifications/devices/preferences`

Updates notification preferences for a registered device.

#### Request Body

```json
{
  "deviceId": "android-unique-id",
  "deviceToken": "fcm-token-here",
  "notificationsEnabled": false
}
```

#### Important Rule

At least one of the following must be supplied:

- `deviceId`
- `deviceToken`

#### Recommended Use

- When the user disables notifications inside the app
- When the user enables notifications again

### POST `/api/notifications/devices/unregister`

Unregisters or deactivates a device.

#### Request Body

```json
{
  "deviceId": "android-unique-id",
  "deviceToken": "fcm-token-here"
}
```

#### Response

```json
{
  "count": 1
}
```

#### Recommended Use

- On logout
- On account removal from the device
- When removing an old token association

## Notification Types

Common notification types currently used by the backend include:

- `order_status_changed`
- `order_cancelled`
- `order_placed`
- `vendor_new_order`
- `new_banner`

For customer mobile, the most important ones are usually:

- `order_status_changed`
- `order_cancelled`

## Recommended Mobile Handling Strategy

Mobile should not depend on the visible text only.

Use:

- `type`
- `referenceId`
- `dataObject`

For example:

- `type = order_status_changed`
- `referenceId = orderId`
- `dataObject.action = placed`

This is better than deciding behavior based on `title` or `body`.

## Recommended Flutter Flow

### On Login

1. Authenticate the customer
2. Get the FCM token from Firebase
3. Call `POST /api/notifications/devices/register`

### On Token Refresh

1. Listen to Firebase token refresh
2. Call `POST /api/notifications/devices/register` again

### On Opening the Notifications Screen

1. Call `GET /api/notifications?page=1&per_page=20`
2. Store the response page
3. Use `hasMore` to paginate

### On Badge Refresh

1. Call `GET /api/notifications/unread-count`

### On Opening a Single Notification

1. Call `POST /api/notifications/{id}/read`
2. Navigate using `type`, `referenceId`, and `dataObject`

### On Mark All as Read

1. Call `POST /api/notifications/read-all`

### On Logout

1. Call `POST /api/notifications/devices/unregister`

## Suggested Flutter Models

### Notification Model

```dart
class AppNotification {
  final String id;
  final String titleAr;
  final String titleEn;
  final String bodyAr;
  final String bodyEn;
  final String? type;
  final String? referenceId;
  final String? data;
  final Map<String, dynamic>? dataObject;
  final bool isRead;
  final DateTime createdAtUtc;

  AppNotification({
    required this.id,
    required this.titleAr,
    required this.titleEn,
    required this.bodyAr,
    required this.bodyEn,
    required this.type,
    required this.referenceId,
    required this.data,
    required this.dataObject,
    required this.isRead,
    required this.createdAtUtc,
  });

  factory AppNotification.fromJson(Map<String, dynamic> json) {
    return AppNotification(
      id: json['id'],
      titleAr: json['titleAr'] ?? '',
      titleEn: json['titleEn'] ?? '',
      bodyAr: json['bodyAr'] ?? '',
      bodyEn: json['bodyEn'] ?? '',
      type: json['type'],
      referenceId: json['referenceId'],
      data: json['data'],
      dataObject: json['dataObject'] == null
          ? null
          : Map<String, dynamic>.from(json['dataObject']),
      isRead: json['isRead'] ?? false,
      createdAtUtc: DateTime.parse(json['createdAtUtc']),
    );
  }
}
```

### Notifications Page Model

```dart
class NotificationsPage {
  final List<AppNotification> items;
  final int page;
  final int perPage;
  final int total;
  final int unreadCount;
  final bool hasMore;

  NotificationsPage({
    required this.items,
    required this.page,
    required this.perPage,
    required this.total,
    required this.unreadCount,
    required this.hasMore,
  });
}
```

## Suggested Navigation Logic

```dart
void handleNotificationTap(AppNotification notification) {
  switch (notification.type) {
    case 'order_status_changed':
    case 'order_cancelled':
      final orderId =
          notification.referenceId ?? notification.dataObject?['orderId'];
      if (orderId != null) {
        // navigate to order details screen
      }
      break;
    default:
      // fallback behavior
      break;
  }
}
```

## Important Limitation

The current backend does **not** yet deliver push notifications through Firebase or APNs.

What is already ready:

- Device token registration
- Device preference updates
- Device deactivation
- Notification inbox APIs

What still needs a backend delivery implementation later:

- Sending push notifications to FCM
- Sending push notifications to APNs
- Triggering real device push on notification creation

## Error Cases to Expect

### Authentication Error

If no valid customer token is sent:

- `USER_NOT_AUTHENTICATED`

### Register Device Errors

- `DEVICE_TOKEN_REQUIRED`
- `INVALID_PUSH_PLATFORM`

### Preferences or Unregister Errors

If both `deviceId` and `deviceToken` are missing:

- `DEVICE_IDENTIFIER_REQUIRED`

## Final Recommendation

For the mobile team today:

- Integrate the inbox APIs immediately
- Integrate device registration immediately
- Use `type + referenceId + dataObject` for notification behavior
- Do not assume real push delivery is active yet

For the next backend phase:

- Add FCM/APNs sending service
- Reuse the existing `UserPushDevices` registrations
- Trigger push delivery whenever a new customer notification is created
