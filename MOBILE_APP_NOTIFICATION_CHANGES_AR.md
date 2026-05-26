# ملف تعديلات الموبايل - الإشعارات وتتبع الطلب

تاريخ التحديث: 2026-05-26

هذا الملف موجه لمبرمج الموبايل بعد تعديلات الباك إند الخاصة بإشعارات المندوب والعميل.

القنوات المطلوبة للحالات المهمة:

- popup داخل التطبيق عن طريق SignalR.
- push notification تعمل حتى لو التطبيق killed/background عن طريق OneSignal.
- email للعميل عن طريق Email Center.

مهم جدا:

- في foreground يعرض التطبيق popup داخلي من SignalR.
- في background/killed الذي يظهر للمستخدم هو system notification من OneSignal.
- بعد ضغط المستخدم على notification، افتح الطلب واعرض نفس popup/رسالة الحالة من `data`.
- لازم تطبيق Android ينشئ channel باسم `zadana_heads_up_notifications` بأهمية عالية `High/Max` حتى يظهر heads-up فوق الشاشة.

## 1. إعدادات إشعارات جهاز المندوب

تم إلغاء استخدام endpoint القديم:

```http
GET /api/drivers/notifications/preferences
PUT /api/drivers/notifications/preferences
```

لا تستخدمه في التطبيق.

استخدم endpoints الخاصة بالجهاز فقط:

```http
GET /api/drivers/notifications/devices/preferences?deviceId={deviceId}&deviceToken={deviceToken}
PUT /api/drivers/notifications/devices/preferences
POST /api/drivers/notifications/devices/register
```

ملاحظات مهمة:

- لازم تبعت `deviceId` أو `deviceToken` على الأقل.
- الأفضل تبعت الاثنين لو متاحين.
- استخدم `camelCase` فقط في أسماء الحقول.
- لا تستخدم `push_enabled` أو `sound`.
- استخدم `notificationsEnabled` و`notificationSound`.

مثال تحديث صحيح:

```json
{
  "deviceId": "9825eeac-6851-4755-9826-7a72562e9230",
  "deviceToken": "FCM_TOKEN_HERE",
  "notificationsEnabled": true,
  "dispatchPushEnabled": true,
  "assignmentPushEnabled": true,
  "supportPushEnabled": true,
  "walletPushEnabled": true,
  "accountPushEnabled": true,
  "notificationSound": "chime"
}
```

لو رجع:

- `405 Method Not Allowed`: النسخة الجديدة لم يتم نشرها على السيرفر بعد، أو التطبيق ما زال ينادي endpoint غير صحيح.
- `404 NotificationDevice`: سجل الجهاز أولا عن طريق `POST /register`.
- `400 DEVICE_IDENTIFIER_REQUIRED`: ابعت `deviceId` أو `deviceToken`.

## 2. إشعار العميل: المندوب في الطريق

عند تحول الطلب إلى `OnTheWay` لازم التطبيق يعرض popup للعميل.

الباك إند يرسل لهذه الحالة:

- popup/realtime.
- OneSignal heads-up push.
- email event: `customer_order_out_for_delivery`.

استمع إلى Hub:

```http
/hubs/notifications
```

واستمع للأحداث:

```text
ReceiveNotification
ReceiveOrderStatusChanged
```

في `ReceiveNotification` سيصل `data` يحتوي:

```json
{
  "orderId": "ORDER_ID",
  "orderNumber": "ORD-123",
  "oldStatus": "PickedUp",
  "newStatus": "OnTheWay",
  "actorRole": "driver",
  "action": "on_the_way",
  "targetUrl": "/orders/ORDER_ID",
  "category": "order",
  "screen": "order_tracking",
  "presentation": "popup",
  "popupType": "order_status_changed",
  "showPopup": true,
  "eventName": "order.status.ontheway"
}
```

في `ReceiveOrderStatusChanged` سيصل payload مثل:

```json
{
  "orderId": "ORDER_ID",
  "orderNumber": "ORD-123",
  "vendorId": "VENDOR_ID",
  "oldStatus": "out_for_delivery",
  "newStatus": "out_for_delivery",
  "oldStatusRaw": "PickedUp",
  "newStatusRaw": "OnTheWay",
  "actorRole": "driver",
  "action": "on_the_way",
  "targetUrl": "/orders/ORDER_ID",
  "presentation": "popup",
  "popupType": "order_status_changed",
  "showPopup": true
}
```

مهم:

