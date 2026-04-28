# Order Realtime Subscription Contract

## Order Status Changed

Hub URL:

```text
/hubs/notifications
```

Event Name:

```text
ReceiveOrderStatusChanged
```

Subscribe Method:

```dart
connection.on('ReceiveOrderStatusChanged', (arguments) {
  final payload = arguments?[0];
});
```

Subscribe Params:

```text
No manual subscribe params.
```

The mobile app does not send `orderId` to the hub. The backend resolves the authenticated user from the access token and adds the connection to the user's internal SignalR group automatically.

The app should filter locally:

```dart
payload['orderId'] == openedOrderId
```

Auth Required:

```text
Yes
```

The hub is protected by `[Authorize]`. Connect with the customer access token using SignalR `access_token`.

```dart
final connection = HubConnectionBuilder()
    .withUrl(
      'https://api.example.com/hubs/notifications',
      options: HttpConnectionOptions(
        accessTokenFactory: () async => accessToken,
      ),
    )
    .withAutomaticReconnect()
    .build();
```

Payload Sample:

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "ORD-12345",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "pending",
  "newStatus": "accepted",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-28T10:05:00Z"
}
```

Supported `newStatus` values for tracking screens:

```text
pending
accepted
preparing
out_for_delivery
delivered
returning
cancelled
```

## Driver Arrival State Changed

Hub URL:

```text
/hubs/notifications
```

Event Name:

```text
ReceiveDriverArrivalStateChanged
```

Subscribe Method:

```dart
connection.on('ReceiveDriverArrivalStateChanged', (arguments) {
  final payload = arguments?[0];
});
```

Subscribe Params:

```text
No manual subscribe params.
```

The app should filter locally:

```dart
payload['orderId'] == openedOrderId
```

Auth Required:

```text
Yes
```

Payload Sample:

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "ORD-12345",
  "arrivalState": "arrived_at_customer",
  "driverName": "Ahmed Ali",
  "actorRole": "driver",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-28T10:08:00Z"
}
```

Supported `arrivalState` values:

```text
en_route
arrived_at_vendor
arrived_at_customer
```
