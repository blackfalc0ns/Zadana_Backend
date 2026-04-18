# Order Status Workflow And Notifications

هذا الملف يشرح دورة حياة الطلب في النظام:
- ما هي حالات الطلب الموجودة فعليًا في الكود
- من الذي يغيّر كل حالة
- ما الذي يحدث في الدفع `cash / bank / card`
- متى يصل إشعار للعميل
- متى يصل إشعار للتاجر
- ما هو شكل الـpayload الذي يعتمد عليه الفرونت أو الموبايل

الملفات المرجعية الأساسية:
- `src/Zadana.Domain/Modules/Orders/Enums/OrderStatus.cs`
- `src/Zadana.Application/Modules/Checkout/Commands/PlaceCheckoutOrder/PlaceCheckoutOrderCommand.cs`
- `src/Zadana.Application/Modules/Payments/Commands/ConfirmPaymobPayment/ConfirmPaymobPaymentCommand.cs`
- `src/Zadana.Application/Modules/Orders/Commands/VendorUpdateOrderStatus/VendorUpdateOrderStatusCommand.cs`
- `src/Zadana.Application/Modules/Orders/Commands/DriverUpdateOrderStatus/DriverUpdateOrderStatusCommand.cs`
- `src/Zadana.Application/Modules/Orders/Commands/CancelCustomerOrder/CancelCustomerOrderCommand.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedNotification.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`

## 1. الحالات الموجودة فعليًا

الحالات المعرفة في `OrderStatus` هي:

```csharp
PendingPayment,
Placed,
PendingVendorAcceptance,
VendorRejected,
Accepted,
Preparing,
ReadyForPickup,
DriverAssignmentInProgress,
DriverAssigned,
PickedUp,
OnTheWay,
Delivered,
DeliveryFailed,
Cancelled,
Refunded
```

المعنى التشغيلي المختصر:

- `PendingPayment`
  تم إنشاء الطلب لكن الدفع الأونلاين لم يكتمل بعد.

- `Placed`
  حالة داخلية انتقالية بعد إنشاء الطلب وقبل أن يدخل تشغيليًا إلى انتظار التاجر.

- `PendingVendorAcceptance`
  الطلب وصل للتاجر وينتظر قبول أو رفض.

- `VendorRejected`
  التاجر رفض الطلب.

- `Accepted`
  التاجر قبل الطلب.

- `Preparing`
  التاجر بدأ تجهيز الطلب.

- `ReadyForPickup`
  الطلب جاهز للاستلام من السائق.

- `DriverAssignmentInProgress`
  النظام يحاول إسناد سائق.

- `DriverAssigned`
  تم تعيين سائق للطلب.

- `PickedUp`
  السائق استلم الطلب من التاجر.

- `OnTheWay`
  الطلب في الطريق للعميل.

- `Delivered`
  تم التسليم بنجاح.

- `DeliveryFailed`
  فشل التوصيل.

- `Cancelled`
  تم إلغاء الطلب.

- `Refunded`
  تم رد المبلغ.

## 2. من الذي يغيّر الحالة

### العميل

العميل لا يغيّر حالات كثيرة مباشرة.

الموجود حاليًا:
- إنشاء الطلب عبر `PlaceCheckoutOrderCommand`
- إلغاء الطلب عبر `CancelCustomerOrderCommand`

### بوابة الدفع

بوابة الدفع تغيّر المسار عند نجاح الدفع الأونلاين عبر:
- `ConfirmPaymobPaymentCommand`

### التاجر

التاجر يغيّر الحالات التالية فقط عبر:
- `VendorUpdateOrderStatusCommand`

الحالات المسموح بها للتاجر:
- `Accepted`
- `VendorRejected`
- `Preparing`
- `ReadyForPickup`

والانتقالات الصحيحة هي:
- `PendingVendorAcceptance -> Accepted`
- `PendingVendorAcceptance -> VendorRejected`
- `Accepted -> Preparing`
- `Preparing -> ReadyForPickup`

### السائق

السائق يغيّر الحالات التالية فقط عبر:
- `DriverUpdateOrderStatusCommand`

الحالات المسموح بها للسائق:
- `PickedUp`
- `OnTheWay`
- `Delivered`
- `DeliveryFailed`

والانتقالات الصحيحة هي:
- `DriverAssigned -> PickedUp`
- `PickedUp -> OnTheWay`
- `OnTheWay -> Delivered`
- `OnTheWay -> DeliveryFailed`
- `DriverAssigned -> DeliveryFailed`

## 3. مسار إنشاء الطلب حسب طريقة الدفع

### `cash`

عند إنشاء الطلب:
- يتم إنشاء الطلب
- payment تصبح `Pending`
- حالة الطلب تنتقل:
  - `PendingPayment -> Placed -> PendingVendorAcceptance`

النتيجة العملية:
- الطلب يدخل مباشرة في queue التاجر
- يخرج إشعار للعميل
- يخرج إشعار للتاجر

### `bank`