- لا تعتمد فقط على مقارنة `oldStatus` و`newStatus` لأن الاثنين قد يظهروا `out_for_delivery` للعميل.
- استخدم `newStatusRaw == "OnTheWay"` أو `action == "on_the_way"` لإظهار popup.

## 3. إشعار العميل: المندوب وصل عنوان التسليم

عند وصول المندوب للعميل ستصل حالة:

```text
arrived_at_customer
```

الباك إند يرسل لهذه الحالة:

- popup/realtime.
- OneSignal heads-up push.
- email event: `customer_driver_arrived_at_delivery`.

استمع إلى:

```text
ReceiveNotification
ReceiveDriverArrivalStateChanged
```

مثال payload:

```json
{
  "orderId": "ORDER_ID",
  "orderNumber": "ORD-123",
  "arrivalState": "arrived_at_customer",
  "driverName": "Driver User",
  "actorRole": "driver",
  "targetUrl": "/orders/ORDER_ID",
  "presentation": "popup",
  "popupType": "driver_arrival_state_changed",
  "showPopup": true
}
```

المطلوب في التطبيق:

- لو `arrivalState == "arrived_at_customer"` اعرض popup للعميل.
- افتح شاشة تتبع الطلب أو تفاصيل الطلب من `targetUrl`.
- جهز النص داخل التطبيق حسب اللغة، أو استخدم عنوان وجسم الإشعار من `ReceiveNotification`.

## 4. شاشة تتبع الطلب المباشرة

لو التطبيق داخل شاشة تتبع الطلب، استخدم Hub:

```http
/hubs/order-tracking
```

بعد الاتصال ناد:

```text
SubscribeToOrder(orderId)
```

واستمع إلى:

```text
ReceiveOrderTrackingStatusChanged
ReceiveOrderTrackingArrivalState
ReceiveDriverLocation
```

ملاحظات:

- `ReceiveOrderTrackingArrivalState` مفيد لتحديث واجهة التتبع أثناء فتح الشاشة.
- `ReceiveDriverArrivalStateChanged` من `/hubs/notifications` مفيد للـ popup الشخصي للعميل.

## 5. قاعدة عامة للـ popup

أي payload يحتوي:

```json
{
  "presentation": "popup",
  "showPopup": true
}
```

يجب أن يعرض popup مناسب حسب `popupType`.

أنواع popup الحالية المهمة:

```text
order_status_changed
driver_arrival_state_changed
support_case_status_update
```

## 6. OneSignal

الباك إند يرسل heads-up push للحالات المهمة. على الموبايل تأكد من:

- تسجيل FCM token عن طريق `POST /api/drivers/notifications/devices/register` في تطبيق المندوب.
- عدم إرسال تفضيلات تقفل كل الأقسام بالغلط.
- قراءة `data` من push notification بنفس أسماء الحقول `camelCase`.
- إنشاء Android notification channel:

```text
channelId: zadana_heads_up_notifications
importance: high أو max
priority: high
sound: default أو chime
```

- عند وصول notification في background/killed اعتمد على system notification من OneSignal.
- عند فتح التطبيق من notification اقرأ `data.targetUrl` و`data.popupType` و`data.showPopup` وافتح شاشة الطلب.
- لو `showPopup == true` اعرض popup داخل التطبيق بعد الفتح، حتى لو التطبيق كان killed قبل الضغط.

## 7. ملخص سريع للمطلوب من مبرمج الموبايل

1. احذف أي استخدام لـ `/api/drivers/notifications/preferences`.
2. استخدم `GET /api/drivers/notifications/devices/preferences` لقراءة تفضيلات الجهاز.
3. استخدم `PUT /api/drivers/notifications/devices/preferences` لتحديث تفضيلات الجهاز.
4. اعرض popup للعميل عند `newStatusRaw == "OnTheWay"` أو `action == "on_the_way"`.
5. اعرض popup للعميل عند `arrivalState == "arrived_at_customer"`.
6. لا تتجاهل الحدث لو `oldStatus == newStatus == "out_for_delivery"`، استخدم `newStatusRaw`.
7. تعامل مع `presentation: "popup"` و`showPopup: true` كأمر عرض popup.
8. لا يحتاج الموبايل لإرسال الإيميل؛ الباك إند يرسله تلقائيا عن طريق Email Center.
9. في killed/background لا يوجد SignalR popup؛ المطلوب system push notification ثم popup داخلي بعد فتح التطبيق من الإشعار.
