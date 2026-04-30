# Driver App Order Details Contract

> آخر تحديث: 2026-04-30
> الحالة: `implemented in backend code`
> المصدر المعتمد: كود الـ backend نفسه

## الهدف

هذا الملف يجمع في مكان واحد كل ما يخص صفحة `Order Details` في تطبيق المندوب:

- من أين الصفحة تحمل البيانات أول مرة
- ما هي الـ streams / SignalR events التي تؤثر على الصفحة
- كل stream يسمع على أي group / id
- الصفحة تفلتر على `assignmentId` أم `orderId`
- ما هي الـ REST actions الموجودة داخل الصفحة
- أمثلة `request` و `response` JSON
- ما هو الـ source of truth الحقيقي للواجهة

## الخلاصة السريعة

- Hub المستخدم: `/hubs/notifications`
- Authentication للـ SignalR: `access_token` في query string
- المندوب عند الاتصال يدخل تلقائيًا في group اسمه:

```text
customer-{driverUserId}
```

- رغم أن اسم الـ prefix هو `customer-`، الكود الحالي يستخدمه لكل المستخدمين وليس العملاء فقط.
- الصفحة نفسها يتم فتحها فعليًا بالـ `assignmentId`
- بعض actions تعمل بالـ `orderId`
- أهم stream للصفحة هو:

```text
ReceiveAssignmentUpdated
```

- الـ event المساند الذي يعطي إشارة سريعة أن الحالة تغيرت هو:

```text
ReceiveOrderStatusChanged
```

- عندما المندوب نفسه يعمل action من الصفحة، الـ source of truth الأول هو `updatedAssignment` الموجود في نفس رد الـ API.
- الـ SignalR هنا يعتبر instant sync / echo / external refresh لو التغيير جاء من vendor أو admin أو system.

---

## 1. الـ IDs المستخدمة في الصفحة

| النوع | يستخدم في ماذا |
|---|---|
| `driverUserId` | الـ SignalR group: `customer-{driverUserId}` |
| `assignmentId` | فتح صفحة التفاصيل + `GET assignment detail` + `verify-otp` + الفلترة الأساسية لحدث `ReceiveAssignmentUpdated` |
| `orderId` | status actions مثل `picked-up`, `on-the-way`, `delivered`, `delivery-failed` + الفلترة في `ReceiveOrderStatusChanged` |

## 2. الـ Source Of Truth للواجهة

### أ. أول تحميل للصفحة

الصفحة تعتمد على:

```http
GET /api/drivers/assignments/{assignmentId}
```

### ب. بعد أي action من نفس المندوب

الواجهة تعتمد على:

```json
updatedAssignment
```

الموجود داخل response نفسه.

### ج. لو التغيير حصل من طرف آخر

الواجهة تعتمد على:

```text
ReceiveAssignmentUpdated
```

### د. هل `ReceiveOrderStatusChanged` يكفي وحده؟

لا.

هذا الحدث payload خفيف، ويعطيك إشارة أن الحالة تغيرت، لكنه ليس snapshot كامل للشاشة. لذلك استخدامه الصحيح في صفحة التفاصيل يكون:

- تحديث سريع أو logging
- أو trigger لإعادة state من `ReceiveAssignmentUpdated`

---

## 3. الـ Bootstrap Flow الخاص بصفحة التفاصيل

### السيناريو الطبيعي

1. الموبايل يحصل على الـ active assignment من:

```http
GET /api/drivers/assignments/current
```

2. يأخذ منه:

- `assignment.id`
- `assignment.orderId`

3. يفتح صفحة التفاصيل باستخدام `assignmentId`

4. يعمل:

```http
GET /api/drivers/assignments/{assignmentId}
```

5. يفتح اتصال SignalR على:

```http
/hubs/notifications
```

6. يسمع على:

- `ReceiveAssignmentUpdated`
- `ReceiveOrderStatusChanged`
- `ReceiveNotification`

---

## 4. SignalR Contract

