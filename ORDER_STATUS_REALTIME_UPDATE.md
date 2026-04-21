# Order Status Realtime Update

هذا الملف يشرح التغيير الجديد فقط الخاص بتحديث حالة الأوردر لحظيًا عند العميل عندما يغيّر التاجر الحالة.

## الهدف

عندما يقوم التاجر بتغيير حالة الطلب، يجب أن تظهر الحالة الجديدة عند العميل فورًا بدون `refresh`.

## ما الذي تم إضافته

تمت إضافة event جديد على نفس `SignalR Notification Hub`:

```text
ReceiveOrderStatusChanged
```

ويتم إرساله مباشرة إلى المستخدم المستهدف بمجرد تغيير حالة الأوردر.

## مسار الـHub

```http
/hubs/notifications
```

## متى يتم إرسال الحدث

الحدث يُرسل بعد نجاح تغيير حالة الأوردر من الباك إند، مثل:

- قبول الطلب من التاجر
- رفض الطلب من التاجر
- بدء التحضير
- الطلب جاهز للاستلام

وأيضًا يمكن استخدامه مع بقية تغييرات الحالة لاحقًا لأن الحدث عام وليس مربوطًا بحالة واحدة فقط.

## اسم الحدث

```text
ReceiveOrderStatusChanged
```

## شكل الـpayload

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "PendingVendorAcceptance",
  "newStatus": "Accepted",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-21T14:45:00Z"
}
```

## المطلوب من تطبيق العميل

بعد الاتصال بالـhub:

- الاستماع إلى `ReceiveOrderStatusChanged`
- مقارنة `payload.orderId` بالأوردر المفتوح حاليًا
- تحديث حالة الأوردر في الشاشة مباشرة من `payload.newStatus`
- عدم انتظار `refresh`

## ملاحظات مهمة

- هذا الحدث مخصص للتحديث اللحظي للـorder state.
- ما زال `ReceiveNotification` موجودًا كما هو للـinbox والـnotifications العامة.
- يمكن للعميل استخدام `ReceiveOrderStatusChanged` لتحديث شاشة التفاصيل مباشرة، واستخدام `ReceiveNotification` لتحديث مركز الإشعارات.

## مثال استخدام على العميل

Pseudo flow:

```text
1. Connect to /hubs/notifications with authenticated user token
2. Listen to ReceiveOrderStatusChanged
3. If payload.orderId == currentOrderId:
4.    update local order status = payload.newStatus
5.    redraw UI immediately
```

## الملفات المرتبطة في الباك إند

- `src/Zadana.Api/Realtime/NotificationHub.cs`
- `src/Zadana.Api/Realtime/NotificationService.cs`
- `src/Zadana.Application/Common/Interfaces/INotificationService.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`
- `src/Zadana.Api/Realtime/Contracts/OrderStatusChangedRealtimePayload.cs`
