# دليل ربط تطبيق العميل للنزاعات والاسترجاع والتعويض

هذا الملف مخصص لفريق تطبيق العميل فقط `users app`.

الغرض منه أن يكون المرجع الواضح والنهائي لربط:

- الشكاوى `complaint`
- طلبات الاسترجاع `return_request`
- المحادثة داخل الحالة
- حالة الاسترجاع
- التعويض بالكوبون في طلبات `COD`

إذا كان فريق العميل سيقرأ ملفًا واحدًا فقط، فليكن هذا الملف.

## 1. ما الذي يدعمه تطبيق العميل الآن

تطبيق العميل يجب أن يدعم هذه الوظائف:

- رفع مرفق للحالة
- إنشاء شكوى أو طلب استرجاع
- جلب كل الحالات الخاصة بطلب معين
- جلب تفاصيل حالة واحدة
- إرسال متابعة أو رسالة داخل الحالة
- قراءة حالة الاسترجاع المختصرة من شاشة الطلب
- استقبال أثر موافقة الأدمن سواء كانت:
  - استرجاع نقدي
  - أو كوبون تعويضي

## 2. الفكرة الأساسية

النظام يستخدم كيانًا موحدًا اسمه `OrderSupportCase`.

بالنسبة لتطبيق العميل، الأنواع المهمة فقط هي:

- `complaint`
- `return_request`

مهم جدًا:

- ليس كل إنشاء حالة ينتج `caseId` جديدة
- أحيانًا النظام يدمج الطلب الجديد داخل `case` مفتوحة موجودة
- لذلك يجب دائمًا الاعتماد على `case.id` العائد من الـ API

## 3. المصادقة

كل المسارات هنا تحتاج:

- `Authorization: Bearer <customer_token>`

## 4. المسارات التي يحتاجها تطبيق العميل

### 4.1 رفع مرفق

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
- ثم استخدموا `file_name` و`url` داخل إنشاء الحالة أو الرسالة

### 4.2 إنشاء شكوى أو طلب استرجاع

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

القيم المسموحة في `type`:

- `complaint`
- `return_request`

### 4.3 جلب كل الحالات على الطلب

- `GET /api/orders/{orderId}/cases`

تستخدم في:

- شاشة سجل النزاعات على الطلب
- معرفة هل هناك حالة مفتوحة أو قديمة

### 4.4 جلب تفاصيل حالة واحدة

- `GET /api/orders/{orderId}/cases/{caseId}`

هذه أهم شاشة في الفلو كله.

يجب أن تعرض:

- نوع الحالة
- حالتها الحالية
- الرسائل `messages`
- النشاطات `activities`
- المرفقات
- من المطلوب منه الرد
- قرار الأدمن إن وجد
- بيانات التعويض أو الاسترجاع

### 4.5 إرسال رسالة داخل الحالة

المسار الموصى به:

- `POST /api/orders/{orderId}/cases/{caseId}/messages`

يوجد أيضًا مسار متوافق قديم:

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

### 4.6 حالة الاسترجاع المختصرة داخل شاشة الطلب

- `GET /api/orders/{orderId}/refund-status`

هذا endpoint مهم جدًا لعرض بطاقة سريعة داخل order details.

## 5. القواعد المهمة التي يجب أن تبني عليها الواجهة

لا تبنوا الواجهة على `refund_method` فقط.

الأولوية الصحيحة لفهم نتيجة الحالة هي:

1. `settlement_status`
2. `compensation_type`
3. `coupon_code`
4. `coupon_expires_at`
5. `coupon_redeemed`

## 6. معاني الحقول المهمة

### `type`

- `complaint`
- `return_request`

### `status`

- `submitted`
- `in_review`
- `awaiting_customer_evidence`
- `approved`
- `rejected`
- `resolved`

### `compensation_type`

- `cash_refund`
- `coupon_compensation`

### `settlement_status`

- `pending_review`
- `cash_refunded`
- `coupon_issued`
- `coupon_redeemed`
- `rejected`
- `approved`

المعنى:

