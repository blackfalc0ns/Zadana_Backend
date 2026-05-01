# 🔧 Mobile Fix Required: Order Details Real-Time Updates

## المشكلة

صفحة تفاصيل الطلب (`OrderDetailPage`) عند المندوب **لا تتحدث تلقائياً** عند تغيير الحالة من التاجر أو أي طرف آخر. التحديث يظهر فقط بعد `manual GET /drivers/assignments/{assignmentId}`.

## السبب

التطبيق لا يستمع لـ events الخاصة بتفاصيل الطلب على SignalR hub. حالياً فقط `ReceiveDeliveryOffer` (الـ Home) يعمل.

## التأكيد من الباك إند

✅ الباك إند **يرسل فعلاً** الأحداث التالية عند كل تغيير حالة:

| Event | يُرسل؟ | Payload |
|---|---|---|
| `ReceiveOrderStatusChanged` | ✅ نعم | `orderId`, `orderNumber`, `oldStatus`, `newStatus`, `actorRole` |
| `ReceiveAssignmentUpdated` | ✅ نعم | **Full `DriverAssignmentDetailDto`** (نفس response الـ `GET /assignments/{id}`) |

كلا الحدثين يُرسلان على نفس الـ hub ونفس الـ group اللي `ReceiveDeliveryOffer` يعمل عليه:

```
Hub:   /hubs/notifications
Group: customer-{driverUserId}
```

---

## المطلوب تنفيذه

### 1. في `OrderDetailPage` — أضف listeners عند فتح الصفحة

```dart
// ======================================================
// أضف هذا الكود عند فتح صفحة تفاصيل الطلب (initState أو equivalent)
// ======================================================

// ⭐ الحدث الأساسي: يعطيك snapshot كامل للشاشة
hubConnection.on('ReceiveAssignmentUpdated', (List<Object?>? args) {
  if (args == null || args.isEmpty) return;
  
  final Map<String, dynamic> json = args[0] as Map<String, dynamic>;
  final receivedAssignmentId = json['assignmentId'] as String?;
  
  // فلتر: فقط لو هذا هو الـ assignment المعروض حالياً
  if (receivedAssignmentId == currentAssignmentId) {
    final updatedDetail = DriverAssignmentDetailDto.fromJson(json);
    setState(() {
      currentDetail = updatedDetail;
      // ← الآن تلقائياً:
      //   - assignmentStatus يتحدث
      //   - allowedActions تتحدث (الأزرار تتغير)
      //   - pickupOtpCode يختفي بعد التأكيد
      //   - pickupOtpStatus يتحول لـ "verified"
      //   - driverArrivalState يتحدث
    });
  }
});

// ⭐ حدث مساند: إشارة سريعة أن الحالة تغيرت (payload خفيف)
hubConnection.on('ReceiveOrderStatusChanged', (List<Object?>? args) {
  if (args == null || args.isEmpty) return;
  
  final Map<String, dynamic> json = args[0] as Map<String, dynamic>;
  final receivedOrderId = json['orderId'] as String?;
  
  if (receivedOrderId == currentOrderId) {
    // يمكن استخدامه لعرض toast أو log
    // لا تعتمد عليه وحده لتحديث الـ UI — استخدم ReceiveAssignmentUpdated
    print('Order status changed: ${json['newStatus']}');
  }
});
```

### 2. في `OrderDetailPage` — بعد أي POST action من المندوب نفسه