السلوك الحالي قريب من `cash`:
- payment تصبح `Pending`
- حالة الطلب تنتقل:
  - `PendingPayment -> Placed -> PendingVendorAcceptance`

النتيجة العملية:
- الطلب يظهر للتاجر مباشرة
- يخرج إشعار للعميل
- يخرج إشعار للتاجر

### `card`

عند إنشاء الطلب:
- يتم إنشاء payment session في Paymob
- payment تكون `Pending`
- الطلب يظل عمليًا في `PendingPayment`

بعد نجاح Paymob عبر:
- `POST /api/payments/paymob/webhook`
- أو `GET /api/payments/paymob/return`

يتم:
- payment تصبح `Paid`
- cart يتم تنظيفها
- حالة الطلب تنتقل تشغيليًا إلى `PendingVendorAcceptance`

النتيجة العملية:
- الطلب يدخل queue التاجر بعد نجاح الدفع
- يخرج إشعار للعميل
- يخرج إشعار للتاجر
- يخرج أيضًا `Web Push` للتاجر عبر OneSignal إذا كان مشتركًا

## 4. أهم المسارات التشغيلية الشائعة

### سيناريو `cash / bank`

```text
PendingPayment
-> Placed
-> PendingVendorAcceptance
-> Accepted
-> Preparing
-> ReadyForPickup
-> DriverAssigned
-> PickedUp
-> OnTheWay
-> Delivered
```

### سيناريو `card`

```text
PendingPayment
-> [Paymob pending]
-> PendingVendorAcceptance
-> Accepted
-> Preparing
-> ReadyForPickup
-> DriverAssigned
-> PickedUp
-> OnTheWay
-> Delivered
```

### سيناريو الرفض

```text
PendingVendorAcceptance
-> VendorRejected
```

### سيناريو الإلغاء من العميل

الحالات التي يمكن للعميل الإلغاء خلالها حاليًا:
- `PendingPayment`
- `Placed`
- `PendingVendorAcceptance`
- `Accepted`
- `Preparing`
- `ReadyForPickup`
- `DriverAssignmentInProgress`

الانتقال:

```text
{أي حالة من الحالات المسموحة}
-> Cancelled
```

### سيناريو فشل التوصيل

```text
DriverAssigned -> DeliveryFailed
```

أو:

```text
OnTheWay -> DeliveryFailed
```

## 5. نظام الإشعارات المرتبط بالحالة

كل تغير مهم في الحالة ينشر event موحد:
- `OrderStatusChangedNotification`

الـevent يحتوي على:
- `orderId`
- `userId`
- `vendorId`
- `orderNumber`
- `oldStatus`
- `newStatus`
- `notifyCustomer`
- `notifyVendor`
- `actorRole`

ثم يقوم `OrderStatusChangedHandler` بتحويله إلى:
- inbox notification
- realtime notification عبر `NotificationHub`
- و`OneSignal Web Push` للتاجر في حالة الطلب الجديد

## 6. متى يُشعَر العميل

القاعدة الحالية:
- معظم تغييرات الحالة التشغيلية تشغّل `NotifyCustomer: true`

أهم السيناريوهات التي يصل فيها إشعار للعميل:
- وصول الطلب إلى `PendingVendorAcceptance`
- قبول التاجر `Accepted`
- رفض التاجر `VendorRejected`
- بدء التحضير `Preparing`
- جاهز للاستلام `ReadyForPickup`
- تعيين سائق `DriverAssigned`
- تم الاستلام من التاجر `PickedUp`
- في الطريق `OnTheWay`
- تم التسليم `Delivered`
- فشل التوصيل `DeliveryFailed`
- تم الإلغاء `Cancelled`
- تم رد المبلغ `Refunded`

### ملاحظة مهمة

في هذه المرحلة:
- العميل لديه `Inbox + Real-time`
- لا يوجد `FCM/APNs push` حقيقي من الباك للعميل بعد

يعني الموبايل يعتمد على:
- `GET /api/notifications`
- `GET /api/notifications/unread-count`
- `/hubs/notifications`

## 7. متى يُشعَر التاجر

القاعدة الحالية:
- التاجر لا يُشعَر على كل تغيّر حالة
- التاجر يُشعَر بشكل أساسي عند:
  - وصول طلب جديد إلى `PendingVendorAcceptance`
  - `Cancelled`
  - `DeliveryFailed`

لكن الإشعار الأهم تشغيليًا هو:
- `PendingVendorAcceptance`

لأنه يعني:
- يوجد طلب جديد يحتاج قرارًا من التاجر

## 8. OneSignal للتاجر

عند وصول الطلب إلى:
- `PendingVendorAcceptance`

يقوم النظام الآن بـ:
- إنشاء inbox notification للتاجر
- إرسال realtime إلى الجرس داخل `vendor-panel`
- إرسال `OneSignal Web Push` إذا:
  - `OneSignal.Enabled = true`
  - `RestApiKey` موجودة
  - التاجر مشترك فعليًا
  - `NewOrdersNotificationsEnabled = true`

