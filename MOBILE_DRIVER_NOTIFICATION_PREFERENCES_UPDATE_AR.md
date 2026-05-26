# تحديث إعدادات إشعارات المندوب للموبايل

تاريخ التحديث: 2026-05-26

## المطلوب تغييره في تطبيق المندوب

تم حذف endpoint إعدادات الإشعارات العام للمندوب:

```http
GET /api/drivers/notifications/preferences
PUT /api/drivers/notifications/preferences
```

لا تستخدمه بعد الآن. مصدر الحقيقة الوحيد لإعدادات إشعارات جهاز المندوب هو:

```http
PUT /api/drivers/notifications/devices/preferences
```

ولقراءة إعدادات الجهاز الحالي قبل عرض شاشة الإعدادات استخدم:

```http
GET /api/drivers/notifications/devices/preferences?deviceId={deviceId}&deviceToken={deviceToken}
```

## متى تستدعي endpoint الجديد؟

استخدمه عند تغيير أي إعداد إشعارات في شاشة إعدادات المندوب:

- تشغيل أو إيقاف كل الإشعارات.
- تشغيل أو إيقاف إشعارات العروض/الديسباتش.
- تشغيل أو إيقاف إشعارات الطلبات المسندة.
- تشغيل أو إيقاف إشعارات الدعم والنزاعات والاسترجاع.
- تشغيل أو إيقاف إشعارات المحفظة.
- تغيير صوت الإشعار.

## مهم جدا

استخدم أسماء الحقول بصيغة camelCase فقط. لا تستخدم snake_case.

خطأ:

```json
{
  "push_enabled": true,
  "sound": "chime"
}
```

صحيح:

```json
{
  "notificationsEnabled": true,
  "notificationSound": "chime"
}
```

## Request

```http
PUT https://zadana.runasp.net/api/drivers/notifications/devices/preferences
Authorization: Bearer {driver_access_token}
Content-Type: application/json
```

لازم تبعت `deviceId` أو `deviceToken` على الأقل. الأفضل تبعت الاتنين لو متاحين.

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

## معنى الحقول

`notificationsEnabled`: المفتاح الرئيسي لكل push notifications على الجهاز الحالي.

`dispatchPushEnabled`: إشعارات عروض التوصيل والديسباتش.

`assignmentPushEnabled`: إشعارات تحديثات الطلبات المسندة للمندوب.

`supportPushEnabled`: إشعارات الدعم والنزاعات والاسترجاع.

`walletPushEnabled`: إشعارات المحفظة والسحب.

`accountPushEnabled`: إشعارات الحساب والمراجعات والحظر/فك الحظر.

`notificationSound`: صوت الإشعار، مثال `chime`.

## Response المتوقع

```json
{
  "id": "beebdd97-594e-44b9-96c3-feb0bbf9b3d9",
  "deviceToken": "FCM_TOKEN_HERE",
  "platform": "fcm",
  "deviceId": "9825eeac-6851-4755-9826-7a72562e9230",
  "deviceName": "Android",
  "appVersion": "1.0.0",
  "locale": "ar",
  "notificationsEnabled": true,
  "dispatchPushEnabled": true,
  "assignmentPushEnabled": true,
  "supportPushEnabled": true,
  "walletPushEnabled": true,
  "accountPushEnabled": true,
  "notificationSound": "chime",
  "isActive": true,
  "lastRegisteredAtUtc": "2026-05-26T16:02:01.2895739Z",
  "lastSeenAtUtc": "2026-05-26T16:05:42.8995123Z"
}
```

## عند إيقاف كل الإشعارات

لو المستخدم قفل الإشعارات بالكامل:

```json
{
  "deviceId": "9825eeac-6851-4755-9826-7a72562e9230",
  "deviceToken": "FCM_TOKEN_HERE",
  "notificationsEnabled": false,
  "dispatchPushEnabled": false,
  "assignmentPushEnabled": false,
  "supportPushEnabled": false,
  "walletPushEnabled": false,
  "accountPushEnabled": false,
  "notificationSound": "chime"
}
```

## عند تشغيل كل الإشعارات

لو المستخدم شغل الإشعارات بالكامل:

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

## تسجيل الجهاز

قبل تحديث إعدادات جهاز، لازم الجهاز يكون متسجل:

```http
POST /api/drivers/notifications/devices/register
```

مثال:

```json
{
  "deviceToken": "FCM_TOKEN_HERE",
  "platform": "fcm",
  "deviceId": "9825eeac-6851-4755-9826-7a72562e9230",
  "deviceName": "Android",
  "appVersion": "1.0.0",
  "locale": "ar",
  "notificationsEnabled": true,
  "dispatchPushEnabled": true,
  "assignmentPushEnabled": true,
  "supportPushEnabled": true,
  "walletPushEnabled": true,
  "accountPushEnabled": true,
  "notificationSound": "chime"
}
```

## ملاحظات للموبايل

- لا تبعت request إلى `/api/drivers/notifications/preferences` بعد الآن.
- لا تبعت endpointين ورا بعض لتحديث نفس الإعدادات.
- خزّن `deviceId` محليا وثبته للجهاز.
- استخدم آخر FCM token في `deviceToken`.
- لو تغير FCM token، ناد `register` مرة أخرى ثم ناد `devices/preferences` إذا احتجت تثبيت التفضيلات.
- لو رجع `404 NotificationDevice`، سجل الجهاز أولا.
- لو رجع `400 DEVICE_IDENTIFIER_REQUIRED`، ابعت `deviceId` أو `deviceToken`.
