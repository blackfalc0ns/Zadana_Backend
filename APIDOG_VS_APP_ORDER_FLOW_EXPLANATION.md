# APIDog vs App Order Flow Explanation

هذا الملف يشرح لماذا قد ينجح إنشاء الطلب ووصوله للتاجر عند التجربة من `APIDog` بينما لا يظهر بنفس الشكل عند التجربة من التطبيق.

## الملخص السريع

إذا كان الطلب يعمل من `APIDog` ولا يعمل من التطبيق، فهذا لا يعني تلقائيًا أن الباك إند به عطل.

في النظام الحالي توجد نقطتان مهمتان جدًا:

1. الباك إند يقبل إنشاء الطلب من `POST /api/orders`.
2. الطلب لا يصل للتاجر كطلب جديد ولا كإشعار جديد إلا عندما تكون حالته `PendingVendorAcceptance`.

هذا يعني أن الفرق بين `APIDog` والتطبيق يكون غالبًا في واحد من هذه الأمور:

- نوع الدفع المستخدم في الاختبار
- اكتمال مسار الدفع الأونلاين من عدمه
- اختلاف `payload`
- اختلاف `base URL`
- اختلاف التاجر المختار فعليًا

## ما الذي يحدث عند استخدام APIDog

عندما نرسل طلبًا إلى:

- `POST /api/orders`

فالكنترولر المسؤول هو:

- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`

وهو يبني الأمر التالي:

- `PlaceCheckoutOrderCommand`

من خلال:

- `src/Zadana.Api/Modules/Orders/Requests/CheckoutRequests.cs`

والباك إند هنا يدعم الآن:

- `snake_case`
- `camelCase`

ويعمل normalization لقيم الدفع القديمة مثل:

- `cash_on_delivery` -> `cash`
- `credit_card` -> `card`
- `bank_transfer` -> `bank`

## لماذا قد ينجح APIDog أكثر من التطبيق

`APIDog` غالبًا ينجح لأنه يرسل طلبًا نظيفًا ومباشرًا، مثل:

```json
{
  "vendor_id": "guid",
  "address_id": "guid",
  "delivery_slot_id": "standard-30-45",
  "payment_method": "cash",
  "promo_code": null,
  "notes": "apidog test"
}
```

أو حتى لو أرسل بصيغة التطبيق القديمة، فالباك إند يتعامل معها.

لكن التطبيق قد يختلف في واحد من السيناريوهات التالية.

## السيناريو الأهم: الدفع الأونلاين

إذا كان التطبيق يستخدم `card` أو أي دفع أونلاين، فالنظام الحالي لا يرسل الطلب للتاجر مباشرة بعد `POST /api/orders`.

بدلًا من ذلك:

1. يتم إنشاء الطلب أولًا بحالة `PendingPayment`.
2. لا يظهر للتاجر أثناء هذه الحالة.
3. لا يخرج إشعار `vendor_new_order` للتاجر أثناء هذه الحالة.
4. بعد نجاح تأكيد الدفع فقط، يتحول الطلب إلى `PendingVendorAcceptance`.
5. عندها فقط يظهر للتاجر ويرسل له الإشعار.

الملفات المسؤولة عن ذلك:

- `src/Zadana.Application/Modules/Payments/Commands/ConfirmPaymobPayment/ConfirmPaymobPaymentCommand.cs`
- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`
- `src/Zadana.Infrastructure/Modules/Orders/Services/OrderReadService.cs`

## معنى هذا عمليًا

إذا جربت من `APIDog` باستخدام:

- `payment_method = cash`

فغالبًا سيصل الطلب للتاجر فورًا.

أما إذا جرب التطبيق باستخدام:

- `paymentMethod = card`

فلن يصل الطلب للتاجر إلا بعد اكتمال خطوة تأكيد الدفع.

لذلك قد يبدو لك أن:

- `APIDog` يعمل
- التطبيق لا يعمل

بينما الحقيقة أن:

- `APIDog` ربما يختبر `cash`
- التطبيق يختبر `card`

## متى يخرج إشعار التاجر

إشعار التاجر الحقيقي للطلب الجديد يخرج عند انتقال حالة الطلب إلى:

- `PendingVendorAcceptance`

وليس عند مجرد إنشاء السجل في قاعدة البيانات.