## 4.1 Hub

```http
/hubs/notifications
```

### Authentication

الـ backend يقرأ التوكن من:

```text
access_token
```

مثال:

```text
wss://{baseUrl}/hubs/notifications?access_token={jwt}
```

### الـ group الذي يدخل فيه المندوب

```text
customer-{driverUserId}
```

مثال:

```text
customer-11111111-1111-1111-1111-111111111111
```

### ملاحظة مهمة

الـ driver لا يشترك في group مبني على `orderId` أو `assignmentId`.

الاشتراك الحقيقي يكون على `driverUserId` فقط، وبعد وصول الحدث التطبيق نفسه يفلتر داخليًا حسب:

- `assignmentId` في `ReceiveAssignmentUpdated`
- `orderId` في `ReceiveOrderStatusChanged`

---

## 4.2 Streams المؤثرة على صفحة التفاصيل

| Event | يرسل على أنهي group | الصفحة تفلتر على | هل هو أساسي للصفحة؟ |
|---|---|---|---|
| `ReceiveAssignmentUpdated` | `customer-{driverUserId}` | `assignmentId` | نعم، هذا أهم event |
| `ReceiveOrderStatusChanged` | `customer-{driverUserId}` | `orderId` | نعم، لكنه event خفيف |
| `ReceiveNotification` | `customer-{driverUserId}` | غالبًا `referenceId` | ثانوي، useful للـ banners / inbox |
| `ReceiveDeliveryOffer` | `customer-{driverUserId}` | `assignmentId` | غالبًا للهوم/العروض، ليس المصدر الأساسي لصفحة التفاصيل |

## 4.3 Event: `ReceiveAssignmentUpdated`

### متى يخرج؟

يخرج للمندوب في الحالات التالية:

- بعد تغيير حالة الطلب لأي حالة تخص assignment active
- بعد vendor يؤكد pickup OTP
- بعد admin أو system يغير حالة الطلب
- بعد المندوب نفسه يعمل status update
- بعد المندوب يعمل arrival update

### يخرج على أنهي id؟

ليس على `assignmentId` مباشرة.

يخرج على:

```text
customer-{driverUserId}
```

### التطبيق يفلتر على ماذا؟

يفلتر على:

```text
payload.assignmentId == currentAssignmentId
```

### الـ payload

الـ payload هو **full `DriverAssignmentDetailDto`**.

