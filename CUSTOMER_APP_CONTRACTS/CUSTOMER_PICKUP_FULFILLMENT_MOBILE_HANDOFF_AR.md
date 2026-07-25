# استلام من الفرع (Pickup) — دليل تنفيذ كامل لتطبيق العميل

تاريخ التحديث: 2026-07-25  
الحالة: مطبّق في الباك إند ومرفوع على الإنتاج  
الجمهور: مبرمج تطبيق العميل فقط  
ملاحظة: هذا الملف **مستقل بالكامل** — لا تحتاج الرجوع لأي ملف عقد آخر لتنفيذ الميزة.

---

## 0) ما الذي تغيّر؟

المنصة أصبحت تدعم نوعين لتنفيذ الطلب:

| النوع | القيمة في الـ API | المعنى |
|---|---|---|
| توصيل للعنوان | `delivery` | السلوك القديم (مندوب + عنوان + رسوم توصيل) |
| استلام من الفرع | `pickup` | العميل يستلم من فرع التاجر بنفسه |

### قواعد عامة للـ pickup

1. لا تحتاج `address_id` ولا `delivery_slot_id`.
2. لازم تختار `vendor_branch_id` (فرع الاستلام).
3. `summary.shipping_cost` دائمًا `0`.
4. العميل يشوف **عنوان الفرع + ساعات اليوم فقط** (بدون خريطة/هاتف في عقد الاستلام).
5. عند جاهزية الطلب (`ready_for_pickup`) يظهر **كود OTP**؛ العميل يعطيه للتاجر.
6. الدفع المدعوم: `card` و `apple_pay` دائمًا، و`cash` فقط لو الأدمن فعّل الدفع عند الاستلام.
7. التحويل البنكي **غير مدعوم** للـ pickup.
8. لا سائق ولا خريطة ولا OTP توصيل في شاشات الـ pickup.
9. لو التاجر/الأدمن حوّل الطلب لتوصيل وفيه فرق رسوم، العميل يدفع فرق Moyasar (`delivery_upgrade`).

### Base URL

- إنتاج: `https://api.zadna0.com`
- كل المسارات نسبية لـ `API_BASE_URL`

### Auth

| Endpoint | Auth |
|---|---|
| `GET /api/checkout/config` | اختياري (AllowAnonymous) |
| باقي endpoints العميل أدناه | `Authorization: Bearer <access_token>` + دور Customer |

Aliases مقبولة في أغلب الطلبات (snake_case و camelCase):  
`fulfillment_type` / `fulfillmentType` ، `vendor_branch_id` / `vendorBranchId` ، `vendor_id` / `vendorId` ، `payment_method` / `paymentMethod` ، `reason_code` / `reasonCode`

---

## 1) إعدادات المنصة قبل فتح Checkout

### Endpoint

```http
GET /api/checkout/config
```

### Response مثال

```json
{
  "delivery_enabled": true,
  "pickup_enabled": true,
  "pickup_cash_on_pickup_enabled": false,
  "allowed_payments_for_pickup": ["card", "apple_pay"]
}
```

### معنى الحقول

| الحقل | النوع | سلوك الواجهة |
|---|---|---|
| `delivery_enabled` | bool | لو `false` اخفِ بلاطة «توصيل» بالكامل |
| `pickup_enabled` | bool | لو `false` اخفِ بلاطة «استلام من الفرع» بالكامل |
| `pickup_cash_on_pickup_enabled` | bool | لو `true` اعرض «الدفع عند الاستلام» |
| `allowed_payments_for_pickup` | string[] | **المصدر الوحيد** لطرق دفع الـ pickup المعروضة |

### قواعد واجهة

1. استدعِ الـ config عند دخول checkout (أو قبل اختيار نوع التنفيذ).
2. لا تعرض إلا الطرق الموجودة في `allowed_payments_for_pickup`.
3. لو القائمة فيها `cash` اعرض نصًا مثل: «الدفع عند الاستلام».
4. لو الأدمن قفل التوصيل، لا تعرضه حتى لو السلة كانت تستخدم عنوانًا قديمًا.

---

## 2) اختيار نوع التنفيذ (Delivery / Pickup)

في شاشة الدفع أضف اختيارًا:

- توصيل → `fulfillment_type = "delivery"` (+ عنوان + slot كالعادة)
- استلام → `fulfillment_type = "pickup"` (+ اختيار فرع `vendor_branch_id`)

