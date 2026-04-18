# Mobile Order vs Swagger Vendor Notification Debug Guide

هذا الملف يشرح بالتفصيل لماذا كان إنشاء الأوردر يعمل من `Swagger` وتظهر إشعارات التاجر، بينما نفس العملية من الموبايل لم تكن تعطي نفس النتيجة، وما الذي تم تعديله في الباك إند، وكيف نختبر ونشخّص أي مشكلة متبقية.

## 1. الملخص التنفيذي

المشكلة لم تكن في شاشة الإشعارات نفسها، ولا في `SignalR` فقط، ولا في `test notification endpoint`.

المشكلة كانت خليطًا من نقطتين:

1. كان يوجد عطل في بعض مسارات إنشاء الأوردر يمنع حفظ انتقال الحالة بشكل سليم قبل اكتمال مسار إشعار التاجر.
2. الموبايل كان على الأرجح يرسل `POST /api/orders` بصيغة payload مختلفة عن الصيغة الحالية المعتمدة في الـ API، بينما `Swagger` والسكربت كانا يرسلان الصيغة الجديدة الصحيحة.

بعد التعديلات الحالية:

- إشعار التاجر الداخلي `inbox + realtime` يتم إنشاؤه عندما يصل الطلب إلى `PendingVendorAcceptance`.
- `POST /api/orders` أصبح يقبل الصيغتين:
  - الصيغة الحالية `snake_case`
  - الصيغة القديمة أو الشائعة في الموبايل `camelCase`
- كما أصبح يقبل بعض أسماء طرق الدفع القديمة مثل:
  - `cash_on_delivery`
  - `bank_transfer`
  - `credit_card`
  - `debit_card`

## 2. ما الذي كان يعمل بالضبط من Swagger

عندما كنا نختبر من `Swagger` أو من السكربت:

- كان الطلب يذهب إلى `POST /api/orders`
- وكان الـ body مرسلًا بالشكل الصحيح الحالي:

```json
{
  "vendor_id": "82e135c4-3023-4d12-810b-9a6c0d9bd537",
  "address_id": "69afae0a-8434-4942-a0ce-0d259e09771b",
  "delivery_slot_id": "standard-30-45",
  "payment_method": "cash",
  "promo_code": null,
  "notes": "debug order from script"
}
```

هذا الشكل كان يدخل المسار التشغيلي الصحيح، فيتم:

- إنشاء الأوردر
- نقل الحالة إلى `PendingVendorAcceptance`
- إرسال `OrderStatusChangedNotification`
- إنشاء Notification في جدول `Notifications`
- وصول `vendor_new_order` للتاجر

## 3. لماذا كان الموبايل مختلفًا

السبب الأقرب الذي ثبت عمليًا هو اختلاف عقد الطلب `request contract`.

الموبايل غالبًا كان يرسل واحدًا أو أكثر من هذه الفروقات:

```json
{
  "vendorId": "82e135c4-3023-4d12-810b-9a6c0d9bd537",
  "addressId": "69afae0a-8434-4942-a0ce-0d259e09771b",
  "deliverySlotId": "standard-30-45",
  "paymentMethod": "cash_on_delivery",
  "promoCode": null,
  "note": "legacy mobile payload test"
}
```

الفروقات هنا:

- `vendorId` بدل `vendor_id`
- `addressId` بدل `address_id`
- `deliverySlotId` بدل `delivery_slot_id`
- `paymentMethod` بدل `payment_method`
- `promoCode` بدل `promo_code`
- `note` بدل `notes`
- `cash_on_delivery` بدل `cash`

إذا كانت نسخة الباك إند لا تتعامل مع هذه الفروقات، فالموبايل قد:

- يرسل أوردر ناقص
- أو يدخل بقيم افتراضية غير مقصودة
- أو يفشل في تحديد التاجر الصحيح
- أو لا يدخل مسار إشعار التاجر كما هو متوقع

## 4. ما الذي تم تعديله

### 4.1 إصلاح مسار الأوردر نفسه

تم إصلاح مسار إنشاء الأوردر حتى لا يفشل قبل اكتمال نشر الإشعار في بعض الحالات المتعلقة بـ `OrderStatusHistory`.

الملفات المهمة:

- `src/Zadana.Application/Modules/Checkout/Commands/PlaceCheckoutOrder/PlaceCheckoutOrderCommand.cs`
- `src/Zadana.Application/Modules/Payments/Commands/ConfirmPaymobPayment/ConfirmPaymobPaymentCommand.cs`
- `src/Zadana.Application/Modules/Orders/Support/OrderStatusHistoryTracking.cs`