مثال JSON:

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-20260430-001",
  "assignmentStatus": "Accepted",
  "homeState": "OnMission",
  "allowedActions": [
    "arrived_at_vendor"
  ],
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
  "pickupOtpRequired": true,
  "pickupOtpStatus": "pending",
  "deliveryOtpRequired": false,
  "deliveryOtpStatus": "not_required",
  "pickupOtpCode": "4821",
  "driverArrivalState": "en_route",
  "orderItems": [
    {
      "name": "Olive Oil 1L",
      "quantity": 2,
      "unitPrice": 52.5,
      "lineTotal": 105.0
    },
    {
      "name": "Cheese 500g",
      "quantity": 1,
      "unitPrice": 80.75,
      "lineTotal": 80.75
    }
  ]
}
```

### الصفحة تعمل إيه عند الاستقبال؟

- لو `assignmentId` هو نفس المعروض حاليًا: replace كامل للـ local state
- لا تعتمد على merge جزئي
- استخدم الـ payload نفسه كـ UI source of truth

### لماذا هذا الحدث هو الأهم؟

لأنه الوحيد الذي يعطي snapshot كامل للشاشة:

- الحالة
- الـ actions المتاحة
- OTP visibility
- arrival state
- order items
- COD
- customer / vendor info

---

## 4.4 Event: `ReceiveOrderStatusChanged`

### متى يخرج؟

يخرج عند أي تغيير في `OrderStatus` يؤثر على المندوب المخصص للطلب.

أمثلة:

- vendor أكد pickup OTP
- driver غيّر الحالة إلى `OnTheWay`
- driver سلّم الطلب
- admin غيّر الحالة

### يخرج على أنهي id؟

يخرج على:

```text
customer-{driverUserId}
```

### التطبيق يفلتر على ماذا؟

يفلتر على:

```text
payload.orderId == currentOrderId
```

### ملاحظة مهمة جدًا

`oldStatus` و `newStatus` هنا **normalized tracking statuses** وليست دائمًا raw `OrderStatus` enum.

أمثلة normalization:

- `DriverAssigned` => `preparing`
- `PickedUp` => `out_for_delivery`
- `OnTheWay` => `out_for_delivery`
- `Delivered` => `delivered`
- `DeliveryFailed` => `cancelled`

### مثال payload

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

### الصفحة تستخدمه إزاي؟

- كإشارة سريعة أن order state اتغير
- أو لعمل lightweight update
- أو مجرد log / analytics / toast

لكن يفضل ألا تبني UI صفحة التفاصيل بالكامل عليه، لأن:

- لا يحتوي `allowedActions`
- لا يحتوي `pickupOtpCode`
- لا يحتوي `driverArrivalState`
- لا يحتوي `orderItems`

---

## 4.5 Event: `ReceiveNotification`

### دوره بالنسبة للصفحة

هذا event عام للإشعارات والـ inbox، وليس source of truth لصفحة التفاصيل.

يمكن استخدامه في صفحة التفاصيل فقط من أجل:

- banner
- toast
- badge
- inbox counter

### يخرج على أنهي id؟

```text
customer-{driverUserId}
```

### مثال payload

```json
{
  "id": "66666666-6666-6666-6666-666666666666",
  "titleAr": "تم تحديث الطلب",
  "titleEn": "Order updated",
  "bodyAr": "تم تحديث حالة الطلب رقم ORD-20260430-001.",
  "bodyEn": "Order ORD-20260430-001 has been updated.",
  "type": "order_status",
  "referenceId": "44444444-4444-4444-4444-444444444444",
  "data": "action=status_changed",
  "dataObject": null,
  "isRead": false,
  "createdAtUtc": "2026-04-30T12:15:30Z"
}
```

---

## 4.6 Event: `ReceiveDeliveryOffer`

### هل هو event خاص بصفحة التفاصيل؟

ليس أساسيًا لصفحة تفاصيل الطلب الحالية.

هو أهميته الأساسية في:

- home screen
- incoming offer flow

### يخرج على أنهي id؟

```text
customer-{driverUserId}
```

### payload

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-20260430-001",
  "vendorName": "Fresh Market",
  "deliveryFee": 28.0,
  "totalAmount": 185.75,
  "codAmount": 185.75,
  "paymentMethod": "CashOnDelivery",
  "countdownSeconds": 45,
  "timestamp": "2026-04-30T11:59:40Z"
}
```

---

## 4.7 Streams موجودة في الـ Hub ولكن ليست target لصفحة المندوب

يوجد event اسمه:

```text
ReceiveDriverArrivalStateChanged
```

لكن في التنفيذ الحالي هو يُرسل إلى:

- vendor
- customer

وليس إلى المندوب نفسه.

المندوب بدلًا من ذلك يحصل على تحديث شاشته عبر:

```text
ReceiveAssignmentUpdated
```

---

## 5. Contract الـ REST الخاص بصفحة التفاصيل

## 5.1 Get Current Assignment

```http
GET /api/drivers/assignments/current
Authorization: Bearer {driver_token}
```

### يستخدم في ماذا؟

- معرفة هل يوجد active assignment
- أخذ `assignment.id`
- أخذ `orderId`
- فتح صفحة التفاصيل

### مثال response لو لا يوجد assignment

```json
{
  "hasAssignment": false
}
```

### مثال response لو يوجد assignment

```json
{
  "hasAssignment": true,
  "assignment": {
    "id": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "orderNumber": "ORD-20260430-001",
    "status": "Accepted",
    "codAmount": 185.75,
    "createdAtUtc": "2026-04-30T11:45:00Z"
  }
}
```

