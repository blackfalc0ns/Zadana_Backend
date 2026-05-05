# دليل ربط النزاعات والاسترجاع والتعويض لتطبيقات الموبايل

هذا الملف هو المرجع العملي الموحّد لفريق الموبايل بعد آخر تعديلات النظام، حتى يتم الربط بشكل صحيح مع:

- نزاعات العميل `complaint`
- طلبات الاسترجاع `return_request`
- بلاغات المندوب `driver_report`
- نزاعات المندوب `driver_dispute`
- التعويض بالكوبون في طلبات `COD`
- الاسترجاع النقدي في الطلبات المدفوعة أونلاين

هذا الدليل يغطي أساسًا:

- تطبيق العميل
- تطبيق المندوب
- تطبيق التاجر إذا كان له واجهات موبايل

أما موافقات الأدمن نفسها فتتم من `superadmin-panel`، لكن أثر قرارات الأدمن يظهر داخل نفس الـ APIs التي سيقرأ منها الموبايل.

## 1. الملفات المرجعية الأساسية في الباك إند

- `Zadana-Backend/src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`
- `Zadana-Backend/src/Zadana.Api/Modules/Delivery/Controllers/DriverSupportController.cs`
- `Zadana-Backend/src/Zadana.Api/Modules/Vendors/Controllers/VendorOrderCasesController.cs`
- `Zadana-Backend/src/Zadana.Application/Modules/Orders/Services/OrderSupportCaseWorkflowService.cs`
- `Zadana-Backend/src/Zadana.Infrastructure/Modules/Orders/Services/OrderReadService.cs`

## 2. الفكرة العامة التي يجب أن يفهمها فريق الموبايل

النظام الآن مبني على كيان موحّد اسمه `OrderSupportCase`.

يعني أن كل ما يخص:

- الشكوى
- طلب الاسترجاع
- بلاغ المندوب
- نزاع المندوب

يخرج في النهاية كـ `case` واحدة لها:

- `id`
- `type`
- `status`
- `messages`
- `activities`
- `participants`
- `allowed_actions`

مهم جدًا:

- ليس كل `POST` ينشئ `case` جديدة
- النظام قد يدمج الطلب داخل `case` مفتوحة موجودة أصلًا إذا كان الدمج مسموحًا
- لذلك لا تعتمدوا على افتراض أن كل إنشاء ينتج `caseId` جديدة
- اعتمدوا دائمًا على `case.id` الراجع من الـ API

## 3. قواعد الدمج الحالية التي يجب أن يعرفها فريق الموبايل

بعد التعديلات الأخيرة:

- شكوى العميل لا تندمج مع نزاع المندوب
- `return_request` لا تندمج مع `complaint`
- الدمج يحدث فقط إذا كانت الحالة:
  - من نفس النوع
  - ومن نفس صاحب المبادرة في حالات العميل والمندوب

أمثلة:

- عميل فتح `complaint` ثم فتح `complaint` ثانية على نفس الطلب: قد يتم إرجاع نفس `case.id`
- عميل فتح `complaint` ثم المندوب فتح `driver_dispute` على نفس الطلب: لا يحدث دمج
- عميل فتح `return_request` ثم حاول فتح `complaint`: يرفض إذا كانت هناك حالة نشطة غير متوافقة

## 4. المصادقة

### تطبيق العميل

كل المسارات تحتاج:

- `Authorization: Bearer <customer_token>`

### تطبيق المندوب

كل المسارات تحتاج:

- `Authorization: Bearer <driver_token>`

### تطبيق التاجر

كل المسارات تحتاج:

- `Authorization: Bearer <vendor_token>`

## 5. المفاهيم التي يجب أن تُبنى عليها الواجهة

لا تبنوا الـ UI على `refund_method` فقط.

الحقول الأهم لفهم نتيجة الحالة:

- `type`
- `status`
- `initiator_role`
- `waiting_on_role`
- `participants`
- `allowed_actions`
- `refund_method`
- `compensation_type`
- `settlement_status`
- `coupon_code`
- `coupon_expires_at`
- `coupon_redeemed`