### 4.2 جعل إشعار الأوردر الجديد للتاجر دائمًا داخليًا

تم تثبيت أن إشعار الأوردر الجديد للتاجر عند `PendingVendorAcceptance` يذهب دائمًا إلى:

- `inbox`
- `SignalR realtime`

أما `newOrdersNotificationsEnabled` فأصبح يتحكم فقط في `push` الخارجي، وليس في الإشعار الداخلي داخل النظام.

الملف المهم:

- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`

### 4.3 دعم payload الموبايل القديمة والجديدة معًا

تم تعديل عقد `POST /api/orders` ليقبل:

- الصيغة الحالية `snake_case`
- الصيغة القديمة `camelCase`

كما تم عمل normalization لبعض قيم الدفع القديمة.

الملفات المهمة:

- `src/Zadana.Api/Modules/Orders/Requests/CheckoutRequests.cs`
- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`

## 5. ما الذي يقبله `POST /api/orders` الآن

### الصيغة الحالية المفضلة

```json
{
  "vendor_id": "guid",
  "address_id": "guid",
  "delivery_slot_id": "standard-30-45",
  "payment_method": "cash",
  "promo_code": null,
  "notes": "optional"
}
```

### الصيغة القديمة التي أصبحت مدعومة أيضًا

```json
{
  "vendorId": "guid",
  "addressId": "guid",
  "deliverySlotId": "standard-30-45",
  "paymentMethod": "cash_on_delivery",
  "promoCode": null,
  "note": "optional"
}
```

### قيم `paymentMethod` المقبولة الآن

القيم الأساسية:

- `cash`
- `bank`
- `card`
- `apple_pay`

القيم القديمة أو البديلة التي يتم تحويلها داخليًا:

- `cash_on_delivery` -> `cash`
- `cashondelivery` -> `cash`
- `cod` -> `cash`
- `bank_transfer` -> `bank`
- `banktransfer` -> `bank`
- `credit_card` -> `card`
- `creditcard` -> `card`
- `debit_card` -> `card`
- `debitcard` -> `card`
- `applePay` -> `apple_pay`
- `applepay` -> `apple_pay`

## 6. متى يخرج إشعار التاجر

إشعار التاجر المهم هنا هو:

- `type = vendor_new_order`

هذا الإشعار يخرج عندما يصل الأوردر إلى:

- `PendingVendorAcceptance`

### في `cash`

يصل مباشرة بعد إنشاء الأوردر.

### في `bank`

يصل مباشرة بعد إنشاء الأوردر.

### في `card`

لا يصل مباشرة بعد `POST /api/orders`.

بل يصل فقط بعد نجاح تأكيد الدفع عبر:

- `POST /api/payments/paymob/webhook`
- أو `GET /api/payments/paymob/return`

## 7. لماذا `test notification` ليس دليلًا كافيًا

الـ endpoint:

- `POST /api/admin/vendors/{vendorId}/notifications/test`

أو أي endpoint مشابه لـ `test`

يثبت فقط أن:

- إدخال notification يدوي إلى جدول `Notifications` يعمل
- عرض الجرس يعمل

لكنه لا يثبت أن مسار الأوردر الحقيقي يعمل.

الفرق مهم جدًا:

- `test` يدخل `Notifications` مباشرة
- إشعار الأوردر الحقيقي ينتج من `OrderStatusChangedHandler`

إذا ظهر `test` ولم يظهر إشعار الأوردر، فالمشكلة ليست بالضرورة في الواجهة، بل في أن الأوردر لم يولد `vendor_new_order` من الأصل.

## 8. أهم سبب تشغيلي شائع جدًا

إذا لم يرسل الموبايل `vendor_id` أو `vendorId` بشكل صحيح، فالباك إند قد يختار تلقائيًا التاجر القادر على تغطية كل السلة حسب منطق الـ checkout.

هذا يعني:

- قد يذهب الأوردر لتاجر آخر
- والإشعار يذهب لذلك التاجر الآخر
- فيبدو لك أن الإشعار "لم يصل"

لذلك عند التشخيص لا يكفي أن تقول "الأوردر اتعمل".

لازم تتأكد أيضًا:

- من هو `vendorId` الفعلي داخل الأوردر
- ومن هو `vendor.UserId` الذي تم إرسال الإشعار له

## 9. كيف تم التحقق فعليًا بعد التعديل

تم التحقق بطريقتين:

### 9.1 بالسكربت الكامل

السكربت:

- `scripts/Send-DebugOrder.ps1`

يقوم بـ:

1. تسجيل دخول العميل
2. تسجيل دخول التاجر
3. تنظيف السلة
4. إضافة منتج
5. تحديد العنوان
6. تحديد التاجر
7. تنفيذ `POST /api/orders`
8. فحص:
   - `/api/vendor/notifications?type=vendor_new_order`
   - `/api/vendor/orders`