## 5.2 Get Assignment Detail

```http
GET /api/drivers/assignments/{assignmentId}
Authorization: Bearer {driver_token}
```

### الـ id المستخدم

```text
assignmentId
```

### هذا هو الـ response الأساسي للشاشة

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "orderNumber": "ORD-20260430-001",
  "assignmentStatus": "Accepted",
  "homeState": "OnMission",
  "allowedActions": [
    "arrived_at_vendor"
  ],
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
  "pickupOtpRequired": true,
  "pickupOtpStatus": "pending",
  "deliveryOtpRequired": false,
  "deliveryOtpStatus": "not_required",
  "pickupOtpCode": "4821",
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

## 5.3 Verify OTP

```http
POST /api/drivers/assignments/{assignmentId}/verify-otp
Authorization: Bearer {driver_token}
Content-Type: application/json
```

### الـ id المستخدم

```text
assignmentId
```

### request

```json
{
  "otpType": "delivery",
  "otpCode": "1234"
}
```

### `otpType` values

- `pickup`
- `delivery`

### response الحقيقي من الكود

```json
{
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "orderId": "44444444-4444-4444-4444-444444444444",
  "otpType": "delivery",
  "status": "delivered",
  "messageAr": "تم التحقق من رمز التوصيل بنجاح",
  "messageEn": "Delivery OTP verified successfully",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "orderNumber": "ORD-20260430-001",
    "assignmentStatus": "Delivered",
    "homeState": "OnMission",
    "allowedActions": [],
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
    "deliveryOtpRequired": false,
    "deliveryOtpStatus": "verified",
    "pickupOtpCode": null,
    "driverArrivalState": "arrived_at_customer",
    "orderItems": [
      {
        "name": "Olive Oil 1L",
        "quantity": 2,
        "unitPrice": 52.5,
        "lineTotal": 105.0
      }
    ]
  }
}
```

### ملاحظة مهمة

الـ UI بعد هذا الطلب يجب أن يعتمد مباشرة على:

```text
response.updatedAssignment
```

بدون عمل GET إضافي.

## 5.4 Arrived At Vendor

```http
POST /api/drivers/orders/{orderId}/arrived-at-vendor
Authorization: Bearer {driver_token}
```

### الـ id المستخدم

```text
orderId
```

### response

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "arrivalState": "arrived_at_vendor",
  "messageAr": "تم تسجيل الوصول إلى المتجر",
  "messageEn": "Arrival at vendor recorded",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "assignmentStatus": "ArrivedAtVendor",
    "allowedActions": [],
    "pickupOtpRequired": true,
    "pickupOtpStatus": "pending",
    "pickupOtpCode": "4821",
    "driverArrivalState": "arrived_at_vendor"
  }
}
```

### side effects

- vendor يستقبل `ReceiveNotification`
- vendor يستقبل `ReceiveDriverArrivalStateChanged`
- المندوب نفسه يستقبل أيضًا `ReceiveAssignmentUpdated`

## 5.5 Picked Up

```http
POST /api/drivers/orders/{orderId}/picked-up
Authorization: Bearer {driver_token}
```

### ملاحظة

هذا endpoint يتطلب أن `pickupOtp` يكون verified أولًا.

### response

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "PickedUp",
  "messageAr": "تم تحديث حالة الطلب",
  "messageEn": "Order status updated",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "assignmentStatus": "PickedUp",
    "allowedActions": [
      "mark_on_the_way"
    ],
    "pickupOtpStatus": "verified",
    "deliveryOtpRequired": false,
    "deliveryOtpStatus": "not_required",
    "pickupOtpCode": null,
    "driverArrivalState": "en_route"
  }
}
```

## 5.6 On The Way

```http
POST /api/drivers/orders/{orderId}/on-the-way
Authorization: Bearer {driver_token}
```

