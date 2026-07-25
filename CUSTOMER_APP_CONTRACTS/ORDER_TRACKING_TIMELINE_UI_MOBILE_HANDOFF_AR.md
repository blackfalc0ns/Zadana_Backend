# تايملاين تتبع الطلب — دليل تصميم UI للموبايل

تاريخ التحديث: 2026-07-25  
الحالة: مطبّق في الباك إند  
الجمهور: مبرمج تطبيق العميل فقط  
النطاق: **تصميم وعرض `timeline` في شاشة التتبع** (توصيل + استلام)

Endpoint:

```http
GET /api/orders/{orderId}/tracking
```

هذا الملف مستقل: يغطي جزء التايملاين البصري فقط.

---

## 1) القاعدة الذهبية

الـ API يرجع في أي لحظة:

- **خطوة واحدة فقط** (أو صفر في حالات نادرة) فيها `is_active: true`
- باقي الخطوات إما مكتملة أو قادمة

**ممنوع** تعمل highlight على كل الخطوات اللي حالتها:

```text
is_active: false
is_completed: false
```

دي خطوات **قادمة** وبس — شكل باهت بدون حركة.

---

## 2) الحالات الثلاث

كل عنصر في `timeline[]`:

```json
{
  "id": "vendor_confirmed",
  "title": "أكد المتجر الطلب",
  "time": "",
  "is_active": true,
  "is_completed": false
}
```

| الحالة | الشرط | المعنى | التصميم |
|---|---|---|---|
| مكتملة | `is_completed == true` و `is_active == false` | المرحلة خلصت | أيقونة ✓، لون هادئ (رمادي/teal خفيف)، بدون pulse |
| الحالية | `is_active == true` | المرحلة الجارية الآن | **التركيز البصري هنا فقط**: لون أساسي أقوى، نقطة أكبر، عنوان أسمك، pulse خفيف اختياري |
| قادمة | `is_active == false` و `is_completed == false` | لسه مجتش | باهتة، أيقونة فارغة/outline، بدون حركة، بدون لون قوي |

> لو خطوة terminal منتهية (مثل `delivered`) ممكن تكون `is_active: true` و `is_completed: true` معًا — اعتبرها **حالية منتهية** (آخر خطوة ناجحة) وطبّق ستايل completed + تمييز بسيط إنها النهاية.

---

## 3) مثال من الـ API (توصيل — بانتظار قبول المتجر)

```json
{
  "order": {
    "id": "b4e3e1b2-971f-4bdf-ab13-0a3d446eb3ae",
    "order_number": "ORD-20260725-7ED99E2B",
    "status": "pending"
  },
  "fulfillment_type": "delivery",
  "timeline": [
    { "id": "order_placed", "title": "تم إنشاء الطلب", "time": "10:54 PM", "is_active": false, "is_completed": true },
    { "id": "vendor_confirmed", "title": "أكد المتجر الطلب", "time": "", "is_active": true, "is_completed": false },
    { "id": "preparing", "title": "جاري تجهيز الطلب", "time": "", "is_active": false, "is_completed": false },
    { "id": "out_for_delivery", "title": "في الطريق إليك", "time": "", "is_active": false, "is_completed": false },
    { "id": "delivered", "title": "تم التسليم", "time": "", "is_active": false, "is_completed": false }
  ]
}
```

تفسير UI:

| id | الحالة البصرية |
|---|---|
| `order_placed` | مكتملة ✓ |
| `vendor_confirmed` | **الحالية** ← اعمل الـ design عليها |
| `preparing` | قادمة باهتة |
| `out_for_delivery` | قادمة باهتة |
| `delivered` | قادمة باهتة |

---

## 4) خطوات التايملاين حسب نوع التنفيذ

### توصيل — `fulfillment_type = "delivery"`

| id | المعنى |
|---|---|
| `order_placed` | تم إنشاء الطلب |
| `vendor_confirmed` | بانتظار/تأكيد المتجر |
| `preparing` | جاري التجهيز / جاهز ويبحث عن مندوب |
| `out_for_delivery` | المندوب في الطريق |
| `delivered` أو `cancelled` / `returning` | النهاية |

### استلام من الفرع — `fulfillment_type = "pickup"`

| id | المعنى |
|---|---|
| `order_placed` | تم إنشاء الطلب |
| `vendor_confirmed` | بانتظار/تأكيد المتجر |
| `preparing` | جاري التجهيز |
| `ready_for_pickup` | جاهز للاستلام من الفرع |
| `delivered` أو `cancelled` | تم الاستلام / إلغاء |

لا تستخدم `out_for_delivery` في مسار الاستلام.

---

## 5) كود مقترح (Flutter)

```dart
enum TimelineVisualState { completed, current, pending }

TimelineVisualState resolveTimelineState({
  required bool isActive,
  required bool isCompleted,
}) {
  if (isActive) return TimelineVisualState.current;
  if (isCompleted) return TimelineVisualState.completed;
  return TimelineVisualState.pending;
}

Widget buildTimeline(List<TrackingStep> timeline) {
  final activeCount = timeline.where((s) => s.isActive).length;
  assert(activeCount <= 1, 'API must return at most one active step');

  return Column(
    children: [
      for (final step in timeline)
        switch (resolveTimelineState(
          isActive: step.isActive,
          isCompleted: step.isCompleted,
        )) {
          TimelineVisualState.current => CurrentStepTile(step),   // design هنا فقط
          TimelineVisualState.completed => CompletedStepTile(step),
          TimelineVisualState.pending => PendingStepTile(step),
        },
    ],
  );
}
```

### إرشادات بصرية سريعة

- **Current:** لون البراند الأساسي، stroke أثقل، `time` لو فاضي اعرض نص مثل «الآن» أو اخفيه
- **Completed:** ✓ + `time` من الـ API لو موجود
- **Pending:** opacity منخفضة (~0.45–0.55)، بدون shadow/pulse
- الخط الواصل بين الخطوات: مكتمل→مكتمل بلون قوي، وإلا رمادي فاتح

---

## 6) ماذا لا تفعل

1. لا تحسب المرحلة الحالية من `order.status` لوحدك وتتجاهل `is_active`.
2. لا تظبط كل الخطوات القادمة بنفس ستايل الحالية.
3. لا تعمل أكثر من pulse واحد في نفس الوقت.
4. لا تفترض إن `is_active` و `is_completed` متناقضان دائمًا — الخطوة النهائية ممكن تكون الاتنين `true`.

---

## 7) Checklist

- [ ] اقرأ `timeline` من `GET /api/orders/{id}/tracking` كما هو
- [ ] فرّق UI بثلاث حالات: completed / current / pending
- [ ] الـ design القوي على `is_active == true` فقط
- [ ] الخطوات `false/false` باهتة بدون حركة
- [ ] مسار pickup يستخدم `ready_for_pickup` مش `out_for_delivery`
- [ ] بعد تحديث الحالة (SignalR / refresh) أعد رسم التايملاين من الـ response الجديد

---

## 8) ملفات مرتبطة (اختياري)

| الملف | متى ترجع له |
|---|---|
| `ORDER_TRACKING_CONTRACT.md` | العقد الكامل للتتبع |
| `PICKUP_ORDER_TRACKING_MOBILE_HANDOFF_AR.md` | مسار الاستلام من الفرع |
| `CUSTOMER_PICKUP_FULFILLMENT_MOBILE_HANDOFF_AR.md` | ميزة الاستلام كاملة |