### المقارنة السريعة

| البند | Delivery | Pickup |
|---|---|---|
| `fulfillment_type` | `delivery` | `pickup` |
| العنوان | مطلوب | غير مطلوب |
| الفرع | يُحدَّد تلقائيًا للتوصيل | مطلوب من العميل |
| الشحن | من الـ API | دائمًا `0` |
| OTP | OTP توصيل للمندوب | OTP استلام للتاجر |
| السائق/الخريطة | نعم | لا |

---

## 3) فروع الاستلام حسب المدينة

قبل الـ summary، اجلب فروع التاجر في مدينة العميل. لو أكتر من فرع، اعرض اللي يقدر يوفّر منتجات السلة (`can_fulfill_cart = true`) واختر منه.

### Endpoint

```http
GET /api/checkout/pickup-branches?vendor_id={vendorId}&city={city}
```

بديل: `address_id` بدل `city` (المدينة تُقرأ من العنوان).

### Response مثال

```json
{
  "vendor_id": "0f42e51e-5252-4aa5-ae79-704478ae9b24",
  "city": "الرياض",
  "branches": [
    {
      "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "name": "فرع العليا",
      "address_line": "طريق الملك فهد",
      "city": "الرياض",
      "address": "طريق الملك فهد, الرياض",
      "hours_today": "10:00 - 22:00",
      "is_primary": true,
      "can_fulfill_cart": true,
      "missing_items_count": 0
    },
    {
      "id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      "name": "فرع النسيم",
      "address_line": "شارع النسيم",
      "city": "الرياض",
      "address": "شارع النسيم, الرياض",
      "hours_today": "09:00 - 23:00",
      "is_primary": false,
      "can_fulfill_cart": false,
      "missing_items_count": 1
    }
  ]
}
```

### قواعد واجهة اختيار الفرع

1. ابعت `city` من عنوان العميل (أو `address_id`).
2. رتّب/فلتر حسب `can_fulfill_cart` — امنع اختيار فرع `false` أو وضّح أنه ناقص منتجات.
3. لو فرع واحد متاح فقط → اختَره تلقائيًا.
4. بعد الاختيار ابعت `vendor_branch_id` في summary و place order.

---

## 3.1) Checkout Summary للـ Pickup

### Endpoint

```http
GET /api/checkout/summary?vendor_id={vendorId}&fulfillment_type=pickup&vendor_branch_id={branchId}&payment_method=card
```

أمثلة طرق دفع في الـ query:

- بطاقة: `payment_method=card`
- Apple Pay: `payment_method=apple_pay`
- كاش (لو مفعّل): `payment_method=cash`

### Response مثال (pickup جاهز)

```json
{
  "fulfillment_type": "pickup",
  "pickup_branch": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "name": "Mohandessin Branch",
    "address_line": "12 Lebanon Sq",
    "city": "Giza",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
  },
  "delivery_check": {
    "status": "pickup_ready",
    "is_deliverable": true,
    "can_proceed_to_checkout": true,
    "message": "Pickup checkout is ready.",
    "message_ar": "يمكن متابعة الطلب للاستلام من الفرع.",
    "message_en": "Pickup checkout is ready.",
    "delivery_fee": 0.0,
    "distance_km": 0.0
  },
  "payment_methods": [
    {
      "code": "card",
      "label": "Credit / Debit Card",
      "is_available": true,
      "is_default": true
    },
    {
      "code": "apple_pay",
      "label": "Apple Pay",
      "is_available": true,
      "is_default": false
    }
  ],
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 0.0,
    "discount": 0.0,
    "total": 105.0,
    "currency": "SAR"
  }
}
```

### حالات `delivery_check.status` المهمة للـ pickup

| status | المعنى | ماذا تفعل |
|---|---|---|
| `pickup_ready` | الفرع محدد والـ checkout جاهز | فعّل زر إتمام الطلب |
| `pickup_branch_required` | مفيش فرع | امنع الإتمام واطلب اختيار فرع |

### قواعد واجهة Summary

1. اخفِ اختيار العنوان ومواعيد التوصيل في وضع pickup.
2. اعرض بطاقة الفرع من `pickup_branch`: الاسم + `address` (عنوان المتجر الكامل) + `hours_today` إن وُجد. يمكن استخدام `address_line` + `city` كبديل.
3. اعرض الشحن = `0` من `summary.shipping_cost` (لا تحسب على الجهاز).
4. طرق الدفع المعروضة = من response الـ summary / config فقط.
5. عند تطبيق/إزالة كوبون في وضع pickup مرّر نفس `fulfillment_type` و `vendor_branch_id` عشان `pickup_branch` يفضل راجع في الـ summary.

