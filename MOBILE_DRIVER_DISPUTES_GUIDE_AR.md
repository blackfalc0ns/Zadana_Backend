# دليل ربط النزاعات لمطور تطبيق المندوب

المرجع الأساسي الحالي:

- `Zadana-Backend/Zadana_APIDog_Folders.json`

مهم:

- مسارات المندوب تعمل فعليًا في الـ backend
- لكنها ليست موثقة داخل ملف APIDog الحالي بنفس وضوح مسارات العميل

الكنترولر المسؤول:

- `Zadana-Backend/src/Zadana.Api/Modules/Delivery/Controllers/DriverSupportController.cs`

## 1. الفكرة العامة

نزاعات ومشكلات المندوب مرتبطة أيضًا بـ `OrderSupportCase`.

المندوب يمكنه:

- الإبلاغ عن مشكلة تشغيلية على الطلب
- فتح dispute مرتبطة بالطلب
- متابعة القضايا السابقة
- إرسال رسائل جديدة داخل نفس القضية

## 2. المصادقة

كل المسارات هنا تتطلب:

- `Authorization: Bearer <driver_token>`

## 3. الإبلاغ عن مشكلة تشغيلية

`POST /api/drivers/support/orders/{orderId}/report-issue`

مثال body:

```json
{
  "reasonCode": "customer_unreachable",
  "message": "العميل لا يرد على الهاتف",
  "attachments": [
    {
      "fileName": "note.jpg",
      "fileUrl": "https://..."
    }
  ]
}
```

أمثلة الاستخدام:

- العميل غير متاح
- عنوان خاطئ
- مشكلة في التسليم
- ضرر في الطرد

مهم:

- لن يقبل النظام هذا المسار إلا لو كان الطلب مرتبطًا بالمندوب الحالي

## 4. إنشاء نزاع للمندوب

`POST /api/drivers/support/orders/{orderId}/dispute`

مثال body:

```json
{
  "reasonCode": "payout_dispute",
  "message": "يوجد خصم غير صحيح على هذا الطلب"
}
```

أمثلة الاستخدام:

- اعتراض على خصم
- مشكلة في المستحقات
- نزاع مالي أو تشغيلي مرتبط بالطلب

## 5. جلب قائمة القضايا

`GET /api/drivers/support/cases?page=1&pageSize=20`

الاستجابة مناسبة لشاشة list في تطبيق المندوب.

أهم الحقول:

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

## 6. جلب تفاصيل قضية واحدة

`GET /api/drivers/support/cases/{caseId}`

الاستجابة الحالية تعرض:

- بيانات القضية
- `attachments`
- `activities`

مهم:

- response المندوب الحالية أبسط من Response العميل
- لا تُرجع `messages[]` بنفس التفصيل حاليًا
- لكن إرسال رسائل داخل القضية مدعوم فعليًا

## 7. إرسال رسالة داخل القضية

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
- متابعة نفس النزاع
- إضافة توضيح أو إثبات جديد

## 8. شكل الاستجابات الأساسية

### Response عند الإنشاء أو الإرسال

```json
{
  "id": "guid",
  "orderId": "guid",
  "orderNumber": "ORD-10021",
  "type": "DriverReport",
  "status": "Submitted",
  "priority": "Medium",
  "reasonCode": "customer_unreachable",
  "message": "العميل لا يرد",
  "createdAt": "2026-05-02T12:00:00Z"
}
```

### Response تفاصيل القضية

```json
{
  "id": "guid",
  "orderId": "guid",
  "orderNumber": "ORD-10021",
  "type": "DriverReport",
  "status": "InReview",
  "priority": "Medium",
  "queue": "Operations",
  "reasonCode": "customer_unreachable",
  "message": "العميل لا يرد",
  "adminNote": "تمت المراجعة",
  "decisionNotes": null,
  "createdAt": "2026-05-02T12:00:00Z",
  "updatedAt": "2026-05-02T12:15:00Z",
  "closedAt": null,
  "attachments": [],
  "activities": []
}
```

## 9. قواعد مهمة للموبايل

### لا تفتح dispute جديدة لكل متابعة

لو القضية موجودة بالفعل:

- استخدم endpoint الرسائل
- لا تكرر فتح نزاع جديد بدون داع

### report-issue و dispute ليسا نفس الشيء

- `report-issue` للمشكلة التشغيلية أثناء التنفيذ
- `dispute` للاعتراض أو النزاع المرتبط بالطلب

### لا تعتمد على APIDog فقط

لأن مسارات المندوب الحالية موجودة في الـ backend لكن ليست مضافة بالكامل داخل `Zadana_APIDog_Folders.json`.

## 10. ملخص المسارات

- `POST /api/drivers/support/orders/{orderId}/report-issue`
- `POST /api/drivers/support/orders/{orderId}/dispute`
- `GET /api/drivers/support/cases`
- `GET /api/drivers/support/cases/{caseId}`
- `POST /api/drivers/support/orders/{orderId}/cases/{caseId}/messages`