- `pending_review`: الطلب ما زال تحت المراجعة
- `cash_refunded`: تمت الموافقة وتم الاسترجاع النقدي
- `coupon_issued`: تمت الموافقة وتم إصدار كوبون تعويضي
- `coupon_redeemed`: الكوبون صدر وتم استخدامه
- `rejected`: تم رفض الطلب
- `approved`: موافقة عامة، لكن لا تعتمدوا عليها وحدها لعرض النتيجة النهائية

### `waiting_on_role`

بالنسبة للعميل، أهم قيمة هنا:

- `customer`

إذا كانت `customer` فهذا يعني أن النظام ينتظر رد العميل الآن.

### `allowed_actions`

لو كانت تحتوي `message`:

- اعرضوا زر إرسال متابعة أو رسالة

## 7. قواعد الأهلية

### إنشاء `complaint`

مسموح فقط بعد خروج الطلب من `pending_payment`

### إنشاء `return_request`

مسموح فقط عندما يكون الطلب `delivered`

### قاعدة وجود حالة نشطة

إذا كانت هناك حالة نشطة غير متوافقة، قد يرجع النظام:

- `ORDER_SUPPORT_CASE_ALREADY_EXISTS`

## 8. قواعد الدمج التي تهم العميل

الآن النظام لا يخلط بين الحالات المختلفة.

أمثلة:

- `complaint` لا تندمج مع `return_request`
- حالة العميل لا تندمج مع حالة المندوب

لكن لو العميل فتح نفس النوع مرة أخرى على نفس الطلب:

- قد يرجع نفس `case.id`

لذلك:

- بعد الإنشاء، افتحوا الشاشة بالـ `case.id` الراجع من الرد
- لا تعتمدوا على افتراض إنشاء حالة جديدة دائمًا

## 9. كيف يظهر قرار الأدمن للعميل

### إذا كان الطلب مدفوعًا أونلاين

بعد موافقة الأدمن على `return_request`:

- `compensation_type = cash_refund`
- `refund_method = same_method`
- `settlement_status = cash_refunded`

### إذا كان الطلب `COD`

بعد موافقة الأدمن على `return_request`:

- `compensation_type = coupon_compensation`
- `refund_method = coupon`
- `settlement_status = coupon_issued`

ثم يتم إنشاء كوبون مخصص لنفس العميل.

بعد استخدام الكوبون:

- `coupon_redeemed = true`
- `settlement_status = coupon_redeemed`

## 10. أين يظهر الكوبون التعويضي للعميل

الكوبون يجب أن يظهر للعميل في 3 أماكن:

### 10.1 الإشعار

عند موافقة الأدمن، يصل إشعار يحتوي عادة على:

- `couponCode`
- `couponValue`
- `couponExpiresAt`

### 10.2 شاشة `refund-status`

من:

- `GET /api/orders/{orderId}/refund-status`

### 10.3 شاشة تفاصيل الحالة

من:

- `GET /api/orders/{orderId}/cases/{caseId}`

## 11. الحقول التي يجب قراءتها من `refund-status`

أهم الحقول:

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

## 12. كيف يجب أن تعرض شاشة العميل النتيجة

### لو `settlement_status = pending_review`

اعرضوا:

- الطلب قيد المراجعة

### لو `settlement_status = cash_refunded`

اعرضوا:

- تم استرجاع المبلغ
- قيمة المبلغ
- وسيلة الاسترجاع إن وجدت

### لو `settlement_status = coupon_issued`

اعرضوا:

- تمت الموافقة على طلبك
- تم إصدار كوبون تعويضي
- `coupon_code`
- `approved_amount`
- `coupon_expires_at`
- زر نسخ الكود

### لو `settlement_status = coupon_redeemed`

اعرضوا:

- تم إصدار كوبون تعويضي
- تم استخدام الكوبون

### لو `settlement_status = rejected`

اعرضوا:

- تم رفض الطلب
- `customer_note` أو `decision_notes` إن وجدت

## 13. شكل response مهم لتفاصيل الحالة