---

## 4) إنشاء الطلب Place Order

### Endpoint

```http
POST /api/orders
Content-Type: application/json
Authorization: Bearer <token>
```

### Body — Pickup + بطاقة

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

### Body — Pickup + Apple Pay

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "fulfillment_type": "pickup",
  "vendor_branch_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "payment_method": "apple_pay",
  "promo_code": null,
  "notes": null
}
```

### Body — Pickup + كاش عند الاستلام

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

### سلوك الدفع بعد Place Order

#### أ) `card` / `apple_pay`

الرد يحتوي كائن `payment` لـ Moyasar (نفس تدفق الدفع الحالي):

```json
{
  "payment": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "provider": "moyasar",
    "status": "pending",
    "iframe_url": "RenderMoyasarForm",
    "provider_reference": "",
    "provider_config": {
      "publishableKey": "pk_test_or_live",
      "amount": 10500,
      "currency": "SAR",
      "description": "Order ORD-000001",
      "callbackUrl": "https://api.zadna0.com/api/payments/moyasar/verify",
      "methods": ["creditcard"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "33333333-3333-3333-3333-333333333333",
        "payment_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "order_number": "ORD-000001"
      }
    }
  }
}
```

قواعد Moyasar:

1. ارسم نموذج Moyasar من `payment.provider_config` (لا تفتح `iframe_url` كرابط؛ قيمته تلميح `RenderMoyasarForm`).
2. `publishableKey` → `publishable_api_key`
3. `callbackUrl` → `callback_url`
4. `supportedNetworks` → `supported_networks`
5. لا ترسل بيانات البطاقة لسيرفر زادنا.
6. بعد رجوع `payment.id` من Moyasar أكّد:

```http
POST /api/payments/moyasar/confirm
```

```json
{
  "id": "moyasar_payment_id"
}
```

Aliases مقبولة: `id` / `provider_payment_id` / `providerPaymentId`

رد ناجح مثال:

```json
{
  "message": "Payment confirmed successfully",
  "paymentId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "paymentStatus": "paid",
  "userId": "66666666-6666-6666-6666-666666666666",
  "orderId": "33333333-3333-3333-3333-333333333333",
  "orderStatus": "pending_vendor_acceptance",
  "alreadyConfirmed": false
}
```

7. لا تعتبر الطلب مدفوعًا محليًا إلا لو `paymentStatus = "paid"`.
8. بعد الدفع حدّث `GET /api/orders/{orderId}` أو قائمة الطلبات النشطة.

#### ب) `cash`

- مفيش جلسة بوابة دفع.
- الطلب يكمل والدفع بيتحصّل عند الاستلام لدى التاجر.
- حالة الدفع تبقى غير مدفوعة أونلاين إلى أن يتحقق التاجر من OTP عند التسليم.

### أكواد أخطاء Place/Checkout المهمة

| Code | متى يظهر | رسالة واجهة مقترحة |
|---|---|---|
| `PICKUP_DISABLED_BY_ADMIN` | الاستلام مقفول | الاستلام غير متاح حاليًا |
| `PICKUP_BRANCH_REQUIRED` | مفيش فرع | اختر فرع الاستلام |
| `PICKUP_BRANCH_INVALID` | فرع غلط/غير تابع للتاجر | الفرع غير متاح |
| `PICKUP_CASH_DISABLED` | اختار كاش والكاش مقفول | الدفع عند الاستلام غير مفعّل |
| `PICKUP_ONLY_ONLINE_PAYMENT` | طريقة غير مدعومة (مثل bank transfer) | اختر بطاقة أو Apple Pay |

---

## 5) دورة حالات طلب الاستلام

```text
pending_payment
placed / pending_vendor_acceptance
        ↓ (التاجر يقبل)
accepted
        ↓
preparing
        ↓
ready_for_pickup   ← هنا يظهر OTP + مهلة الاستلام
        ↓ (التاجر يتحقق من OTP)