الـ`external_id` المستخدم في OneSignal:
- `vendor.UserId`

والوجهة الافتراضية داخل الإشعار:
- `/orders/{orderId}`

## 9. شكل الـpayload الموحد للإشعار

`OrderStatusChangedHandler` يبني `data` موحدة تحتوي على:

```json
{
  "orderId": "guid",
  "orderNumber": "12345",
  "vendorId": "guid",
  "oldStatus": "PendingVendorAcceptance",
  "newStatus": "Accepted",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/guid"
}
```

الحقول المهمة للفرونت والموبايل:
- `orderId`
  استخدمه للانتقال إلى شاشة تفاصيل الطلب

- `oldStatus`
  الحالة السابقة

- `newStatus`
  الحالة الحالية

- `actorRole`
  من الذي أحدث التغيير:
  - `customer`
  - `vendor`
  - `driver`
  - `payment_gateway`

- `action`
  قيمة تشغيلية مختصرة:
  - `placed`
  - `cancelled`
  - `status_changed`

- `targetUrl`
  route جاهز يمكن فتحه مباشرة من الإشعار

## 10. كيف يظهر الإشعار للعميل والتاجر

### Inbox API

نفس الإشعار يُقرأ من:
- `GET /api/notifications`
- `GET /api/vendor/notifications`

### Real-time

نفس الشكل الأساسي يصل لحظيًا عبر:
- `/hubs/notifications`

واسم الحدث:
- `ReceiveNotification`

يعني الفرونت يجب أن يتعامل مع الإشعار القادم من الـAPI والـHub بنفس الـmodel تقريبًا.

## 11. Mapping سريع بين الحالة والإشعار

### طلب جديد للتاجر

عندما تصبح الحالة:
- `PendingVendorAcceptance`

النتيجة:
- العميل: inbox + realtime
- التاجر: inbox + realtime bell + OneSignal push

### قبول التاجر

عندما تصبح الحالة:
- `Accepted`

النتيجة:
- العميل: inbox + realtime
- التاجر: لا يوجد new-order push هنا

### رفض التاجر

عندما تصبح الحالة:
- `VendorRejected`

النتيجة:
- العميل: inbox + realtime
- التاجر: لا يعتمد عليه كإشعار جديد

### في الطريق

عندما تصبح الحالة:
- `OnTheWay`

النتيجة:
- العميل: inbox + realtime

### تم التسليم

عندما تصبح الحالة:
- `Delivered`

النتيجة:
- العميل: inbox + realtime

### تم الإلغاء

عندما تصبح الحالة:
- `Cancelled`

النتيجة:
- العميل: inbox + realtime
- التاجر: inbox

## 12. ما الذي يعتمد عليه مبرمج الموبايل

إذا كان المطلوب في تطبيق العميل:
- عرض timeline أو badge أو inbox أو تحديث لحظي

فالاعتماد يكون على:
- `newStatus`
- `oldStatus`
- `orderId`
- `type`
- `referenceId`
- `dataObject`
- `targetUrl`

الأفضل:
- العرض من `title/body`
- المنطق والتنقل من `type + referenceId + dataObject`

## 13. ما الذي يعتمد عليه مبرمج لوحة التاجر

لوحة التاجر يجب أن تعتبر:
- `ReceiveNotification` هو المصدر الفوري
- polling مجرد fallback

وعند وصول إشعار نوع:
- `vendor_new_order`

يفضل:
- تحديث الجرس فورًا
- تحديث unread count فورًا
- تشغيل tone / desktop notification
- التنقل إلى:
  - `/orders/{orderId}`
  - أو `targetUrl` لو كانت موجودة

## 14. ملاحظات مهمة

- `Placed` حالة انتقالية داخلية أكثر من كونها حالة UI نهائية.
- الحالة التشغيلية المهمة لوصول الطلب للتاجر هي:
  - `PendingVendorAcceptance`
- الدفع الأونلاين لا يجب أن يظل في `PendingPayment` بعد النجاح.
- التاجر لا يجب أن يعتمد على polling فقط، لأن `SignalR` موجود الآن كمسار لحظي أساسي.
- إشعارات العميل الحالية ليست `push` خارج التطبيق بعد، لكنها جاهزة كـ`Inbox + Realtime`.

## 15. الخلاصة

القاعدة الذهبية في النظام الآن:

- إذا تغيّرت حالة الطلب، يتم نشر `OrderStatusChangedNotification`
- هذا الحدث هو المصدر الموحد للإشعارات
- العميل يأخذ:
  - `Inbox + Real-time`
- التاجر يأخذ:
  - `Inbox + Bell Real-time`
  - و`OneSignal Web Push` في سيناريو الطلب الجديد

وأهم حالة تشغيلية يجب مراقبتها في التكاملات:
- `PendingVendorAcceptance`

لأنها تعني:
- الطلب صار صالحًا للعمل عند التاجر
- ويجب أن يظهر في الجرس
- ويجب أن يصل كتجربة `new order`