### response

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "OnTheWay",
  "messageAr": "تم تحديث حالة الطلب",
  "messageEn": "Order status updated",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "assignmentStatus": "PickedUp",
    "allowedActions": [
      "arrived_at_customer"
    ],
    "deliveryOtpRequired": true,
    "deliveryOtpStatus": "pending",
    "driverArrivalState": "en_route"
  }
}
```

### ملاحظة مهمة

بعد `OnTheWay`:

- النظام يجهز `deliveryOtp`
- العميل قد يستقبل notification فيها OTP
- `assignmentStatus` قد يظل `PickedUp`
- لكن `orderStatus` أصبح `OnTheWay`
- لذلك `allowedActions` تتحول إلى:

```text
arrived_at_customer
```

## 5.7 Arrived At Customer

```http
POST /api/drivers/orders/{orderId}/arrived-at-customer
Authorization: Bearer {driver_token}
```

### response

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "assignmentId": "33333333-3333-3333-3333-333333333333",
  "arrivalState": "arrived_at_customer",
  "messageAr": "تم تسجيل الوصول إلى العميل",
  "messageEn": "Arrival at customer recorded",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "assignmentStatus": "ArrivedAtCustomer",
    "allowedActions": [
      "verify_delivery_otp"
    ],
    "deliveryOtpRequired": true,
    "deliveryOtpStatus": "pending",
    "driverArrivalState": "arrived_at_customer"
  }
}
```

### side effects

- customer يستقبل `ReceiveNotification`
- customer يستقبل `ReceiveDriverArrivalStateChanged`
- المندوب يستقبل `ReceiveAssignmentUpdated`

## 5.8 Delivered

```http
POST /api/drivers/orders/{orderId}/delivered
Authorization: Bearer {driver_token}
```

### ملاحظة

هذا endpoint يتطلب أن `deliveryOtp` يكون verified أولًا.

