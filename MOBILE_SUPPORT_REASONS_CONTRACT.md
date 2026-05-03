# دليل تكامل أسباب النزاعات والدعم للموبايل (Mobile Support & Dispute Reasons Contract)

تم إضافة Endpoints جديدة لجلب قائمة أسباب النزاعات والشكاوى (Reasons) بشكل ديناميكي من الخادم، لكي يتم عرضها للمستخدم (سواء كان العميل أو المندوب) في واجهة التطبيق عند رفع شكوى أو نزاع.

---

## 1. تطبيق المندوب (Driver App)

### جلب أسباب النزاعات والشكاوى
- **Endpoint**: `GET /api/drivers/support/reasons/{type}`
- **Path Variable**: `{type}`
  - استخدم `report` عند الإبلاغ عن مشكلة في الطلب (مثل: عنوان خاطئ، العميل غير متوفر).
  - استخدم `dispute` عند رفع نزاع مالي (مثل: خصم غير صحيح، نزاع على المستحقات).

**شكل الاستجابة (Response):**
```json
[
  {
    "code": "payout_dispute",
    "labelAr": "نزاع مالي",
    "labelEn": "Payout dispute",
    "requiresNote": false
  },
  {
    "code": "other",
    "labelAr": "أخرى",
    "labelEn": "Other",
    "requiresNote": true
  }
]
```

**ملاحظات للمبرمج:**
- استخدم الـ `code` لإرساله في الـ Request Body (حقل `reason_code`) عند إنشاء الشكوى/النزاع.
- استخدم `labelAr` أو `labelEn` للعرض في الـ Dropdown بناءً على لغة التطبيق.
- إذا كان حقل `requiresNote` يساوي `true`، يجب إجبار المندوب على كتابة ملاحظة/رسالة (Message) وتوضيح السبب.

---

## 2. تطبيق العميل (Customer App)

### جلب أسباب الشكاوى والترجيع
- **Endpoint**: `GET /api/orders/support-reasons/{type}`
- **Path Variable**: `{type}`
  - استخدم `complaint` عند رفع شكوى عامة على الطلب (مثل: تأخر التوصيل، المندوب سيء).
  - استخدم `return` عند طلب إرجاع منتجات (مثل: منتج معيب، مقاس خاطئ).

**شكل الاستجابة (Response):**
```json
[
  {
    "code": "late_delivery",
    "labelAr": "تأخر التوصيل",
    "labelEn": "Late delivery",
    "requiresNote": false
  },
  {
    "code": "poor_quality",
    "labelAr": "جودة سيئة",
    "labelEn": "Poor quality",
    "requiresNote": true
  }
]
```

**ملاحظات للمبرمج:**
- يتم إرسال الـ `code` في حقل `reason_code` في الـ Request الخاص بإنشاء Support Case للعميل.
- يتم عرض `labelAr` أو `labelEn` في القائمة المنسدلة.
- يجب التحقق من حقل `requiresNote` لإظهار حقل إدخال النص (Text Area) وجعله إجبارياً في حالة `true`.