## 6. معاني الحقول المهمة

### `type`

- `complaint`
- `return_request`
- `driver_report`
- `driver_dispute`

### `status`

- `submitted`
- `in_review`
- `awaiting_customer_evidence`
- `approved`
- `rejected`
- `resolved`

### `refund_method`

في الفلو الحالي استخدموه كمعلومة مساعدة فقط، لا كمعنى نهائي:

- `same_method`
- `coupon`

### `compensation_type`

- `cash_refund`
- `coupon_compensation`

### `settlement_status`

هذه أهم خانة لعرض النتيجة النهائية للعميل:

- `pending_review`
- `cash_refunded`
- `coupon_issued`
- `coupon_redeemed`
- `rejected`
- `approved`

ومعناها:

- `pending_review`: الحالة ما زالت قيد المراجعة
- `cash_refunded`: تمت الموافقة وتم الاسترجاع النقدي
- `coupon_issued`: تمت الموافقة وتم إصدار كوبون تعويضي
- `coupon_redeemed`: الكوبون صدر ثم تم استخدامه
- `rejected`: تم رفض الطلب
- `approved`: موافقة عامة، لكن لا تعتمدوا عليها وحدها لفهم شكل التسوية

### `waiting_on_role`

تحدد الطرف المطلوب منه الرد الآن:

- `customer`
- `vendor`
- `driver`
- `null`

### `allowed_actions`

هذه القائمة هي التي يجب أن تتحكم في الأزرار داخل الواجهة.

أهم مثال:

- لو تحتوي `message` اعرضوا زر إرسال رسالة أو متابعة

## 7. فلو العميل

### 7.1 رفع مرفق

- `POST /api/orders/{orderId}/cases/attachments`

نوع الطلب:

- `multipart/form-data`

الحقل:

- `file`

مثال response:

```json
{
  "file_name": "evidence.jpg",
  "url": "https://cdn.example.com/orders/support/evidence.jpg"
}
```

الاستخدام:

- ارفعوا الملفات أولًا
- ثم استخدموا `file_name` و`url` في إنشاء الحالة أو إرسال الرسالة

### 7.2 إنشاء شكوى أو طلب استرجاع

- `POST /api/orders/{orderId}/cases`

مثال body:

```json
{
  "type": "return_request",
  "reason_code": "wrong_item",
  "message": "وصلني منتج مختلف عن المطلوب",
  "attachments": [
    {
      "file_name": "wrong-item.jpg",
      "file_url": "https://cdn.example.com/wrong-item.jpg"
    }
  ]
}
```

القيم المسموحة للعميل في `type`:

- `complaint`
- `return_request`

### 7.3 جلب كل الحالات على الطلب

- `GET /api/orders/{orderId}/cases`

تستخدم في:

- شاشة سجل النزاعات على الطلب
- تحديد إن كان هناك case مفتوحة أو قديمة

### 7.4 جلب تفاصيل حالة واحدة

- `GET /api/orders/{orderId}/cases/{caseId}`

هذه الشاشة يجب أن تعرض:

- نوع الحالة
- الحالة الحالية
- من بدأ الحالة
- من المطلوب منه الرد
- المحادثة `messages`
- النشاطات `activities`
- المرفقات
- قرار الأدمن إن وجد
- بيانات التعويض أو الاسترجاع إن وجدت

### 7.5 إرسال متابعة داخل الحالة

المسار الموصى به:

- `POST /api/orders/{orderId}/cases/{caseId}/messages`

يوجد مسار متوافق قديم:

- `POST /api/orders/{orderId}/cases/{caseId}/reply`

مثال body:

```json
{
  "message": "أرفقت صورة أوضح",
  "attachments": [
    {
      "file_name": "extra-proof.jpg",
      "file_url": "https://cdn.example.com/extra-proof.jpg"
    }
  ]
}
```

### 7.6 حالة الاسترجاع المختصرة

