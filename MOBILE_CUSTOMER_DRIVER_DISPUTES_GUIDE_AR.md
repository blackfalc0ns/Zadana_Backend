# دليل ربط النزاعات لمطور الموبايل

هذا الملف مخصص لمطورَي تطبيق:

- العميل
- المندوب

ويشرح مسارات النزاعات المرتبطة بالنظام الموحد الجديد المبني على `OrderSupportCase`.

المرجع الأساسي لملف APIDog:

- `Zadana-Backend/Zadana_APIDog_Folders.json`

## 1. الفكرة العامة

النظام الآن يعمل بفكرة:

- `Case` واحدة فعالة لكل طلب
- كل الردود تتحول إلى `messages[]` داخل نفس النزاع
- الأدمن يرى كل الرسائل
- كل طرف خارجي يرى فقط الرسائل الموجهة له أو الرسائل العامة

حقول مهمة في استجابات النزاعات:

- `initiator_role`
  يحدد من بدأ النزاع: `customer` أو `driver` أو `admin`
- `waiting_on_role`
  يحدد الطرف المطلوب منه الرد الآن: `customer` أو `driver` أو `vendor` أو `null`
- `participants`
  قائمة الأطراف المشاركة في النزاع
- `allowed_actions`
  الإجراءات المتاحة للطرف الحالي
- `messages`
  المحادثة الفعلية المرتبة داخل النزاع

## 2. التوثيق داخل APIDog

الموجود حاليًا داخل `Zadana_APIDog_Folders.json` بوضوح:

- `POST /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases/{caseId}`
- `POST /api/orders/{orderId}/cases/attachments`

ملاحظة مهمة:

- مسارات نزاعات العميل موجودة داخل ملف APIDog.
- مسارات المندوب موجودة في الـ backend وتعمل فعليًا، لكن ليست مضافة حاليًا بنفس الوضوح داخل `Zadana_APIDog_Folders.json`.

## 3. المصادقة المطلوبة

### تطبيق العميل

كل مسارات العميل هنا تتطلب:

- `Authorization: Bearer <customer_token>`

### تطبيق المندوب

كل مسارات المندوب هنا تتطلب:

- `Authorization: Bearer <driver_token>`

## 4. مسارات العميل

الكنترولر:

- `Zadana-Backend/src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`

### 4.1 رفع مرفق للنزاع

`POST /api/orders/{orderId}/cases/attachments`

الغرض:

- رفع ملف أولًا
- ثم استخدام `file_name` و`url` في إنشاء النزاع أو إرسال رسالة جديدة

نوع الطلب:

- `multipart/form-data`

المدخلات:

- `file`

الاستجابة:

```json
{
  "file_name": "invoice.jpg",
  "url": "https://..."
}
```

### 4.2 إنشاء نزاع جديد للعميل

`POST /api/orders/{orderId}/cases`

مثال body:

```json
{
  "type": "return_request",
  "reason_code": "wrong_item",
  "message": "وصلني منتج مختلف عن المطلوب",
  "attachments": [
    {
      "file_name": "photo1.jpg",
      "file_url": "https://..."
    }
  ]
}
```

القيم المتوقعة في `type` للعميل:

- `complaint`
- `return_request`

ملاحظات:

- لو فيه `active case` مفتوحة على نفس الطلب، النظام يدمج الرسالة داخل نفس النزاع بدل إنشاء نزاع جديد.
- لذلك تطبيق الموبايل يجب أن يتعامل مع الاستجابة النهائية على أنها "الحالة الحالية للنزاع" وليس فقط "تم إنشاء سجل جديد".

### 4.3 جلب كل نزاعات الطلب

`GET /api/orders/{orderId}/cases`

الاستجابة:

- `items[]`

كل عنصر يحتوي الآن على:

- بيانات الحالة
- `initiator_role`
- `waiting_on_role`
- `participants`
- `allowed_actions`
- `attachments`
- `activities`
- `messages`

### 4.4 جلب تفاصيل نزاع واحد

`GET /api/orders/{orderId}/cases/{caseId}`

هذه أهم شاشة لتفاصيل النزاع في تطبيق العميل.

الأفضل في الموبايل عرض:

- الحالة الحالية
- من بدأ النزاع
- مطلوب الرد من من
- آخر الرسائل
- المرفقات
- القرار النهائي إن وجد

### 4.5 إرسال رسالة جديدة داخل النزاع

المسار الموصى به:

`POST /api/orders/{orderId}/cases/{caseId}/messages`

يوجد أيضًا مسار قديم متوافق:

`POST /api/orders/{orderId}/cases/{caseId}/reply`

مثال body:

```json
{
  "message": "أرفقت صورة أوضح للمنتج",
  "attachments": [
    {
      "file_name": "evidence-2.jpg",
      "file_url": "https://..."
    }
  ]
}
```

متى يستخدم؟

- الرد على طلب معلومات من الأدمن
- إرفاق إثبات جديد
- متابعة نفس النزاع بدل فتح نزاع جديد

### 4.6 حالة الاسترجاع

`GET /api/orders/{orderId}/refund-status`

الغرض:

- شاشة مختصرة في تطبيق العميل لمعرفة هل يوجد refund/support case نشطة

مثال fields:

- `has_active_case`
- `case_status`
- `case_type`
- `requested_amount`
- `approved_amount`
- `refund_method`
- `refund_status`

## 5. مسارات المندوب

الكنترولر:

- `Zadana-Backend/src/Zadana.Api/Modules/Delivery/Controllers/DriverSupportController.cs`

## 5.1 الإبلاغ عن مشكلة تشغيلية على الطلب

`POST /api/drivers/support/orders/{orderId}/report-issue`

مثال body:

```json
{
  "reasonCode": "customer_unreachable",
  "message": "العميل لا يرد على الهاتف",
  "attachments": [
    {
      "fileName": "location-note.jpg",
      "fileUrl": "https://..."
    }
  ]
}
```

ملاحظات:

- هذا المسار مخصص للمشاكل التشغيلية مثل:
  - العميل غير متاح
  - عنوان خاطئ
  - مشكلة في الطرد
  - ضرر ظاهر في الطلب
- لا يسمح به إلا إذا كان الطلب مرتبطًا فعلًا بالمندوب الحالي

### 5.2 نزاع مالي أو تشغيلي من المندوب

`POST /api/drivers/support/orders/{orderId}/dispute`

مثال body:

```json
{
  "reasonCode": "payout_dispute",
  "message": "يوجد خصم غير صحيح على هذا الطلب"
}
```

ملاحظات:

- هذا مناسب لمشاكل مثل:
  - مستحقات ناقصة
  - خصم غير صحيح
  - اعتراض على تسوية مرتبطة بالطلب

### 5.3 قائمة نزاعات المندوب

`GET /api/drivers/support/cases?page=1&pageSize=20`

الاستجابة مناسبة لشاشة list في تطبيق المندوب.

حقول مفيدة:

- `id`
- `orderId`
- `orderNumber`
- `type`
- `status`
- `priority`
- `reasonCode`
- `message`
- `adminNote`
- `createdAt`
- `updatedAt`
- `closedAt`

### 5.4 تفاصيل نزاع المندوب

`GET /api/drivers/support/cases/{caseId}`

تعرض:

- بيانات النزاع
- `attachments`
- `activities`

ملاحظة:

- واجهة المندوب الحالية في الـ backend لا تعيد `messages[]` بنفس تفصيل واجهة العميل.
- لكن إرسال الرسائل داخل نفس النزاع مدعوم فعليًا من خلال endpoint الرسائل أدناه.

### 5.5 إرسال رسالة داخل نزاع المندوب

`POST /api/drivers/support/orders/{orderId}/cases/{caseId}/messages`

مثال body:

```json
{
  "reasonCode": "follow_up",
  "message": "أضفت تفاصيل إضافية بخصوص المشكلة",
  "attachments": [
    {
      "fileName": "proof.jpg",
      "fileUrl": "https://..."
    }
  ]
}
```

الاستخدام:

- الرد على طلب معلومات
- متابعة النزاع نفسه
- إضافة إثبات أو توضيح جديد

## 6. شكل الرسائل داخل نزاع العميل

في استجابة العميل ستجد `messages[]` بالشكل التالي:

```json
{
  "id": "guid",
  "action": "customer_reply",
  "message_type": "participant_message",
  "title": "Customer replied",
  "body": "أرفقت صورة إضافية",
  "author_role": "customer",
  "visible_to": ["customer", "vendor"],
  "is_internal_only": false,
  "created_at": "2026-05-02T12:00:00Z",
  "attachments": []
}
```

شرح الحقول:

- `message_type`
  يوضح نوع الرسالة مثل:
  - `case_opened`
  - `participant_message`
  - `internal_note`
  - `evidence_requested`
  - `decision`
- `visible_to`
  يحدد من يحق له رؤية الرسالة
- `is_internal_only`
  إذا كانت `true` فهي ملاحظة داخلية للأدمن فقط ولا يجب أن تظهر للعميل أو المندوب

## 7. حالات مهمة للموبايل

### 7.1 لو أنشأ العميل نزاعًا وهناك نزاع نشط بالفعل

النظام لن ينشئ case جديدة بالضرورة.

بدل ذلك:

- قد يدمج الرسالة داخل نفس النزاع
- وقد يعيد نفس `caseId` القديمة

لذلك:

- لا تعتمد في الموبايل على أن كل `POST /cases` سينتج `caseId` جديدة
- اعتمد على `case.id` الراجع من الاستجابة

### 7.2 لو `waiting_on_role = customer`

هذا يعني:

- المطلوب الآن رد من العميل

### 7.3 لو `waiting_on_role = driver`

هذا يعني:

- المطلوب الآن رد من المندوب

### 7.4 لو `allowed_actions` تحتوي `message`

هذا يعني:

- الطرف الحالي يستطيع إرسال رسالة متابعة على النزاع

## 8. توصية واجهات الموبايل

### شاشة العميل

يفضل عرض:

- حالة النزاع
- نوع النزاع
- آخر تحديث
- من بدأ النزاع
- الطرف المطلوب منه الرد
- Timeline / messages
- زر إضافة رسالة
- زر إضافة مرفق

### شاشة المندوب

يفضل عرض:

- قائمة القضايا
- تفاصيل كل قضية
- timeline مختصر
- زر متابعة/إرسال رسالة

## 9. ملاحظات تنفيذية سريعة

- مسارات العميل هي الأكثر اكتمالًا من ناحية `messages[]`.
- مسارات المندوب تعمل بالفعل لكن response detail ما زال أبسط من العميل.
- إذا كان تطبيق المندوب يحتاج `participants` و`messages` بنفس شكل العميل، فهذا تطوير إضافي ممكن إضافته لاحقًا.

## 10. ملخص سريع للمسارات

### العميل

- `POST /api/orders/{orderId}/cases/attachments`
- `POST /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases/{caseId}`
- `POST /api/orders/{orderId}/cases/{caseId}/messages`
- `GET /api/orders/{orderId}/refund-status`

### المندوب

- `POST /api/drivers/support/orders/{orderId}/report-issue`
- `POST /api/drivers/support/orders/{orderId}/dispute`
- `GET /api/drivers/support/cases`
- `GET /api/drivers/support/cases/{caseId}`
- `POST /api/drivers/support/orders/{orderId}/cases/{caseId}/messages`