نوع الإشعار يكون:

- `vendor_new_order`

والمعالج المسؤول عن ذلك:

- `src/Zadana.Application/Modules/Orders/Events/OrderStatusChangedHandler.cs`

## ماذا يحدث في الدفع الأونلاين بعد النجاح

عند نجاح تأكيد الدفع:

1. يتم تعليم الدفع على أنه `Paid`.
2. يتم تحويل حالة الطلب من `PendingPayment` إلى `PendingVendorAcceptance`.
3. يتم نشر `OrderStatusChangedNotification`.
4. يتم إرسال إشعار داخلي للتاجر.
5. يتم إرسال push أيضًا إذا كانت إعدادات التاجر تسمح بذلك.

هذا يتم في:

- `src/Zadana.Application/Modules/Payments/Commands/ConfirmPaymobPayment/ConfirmPaymobPaymentCommand.cs`

## لماذا لا يرى التاجر الطلب قبل الدفع

تم تعديل قراءة طلبات التاجر بحيث لا تعرض الطلبات التي ما زالت في:

- `PendingPayment`

وهذا مقصود حتى لا يظهر للتاجر طلب غير مدفوع بعد في حالة الدفع الأونلاين.

الملف المسؤول:

- `src/Zadana.Infrastructure/Modules/Orders/Services/OrderReadService.cs`

## ما الذي يثبت أن الباك إند يعمل

إذا حصلت هذه الأربع نقاط، فالمسار الخلفي يعمل بشكل صحيح:

1. `POST /api/orders` يرجع نجاحًا.
2. الطلب في الدفع الأونلاين يدخل أولًا `PendingPayment`.
3. بعد نجاح تأكيد الدفع يتحول إلى `PendingVendorAcceptance`.
4. يظهر `vendor_new_order` للتاجر ويظهر الطلب في `vendor/orders`.

## لماذا يفشل التطبيق رغم نجاح APIDog

الأسباب الأكثر احتمالًا:

- التطبيق يرسل `card` بينما `APIDog` يختبر `cash`.
- التطبيق لا يكمل `Paymob iframe / return / webhook` بنجاح.
- التطبيق يضرب بيئة مختلفة عن التي جربت عليها في `APIDog`.
- التطبيق يرسل `vendorId` أو `addressId` أو `paymentMethod` بقيم مختلفة.
- التطبيق ينشئ الطلب، لكن مسار تأكيد الدفع لا يكتمل، فيظل الطلب `PendingPayment`.

## كيف تفرّق بسرعة بين المشكلتين

### الحالة 1

إذا كان `APIDog` يعمل مع `cash` والتطبيق لا يعمل مع `card`:

فهذه ليست مقارنة عادلة بين نفس المسار.

### الحالة 2

إذا كان `APIDog` يعمل مع `card` كاملًا حتى تأكيد الدفع، بينما التطبيق لا:

فهنا المشكلة غالبًا في التطبيق أو في تكامل Paymob داخل التطبيق.

### الحالة 3

إذا كان كلاهما ينشئ الطلب لكن الطلب في التطبيق يظل `PendingPayment`:

فهنا المشكلة في مسار تأكيد الدفع بعد الإنشاء، وليس في إنشاء الطلب نفسه.

## ما الذي يجب مراجعته داخل التطبيق

إذا أردت مطابقة التطبيق مع `APIDog`، يجب التحقق من:

1. نفس `base URL`
2. نفس `Authorization token`
3. نفس `payment method`
4. نفس `vendorId`
5. نفس `addressId`
6. هل اكتملت خطوة Paymob فعلًا
7. هل تم استدعاء مسار `return` أو `webhook` بنجاح

## الخلاصة

الباك إند الحالي يعمل كالتالي:

- الطلب الكاش يصل للتاجر مباشرة.
- الطلب الأونلاين لا يصل للتاجر إلا بعد نجاح الدفع.
- الإشعار لا يخرج عند مجرد إنشاء الطلب، بل عند وصوله إلى `PendingVendorAcceptance`.

لذلك إذا كان `APIDog` شغالًا والتطبيق لا، فالغالب أن الفرق في:

- نوع الدفع
- أو اكتمال تأكيد الدفع
- أو بيانات الطلب المرسلة من التطبيق

وليس في منطق إشعار التاجر نفسه.