delivered
```

حالات خاصة:

| الحالة | المعنى في الواجهة |
|---|---|
| `ready_for_pickup` | جاهز — اعرض الفرع + OTP + العد التنازلي |
| `cancellation_requested` | طلب إلغاء بانتظار موافقة التاجر (الطلب لسه نشط) |
| `cancelled` | ملغي (يشمل انتهاء مهلة عدم الحضور / no-show) |

> لا تعتمد على الحالة وحدها لعرض OTP. اعتمد على `pickup_otp_code != null`.

---

## 6) تفاصيل الطلب + OTP

### Endpoint

```http
GET /api/orders/{orderId}
```

### Response مثال (pickup جاهز للاستلام)

```json
{
  "id": "44444444-4444-4444-4444-444444444444",
  "order_number": "ORD-10246",
  "created_at": "2026-04-25T11:00:00Z",
  "total_price": 105.0,
  "status": "ready_for_pickup",
  "payment_status": "paid",
  "payment_method": "card",
  "fulfillment_type": "pickup",
  "can_retry_payment": false,
  "can_delete": false,
  "can_cancel": true,
  "items_count": 2,
  "summary": {
    "subtotal": 105.0,
    "shipping_cost": 0.0,
    "total": 105.0
  },
  "pickup_otp_code": "4821",
  "pickup_otp_expires_at_utc": "2026-04-25T14:00:00Z",
  "pickup_no_show_deadline_utc": "2026-04-25T18:00:00Z",
  "pickup_branch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
  },
  "items": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "name": "Olive Oil 1L",
      "quantity": 2,
      "price": 52.5
    }
  ]
}
```

### قواعد الحقول

#### `fulfillment_type`

- `delivery` أو `pickup`
- استخدمه لتبديل Layout الشاشة (توصيل vs استلام)

#### `pickup_otp_code` / `pickup_otp_expires_at_utc` / `pickup_no_show_deadline_utc`

يظهر الكود فقط عندما:

1. `fulfillment_type = "pickup"`
2. الطلب `ready_for_pickup`
3. OTP لم يُتحقق بعد

بعد التحقق أو اكتمال الطلب → الحقول ترجع `null` → اخفِ بطاقة OTP فورًا.

#### `pickup_branch`

```json
{
  "name": "string",
  "address": "string",
  "hours_today": "string | null"
}
```

اعرض فقط هذه الحقول. لا تفترض وجود إحداثيات/هاتف.

#### أعلام الإجراءات

ثق مباشرة في:

- `can_retry_payment`
- `can_delete`
- `can_cancel`

---

## 7) إعادة إرسال OTP

### Endpoint

```http
POST /api/orders/{orderId}/resend-pickup-otp
```

Body: فارغ / بدون حقول مطلوبة.

### Response مثال

```json
{
  "order_id": "44444444-4444-4444-4444-444444444444",
  "expires_at_utc": "2026-07-25T18:10:00Z",
  "message": "..."
}
```

### بعد النجاح

1. حدّث `GET /api/orders/{orderId}` لجلب الكود الجديد.
2. أو اعتمد على حدث Realtime / إشعار `pickup_otp_regenerated`.

### أخطاء محتملة

| Code | المعنى |
|---|---|
| `PICKUP_OTP_RESEND_RATE_LIMIT` | إعادة الإرسال سريعة جدًا — انتظر |
| `PICKUP_OTP_LOCKED` | الكود مقفول مؤقتًا بسبب محاولات فاشلة عند التاجر |
| `PICKUP_OTP_ALREADY_VERIFIED` | تم التحقق بالفعل |

---

## 8) شاشة التتبع Tracking (Pickup)

### Endpoint

```http
GET /api/orders/{orderId}/tracking
```

### Response مثال (pickup)

```json
{
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "order_number": "ORD-10246",
    "status": "ready_for_pickup"
  },
  "fulfillment_type": "pickup",
  "estimated_delivery": null,
  "driver": null,
  "assigned_driver": null,
  "driver_arrival_state": "",
  "driver_arrival_updated_at_utc": null,
  "delivery_otp": null,
  "show_delivery_otp": false,
  "pickup_otp_code": "4821",
  "pickup_otp_expires_at_utc": "2026-04-25T14:00:00Z",
  "pickup_no_show_deadline_utc": "2026-04-25T18:00:00Z",
  "pickup_branch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hours_today": "Today: 10:00 AM - 10:00 PM"
  },
  "timeline": [
    {
      "id": "placed",
      "title": "Order placed",
      "time": "11:00 AM",
      "is_active": false,
      "is_completed": true
    },
    {
      "id": "ready_for_pickup",
      "title": "Ready for pickup",
      "time": "12:10 PM",
      "is_active": true,
      "is_completed": true
    }
  ]
}
```

### ماذا تعرض في Pickup Tracking

- حالة الطلب
- بطاقة الفرع (اسم/عنوان/ساعات)
- بطاقة OTP لو `pickup_otp_code != null`
- عدّاد/نص لمهلة `pickup_no_show_deadline_utc`
- التايملاين

### ماذا تخفي تمامًا في Pickup

- خريطة / موقع المندوب
- كارت السائق / الاتصال بالمندوب
- OTP التوصيل (`delivery_otp` / `show_delivery_otp`)
- حالات وصول السائق (`driver_arrival_state`)
- ETA التوصيل التقليدي (`estimated_delivery` غالبًا `null`)

القاعدة الذهبية:  
**لو `fulfillment_type === "pickup"` → UI فرع+OTP، مش UI مندوب.**

---

## 9) الإلغاء Cancellation

### أسباب الإلغاء

```http
GET /api/orders/cancellation-reasons
```

```json
[
  {
    "code": "changed_my_mind",
    "label": "Changed my mind",
    "requires_note": false
  },
  {
    "code": "other",
    "label": "Other",
    "requires_note": true
  }
]
```

### تنفيذ الإلغاء

```http
POST /api/orders/{orderId}/cancel
```

```json
{
  "reason_code": "changed_my_mind",
  "reason": null,
  "note": null
}
```

قواعد:

- أرسل `reason_code` أو `reason` القديم.
- لو `reason_code = "other"` لازم `note`.

### سلوك Pickup تحديدًا

| مرحلة الطلب | النتيجة |
|---|---|
| قبل قبول التاجر: `pending_payment` / `placed` / `pending_vendor_acceptance` | إلغاء فوري → `cancelled` (وقد يحدث استرداد لو كان مدفوع أونلاين) |
| بعد القبول: `accepted` / `preparing` / `ready_for_pickup` | طلب موافقة → الرد يرجع `cancellation_requested` |

#### رد إلغاء فوري

```json
{
  "message": "Order cancelled successfully.",
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "cancelled"
  }
}
```

#### رد بانتظار موافقة التاجر

```json
{
  "message": "Cancellation request submitted and is awaiting vendor approval.",
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "cancellation_requested"
  }
}
```

#### لو فيه طلب إلغاء معلّق بالفعل

```json
{
  "message": "A cancellation request is already pending vendor approval.",
  "order": {
    "id": "44444444-4444-4444-4444-444444444444",
    "status": "cancellation_requested"
  }
}
```

### قواعد واجهة الإلغاء

1. اعتمد على `can_cancel` من تفاصيل الطلب.
2. بعد `accepted`/`preparing`/`ready_for_pickup` وضّح أن الإلغاء قد يحتاج موافقة التاجر.
3. لو رجع `cancellation_requested`:
   - لا تحذف الطلب من القائمة النشطة فورًا
   - اعرض بانر «بانتظار موافقة التاجر»
   - حدّث التفاصيل بعد القرار
4. أكواد مرتبطة: `ORDER_CANNOT_BE_CANCELLED` ، `PICKUP_CANCEL_NEEDS_VENDOR_APPROVAL` ، `CANCELLATION_REQUEST_ALREADY_PENDING`

### No-show (عدم حضور)

- لو انتهى وقت `pickup_no_show_deadline_utc` بدون استلام، الباك يلغي الطلب تلقائيًا.
- prepaid أونلاين: يتم الاسترداد تلقائيًا من السيرفر.
- cash pickup: لا استرداد (لم يُدفع أونلاين).
- العميل يستلم إشعار `pickup_expired`.

---

## 10) الإشعارات Push / Inbox

| `type` | متى | Action في التطبيق |
|---|---|---|
| `pickup_ready` | التاجر علّم الطلب جاهز | افتح تفاصيل الطلب لعرض OTP والفرع |
| `pickup_reminder` | تذكير بمهلة الاستلام (منتصف/قرب النهاية) | افتح التفاصيل |
| `pickup_deadline_extended` | تم تمديد المهلة (الفرع كان مغلق) | حدّث عدّاد المهلة |
| `pickup_expired` | انتهت المهلة واتلغى الطلب | اعرض ملغي + حدّث التفاصيل |
| `pickup_otp_regenerated` | العميل طلب إعادة OTP | حدّث التفاصيل للكود الجديد |

### مثال `pickup_ready`

```json
{
  "id": "99999999-9999-9999-9999-999999999999",
  "titleAr": "الطلب جاهز للاستلام",
  "titleEn": "Order Ready for Pickup",
  "bodyAr": "طلبك رقم ORD-10246 جاهز للاستلام من الفرع. افتح تفاصيل الطلب لعرض رمز الاستلام.",
  "bodyEn": "Your order #ORD-10246 is ready for pickup at the branch. Open order details to view your pickup code.",
  "type": "pickup_ready",
  "referenceId": "44444444-4444-4444-4444-444444444444",
  "data": "{\"orderId\":\"44444444-4444-4444-4444-444444444444\",\"fulfillmentType\":\"Pickup\",\"eventName\":\"order.pickup.ready\"}",
  "dataObject": null,
  "isRead": false,
  "createdAtUtc": "2026-04-25T12:10:00Z"
}
```

### ملاحظات مهمة

1. **كود OTP لا يُرسل داخل الإشعار** أبدًا.
2. Deep link عبر `referenceId` = `orderId`.
3. بعد فتح الإشعار: `GET /api/orders/{orderId}` لجلب OTP.

---

## 11) Realtime (SignalR)

### Hub

```text
/hubs/notifications
```

### حدث حالة الطلب

```text
ReceiveOrderStatusChanged
```

اشتراك مثال (Dart):

```dart
connection.on('ReceiveOrderStatusChanged', (arguments) {
  final payload = arguments?[0] as Map<String, dynamic>?;
  if (payload == null) return;
  if (payload['orderId'] == openedOrderId) {
    // حدّث UI / أعد جلب details أو tracking
  }
});
```

### Payload مثال Pickup (قناة العميل فقط تحمل OTP)

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "ORD-12346",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "preparing",
  "newStatus": "ready_for_pickup",
  "actorRole": "vendor",
  "action": "status_changed",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-04-28T10:05:00Z",
  "fulfillmentType": "Pickup",
  "pickupOtpCode": "4821",
  "pickupOtpExpiresAtUtc": "2026-04-28T12:05:00Z",
  "pickupNoShowDeadlineUtc": "2026-04-28T16:05:00Z",
  "pickupBranch": {
    "name": "Mohandessin Branch",
    "address": "12 Lebanon Sq, Giza",
    "hoursToday": "Today: 10:00 AM - 10:00 PM"
  }
}
```

