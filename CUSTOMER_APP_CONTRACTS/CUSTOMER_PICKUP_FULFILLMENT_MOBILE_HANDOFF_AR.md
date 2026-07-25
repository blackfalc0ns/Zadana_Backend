# استلام من الفرع (Pickup) — Handoff لتطبيق العميل

تاريخ التحديث: 2026-07-25  
الحالة: `implemented` في الباك إند (مرفوع على `main` + مطبق على السيرفر)  
الجمهور: مبرمج تطبيق العميل (Customer App)

## الخلاصة

المنصة تدعم الآن نوعين للطلب:

| النوع | القيمة |
|---|---|
| توصيل | `delivery` |
| استلام من الفرع | `pickup` |

للـ pickup:

- لا عنوان توصيل
- رسوم الشحن = `0`
- العميل يستلم كود OTP ويعطيه للتاجر عند الاستلام
- الدفع: بطاقة / Apple Pay دائمًا، وكاش عند الاستلام لو الأدمن فعّله
- الإلغاء قبل قبول التاجر فوري؛ بعده يحتاج موافقة التاجر

التفاصيل الكاملة موزعة في العقود الإنجليزية؛ هذا الملف هو دليل التنفيذ العملي.

## العقود المرتبطة (مصدر الحقيقة)

| الملف | متى ترجع له |
|---|---|
| `CHECKOUT_CONTRACT.md` | اختيار الاستلام + summary + config |
| `PAYMENT_CONTRACT.md` | دفع pickup + دفع فرق التحويل لتوصيل |
| `ORDER_DETAILS_CONTRACT.md` | تفاصيل الطلب + OTP |
| `ORDER_TRACKING_CONTRACT.md` | شاشة التتبع بدون سائق |
| `ORDER_CANCELLATION_CONTRACT.md` | إلغاء pickup بموافقة التاجر |
| `ORDER_REALTIME_SUBSCRIPTION_CONTRACT.md` | SignalR لتحديث OTP والحالة |
| `NOTIFICATIONS_CONTRACT.md` | أنواع إشعارات pickup |

Base URL إنتاج: `https://api.zadna0.com`  
Auth لمعظم الـ endpoints: `Authorization: Bearer <access_token>` + سياسة `CustomerOnly`

---

## 1) إعدادات المنصة (قبل شاشة الدفع)

```http
GET /api/checkout/config
```

- Auth: اختياري (`AllowAnonymous`)
- استدعِه عند دخول الـ checkout

```json
{
  "delivery_enabled": true,
  "pickup_enabled": true,
  "pickup_cash_on_pickup_enabled": false,
  "allowed_payments_for_pickup": ["card", "apple_pay"]
}
```

### قواعد الواجهة

1. لو `delivery_enabled = false` → اخفِ خيار التوصيل بالكامل.
2. لو `pickup_enabled = false` → اخفِ خيار الاستلام بالكامل.
3. طرق دفع الاستلام = فقط `allowed_payments_for_pickup`.
4. لو القائمة فيها `cash` → اعرض «الدفع عند الاستلام».
5. التحويل البنكي غير مدعوم للـ pickup.

---

## 2) Checkout Summary للاستلام

```http
GET /api/checkout/summary?vendor_id={vendorId}&fulfillment_type=pickup&vendor_branch_id={branchId}&payment_method=card
```

Aliases مقبولة: `fulfillmentType`, `vendorBranchId`, `vendorId`, `paymentMethod`

### ملاحظات

- `vendor_branch_id` **مطلوب** للـ pickup
- `address_id` **غير مطلوب**
- `delivery_slot_id` يُتجاهل
- `summary.shipping_cost` = `0`
- `delivery_check.status` المتوقع عند جاهزية الفرع: `pickup_ready`
- لو الفرع ناقص: `pickup_branch_required`

مثال مختصر لاستجابة pickup:

```json
{
  "fulfillment_type": "pickup",
  "pickup_branch": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "Mohandessin Branch",
    "address_line": "12 Lebanon Sq",
    "city": "Giza"
  },
  "delivery_check": {
    "status": "pickup_ready",
    "is_deliverable": true,
    "can_proceed_to_checkout": true,
    "delivery_fee": 0.0
  },
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 0.0,
    "total": 105.0
  }
}
```

> اختيار الفرع: أرسل `vendor_branch_id` من فرع التاجر النشط. الـ summary/order يعيدان بيانات الفرع للعرض (عنوان + ساعات)، بدون خريطة/هاتف للعميل في شاشة الاستلام.

---

## 3) إنشاء الطلب

```http
POST /api/orders
```

Body (pickup + بطاقة):

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "fulfillment_type": "pickup",
  "vendor_branch_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "payment_method": "card",
  "promo_code": null,
  "notes": null
}
```

Body (pickup + كاش — فقط لو مفعّل من الأدمن):

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "fulfillment_type": "pickup",
  "vendor_branch_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "payment_method": "cash",
  "promo_code": null,
  "notes": null
}
```

### سلوك الدفع