وقد نجح فعليًا وأثبت:

- إنشاء الأوردر
- ظهور الأوردر في Orders API
- وجود `vendor_new_order` مربوط بنفس `orderId`

### 9.2 بطلب legacy شبيه بالموبايل

تم إرسال `POST /api/orders` يدويًا بصيغة:

- `vendorId`
- `addressId`
- `deliverySlotId`
- `paymentMethod = cash_on_delivery`
- `note`

والنتيجة:

- تم قبول الطلب
- تم تحويل `cash_on_delivery` إلى `cash`
- تم إنشاء أوردر حقيقي
- تم إنشاء `vendor_new_order`

هذا مهم جدًا لأنه يثبت أن التوافق مع payload الموبايل لم يعد مجرد توقع.

## 10. كيف تختبر بنفسك من Swagger

### اختبار `cash`

استخدم:

- `POST /api/orders`

Body:

```json
{
  "vendor_id": "82e135c4-3023-4d12-810b-9a6c0d9bd537",
  "address_id": "69afae0a-8434-4942-a0ce-0d259e09771b",
  "delivery_slot_id": "standard-30-45",
  "payment_method": "cash",
  "promo_code": null,
  "notes": "swagger test"
}
```

بعدها افحص:

- `GET /api/vendor/notifications?type=vendor_new_order`
- `GET /api/vendor/orders`

المتوقع:

- Notification جديدة من النوع `vendor_new_order`
- نفس `referenceId` يساوي `order.id`

### اختبار payload شبيه بالموبايل

جرّب إذا كنت تريد التأكد من التوافق:

```json
{
  "vendorId": "82e135c4-3023-4d12-810b-9a6c0d9bd537",
  "addressId": "69afae0a-8434-4942-a0ce-0d259e09771b",
  "deliverySlotId": "standard-30-45",
  "paymentMethod": "cash_on_delivery",
  "promoCode": null,
  "note": "mobile style test"
}
```

إذا كانت النسخة المنشورة تحتوي على التعديل، فهذا يجب أن يعمل أيضًا.

## 11. كيف تختبر من الموبايل

عند فشل الموبايل، لا نبدأ من الواجهة.

ابدأ بهذه القائمة:

1. التقط الـ request body الفعلي الذي يخرج من الموبايل.
2. تأكد من endpoint الفعلي:
   - يجب أن يكون `POST /api/orders`
3. تأكد من الـ base URL:
   - هل يضرب نفس البيئة التي اختبرتها من Swagger؟
4. تأكد من الـ headers:
   - `Authorization: Bearer <customer token>`
5. تأكد من القيم:
   - `vendor_id` أو `vendorId`
   - `address_id` أو `addressId`
   - `payment_method` أو `paymentMethod`
6. بعد إنشاء الأوردر افحص:
   - هل الأوردر رجع `200`؟
   - ما هو `order.id`؟
   - ما هو `order.status`؟
   - هل التاجر المتوقع هو نفس `vendorId`؟

## 12. كيف تعرف أين توقفت المشكلة

### الحالة 1: `POST /api/orders` لا يرجع `200`

المشكلة قبل الإشعار أصلًا.

افحص:

- validation
- auth
- payload shape
- `paymentMethod`
- `vendorId`
- `addressId`

### الحالة 2: `POST /api/orders` يرجع `200` لكن لا يوجد `vendor_new_order`

افحص:

- هل الأوردر وصل إلى `PendingVendorAcceptance`؟
- هل التاجر الصحيح هو نفسه الذي تراقبه؟
- هل البيئة التي تختبر عليها هي آخر نسخة منشورة؟

### الحالة 3: يوجد `vendor_new_order` في الـ API لكن لا يظهر في الجرس

المشكلة هنا فرونت:

- vendor panel لا يعمل refresh
- أو SignalR غير متصل
- أو الـ UI لا يضيف الإشعار إلى الحالة المحلية

### الحالة 4: يظهر داخل الجرس إذا الصفحة مفتوحة لكن لا يظهر والصفحة مقفولة

هذا ليس `SignalR`.

هذا متعلق بـ:

- `OneSignal`
- صلاحية المتصفح
- إعدادات البيئة
- اشتراك التاجر في push

## 13. ماذا نراجع في اللوجز

عند التشخيص، ابحث في اللوجز عن هذه الأشياء:

### لوج إنشاء الأوردر

- `POST /api/orders`

### لوج حالة الأوردر

- `PendingVendorAcceptance`

### لوج الإشعارات

