# دليل ربط دعم المندوب في تطبيق الموبايل

هذا الملف يوضح ربط شاشة دعم المندوب في تطبيق الموبايل مع الـ backend الحالي.

الكنترولرات المسؤولة:

- `src/Zadana.Api/Modules/Delivery/Controllers/DriverSupportController.cs`
- `src/Zadana.Api/Modules/Delivery/Controllers/DriverAccountSupportController.cs`

## 1. الفكرة العامة

دعم المندوب مقسوم إلى 3 مسارات:

1. بلاغ تشغيلي على طلب مسند للمندوب.
2. نزاع مالي/تشغيلي على طلب مسند للمندوب.
3. دعم حساب المندوب عند الحظر أو الإيقاف أو قفل تسجيل الدخول أو المراجعة.

كل الردود المهمة ترجع عربي وإنجليزي معًا، مثل:

- `type_label_ar` / `type_label_en`
- `status_label_ar` / `status_label_en`
- `priority_label_ar` / `priority_label_en`
- `reason_label_ar` / `reason_label_en`

على تطبيق الموبايل اختيار الحقل المناسب حسب لغة التطبيق.

## 2. المصادقة

المسارات الأساسية تحتاج:

```http
Authorization: Bearer <driver_token>
```

الاستثناء الوحيد:

```http
POST /api/drivers/account-support/appeals
```

هذا المسار يعمل بدون تسجيل دخول، ويستخدم فقط عند عدم قدرة المندوب على الدخول بسبب قفل الحساب أو عدم وجود token صالح.

## 3. فحص حالة حساب المندوب

استخدم هذا المسار بعد تسجيل الدخول، وعند فتح الصفحة الرئيسية، وقبل تفعيل زر "متاح":

```http
GET /api/drivers/me/status
```

أهم الحقول:

```json
{
  "driverId": "guid",
  "gateStatus": "Operational",
  "isOperational": true,
  "canReceiveOrders": true,
  "canGoAvailable": true,
  "verificationStatus": "Approved",
  "accountStatus": "Active",
  "messageAr": "تم اعتماد المندوب ويمكنه استقبال الطلبات.",
  "messageEn": "Driver is approved and can receive orders.",
  "isLoginLocked": false,
  "lockedAtUtc": null,
  "lockReason": null,
  "allowedCapabilities": {
    "canAccessSupport": true,
    "canEditProfile": true,
    "canAccessWallet": true,
    "canReceiveOffers": true
  },
  "supportCta": {
    "endpoint": "/api/drivers/support/account-appeals",
    "reasonType": "other",
    "labelAr": "تواصل مع دعم حساب المندوب",
    "labelEn": "Contact driver account support"
  }
}
```

قيم `gateStatus` المهمة للموبايل:

- `Operational`
- `LoginLocked`
- `NeedsDocuments`
- `UnderReview`
- `Rejected`
- `ExpiredDocuments`
- `Suspended`
- `Banned`
- `PendingActivation`
- `Inactive`
- `Unavailable`

اقتراح UI:

- لو `canGoAvailable = false` اعرض كارت حالة الحساب بدل زر التوفر.
- لو `allowedCapabilities.canAccessSupport = true` اعرض زر الدعم.
- استخدم `supportCta.endpoint` و `supportCta.reasonType` لفتح نموذج دعم الحساب مباشرة.
- لو `isLoginLocked = true` غالبًا لن يملك التطبيق token صالح بعد إعادة فتحه؛ استخدم مسار الدعم العام بدون تسجيل دخول.

## 4. جلب أسباب الدعم

هذا المسار لا يحتاج تسجيل دخول:

```http
GET /api/drivers/support/reasons/{type}
```

القيم المدعومة لـ `type`:

- `driver_report`
- `driver_dispute`
- `driver_account`

مثال:

```http
GET /api/drivers/support/reasons/driver_account
```

رد مختصر:

```json
[
  {
    "code": "account_banned",
    "label_ar": "الحساب محظور",
    "label_en": "Account banned",
    "requires_note": true
  }
]
```

أسباب `driver_account` الحالية:

- `account_banned`
- `account_suspended`
- `login_locked`
- `under_review`
- `documents_required`
- `other`

أسباب `driver_report` الحالية:

- `wrong_address`
- `customer_unavailable`
- `damaged_package`
- `vehicle_issue`
- `other`

أسباب `driver_dispute` الحالية:

- `payout_dispute`
- `incorrect_deduction`
- `other`

## 5. إنشاء بلاغ تشغيلي على طلب

يستخدم عند وجود مشكلة أثناء تنفيذ طلب مسند للمندوب.

```http
POST /api/drivers/support/orders/{orderId}/report-issue
Authorization: Bearer <driver_token>
Content-Type: application/json
```

Body:

```json
{
  "reason_code": "customer_unavailable",
  "message": "العميل لا يرد على الهاتف منذ 10 دقائق.",
  "attachments": [
    {
      "file_name": "proof.jpg",
      "file_url": "https://cdn.example.com/proof.jpg"
    }
  ]
}
```

ملاحظات:

- `message` مطلوب.
- `attachments` اختيارية.
- سيقبل الـ API الطلب فقط لو الطلب مسند للمندوب الحالي.
- في حالة عدم ارتباط الطلب بالمندوب يرجع خطأ `NOT_ASSIGNED_TO_ORDER`.

## 6. إنشاء نزاع مالي على طلب

يستخدم لمشكلة مستحقات أو خصم غير صحيح مرتبط بطلب.

```http
POST /api/drivers/support/orders/{orderId}/dispute
Authorization: Bearer <driver_token>
Content-Type: application/json
```

Body:

```json
{
  "reason_code": "payout_dispute",
  "message": "يوجد خصم غير صحيح على هذا الطلب."
}
```

ملاحظات:

- `message` مطلوب.
- لو لم يتم إرسال `reason_code` سيستخدم النظام `payout_dispute`.
- هذا المسار لا يستقبل `attachments` حاليًا.

## 7. إنشاء طلب دعم حساب للمندوب وهو مسجل دخول

يستخدم عند وجود token صالح، مثل حالة `UnderReview` أو `Suspended` أو `Banned` مع بقاء الجلسة صالحة.

```http
POST /api/drivers/support/account-appeals
Authorization: Bearer <driver_token>
Content-Type: application/json
```

Body:

```json
{
  "reason_code": "account_suspended",
  "message": "أحتاج مراجعة سبب إيقاف الحساب.",
  "attachments": [
    {
      "file_name": "document.jpg",
      "file_url": "https://cdn.example.com/document.jpg"
    }
  ]
}
```

ملاحظات:

- لا ترسل `identifier` في هذا المسار؛ النظام يعرف المندوب من الـ token.
- `message` مطلوب.
- `attachments` اختيارية.

## 8. إنشاء طلب دعم حساب بدون تسجيل دخول

يستخدم في شاشة قبل تسجيل الدخول أو عند فشل الدخول بسبب قفل الحساب.

```http
POST /api/drivers/account-support/appeals
Content-Type: application/json
```

Body:

```json
{
  "identifier": "+966500000000",
  "reason_code": "login_locked",
  "message": "لا أستطيع تسجيل الدخول إلى حسابي.",
  "attachments": []
}
```

ملاحظات مهمة:

- `identifier` مطلوب، ويكون رقم الهاتف أو البريد.
- `message` مطلوب.
- الرد دائمًا لا يكشف هل الحساب موجود أم لا لأسباب أمنية.

رد متوقع:

```json
{
  "message": "If the driver account exists, the support request has been received.",
  "message_ar": "إذا كان حساب المندوب موجودًا، فقد تم استلام طلب الدعم.",
  "message_en": "If the driver account exists, the support request has been received."
}
```

## 9. جلب قضايا الطلبات الخاصة بالمندوب

يعرض البلاغات والنزاعات المرتبطة بطلبات المندوب.

```http
GET /api/drivers/support/cases?page=1&pageSize=20
Authorization: Bearer <driver_token>
```

رد مختصر:

```json
{
  "items": [
    {
      "id": "case-guid",
      "order_id": "order-guid",
      "order_number": "ORD-1001",
      "type": "driver_report",
      "type_label_ar": "بلاغ تشغيلي",
      "type_label_en": "Operational report",
      "status": "submitted",
      "status_label_ar": "تم الاستلام",
      "status_label_en": "Submitted",
      "priority": "medium",
      "reason_code": "customer_unavailable",
      "reason_label_ar": "العميل غير متاح",
      "reason_label_en": "Customer unavailable",
      "message": "العميل لا يرد.",
      "admin_note": null,
      "created_at": "2026-05-21T10:00:00Z",
      "updated_at": "2026-05-21T10:00:00Z",
      "closed_at": null
    }
  ],
  "page": 1,
  "page_size": 20,
  "total": 1
}
```

## 10. جلب قضايا دعم الحساب

يعرض طلبات دعم حساب المندوب مثل الحظر، الإيقاف، قفل الدخول، والمراجعة.

```http
GET /api/drivers/support/account-cases?page=1&pageSize=20
Authorization: Bearer <driver_token>
```

اقتراح للموبايل:

- اعرض شاشة منفصلة باسم "دعم الحساب".
- أو ادمج نتيجة هذا المسار مع `/api/drivers/support/cases` في تبويب واحد باسم "كل طلبات الدعم".
- عند الدمج، رتب العناصر حسب `created_at` أو `updated_at`.

## 11. تفاصيل قضية مرتبطة بطلب

```http
GET /api/drivers/support/cases/{caseId}
Authorization: Bearer <driver_token>
```

أهم الحقول الإضافية:

```json
{
  "queue": "driver_ops",
  "queue_label_ar": "عمليات المندوبين",
  "queue_label_en": "Driver operations",
  "admin_note": "تمت المراجعة.",
  "decision_notes": null,
  "attachments": [
    {
      "file_name": "proof.jpg",
      "file_url": "https://cdn.example.com/proof.jpg"
    }
  ],
  "activities": [
    {
      "action": "submitted",
      "action_label_ar": "تم فتح القضية",
      "action_label_en": "Case opened",
      "title_ar": "تم إنشاء بلاغ من المندوب",
      "title_en": "Driver created a new report",
      "note": null,
      "actor_role": "driver",
      "actor_role_label_ar": "المندوب",
      "actor_role_label_en": "Driver",
      "created_at": "2026-05-21T10:00:00Z"
    }
  ]
}
```