### قواعد Realtime للـ Pickup

1. OTP السري (`pickupOtpCode`) يوصل فقط على قناة المستخدم المصادَق.
2. بث مجموعة التتبع لنفس الحدث **لا** يتضمن OTP.
3. توقّع OTP عندما:
   - `fulfillmentType = "Pickup"`
   - `newStatus = "ready_for_pickup"`
   - لم يتم التحقق بعد
4. بعد التحقق أو التحويل لتوصيل، الأحداث التالية لا تحمل OTP.
5. تجاهل تمامًا حدث `ReceiveDriverArrivalStateChanged` لطلبات pickup (خاص بالتوصيل).

### قيم `newStatus` الشائعة للتتبع

```text
pending
accepted
preparing
ready_for_pickup
out_for_delivery
delivered
returning
cancelled
```

ملاحظة: تحقق OTP عند التاجر يحوّل طلب pickup إلى `delivered`.

---

## 12) تحويل الاستلام → توصيل (دفع الفرق)

التاجر أو الأدمن قد يحوّل طلب pickup إلى delivery.

لو فرق رسوم التوصيل أكبر من المدفوع، ينشئ الباك جلسة Moyasar إضافية.

### تمييز الجلسة

داخل `payment.provider_config.metadata`:

```json
{
  "kind": "delivery_upgrade",
  "originalOrderId": "33333333-3333-3333-3333-333333333333",
  "order_id": "33333333-3333-3333-3333-333333333333",
  "payment_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
}
```