### response

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "Delivered",
  "messageAr": "تم تحديث حالة الطلب",
  "messageEn": "Order status updated",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "assignmentStatus": "Delivered",
    "allowedActions": [],
    "pickupOtpStatus": "verified",
    "deliveryOtpStatus": "verified",
    "driverArrivalState": "arrived_at_customer"
  }
}
```

## 5.9 Delivery Failed

```http
POST /api/drivers/orders/{orderId}/delivery-failed
Authorization: Bearer {driver_token}
Content-Type: application/json
```

### request

```json
{
  "note": "customer_not_available"
}
```

### ملاحظة

الـ `note` هنا مطلوب في الكود.

### response

```json
{
  "orderId": "44444444-4444-4444-4444-444444444444",
  "status": "DeliveryFailed",
  "messageAr": "تم تحديث حالة الطلب",
  "messageEn": "Order status updated",
  "updatedAssignment": {
    "assignmentId": "33333333-3333-3333-3333-333333333333",
    "orderId": "44444444-4444-4444-4444-444444444444",
    "assignmentStatus": "Failed",
    "allowedActions": [],
    "driverArrivalState": "arrived_at_customer"
  }
}
```

---

## 6. الشكل الكامل للـ `DriverAssignmentDetailDto`

```json
{
  "assignmentId": "string-guid",
  "orderId": "string-guid",
  "orderNumber": "string",
  "assignmentStatus": "string",
  "homeState": "string",
  "allowedActions": [
    "string"
  ],
  "vendorName": "string",
  "pickupAddress": "string",
  "pickupLatitude": 0,
  "pickupLongitude": 0,
  "storePhone": "string",
  "customerName": "string",
  "deliveryAddress": "string",
  "deliveryLatitude": 0,
  "deliveryLongitude": 0,
  "customerPhone": "string | null",
  "paymentMethod": "string",
  "codAmount": 0,
  "pickupOtpRequired": true,
  "pickupOtpStatus": "pending | verified | not_required",
  "deliveryOtpRequired": true,
  "deliveryOtpStatus": "pending | verified | not_required",
  "pickupOtpCode": "string | null",
  "driverArrivalState": "en_route | arrived_at_vendor | arrived_at_customer",
  "orderItems": [
    {
      "name": "string",
      "quantity": 0,
      "unitPrice": 0,
      "lineTotal": 0
    }
  ]
}
```

## 6.1 شرح أهم الحقول

| الحقل | المعنى |
|---|---|
| `assignmentStatus` | الحالة التشغيلية للـ assignment نفسه |
| `homeState` | في شاشة التفاصيل غالبًا `OnMission` |
| `allowedActions` | الأزرار التي يجب أن تكون متاحة في الـ UI |
| `pickupOtpRequired` | هل ما زال pickup OTP مطلوبًا الآن |
| `pickupOtpStatus` | `pending` أو `verified` أو `not_required` |
| `deliveryOtpRequired` | هل delivery OTP مطلوب الآن |
| `deliveryOtpStatus` | `pending` أو `verified` أو `not_required` |
| `pickupOtpCode` | يظهر فقط داخل handoff window قبل تأكيد pickup |
| `driverArrivalState` | حالة الوصول الحالية المستخدمة في خطوات الشاشة |

## 6.2 متى يظهر `pickupOtpCode`؟

يظهر فقط عندما:

- `assignmentStatus == Accepted` أو `ArrivedAtVendor`
- و `pickupOtp` لم يتم verify له بعد

بعد التأكيد يصبح:

```json
{
  "pickupOtpCode": null
}
```

---

## 7. Matrix الأزرار والحالات

هذا هو السلوك الحالي الخارج من backend:

| `assignmentStatus` | شرط إضافي | `allowedActions` |
|---|---|---|
| `OfferSent` | لا شيء | `["accept_offer", "reject_offer"]` |
| `Accepted` | لا شيء | `["arrived_at_vendor"]` |
| `ArrivedAtVendor` | لا شيء | `[]` |
| `PickedUp` | لو `orderStatus != OnTheWay` | `["mark_on_the_way"]` |
| `PickedUp` | لو `orderStatus == OnTheWay` | `["arrived_at_customer"]` |
| `ArrivedAtCustomer` | لو `deliveryOtpRequired == true` | `["verify_delivery_otp"]` |
| غير ذلك | لا شيء | `[]` |

## 7.1 ملاحظة مهمة جدًا بخصوص pickup OTP

في الكود يوجد endpoint:

```http
POST /api/drivers/assignments/{assignmentId}/verify-otp
```

وهو يقبل `otpType = pickup`.

لكن في نفس الوقت، `allowedActions` الحالية **لا تُظهر** action باسم:

```text
verify_pickup_otp
```

وهذا معناه أن الـ flow المقصود حاليًا في التنفيذ هو:

- المندوب يعرض أو يعرف `pickupOtpCode`
- vendor هو الذي يؤكد pickup OTP من جهته
- المندوب ينتظر `ReceiveAssignmentUpdated` أو `ReceiveOrderStatusChanged`

إذا كان فريق الموبايل يريد أن المندوب نفسه يُدخل pickup OTP من هذه الصفحة، فهذه نقطة UX/contract تحتاج اتفاق منفصل، لأن الـ endpoint موجود لكن الـ `allowedActions` لا تعكس هذا flow.

---

## 8. من يرسل ماذا ولمن؟

## 8.1 لو vendor أكد pickup OTP

المندوب يستقبل:

- `ReceiveOrderStatusChanged`
- `ReceiveAssignmentUpdated`

الفلترة:

- `orderId` للحدث الأول
- `assignmentId` للحدث الثاني

## 8.2 لو driver عمل `arrived-at-vendor`

المندوب يحصل على:

- response فيه `updatedAssignment`
- `ReceiveAssignmentUpdated`

والـ vendor يحصل على:

- `ReceiveNotification`
- `ReceiveDriverArrivalStateChanged`

## 8.3 لو driver عمل `arrived-at-customer`

المندوب يحصل على:

- response فيه `updatedAssignment`
- `ReceiveAssignmentUpdated`

والـ customer يحصل على:

- `ReceiveNotification`
- `ReceiveDriverArrivalStateChanged`

## 8.4 لو driver غيّر order status

المندوب يحصل على:

- response فيه `updatedAssignment`
- `ReceiveOrderStatusChanged`
- `ReceiveAssignmentUpdated`

## 8.5 لو admin أو system غيّر order status

المندوب يحصل على:

- `ReceiveOrderStatusChanged`
- `ReceiveAssignmentUpdated`

---

## 9. التوصية التنفيذية لفريق الموبايل

## 9.1 عند فتح الصفحة

- استخدم `assignmentId` لبناء الصفحة
- خزّن أيضًا `orderId`
- افتح SignalR مرة واحدة

## 9.2 عند سماع `ReceiveAssignmentUpdated`

- لو `payload.assignmentId == currentAssignmentId`
- اعمل replace كامل للـ local detail state

## 9.3 عند سماع `ReceiveOrderStatusChanged`

- لو `payload.orderId == currentOrderId`
- اعتبره signal سريع
- ولا تعتمد عليه وحده كـ full UI state

## 9.4 بعد أي POST action من الصفحة

- استخدم `response.updatedAssignment` فورًا
- لا تنتظر الـ SignalR حتى تحدث الواجهة

---

## 10. pseudo integration

```dart
void onReceiveAssignmentUpdated(Map<String, dynamic> json) {
  final detail = DriverAssignmentDetailDto.fromJson(json);

  if (detail.assignmentId == currentAssignmentId) {
    setState(() {
      currentDetail = detail;
    });
  }
}

