# Driver App – Real-Time Order Detail Contract

## Status

- `implemented`
- Build: ✅ passing

## Purpose

This contract describes how the driver mobile app can achieve **real-time order detail updates** without manual page refresh.

When the driver performs any action (verify OTP, mark arrived, change status), the API response now includes the **full updated assignment detail** — enabling the mobile app to replace its local state and update the UI instantly.

---

## Overview: Two Channels of Real-Time Updates

| Channel | When to use |
|---|---|
| **API Response** (`updatedAssignment`) | The driver itself performed the action — use the response to update UI immediately |
| **SignalR** (`ReceiveAssignmentUpdated`) | Another actor (vendor/admin/system) changed the order — listen and update UI |

---

## Channel 1: API Response — `updatedAssignment`

Every driver action endpoint now returns a new field `updatedAssignment` containing the full `DriverAssignmentDetailDto`.

### Affected Endpoints

---

### 1. Verify OTP

```
POST /api/drivers/assignments/{assignmentId}/verify-otp
```

**Request Body:**

```json
{
  "otpType": "pickup",
  "otpCode": "1234"
}
```

**Response — `DriverOtpVerificationResultDto`:**

```json
{
  "assignmentId": "aaa-bbb-ccc",
  "orderId": "ddd-eee-fff",
  "otpType": "pickup",
  "status": "picked_up",
  "messageAr": "تم التحقق من رمز الاستلام بنجاح",
  "messageEn": "Pickup OTP verified successfully",
  "updatedAssignment": { ... }
}
```

---

### 2. Update Order Status

```
POST /api/drivers/orders/{orderId}/picked-up
POST /api/drivers/orders/{orderId}/on-the-way
POST /api/drivers/orders/{orderId}/delivered
POST /api/drivers/orders/{orderId}/delivery-failed
```

**Response — `DriverUpdateOrderStatusResultDto`:**

```json
{
  "orderId": "ddd-eee-fff",
  "status": "PickedUp",
  "messageAr": "تم تحديث حالة الطلب",
  "messageEn": "Order status updated",
  "updatedAssignment": { ... }
}
```

---

### 3. Update Arrival State

```
POST /api/drivers/orders/{orderId}/arrived-at-vendor
POST /api/drivers/orders/{orderId}/arrived-at-customer
```

**Response — `DriverArrivalStateResultDto`:**

```json
{
  "orderId": "ddd-eee-fff",
  "assignmentId": "aaa-bbb-ccc",
  "arrivalState": "arrived_at_vendor",
  "messageAr": "تم تسجيل الوصول إلى المتجر",
  "messageEn": "Arrival at vendor recorded",
  "updatedAssignment": { ... }
}
```

---

## The `updatedAssignment` Object — Full Shape

This is the exact same object returned by `GET /api/drivers/assignments/{assignmentId}`.

```json
{
  "assignmentId": "aaa-bbb-ccc",
  "orderId": "ddd-eee-fff",
  "orderNumber": "ORD-20260430-001",
  "assignmentStatus": "PickedUp",
  "homeState": "OnMission",
  "allowedActions": ["mark_on_the_way"],
  "vendorName": "مطعم الشرقية",
  "pickupAddress": "الرياض، حي النزهة، شارع الملك فهد",
  "pickupLatitude": 24.7136,
  "pickupLongitude": 46.6753,
  "storePhone": "+966501234567",
  "customerName": "أحمد محمد",
  "deliveryAddress": "الرياض، حي الروضة، شارع التحلية",
  "deliveryLatitude": 24.7500,
  "deliveryLongitude": 46.7000,
  "customerPhone": "+966509876543",
  "paymentMethod": "CashOnDelivery",
  "codAmount": 150.00,
  "pickupOtpRequired": true,
  "pickupOtpStatus": "verified",
  "deliveryOtpRequired": true,
  "deliveryOtpStatus": "pending",
  "pickupOtpCode": null,
  "driverArrivalState": "en_route",
  "orderItems": [
    {
      "name": "شاورما لحم",
      "quantity": 2,
      "unitPrice": 25.00,
      "lineTotal": 50.00
    },
    {
      "name": "بيبسي كبير",
      "quantity": 1,
      "unitPrice": 5.00,
      "lineTotal": 5.00
    }
  ]
}
```

### Key Fields for UI Logic

| Field | Type | Description |
|---|---|---|
| `assignmentStatus` | string | Current assignment status: `Accepted`, `ArrivedAtVendor`, `PickedUp`, `ArrivedAtCustomer`, `Delivered`, `Failed` |
| `homeState` | string | Overall state: `OnMission`, `WaitingForOffer`, `Offline`, etc. |
| `allowedActions` | string[] | Actions the driver can take right now: `accept_offer`, `reject_offer`, `arrived_at_vendor`, `mark_on_the_way`, `arrived_at_customer`, `verify_delivery_otp` |
| `pickupOtpRequired` | bool | Whether this assignment requires pickup OTP |
| `pickupOtpStatus` | string | `pending`, `verified`, `not_required` |
| `pickupOtpCode` | string? | The 4-digit pickup OTP code — only visible when driver is in the handoff window (ArrivedAtVendor status). `null` otherwise |
| `deliveryOtpRequired` | bool | Whether delivery OTP is required |
| `deliveryOtpStatus` | string | `pending`, `verified`, `not_required` |
| `driverArrivalState` | string | `en_route`, `arrived_at_vendor`, `arrived_at_customer` |
| `codAmount` | decimal | Cash to collect from customer. `0` for non-COD orders |