## 12. تفاصيل قضية دعم حساب

```http
GET /api/drivers/support/account-cases/{caseId}
Authorization: Bearer <driver_token>
```

نفس شكل تفاصيل قضية الطلب تقريبًا، لكن:

- `order_id` = `null`
- `order_number` = `null`
- `type` = `driver_account`

## 13. إرسال رسالة داخل قضية مرتبطة بطلب

```http
POST /api/drivers/support/orders/{orderId}/cases/{caseId}/messages
Authorization: Bearer <driver_token>
Content-Type: application/json
```

Body:

```json
{
  "message": "تم إرفاق صورة إضافية للتوضيح.",
  "attachments": [
    {
      "file_name": "extra.jpg",
      "file_url": "https://cdn.example.com/extra.jpg"
    }
  ]
}
```

## 14. إرسال رسالة داخل قضية دعم حساب

```http
POST /api/drivers/support/account-cases/{caseId}/messages
Authorization: Bearer <driver_token>
Content-Type: application/json
```

Body:

```json
{
  "message": "تم تحديث المستندات، برجاء إعادة المراجعة.",
  "attachments": []
}
```

## 15. القيم الثابتة التي يتعامل معها الموبايل

أنواع القضايا:

- `driver_report`
- `driver_dispute`
- `driver_account`

الحالات:

- `submitted`
- `in_review`
- `awaiting_customer_evidence`
- `approved`
- `rejected`
- `resolved`

الأولويات:

- `low`
- `medium`
- `high`
- `critical`

الأدوار داخل الـ activities:

- `driver`
- `admin`
- `vendor`
- `customer`
- `system`

## 16. ربط الإشعارات والـ deep links

عند وصول إشعار دعم، افتح شاشة التفاصيل حسب نوع القضية:

- لو `type = driver_account`: افتح `/api/drivers/support/account-cases/{caseId}`.
- لو `type = driver_report` أو `driver_dispute`: افتح `/api/drivers/support/cases/{caseId}`.

قد تصل بيانات الإشعار بهذه المفاتيح:

```json
{
  "caseId": "case-guid",
  "orderId": "order-guid",
  "type": "driver_account",
  "action": "request_evidence",
  "targetUrl": "/disputes?caseId=case-guid"
}
```

للموبايل تجاهل `targetUrl` لو كان مخصصًا للأدمن، والاعتماد على `caseId` و `type`.

## 17. سلوك UI المقترح

### شاشة الدعم الرئيسية

- كارت "بلاغ على طلب".
- كارت "نزاع مالي".
- كارت "دعم الحساب".
- قائمة آخر طلبات الدعم.

### من داخل تفاصيل الطلب

- زر "الإبلاغ عن مشكلة" يستخدم `report-issue`.
- زر "نزاع مالي" يستخدم `dispute`.
- بعد الإرسال افتح تفاصيل القضية أو اعرض toast نجاح.

### من شاشة حالة الحساب

- لو `gateStatus` من الحالات غير التشغيلية اعرض رسالة `messageAr/messageEn`.
- زر الدعم يستخدم `supportCta`.
- اختار `reason_code` تلقائيًا من `supportCta.reasonType`، واترك للمندوب تعديله لو مطلوب.

### من شاشة تسجيل الدخول عند القفل

- اعرض رابط "لا أستطيع الدخول؟".
- افتح نموذج فيه `identifier` وسبب `login_locked`.
- أرسل على `/api/drivers/account-support/appeals`.

## 18. أخطاء متوقعة

```json
{
  "code": "INVALID_REQUEST_BODY",
  "message": "Message is required."
}
```

```json
{
  "code": "NOT_ASSIGNED_TO_ORDER",
  "message": "You can only report issues for orders assigned to you."
}
```

تعامل الموبايل:

- اعرض `message` لو لا يوجد `message_ar/message_en`.
- في أخطاء `401` افتح تسجيل الدخول أو شاشة الدعم العام لو المشكلة قفل حساب.
- في `NOT_ASSIGNED_TO_ORDER` اعرض رسالة أن الطلب غير مرتبط بالمندوب الحالي.

## 19. ملاحظات تنفيذية للموبايل

- استخدم مفاتيح JSON كما هي في هذا الملف: `reason_code`, `file_name`, `file_url`.
- لا ترسل ملفات خام داخل هذه endpoints؛ ارفع الملف أولًا وخزن الرابط في `file_url`.
- لا تعتمد على نصوص ثابتة داخل التطبيق لو الرد يحتوي `*_label_ar` و `*_label_en`.
- حالات الحساب لا تحتاج orderId.
- قضايا الطلبات تحتاج orderId عند الإنشاء وإرسال رسالة.
- `pageSize` له حد أقصى 50.