### قواعد الموبايل

1. لو `metadata.kind == "delivery_upgrade"` → اعتبرها **دفع فرق توصيل**، مش طلب جديد.
2. نفس UI Moyasar + نفس `POST /api/payments/moyasar/confirm`.
3. بعد نجاح التأكيد:
   - حدّث `GET /api/orders/{orderId}`
   - وحدّث tracking
4. لا تعتبر الطلب صار delivery محليًا إلا لما الـ API يرجع:
   - `fulfillment_type = "delivery"`
   - وتحديث الإجماليات/العنوان/السائق حسب الاستجابة

---

## 13) قوائم الطلبات

استخدم نفس endpoints الحالية:

```http
GET /api/orders/active
GET /api/orders/completed
GET /api/orders/{orderId}
```

في عناصر القائمة/التفاصيل:

- اعرض بادج «استلام» لو `fulfillment_type = pickup`
- لا تعرض ETA مندوب لطلبات pickup
- زر إلغاء يظهر حسب `can_cancel`

---

## 14) نموذج بيانات مقترح (Flutter)

```dart
enum FulfillmentType { delivery, pickup }

class CheckoutConfig {
  final bool deliveryEnabled;
  final bool pickupEnabled;
  final bool pickupCashOnPickupEnabled;
  final List<String> allowedPaymentsForPickup;
}

class PickupBranch {
  final String? id;          // موجود في checkout summary / place order
  final String name;
  final String address;      // عنوان المتجر الكامل (مفضّل للعرض)
  final String? addressLine; // checkout summary
  final String? city;        // checkout summary
  final String? hoursToday;  // summary + details/tracking
}

class CustomerOrderDetail {
  final String id;
  final String orderNumber;
  final String status;
  final String paymentStatus;
  final String paymentMethod;
  final FulfillmentType fulfillmentType;
  final bool canRetryPayment;
  final bool canDelete;
  final bool canCancel;
  final double totalPrice;
  final OrderSummary summary;
  final String? pickupOtpCode;
  final DateTime? pickupOtpExpiresAtUtc;
  final DateTime? pickupNoShowDeadlineUtc;
  final PickupBranch? pickupBranch;
  final List<OrderItem> items;
}
```