| الطريقة | السلوك في التطبيق |
|---|---|
| `card` / `apple_pay` | نفس تدفق Moyasar الحالي (`payment.provider_config`) ثم confirm |
| `cash` | لا جلسة بوابة؛ الطلب يكمل والدفع عند الاستلام للتاجر |

أكواد أخطاء مهمة:

| Code | المعنى |
|---|---|
| `PICKUP_DISABLED_BY_ADMIN` | الاستلام مقفول من الأدمن |
| `PICKUP_BRANCH_REQUIRED` | لازم تختار فرع |
| `PICKUP_BRANCH_INVALID` | الفرع غير صالح لهذا التاجر |
| `PICKUP_CASH_DISABLED` | الكاش غير مفعّل |
| `PICKUP_ONLY_ONLINE_PAYMENT` | طريقة دفع غير مدعومة للـ pickup |

---

## 4) دورة حالات طلب الاستلام (UI)

حالات شائعة:

```text
pending_payment / placed / pending_vendor_acceptance
        ↓
accepted → preparing → ready_for_pickup
        ↓
delivered   (بعد ما التاجر يتحقق من OTP)
```

حالات خاصة:

| الحالة | ماذا تعرض |
|---|---|
| `ready_for_pickup` | فرع + OTP + عدّاد مهلة الاستلام |
| `cancellation_requested` | بانر «بانتظار موافقة التاجر على الإلغاء» (الطلب لسه نشط) |
| `cancelled` | ملغي (شامل انتهاء مهلة عدم الحضور) |

لا تعرض لطلبات pickup:

- خريطة السائق
- موقع المندوب
- OTP التوصيل (`delivery_otp`)
- ETA التوصيل التقليدي (غالبًا `estimated_delivery = null`)

اعتمد على `fulfillment_type` من الـ API، لا تفترض من الحالة وحدها.

---

## 5) تفاصيل الطلب + OTP

```http
GET /api/orders/{orderId}
```

حقول pickup المهمة:

```json
{
  "fulfillment_type": "pickup",
  "status": "ready_for_pickup",
  "pickup_otp_code": "4821",
  "pickup_otp_expires_at_utc": "2026-07-25T18:00:00Z",
  "pickup_no_show_deadline_utc": "2026-07-25T22:00:00Z",
  "pickup_branch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
  }
}
```

### قواعد عرض OTP

اعرض كود الاستلام فقط لو:

- `fulfillment_type = "pickup"`
- و `pickup_otp_code != null`

بعد التحقق عند التاجر أو اكتمال الطلب، الحقول ترجع `null` — اخفِ البطاقة.

### إعادة إرسال OTP

```http
POST /api/orders/{orderId}/resend-pickup-otp
```

Response مثال:

```json
{
  "order_id": "44444444-4444-4444-4444-444444444444",
  "expires_at_utc": "2026-07-25T18:10:00Z",
  "message": "..."
}
```

بعدها حدّث `GET /api/orders/{orderId}` لجلب الكود الجديد.

أخطاء محتملة:

- `PICKUP_OTP_RESEND_RATE_LIMIT`
- `PICKUP_OTP_LOCKED`
- `PICKUP_OTP_ALREADY_VERIFIED`

---

## 6) التتبع Tracking

```http
GET /api/orders/{orderId}/tracking
```

لنفس طلب pickup ستجد:

- `fulfillment_type: "pickup"`
- `pickup_otp_*`
- `pickup_branch`
- `show_delivery_otp: false`
- بدون تركيز على السائق

شاشة التتبع للـ pickup = **فرع + OTP + مهلة الاستلام + تايملاين الحالات**.

---

## 7) الإلغاء

```http
GET /api/orders/cancellation-reasons
POST /api/orders/{orderId}/cancel
```

Body:

```json
{
  "reason_code": "changed_my_mind",
  "reason": null,
  "note": null
}
```

### سلوك pickup

| مرحلة الطلب | النتيجة |
|---|---|
| قبل قبول التاجر (`pending_payment` / `placed` / `pending_vendor_acceptance`) | إلغاء فوري → `cancelled` |
| بعد القبول (`accepted` / `preparing` / `ready_for_pickup`) | طلب موافقة → `cancellation_requested` |

اعتمد على `can_cancel` من تفاصيل الطلب.

لو رجع `cancellation_requested`:

- لا تشيل الطلب من القائمة النشطة فورًا
- اعرض بانر انتظار موافقة التاجر
- حدّث التفاصيل بعد القرار

---

## 8) الإشعارات (Push / Inbox)

| `type` | المعنى | Action |
|---|---|---|
| `pickup_ready` | جاهز للاستلام | افتح تفاصيل الطلب (OTP + الفرع) |
| `pickup_reminder` | تذكير بمهلة الاستلام | افتح التفاصيل |
| `pickup_deadline_extended` | تم تمديد المهلة (فرع كان مغلق) | حدّث العدّاد |
| `pickup_expired` | انتهت المهلة واتلغى | اعرض ملغي |
| `pickup_otp_regenerated` | اتبعت OTP جديد | حدّث التفاصيل |

ملاحظات:

- كود OTP **لا يُرسل** داخل نص الإشعار؛ اجلبه من API التفاصيل
- deep link عبر `referenceId` = `orderId`

---

## 9) Realtime (SignalR)

عند حدث `ready_for_pickup` على قناة العميل قد يصل:

- `fulfillmentType: "Pickup"`
- `pickupOtpCode`
- `pickupOtpExpiresAtUtc`
- `pickupNoShowDeadlineUtc`
- `pickupBranch`

قواعد:

- OTP السري يوصل على قناة المستخدم المصادَق فقط
- بث مجموعة التتبع لا يتضمن OTP
- تجاهل أحداث وصول السائق لطلبات pickup
- بعد التحقق أو التحويل لتوصيل، الأحداث التالية لا تحمل OTP

التفاصيل: `ORDER_REALTIME_SUBSCRIPTION_CONTRACT.md`

---

## 10) تحويل الاستلام → توصيل (مهم للعميل)

التاجر/الأدمن قد يحوّل طلب pickup إلى delivery.

لو فيه فرق رسوم توصيل، الباك ينشئ جلسة دفع Moyasar إضافية:

```json
{
  "metadata": {
    "kind": "delivery_upgrade",
    "originalOrderId": "...",
    "order_id": "...",
    "payment_id": "..."
  }
}
```

قواعد الموبايل:

1. اعتبرها **دفع فرق توصيل** وليس طلب جديد.
2. نفس UI Moyasar الحالي ثم `POST /api/payments/moyasar/confirm`.
3. بعد النجاح: حدّث details/tracking.
4. لا تعتبر الطلب صار delivery محليًا إلا لما الـ API يرجع `fulfillment_type = "delivery"`.

---

## 11) Checklist تنفيذ سريع

### Checkout
- [ ] جلب `/api/checkout/config` وإظهار/إخفاء delivery/pickup
- [ ] اختيار فرع + إرسال `fulfillment_type=pickup` + `vendor_branch_id`
- [ ] إخفاء عنوان التوصيل ومواعيد السlot في وضع pickup
- [ ] عرض `shipping_cost = 0`
- [ ] طرق الدفع من `allowed_payments_for_pickup` فقط
- [ ] دعم كاش عند الاستلام عند تفعيله

### بعد الطلب
- [ ] شاشة pickup details بدون سائق/خريطة
- [ ] بطاقة OTP عند `pickup_otp_code != null`
- [ ] عدّاد/نص لمهلة `pickup_no_show_deadline_utc`
- [ ] زر إعادة إرسال OTP
- [ ] إشعارات `pickup_*` deep link للتفاصيل
- [ ] SignalR يحدّث OTP/الحالة
- [ ] إلغاء: فوري أو `cancellation_requested`
- [ ] دعم دفع `delivery_upgrade` لو اتحول لتوصيل

### لا تعمل
- [ ] لا تحسب الشحن على الجهاز
- [ ] لا تفترض OTP من الحالة فقط
- [ ] لا تعرض رقم/خريطة الفرع لو مش راجعين من API (العرض = عنوان + ساعات فقط)
- [ ] لا تستخدم Paymob / روابط قديمة

---

## 12) أمثلة أخطاء للترجمة/الـ mapping

| Code | استخدام مقترح |
|---|---|
| `PICKUP_DISABLED_BY_ADMIN` | الاستلام غير متاح حاليًا |
| `PICKUP_BRANCH_REQUIRED` | اختر فرع الاستلام |
| `PICKUP_BRANCH_INVALID` | الفرع غير متاح |
| `PICKUP_CASH_DISABLED` | الدفع عند الاستلام غير مفعّل |
| `PICKUP_ONLY_ONLINE_PAYMENT` | اختر بطاقة أو Apple Pay |
| `PICKUP_OTP_RESEND_RATE_LIMIT` | انتظر قبل إعادة الإرسال |
| `PICKUP_OTP_LOCKED` | الكود مقفول مؤقتًا بسبب محاولات خاطئة |
| `PICKUP_CANCEL_NEEDS_VENDOR_APPROVAL` | الإلغاء يحتاج موافقة التاجر |
| `CANCELLATION_REQUEST_ALREADY_PENDING` | طلب إلغاء قيد الانتظار |

النصوص النهائية localized ترجع من API/`SharedResource`.

---

## 13) ترتيب مقترح للشاشات

```text
Cart
  → Checkout Config
  → اختيار Delivery / Pickup
  → (Pickup) اختيار الفرع
  → Summary
  → Place Order (+ Moyasar لو بطاقة)
  → Order Details / Tracking
       ├─ OTP card عند ready_for_pickup
       ├─ Resend OTP
       └─ Cancel (فوري أو pending approval)
```

---

## حالة السيرفر

- Migrations: `AddPickupFulfillment` + `AddPickupCashOnPickup` مطبّقة
- API commit المرجعي بعد إصلاح التشغيل: ابحث في Backend `main` عن pickup + `AdminPlatformPickupSettings` access mapping
- Production health: `https://api.zadna0.com/health`

لو حابب تفاصيل JSON كاملة أو edge cases، ارجع للعقود في الجدول أعلى الملف.