- `GET /api/orders/{orderId}/refund-status`

هذا endpoint مناسب جدًا لبطاقة سريعة داخل شاشة تفاصيل الطلب.

أهم الحقول التي يجب قراءتها:

- `has_active_case`
- `case_status`
- `case_type`
- `requested_amount`
- `approved_amount`
- `refund_method`
- `compensation_type`
- `settlement_status`
- `coupon_code`
- `coupon_expires_at`
- `coupon_redeemed`
- `refund_status`
- `customer_note`

## 8. كيف يظهر قرار الأدمن للعميل

### حالة الطلبات المدفوعة أونلاين

بعد موافقة الأدمن على `return_request`:

- `compensation_type = cash_refund`
- `refund_method = same_method`
- `settlement_status = cash_refunded`

### حالة طلبات `COD`

بعد موافقة الأدمن على `return_request`:

- `compensation_type = coupon_compensation`
- `refund_method = coupon`
- `settlement_status = coupon_issued`
- يتم إنشاء كوبون مخصص لنفس العميل

بعد استخدام الكوبون لاحقًا:

- `coupon_redeemed = true`
- `settlement_status = coupon_redeemed`

## 9. أين يظهر الكوبون التعويضي للعميل

الكوبون التعويضي في حالة `COD` يجب أن يظهر للمستخدم في 3 أماكن منطقية:

### 9.1 الإشعار

عند موافقة الأدمن يرسل النظام إشعارًا يحتوي على بيانات مفيدة مثل:

- `couponCode`
- `couponValue`
- `couponExpiresAt`

### 9.2 شاشة حالة الاسترجاع

من:

- `GET /api/orders/{orderId}/refund-status`

### 9.3 شاشة تفاصيل الحالة نفسها

من:

- `GET /api/orders/{orderId}/cases/{caseId}`

## 10. كيف يجب أن تعرض واجهة العميل النتيجة

### لو `settlement_status = pending_review`

اعرضوا:

- الطلب قيد المراجعة

### لو `settlement_status = cash_refunded`

اعرضوا:

- تم استرجاع المبلغ
- قيمة المبلغ
- وسيلة الاسترجاع إن كانت موجودة

### لو `settlement_status = coupon_issued`

اعرضوا:

- تمت الموافقة على طلبك
- تم إصدار كوبون تعويضي
- `coupon_code`
- `approved_amount`
- `coupon_expires_at`
- حالة الاستخدام `لم يستخدم بعد`

### لو `settlement_status = coupon_redeemed`

اعرضوا:

- تم إصدار كوبون تعويضي
- تم استخدام الكوبون

### لو `settlement_status = rejected`

اعرضوا:

- تم رفض الطلب
- `customer_note` أو `decision_notes` إن وجدت

## 11. شكل response مهم لتطبيق العميل

هذا مثال مبسط لشكل منطقي يجب أن يكون فريق الموبايل مستعدًا له:

```json
{
  "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "order_id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "type": "return_request",
  "status": "approved",
  "requested_refund_amount": 120.0,
  "approved_refund_amount": 100.0,
  "refund_method": "coupon",
  "compensation_type": "coupon_compensation",
  "settlement_status": "coupon_issued",
  "coupon_code": "RET-AB12CD34",
  "coupon_expires_at": "2026-06-05T12:00:00Z",
  "coupon_redeemed": false,
  "initiator_role": "customer",
  "waiting_on_role": null,
  "participants": [],
  "allowed_actions": ["message"],
  "attachments": [],
  "activities": [],
  "messages": []
}
```

## 12. الرسائل داخل الحالة

الحقل الصحيح للمحادثة هو:

- `messages[]`

مثال:

```json
{
  "id": "guid",
  "action": "customer_reply",
  "message_type": "participant_message",
  "title": "Customer replied",
  "body": "أرسلت صورة إضافية",
  "author_role": "customer",
  "visible_to": ["customer", "vendor"],
  "is_internal_only": false,
  "created_at": "2026-05-02T12:00:00Z",
  "attachments": []
}
```

