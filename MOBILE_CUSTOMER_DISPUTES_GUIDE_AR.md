# دليل ربط النزاعات لمطور تطبيق العميل

المرجع الأساسي لملف APIDog:

- `Zadana-Backend/Zadana_APIDog_Folders.json`

الكنترولر المسؤول:

- `Zadana-Backend/src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`

## 1. الفكرة العامة

نظام النزاعات في تطبيق العميل مبني على `OrderSupportCase`، ويعتمد على:

- `Case` واحدة فعالة لكل طلب
- كل الردود داخل `messages[]`
- النظام قد يدمج الرسالة في نزاع قائم بدل إنشاء `case` جديدة

حقول مهمة جدًا في الاستجابة:

- `initiator_role`
- `waiting_on_role`
- `participants`
- `allowed_actions`
- `messages`

## 2. المصادقة

كل المسارات هنا تتطلب:

- `Authorization: Bearer <customer_token>`

## 3. رفع مرفق للنزاع

`POST /api/orders/{orderId}/cases/attachments`

الغرض:

- رفع الملف أولًا
- ثم إرسال `file_name` و`url` في إنشاء النزاع أو الرسالة

نوع الطلب:

- `multipart/form-data`

الحقل المطلوب:

- `file`

مثال response:

```json
{
  "file_name": "photo.jpg",
  "url": "https://..."
}
```

## 4. إنشاء نزاع جديد

`POST /api/orders/{orderId}/cases`

مثال body:

```json
{
  "type": "return_request",
  "reason_code": "wrong_item",
  "message": "وصلني منتج مختلف عن المطلوب",
  "attachments": [
    {
      "file_name": "evidence.jpg",
      "file_url": "https://..."
    }
  ]
}
```

القيم المتوقعة في `type`:

- `complaint`
- `return_request`

مهم:

- لا تفترض أن كل إنشاء نزاع سيرجع `caseId` جديدة
- قد يعيد نفس الـ `case` المفتوحة على الطلب

## 5. جلب كل نزاعات الطلب

`GET /api/orders/{orderId}/cases`

الاستجابة:

- `items[]`

كل عنصر يحتوي على:

- بيانات الحالة
- `initiator_role`
- `waiting_on_role`
- `participants`
- `allowed_actions`
- `attachments`
- `activities`
- `messages`

## 6. جلب تفاصيل نزاع واحد

`GET /api/orders/{orderId}/cases/{caseId}`

هذه هي شاشة التفاصيل الأساسية في تطبيق العميل.

يفضل عرض:

- الحالة
- نوع النزاع
- من بدأ النزاع
- الطرف المطلوب منه الرد
- الرسائل
- المرفقات
- القرار النهائي إن وجد

## 7. إرسال رسالة داخل النزاع

المسار الموصى به:

`POST /api/orders/{orderId}/cases/{caseId}/messages`

يوجد مسار قديم متوافق:

`POST /api/orders/{orderId}/cases/{caseId}/reply`

مثال body:

```json
{
  "message": "أرفقت صورة أوضح",
  "attachments": [
    {
      "file_name": "more-proof.jpg",
      "file_url": "https://..."
    }
  ]
}
```

## 8. حالة الاسترجاع

`GET /api/orders/{orderId}/refund-status`

الغرض:

- عرض مختصر لحالة طلب الاسترجاع أو وجود support case نشطة

أهم الحقول:

- `has_active_case`
- `case_status`
- `case_type`
- `requested_amount`
- `approved_amount`
- `refund_method`
- `refund_status`

## 9. شكل الرسائل

مثال من `messages[]`:

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

شرح الحقول:

- `message_type`
  يوضح نوع الرسالة
- `visible_to`
  يوضح من يرى الرسالة
- `is_internal_only`
  لو كانت `true` لا يجب عرضها في تطبيق العميل

## 10. قواعد مهمة للموبايل

### لو `waiting_on_role = customer`

يعني النظام ينتظر رد العميل الآن.

### لو `allowed_actions` تحتوي `message`

يعني العميل يمكنه إرسال متابعة داخل النزاع.

### لو فتح العميل نزاعًا وهناك نزاع نشط

قد يدمج النظام الرسالة داخل نفس الـ `case`.

## 11. ملخص المسارات

- `POST /api/orders/{orderId}/cases/attachments`
- `POST /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases`
- `GET /api/orders/{orderId}/cases/{caseId}`
- `POST /api/orders/{orderId}/cases/{caseId}/messages`
- `GET /api/orders/{orderId}/refund-status`