مثال مبسط:

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

## 14. الرسائل داخل الحالة

المحادثة الصحيحة داخل الحالة تكون في:

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

- اعرضوا الرسائل بترتيب زمني
- اعرضوا المرفقات التابعة لكل رسالة
- لا تعرضوا أي رسالة `is_internal_only = true`

## 15. الإشعارات والتحديث اللحظي

النظام يرسل تحديثات عبر:

- Inbox notifications
- SignalR
- Push notifications

### نوع الإشعار

- `order_support_case_changed`

### الـ Hub

- `/hubs/notifications`

### اسم الـ event

- `ReceiveOrderSupportCaseChanged`

السلوك الموصى به:

- إذا كانت شاشة الحالة مفتوحة: أعدوا تحميل `GET /api/orders/{orderId}/cases/{caseId}`
- إذا كانت شاشة الطلب مفتوحة: أعدوا تحميل `refund-status` أو بيانات الطلب
- إذا وصل إشعار كوبون: افتحوا Deep Link على شاشة الطلب أو الحالة

## 16. التوصية الأفضل للواجهات

### شاشة تفاصيل الطلب

اعرضوا:

- بطاقة `Refund / Return Status`
- لو `has_active_case = true` اعرضوا زر `عرض الحالة`

### شاشة تفاصيل الحالة

اعرضوا:

- عنوان الحالة
- `status`
- `settlement_status` كبادج منفصل في حالات الاسترجاع
- الرسائل
- النشاطات
- المرفقات
- القرار النهائي

### في حالة الكوبون

اعرضوا:

- تمت الموافقة
- كوبون تعويضي
- الكود
- القيمة
- تاريخ الانتهاء
- هل استخدم أم لا

## 17. أخطاء يجب أن يتعامل معها التطبيق

- `INVALID_SUPPORT_CASE_TYPE`
- `ORDER_COMPLAINT_NOT_ALLOWED`
- `ORDER_RETURN_NOT_ALLOWED`
- `ORDER_SUPPORT_CASE_ALREADY_EXISTS`
- `INVALID_FILE`

## 18. أخطاء شائعة يجب تجنبها

- لا تفترضوا أن `approved` تعني دائمًا refund نقدي
- لا تفترضوا أن كل `POST /cases` تنشئ `case` جديدة
- لا تبنوا الشاشة على `refund_method` وحده
- لا تعرضوا الرسائل الداخلية

## 19. Checklist سريعة لفريق العميل

1. اربطوا إنشاء الحالة على `POST /api/orders/{orderId}/cases`.
2. اعتمدوا على `case.id` العائد من الرد.
3. استخدموا `messages[]` للمحادثة.
4. استخدموا `allowed_actions` للتحكم في الأزرار.
5. اعتمدوا على `settlement_status` أولًا في حالات الاسترجاع.
6. لو كانت النتيجة كوبونًا اعرضوا:
   - `coupon_code`
   - `coupon_expires_at`
   - `coupon_redeemed`
7. اربطوا الإشعارات وSignalR بتحديث الشاشة فورًا.

## 20. المسارات النهائية المختصرة

- `POST /api/orders/{orderId}/cases/attachments`
- `POST /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases/{caseId}`
- `POST /api/orders/{orderId}/cases/{caseId}/messages`
- `POST /api/orders/{orderId}/cases/{caseId}/reply`
- `GET /api/orders/{orderId}/refund-status`

## 21. الخلاصة التنفيذية لفريق العميل

الربط الصحيح لتطبيق العميل الآن هو:

1. اعتمدوا على `settlement_status` لفهم نتيجة طلب الاسترجاع
2. اعرضوا بيانات الكوبون إذا كانت `coupon_issued` أو `coupon_redeemed`
3. استخدموا `messages[]` و`allowed_actions` و`waiting_on_role` بدل أي افتراضات قديمة
4. حدثوا الشاشة فور وصول إشعار أو event خاص بالحالة

هذا هو الفلو الصحيح الحالي لتطبيق العميل بعد كل التعديلات الأخيرة.
