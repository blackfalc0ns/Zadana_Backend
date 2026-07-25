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

Payload Sample (delivery):

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
  "changedAtUtc": "2026-04-28T10:05:00Z",
  "fulfillmentType": "Delivery"
}
```

Payload Sample (pickup, customer user channel only):

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "ORD-12346",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "preparing",
  "newStatus": "ready_for_pickup",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-28T10:05:00Z",
  "fulfillmentType": "Pickup",
  "pickupOtpCode": "4821",
  "pickupOtpExpiresAtUtc": "2026-04-28T12:05:00Z",
  "pickupNoShowDeadlineUtc": "2026-04-28T16:05:00Z",
  "pickupBranch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hoursToday": "Today: 10:00 AM - 10:00 PM"
  }
}
```

## Pickup Realtime Field Rules

New optional payload fields:

- `fulfillmentType`
- `pickupOtpCode`
- `pickupOtpExpiresAtUtc`
- `pickupNoShowDeadlineUtc`
- `pickupBranch`

OTP visibility rule:

- pickup OTP secrets are sent only on the authenticated customer's user notification channel
- order-tracking group broadcasts for the same event omit `pickupOtpCode`
- mobile must not expect OTP on shared/non-user channels

When to expect pickup secrets:

- `fulfillmentType = "Pickup"`
- `newStatus = "ready_for_pickup"`
- OTP has not yet been verified

After OTP verification or conversion to delivery, later events omit pickup OTP fields.

Supported `newStatus` values for tracking screens:

```text
pending
accepted
preparing
ready_for_pickup
out_for_delivery
delivered
returning
cancelled
```

Notes:

- the customer keeps receiving `ReceiveOrderStatusChanged` updates until the order reaches `delivered`
- pickup OTP confirmation from the vendor can trigger `newStatus = delivered` for pickup orders
- delivery OTP confirmation from the driver can trigger `newStatus = delivered` for delivery orders
- this contract does not include live GPS or driver location streaming

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

Pickup mobile note:

- ignore `ReceiveDriverArrivalStateChanged` for pickup orders
- this event is delivery-only in customer UI

Arrival updates continue through the handoff and delivery flow, but they do not include live map coordinates in this contract.