ابحث عن `INSERT INTO [Notifications]`

والمهم أن يكون النوع:

```text
vendor_new_order
```

وليس:

```text
vendor_admin_test
```

أو أي type آخر

### معنى كل type

- `vendor_admin_test`
  إشعار يدوي من الأدمن للاختبار فقط

- `vendor_new_order`
  هذا هو الإشعار الحقيقي المطلوب عند وصول أوردر جديد للتاجر

## 14. ماذا نراجع في قاعدة البيانات

إذا عندك وصول مباشر للـ DB، راجع:

### جدول `Orders`

تأكد من:

- `Id`
- `VendorId`
- `Status`
- `PlacedAtUtc`

### جدول `Notifications`

ابحث عن:

- `UserId = vendor.UserId`
- `Type = vendor_new_order`
- `ReferenceId = order.Id`

إذا لم تجد هذا الصف، فالمشكلة في المسار الخلفي.

إذا وجدته ولم يظهر في الواجهة، فالمشكلة في الفرونت أو الاتصال اللحظي.

## 15. النتائج التي تعني أن كل شيء صحيح

المسار يعتبر صحيحًا إذا تحققت هذه الأربع نقاط:

1. `POST /api/orders` يرجع `200`
2. الأوردر يدخل `PendingVendorAcceptance`
3. `GET /api/vendor/notifications?type=vendor_new_order` يرجع إشعارًا جديدًا لهذا الأوردر
4. `GET /api/vendor/orders` يظهر الأوردر نفسه

إذا تحققت هذه الأربع نقاط، فالباك إند يعمل بشكل صحيح.

## 16. Checklist سريع قبل اتهام الموبايل أو الباك إند

### Checklist الباك إند

- هل النسخة المنشورة هي آخر نسخة فعلاً؟
- هل `POST /api/orders` في نفس البيئة يعمل من Swagger؟
- هل يظهر `vendor_new_order` في API؟
- هل التاجر الصحيح هو نفسه الذي تتوقعه؟

### Checklist الموبايل

- هل يرسل نفس الـ base URL؟
- هل يرسل نفس الـ token؟
- هل يرسل `vendorId` أو `vendor_id` فعلًا؟
- هل يرسل `paymentMethod` بقيمة مدعومة؟
- هل يستخدم response الأوردر الصحيح؟

### Checklist الواجهة Vendor Panel

- هل `/api/vendor/notifications` يرجع الإشعار؟
- هل SignalR متصل؟
- هل الجرس يعمل refresh؟
- هل الفلترة لا تخفي نوع `vendor_new_order`؟

## 17. الملفات المرجعية المهمة

- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`
- `src/Zadana.Api/Modules/Orders/Requests/CheckoutRequests.cs`
- `src/Zadana.Application/Modules/Checkout/Commands/PlaceCheckoutOrder/PlaceCheckoutOrderCommand.cs`
- `src/Zadana.Application/Modules/Payments/Commands/ConfirmPaymobPayment/ConfirmPaymobPaymentCommand.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`
- `src/Zadana.Application/Modules/Orders/Support/OrderStatusHistoryTracking.cs`
- `scripts/Send-DebugOrder.ps1`
- `ORDER_STATUS_WORKFLOW_AND_NOTIFICATIONS.md`

## 18. التوصية العملية الحالية

إذا `Swagger` يعمل والإشعار يظهر، لكن الموبايل لا:

ابدأ مباشرة بهذه الخطوات:

1. خذ الـ request body الحقيقي الخارج من الموبايل.
2. أعد إرسال نفس الـ body نفسه على نفس البيئة.
3. قارن `order.id`, `vendorId`, `status`.
4. افحص `/api/vendor/notifications?type=vendor_new_order`.
5. إذا فشل فقط من الموبايل، فالسبب شبه مؤكد:
   - اختلاف payload
   - اختلاف environment
   - اختلاف vendor المختار
   - أو نسخة API قديمة على البيئة التي يضربها التطبيق

## 19. الخلاصة النهائية

الاستنتاج الحالي بعد الفحص والتنفيذ والاختبار:

- مسار إشعار التاجر الحقيقي يعمل
- `vendor_new_order` يتم إنشاؤه فعليًا
- `POST /api/orders` أصبح متوافقًا مع صيغة Swagger وصيغة الموبايل القديمة
- إذا استمرت المشكلة من الموبايل بعد نشر آخر نسخة، فالمشكلة ليست في هذا المسار الأساسي غالبًا، بل في:
  - نسخة البيئة
  - payload الفعلي من التطبيق
  - اختيار التاجر
  - أو استهلاك الواجهة للإشعار بعد إنشائه