قواعد العرض:

- اعرضوا الرسائل مرتبة زمنيًا
- اعرضوا المرفقات التابعة لكل رسالة
- لو `is_internal_only = true` لا تعرضوها للمستخدم الخارجي

## 13. فلو المندوب

### 13.1 الإبلاغ عن مشكلة تشغيلية

- `POST /api/drivers/support/orders/{orderId}/report-issue`

مثال:

```json
{
  "reasonCode": "customer_unreachable",
  "message": "العميل لا يرد على الهاتف",
  "attachments": [
    {
      "fileName": "proof.jpg",
      "fileUrl": "https://cdn.example.com/proof.jpg"
    }
  ]
}
```

### 13.2 فتح نزاع للمندوب

- `POST /api/drivers/support/orders/{orderId}/dispute`

مثال:

```json
{
  "reasonCode": "payout_dispute",
  "message": "يوجد خصم غير صحيح على هذا الطلب"
}
```

### 13.3 قائمة حالات المندوب

- `GET /api/drivers/support/cases?page=1&pageSize=20`

مناسبة لـ:

- شاشة قائمة القضايا
- الفلترة حسب النوع أو الحالة

### 13.4 تفاصيل حالة المندوب

- `GET /api/drivers/support/cases/{caseId}`

### 13.5 إرسال متابعة من المندوب

- `POST /api/drivers/support/orders/{orderId}/cases/{caseId}/messages`

مثال:

```json
{
  "reasonCode": "follow_up",
  "message": "أرسلت التوضيح المطلوب",
  "attachments": [
    {
      "fileName": "extra.jpg",
      "fileUrl": "https://cdn.example.com/extra.jpg"
    }
  ]
}
```

## 14. فلو التاجر

### 14.1 قائمة حالات التاجر

- `GET /api/vendor/order-cases`

### 14.2 تفاصيل حالة التاجر

- `GET /api/vendor/order-cases/{caseId}`

في response التاجر أصبح من المهم قراءة:

- `refundMethod`
- `compensationType`
- `settlementStatus`
- `couponCode`
- `couponExpiresAtUtc`
- `couponRedeemed`

هذا يساعد التاجر على فهم هل النتيجة كانت:

- استرجاع نقدي
- أم تعويض بكوبون

### 14.3 رد التاجر داخل الحالة

- `POST /api/vendor/order-cases/{caseId}/respond`

ويوجد أيضًا:

- `POST /api/vendor/order-cases/{caseId}/messages`

## 15. ماذا عن استرجاع حق المنصة من التاجر

هذه النقطة مهمة لفهم المنطق التشغيلي، لكن الموبايل لا يحتاج أن ينفذها بنفسه.

بعد موافقة الأدمن على بعض حالات `return_request`:

- العميل يأخذ حقه أولًا
- وبعدها النظام قد يبدأ `vendor recovery` داخليًا على التاجر حسب `cost_bearer`

هذا لا يغير طريقة الربط للموبايل، لكنه يفسر لماذا قد يحصل العميل على:

- `cash_refunded`
- أو `coupon_issued`

حتى لو كانت عملية التحصيل من التاجر لا تزال داخلية أو لاحقة.

## 16. الإشعارات والتحديث اللحظي

النظام يرسل تحديثات الحالة عبر:

- Inbox notifications
- SignalR
- Push notifications

### نوع الإشعار

- `order_support_case_changed`

### الـ Hub

- `/hubs/notifications`

### اسم الـ event

- `ReceiveOrderSupportCaseChanged`

سلوك الموبايل الموصى به:

- لو شاشة الحالة مفتوحة: أعد تحميل `GET /api/orders/{orderId}/cases/{caseId}`
- لو شاشة الطلب مفتوحة: أعد تحميل الطلب أو `refund-status`
- لو وصل إشعار تعويض كوبون: افتح Deep Link على شاشة الحالة أو شاشة الطلب

## 17. توصيات واجهات العميل