void onReceiveOrderStatusChanged(Map<String, dynamic> json) {
  final orderId = json['orderId'] as String?;

  if (orderId == currentOrderId) {
    // Optional: show badge / toast / analytics
    // Full state should still come from ReceiveAssignmentUpdated
  }
}

Future<void> markArrivedAtCustomer() async {
  final response = await api.postArrivedAtCustomer(currentOrderId);

  if (response.updatedAssignment != null) {
    setState(() {
      currentDetail = response.updatedAssignment!;
    });
  }
}
```

---

## 11. ملاحظات مهمة على التوثيق الحالي

- ملف الـ APIDog الموجود في المشروع ليس محدثًا بالكامل في جزئية:
  - `updatedAssignment`
  - `messageAr`
  - `messageEn`
  - `pickupOtpCode`

- لذلك **source of truth لهذا الملف هو كود الـ backend** وليس الـ APIDog.

---

## 12. الملفات المرجعية في الكود

- `src/Zadana.Api/Realtime/NotificationHub.cs`
- `src/Zadana.Api/Realtime/NotificationService.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriversController.cs`
- `src/Zadana.Application/Modules/Delivery/Commands/VerifyAssignmentOtp/VerifyAssignmentOtpCommand.cs`
- `src/Zadana.Application/Modules/Delivery/Commands/UpdateDriverArrivalState/UpdateDriverArrivalStateCommand.cs`
- `src/Zadana.Application/Modules/Orders/Commands/DriverUpdateOrderStatus/DriverUpdateOrderStatusCommand.cs`
- `src/Zadana.Application/Modules/Orders/Commands/ConfirmVendorPickupOtp/ConfirmVendorPickupOtpCommand.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`
- `src/Zadana.Application/Modules/Delivery/DTOs/DriverMobileDtos.cs`
- `src/Zadana.Infrastructure/Modules/Delivery/Services/DriverReadService.cs`

---

## 13. القرار النهائي لفريق الموبايل

إذا أردنا تلخيص الشاشة في 3 جمل فقط:

1. افتح الصفحة بـ `assignmentId`، واحتفظ أيضًا بـ `orderId`.
2. اعتمد على `updatedAssignment` و `ReceiveAssignmentUpdated` كـ full state.
3. استخدم `ReceiveOrderStatusChanged` كإشارة خفيفة إضافية، وليس كبديل عن snapshot الصفحة.