---

## Channel 2: SignalR — `ReceiveAssignmentUpdated`

### Hub

- URL: `/hubs/notifications`
- Authentication: JWT token via `access_token` query parameter

### Event Name

```
ReceiveAssignmentUpdated
```

### When It Fires

- When the **vendor** confirms an order (vendor acceptance → driver gets updated assignment)
- When the **admin** changes order status
- When any order status changes that affects the driver's assignment
- After the driver's own actions (as a confirmation echo)

### Payload

The exact same `DriverAssignmentDetailDto` object documented above.

```json
{
  "assignmentId": "aaa-bbb-ccc",
  "orderId": "ddd-eee-fff",
  "orderNumber": "ORD-20260430-001",
  "assignmentStatus": "PickedUp",
  "homeState": "OnMission",
  "allowedActions": ["mark_on_the_way"],
  ...
}
```

### Other SignalR Events Relevant to Driver

| Event | Payload | Purpose |
|---|---|---|
| `ReceiveDeliveryOffer` | `DeliveryOfferRealtimePayload` | New delivery offer pushed to driver |
| `ReceiveOrderStatusChanged` | `OrderStatusChangedRealtimePayload` | Lightweight status change notification |
| `ReceiveAssignmentUpdated` | `DriverAssignmentDetailDto` | **Full** assignment detail refresh |

---

## Mobile Integration Guide (Flutter/Dart)

### After API Calls

```dart
// Example: Verify pickup OTP
final response = await driverApi.verifyOtp(
  assignmentId: currentAssignment.assignmentId,
  otpType: 'pickup',
  otpCode: enteredCode,
);

// Show success message
showSnackbar(response.messageAr);

// Immediately update UI with full detail — no second API call needed
if (response.updatedAssignment != null) {
  setState(() {
    currentAssignment = response.updatedAssignment!;
    // The OTP input disappears because pickupOtpStatus is now "verified"
    // allowedActions changes to ["mark_on_the_way"]
    // driverArrivalState returns to "en_route" so the handoff step closes
    // assignmentStatus updates to "PickedUp"
  });
}
```

### SignalR Listener

```dart
// Listen for external changes (vendor/admin actions)
hubConnection.on('ReceiveAssignmentUpdated', (args) {
  final detail = DriverAssignmentDetailDto.fromJson(args[0]);

  // Only update if we're viewing this assignment
  if (detail.assignmentId == currentAssignment?.assignmentId) {
    setState(() {
      currentAssignment = detail;
    });
  }
});
```

### Recommended SignalR Connection

```dart
final hubConnection = HubConnectionBuilder()
  .withUrl(
    '${apiBaseUrl.replaceAll("/api", "")}/hubs/notifications',
    HttpConnectionOptions(
      accessTokenFactory: () async => await getAccessToken(),
    ),
  )
  .withAutomaticReconnect()
  .build();

// Listen for all driver-relevant events
hubConnection.on('ReceiveAssignmentUpdated', handleAssignmentUpdated);
hubConnection.on('ReceiveDeliveryOffer', handleNewOffer);
hubConnection.on('ReceiveOrderStatusChanged', handleStatusChanged);
hubConnection.on('ReceiveNotification', handleNotification);

await hubConnection.start();
```

---

## Order Lifecycle — Driver Perspective

```
Accepted
  ↓ POST /orders/{id}/arrived-at-vendor
ArrivedAtVendor
  ↓ POST /assignments/{id}/verify-otp (type=pickup)
PickedUp
  ↓ POST /orders/{id}/on-the-way
OnTheWay
  ↓ POST /orders/{id}/arrived-at-customer
ArrivedAtCustomer
  ↓ POST /assignments/{id}/verify-otp (type=delivery)
Delivered
```

At **every step**, the response includes `updatedAssignment` with the full new state.

---

## Related Backend Files

- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs`
- `src/Zadana.Application/Modules/Delivery/Commands/VerifyAssignmentOtp/VerifyAssignmentOtpCommand.cs`
- `src/Zadana.Application/Modules/Orders/Commands/DriverUpdateOrderStatus/DriverUpdateOrderStatusCommand.cs`
- `src/Zadana.Application/Modules/Delivery/Commands/UpdateDriverArrivalState/UpdateDriverArrivalStateCommand.cs`
- `src/Zadana.Application/Modules/Delivery/DTOs/DriverMobileDtos.cs`
- `src/Zadana.Application/Modules/Delivery/DTOs/DeliveryDtos.cs`
- `src/Zadana.Api/Realtime/NotificationHub.cs`
- `src/Zadana.Api/Realtime/NotificationService.cs`