### شاشة تفاصيل الطلب

أضيفوا:

- بطاقة `Refund / Return Status`
- لو `has_active_case = true` اعرضوا زر `عرض الحالة`

### شاشة تفاصيل الحالة

اعرضوا:

- عنوان الحالة
- `status` كبادج
- `settlement_status` كبادج منفصل في حالات الاسترجاع
- المحادثة
- النشاطات
- المرفقات
- بلوك القرار النهائي

### بلوك القرار في حالة الكوبون

اعرضوا:

- تمت الموافقة
- كوبون تعويضي
- الكود
- القيمة
- تاريخ الانتهاء
- زر نسخ الكود
- هل تم استخدامه أم لا

## 18. توصيات واجهات المندوب

اعرضوا:

- نوع الحالة
- الحالة الحالية
- هل النظام ينتظر رد المندوب
- آخر رسالة أو آخر نشاط
- زر متابعة إذا كانت `allowed_actions` تسمح

## 19. توصيات واجهات التاجر

اعرضوا:

- نوع النزاع
- حالة النزاع
- نوع التعويض النهائي
- حالة التسوية
- إن كان هناك كوبون تعويضي وهل تم استخدامه

## 20. أخطاء شائعة يجب تجنبها في الواجهة

- لا تفترضوا أن `approved` تعني دائمًا استرجاعًا نقديًا
- لا تفترضوا أن كل `POST /cases` تنشئ `case` جديدة
- لا تبنوا المنطق على `refund_method` وحده
- لا تعرضوا الرسائل `internal_only`
- لا تعتمدوا على ترتيب قديم للحقول بدون قراءة `allowed_actions` و`waiting_on_role`

## 21. ملخص endpoints

### العميل

- `POST /api/orders/{orderId}/cases/attachments`
- `POST /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases/{caseId}`
- `POST /api/orders/{orderId}/cases/{caseId}/messages`
- `POST /api/orders/{orderId}/cases/{caseId}/reply`
- `GET /api/orders/{orderId}/refund-status`

### المندوب

- `POST /api/drivers/support/orders/{orderId}/report-issue`
- `POST /api/drivers/support/orders/{orderId}/dispute`
- `GET /api/drivers/support/cases`
- `GET /api/drivers/support/cases/{caseId}`
- `POST /api/drivers/support/orders/{orderId}/cases/{caseId}/messages`

### التاجر

- `GET /api/vendor/order-cases`
- `GET /api/vendor/order-cases/{caseId}`
- `POST /api/vendor/order-cases/{caseId}/respond`
- `POST /api/vendor/order-cases/{caseId}/messages`

## 22. Checklist سريعة لفريق الموبايل

1. اقرأوا `case.id` من الرد ولا تفترضوا إنشاء case جديدة دائمًا.
2. استخدموا `messages[]` للمحادثة و`activities[]` للتايم لاين.
3. استخدموا `allowed_actions` للتحكم في الأزرار.
4. استخدموا `waiting_on_role` لعرض من المطلوب منه الرد.
5. في حالات الاسترجاع اعتمدوا على `settlement_status` أولًا.
6. لو كانت `coupon_issued` أو `coupon_redeemed` اعرضوا:
   - `coupon_code`
   - `coupon_expires_at`
   - `coupon_redeemed`
7. اربطوا الإشعارات وSignalR بتحديث شاشة الحالة أو شاشة الطلب.

## 23. الخلاصة التنفيذية

لو أراد فريق الموبايل ربطًا صحيحًا وسريعًا بعد كل التعديلات، فالأولوية تكون بهذا الترتيب:

1. قراءة `settlement_status`
2. قراءة `compensation_type`
3. عرض بيانات الكوبون عند وجودها
4. الاعتماد على `messages[]` للمحادثة
5. الاعتماد على `allowed_actions` و`waiting_on_role` بدل الافتراضات الثابتة

هذا هو الربط الصحيح الحالي بعد التعديلات الأخيرة على نظام النزاعات والاسترجاع والتعويض.