### مappers مهمة

- `fulfillment_type` / `fulfillmentType` → enum
- في summary / place order: `pickup_branch.address` (+ `hours_today`)، مع `address_line`/`city` للتوافق
- في details/tracking: `pickup_branch.address` (+ `hours_today`)
- في realtime: `pickupBranch.hoursToday` (camelCase)

وحّد العرض في الـ UI تحت اسم واحد مثل `branchAddress` / `hoursToday`.

---

## 15) تدفق الشاشات المقترح

```text
Cart
  → GET /api/checkout/config
  → اختيار Delivery أو Pickup
      ├─ Delivery: عنوان + slot + summary عادي
      └─ Pickup: GET pickup-branches?city=... → اختيار فرع متاح → GET summary(...vendor_branch_id=...)
  → POST /api/orders
      ├─ card/apple_pay → Moyasar → confirm
      └─ cash → مباشرة لتفاصيل الطلب
  → Order Details / Tracking
      ├─ قبل ready_for_pickup: حالة التحضير
      ├─ ready_for_pickup: OTP + فرع + مهلة + Resend OTP
      ├─ Cancel حسب المرحلة
      └─ إشعارات/Realtime تحدث الشاشة
  → (اختياري) دفع delivery_upgrade لو اتحول لتوصيل
  → delivered / cancelled
```

---

## 16) Checklist تنفيذ إلزامي

### Checkout
- [ ] جلب config وإخفاء/إظهار delivery و pickup
- [ ] جلب فروع المدينة عبر `/api/checkout/pickup-branches` واختيار فرع `can_fulfill_cart=true`
- [ ] إرسال `fulfillment_type=pickup` و `vendor_branch_id` في summary و place
- [ ] إخفاء العنوان والـ slot في وضع pickup
- [ ] عرض `shipping_cost = 0`
- [ ] طرق الدفع من `allowed_payments_for_pickup` فقط
- [ ] دعم `cash` عند التفعيل
- [ ] معالجة أخطاء `PICKUP_*`

### بعد الطلب
- [ ] بادج استلام في القوائم
- [ ] شاشة details بدون سائق/خريطة للـ pickup
- [ ] بطاقة OTP فقط عند `pickup_otp_code != null`
- [ ] عدّاد/`countdown` لـ `pickup_no_show_deadline_utc`
- [ ] زر Resend OTP + معالجة rate limit/lock
- [ ] بانر `cancellation_requested`
- [ ] إشعارات `pickup_*` تفتح التفاصيل
- [ ] SignalR يحدّث الحالة/OTP من قناة المستخدم
- [ ] تجاهل أحداث وصول السائق للـ pickup
- [ ] دعم دفع `delivery_upgrade`