```dart
// ======================================================
// بعد أي action (arrived, picked-up, on-the-way, verify-otp, etc.)
// استخدم updatedAssignment من الـ response مباشرة
// ======================================================

// مثال: arrived at vendor
Future<void> markArrivedAtVendor() async {
  final response = await api.post('/drivers/orders/$orderId/arrived-at-vendor');
  
  if (response.statusCode == 200) {
    final body = jsonDecode(response.body);
    
    // ⭐ استخدم updatedAssignment فوراً — بدون GET إضافي
    if (body['updatedAssignment'] != null) {
      setState(() {
        currentDetail = DriverAssignmentDetailDto.fromJson(body['updatedAssignment']);
      });
    }
  }
}

// مثال: verify OTP
Future<void> verifyOtp(String otpType, String otpCode) async {
  final response = await api.post(
    '/drivers/assignments/$assignmentId/verify-otp',
    body: {'otpType': otpType, 'otpCode': otpCode},
  );
  
  if (response.statusCode == 200) {
    final body = jsonDecode(response.body);
    
    if (body['updatedAssignment'] != null) {
      setState(() {
        currentDetail = DriverAssignmentDetailDto.fromJson(body['updatedAssignment']);
      });
    }
  }
}
```

### 3. تأكد أن الـ `dispose` يزيل الـ listeners

```dart
@override
void dispose() {
  hubConnection.off('ReceiveAssignmentUpdated');
  hubConnection.off('ReceiveOrderStatusChanged');
  super.dispose();
}
```

---

## الفلترة المطلوبة

| Event | الصفحة تفلتر على |
|---|---|
| `ReceiveAssignmentUpdated` | `payload.assignmentId == currentAssignmentId` |
| `ReceiveOrderStatusChanged` | `payload.orderId == currentOrderId` |

---

## payload مثال: `ReceiveAssignmentUpdated`

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-20260430-001",
  "assignmentStatus": "PickedUp",
  "homeState": "OnMission",
  "allowedActions": ["mark_on_the_way"],
  "vendorName": "Fresh Market",
  "pickupAddress": "45 King Faisal St, Giza",
  "pickupLatitude": 30.0131,
  "pickupLongitude": 31.2089,
  "storePhone": "+201001112223",
  "customerName": "Ahmed Hassan",
  "deliveryAddress": "12 Lebanon Sq, Mohandessin",
  "deliveryLatitude": 30.0551,
  "deliveryLongitude": 31.2106,
  "customerPhone": "+201055566677",
  "paymentMethod": "CashOnDelivery",
  "codAmount": 185.75,
  "pickupOtpRequired": false,
  "pickupOtpStatus": "verified",
  "deliveryOtpRequired": true,
  "deliveryOtpStatus": "pending",
  "pickupOtpCode": null,
  "driverArrivalState": "en_route",
  "orderItems": [
    {
      "name": "Olive Oil 1L",
      "quantity": 2,
      "unitPrice": 52.5,
      "lineTotal": 105.0
    }
  ]
}
```

## payload مثال: `ReceiveOrderStatusChanged`

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-20260430-001",
  "vendorId": "55555555-5555-5555-5555-555555555555",
  "oldStatus": "preparing",
  "newStatus": "out_for_delivery",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/44444444-4444-4444-4444-444444444444",
  "changedAtUtc": "2026-04-30T12:15:30Z"
}
```

---

## متى يُرسل `ReceiveAssignmentUpdated`؟

| الحدث | Actor | يُرسل للمندوب؟ |
|---|---|---|
| التاجر يأكد pickup OTP | vendor | ✅ نعم |
| المندوب يعمل arrived-at-vendor | driver | ✅ نعم |
| المندوب يغير الحالة لـ picked-up | driver | ✅ نعم |
| المندوب يغير الحالة لـ on-the-way | driver | ✅ نعم |
| المندوب يعمل arrived-at-customer | driver | ✅ نعم |
| المندوب يعمل verify OTP | driver | ✅ نعم (عبر response.updatedAssignment) |
| Admin يغير الحالة | admin | ✅ نعم |

---

## ملخص سريع

1. **أضف listener على `ReceiveAssignmentUpdated`** في `OrderDetailPage`
2. **فلتر على `assignmentId`**
3. **استبدل الـ local state بالكامل** بالـ payload (لا تعمل merge جزئي)
4. **بعد أي POST action** — استخدم `response.updatedAssignment` فوراً
5. **لا تحتاج GET إضافي** بعد أي action