### ممنوع
- [ ] لا تحسب الشحن على الجهاز
- [ ] لا تفترض OTP من الحالة فقط
- [ ] لا تعرض هاتف/خريطة الفرع لو مش راجعين
- [ ] لا تستخدم Paymob أو روابط قديمة
- [ ] لا تعتبر الدفع ناجحًا قبل confirm زادنا

---

## 17) جدول أخطاء شامل للموبايل

| Code | السياق | تصرف مقترح |
|---|---|---|
| `PICKUP_DISABLED_BY_ADMIN` | config/place | اخفِ خيار الاستلام / رسالة غير متاح |
| `PICKUP_BRANCH_REQUIRED` | summary/place | اطلب اختيار فرع |
| `PICKUP_BRANCH_INVALID` | summary/place | أعد تحميل الفروع واختر غيره |
| `PICKUP_CASH_DISABLED` | place | أخفِ الكاش وحدّث config |
| `PICKUP_ONLY_ONLINE_PAYMENT` | place | أجبر card/apple_pay |
| `PICKUP_OTP_RESEND_RATE_LIMIT` | resend | عطّل الزر مؤقتًا مع عداد |
| `PICKUP_OTP_LOCKED` | resend/UI | اعرض رسالة قفل مؤقت |
| `PICKUP_OTP_ALREADY_VERIFIED` | resend | حدّث التفاصيل — غالبًا delivered |
| `PICKUP_CANCEL_NEEDS_VENDOR_APPROVAL` | cancel | وضّح انتظار موافقة التاجر |
| `CANCELLATION_REQUEST_ALREADY_PENDING` | cancel | اعرض بانر الانتظار |
| `ORDER_CANNOT_BE_CANCELLED` | cancel | أخفِ زر الإلغاء وحدّث التفاصيل |

النصوص النهائية للأخطاء غالبًا ترجع localized من الـ API — اعرض رسالة السيرفر للمستخدم عند توفرها.

---

## 18) حالات اختبار يدوي مطلوبة

1. Config: pickup مقفول → بلاطة الاستلام مختفية.
2. Config: delivery مقفول → بلاطة التوصيل مختفية.
3. Pickup + card: دفع Moyasar ثم قبول تاجر → preparing → ready_for_pickup → OTP يظهر.
4. Resend OTP يعمل ويتحدث الكود/الانتهاء.
5. التاجر يتحقق من OTP → الطلب `delivered` ويختفي OTP.
6. Pickup + cash (مفعّل): إنشاء بدون بوابة دفع.
7. Pickup + cash (مقفول): يرجع `PICKUP_CASH_DISABLED`.
8. إلغاء قبل القبول → `cancelled` فورًا.
9. إلغاء بعد القبول → `cancellation_requested` + بانر.
10. إشعار `pickup_ready` يفتح التفاصيل وفيه OTP من API.
11. Realtime على قناة المستخدم يملأ OTP عند `ready_for_pickup`.
12. لا تظهر خريطة/سائق في أي شاشة pickup.
13. تحويل لتوصيل + دفع `delivery_upgrade` يحدّث `fulfillment_type` إلى delivery بعد confirm.

---

## 19) ملخص Endpoints المستخدمة في الميزة

| Method | Path | الاستخدام |
|---|---|---|
| GET | `/api/checkout/config` | تفعيل التوصيل/الاستلام/الكاش |
| GET | `/api/checkout/pickup-branches?...` | فروع المدينة + توفر السلة |
| GET | `/api/checkout/summary?...` | ملخص pickup |
| POST | `/api/orders` | إنشاء الطلب |
| POST | `/api/payments/moyasar/confirm` | تأكيد دفع البطاقة/فرق التوصيل |
| GET | `/api/orders/active` | الطلبات النشطة |
| GET | `/api/orders/{orderId}` | التفاصيل + OTP |
| GET | `/api/orders/{orderId}/tracking` | تتبع pickup |
| POST | `/api/orders/{orderId}/resend-pickup-otp` | إعادة OTP |
| GET | `/api/orders/cancellation-reasons` | أسباب الإلغاء |
| POST | `/api/orders/{orderId}/cancel` | إلغاء / طلب إلغاء |
| SignalR | `/hubs/notifications` → `ReceiveOrderStatusChanged` | تحديث حي |

---

**انتهى الدليل.** نفّذ من هذا الملف فقط؛ كل التفاصيل اللازمة لتطبيق العميل موجودة أعلاه.
