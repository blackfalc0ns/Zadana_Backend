# Apple Pay عبر Moyasar — Handoff لتطبيق العميل

## الحالة

- الباك إند: `implemented`
- ميسر (Dashboard): شهادة Apple Pay **مفعلة** + نطاق `www.zadna0.com` = **REGISTERED**
- المطلوب من تطبيق العميل (iOS): إظهار Apple Pay وتنفيذ الدفع عبر Moyasar SDK ثم تأكيد الدفع مع زدانا

## المبدأ

1. الموبايل **لا** يقرر لوحده إن Apple Pay متاح.
2. المصدر الوحيد للتوفر: `is_available` من `GET /api/checkout/summary`.
3. عند اختيار `apple_pay`، الدفع يتم عبر **Moyasar** بنفس عقد الكارت، مع `methods: ["applepay"]`.
4. لا تُرسل بيانات البطاقة/التوكن إلى سيرفرات زدانا مباشرة — فقط `provider_payment_id` بعد نجاح Moyasar.

مرجع عام للدفع: `PAYMENT_CONTRACT.md`

---

## متى يظهر زر Apple Pay؟

من `GET /api/checkout/summary?...&payment_method=apple_pay` (أو بدون اختيار بعد):

ابحث في `payment_methods` عن:

```json
{
  "code": "apple_pay",
  "label": "Apple Pay",
  "is_available": true,
  "is_default": false
}
```

| الشرط | سلوك الواجهة |
|---|---|
| `is_available = true` | أظهر Apple Pay (على أجهزة Apple المدعومة فقط) |
| `is_available = false` | اخفِ الزر بالكامل — لا تعطّله فقط |
| الجهاز Android / بدون Apple Wallet | لا تعرض الخيار |

عند تغيير طريقة الدفع إلى `apple_pay` أعد طلب الـ summary (نفس قاعدة VAT/COD).

---

## إنشاء الطلب

### Endpoint

`POST /api/orders`

Authorization: Bearer (Customer)

### Body

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "address_id": "22222222-2222-2222-2222-222222222222",
  "delivery_slot_id": "asap",
  "payment_method": "apple_pay",
  "promo_code": null,
  "notes": null
}
```

الكود المعتمد: **`apple_pay`**  
Aliases مقبولة من الباك: `applepay` — لكن فضّل `apple_pay`.

### استجابة الدفع المتوقعة

```json
{
  "message": "...",
  "order": {
    "id": "33333333-3333-3333-3333-333333333333",
    "status": "pending",
    "payment_method": "apple_pay",
    "payment_status": "pending",
    "total_price": 125.00
  },
  "payment": {
    "id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "provider": "moyasar",
    "status": "pending",
    "iframe_url": "RenderMoyasarForm",
    "provider_reference": "",
    "provider_config": {
      "publishableKey": "pk_live_or_test",
      "amount": 12500,
      "currency": "SAR",
      "description": "Order ORD-000001",
      "callbackUrl": "https://api.zadna0.com/api/payments/moyasar/verify",
      "methods": ["applepay"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "33333333-3333-3333-3333-333333333333",
        "payment_id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        "order_number": "ORD-000001"
      }
    },
    "payment_flow": "online_gateway",
    "is_paid": false,
    "requires_customer_action": true,
    "customer_action": "render_payment_form",
    "confirmation_mode": "provider_payment_id"
  }
}
```

### قواعد مهمة من الاستجابة

- `iframe_url = "RenderMoyasarForm"` **ليس رابطًا** — هو تلميح action.
- ابنِ شاشة Moyasar / Apple Pay من `payment.provider_config`.
- تأكد أن `methods` فيها `applepay`.
- `amount` بالهللة (Minor units): `125.00 SAR` → `12500`.
- لا تخزّن Secret Key داخل التطبيق — فقط `publishableKey`.

---

## تنفيذ الدفع على iOS

### إعداد Xcode (مرة واحدة)

1. تفعيل Capability: **Apple Pay**
2. اختيار / إضافة **Merchant ID** المطابق لما عند ميسر / Apple Developer
3. الاختبار على **جهاز iPhone حقيقي** (Apple Pay لا يعمل على Simulator)

### تدفق التطبيق

1. المستخدم يختار `apple_pay` في Checkout.
2. حدّث الملخص: `GET /api/checkout/summary?...&payment_method=apple_pay`.
3. `POST /api/orders` بـ `"payment_method": "apple_pay"`.
4. من `payment.provider_config` شغّل Moyasar SDK / Apple Pay sheet:
   - `publishable_api_key` ← `publishableKey`
   - `amount` ← `amount`
   - `currency` ← `currency`
   - `description` ← `description`
   - `callback_url` ← `callbackUrl`
   - `methods` ← `["applepay"]`
   - `supported_networks` ← `supportedNetworks`
   - مرّر `metadata` كما هي
5. بعد نجاح Apple Pay ورجوع `moyasar_payment_id` من SDK:
   - أكّد فورًا مع زدانا (الخطوة التالية)
6. لا تعرض «تم الدفع» إلا بعد تأكيد زدانا.

---

## تأكيد الدفع مع زدانا

### Endpoint المفضل

`POST /api/payments/moyasar/confirm`

```json
{
  "id": "moyasar_payment_id"
}
```

Aliases مقبولة: `id` | `provider_payment_id` | `providerPaymentId`

### بديل (callback / verify)

`GET /api/payments/moyasar/verify?id={moyasar_payment_id}`

عادة للـ web callback؛ الموبايل يفضّل `POST /confirm`.

### بعد التأكيد

1. لو الدفع `paid` → انتقل لشاشة نجاح الطلب / تفاصيل الطلب.
2. حدّث الطلب: `GET /api/orders/{orderId}` أو `GET /api/orders/active`.
3. **لا** تعتمد على حالة محلية من SDK وحدها.

---

## إعادة المحاولة

لو الطلب قائم و`can_retry_payment = true`:

`POST /api/orders/{orderId}/retry-payment`

نفس شكل `payment` أعلاه (مع `methods: ["applepay"]` إذا كانت طريقة الطلب Apple Pay).

---

## أخطاء شائعة

| errorCode / حالة | المعنى | سلوك الواجهة |
|---|---|---|
| `PAYMENT_METHOD_NOT_SUPPORTED` | طريقة غير مدعومة / بيئة غير جاهزة | أخفِ Apple Pay وأظهر رسالة عامة |
| `PAYMENT_UNAVAILABLE` | Moyasar غير مفعّل في السيرفر | أخفِ card + apple_pay |
| `is_available = false` | البوابة متوقفة حاليًا | لا تعرض الخيار |
| فشل SDK / إلغاء المستخدم | لم يكتمل الدفع | ابقَ على الطلب pending واعرض إعادة محاولة إن وُجدت |
| confirm فشل | الدفع لم يُعتمد بعد عند زدانا | لا تعرض نجاح؛ اسمح بإعادة confirm أو refresh للطلب |

---

## Checklist قبول (QA)

1. Summary يرجع `apple_pay.is_available = true` بعد deploy الباك.
2. على iPhone فيه بطاقة Wallet يظهر زر Apple Pay.
3. على Android لا يظهر.
4. `POST /api/orders` بـ `apple_pay` يرجع `provider_config.methods = ["applepay"]`.
5. إتمام Apple Pay → `POST /confirm` → حالة الطلب/الدفع `paid`.
6. إلغاء ورقة Apple Pay → الطلب لا يُعتبر مدفوعًا.
7. بعد النجاح، السلة تُفرَّغ حسب سلوك الطلب المدفوع الحالي (نفس مسار الكارت).

---

## ملاحظات للمنصة (ليست شغل الموبايل)

- شهادة Apple Pay في ميسر: **مفعلة**
- نطاق الويب `www.zadna0.com`: **REGISTERED** (مفيد للويب؛ التطبيق يعتمد على الشهادة + Merchant ID)
- الباك يفعّل `apple_pay` عندما تكون بوابة Moyasar Enabled

## Out of scope للموبايل هنا

- إعدادات Dashboard ميسر / رفع الشهادات
- Samsung Pay
- STC Pay
- محفظة زدانا (`wallet`)
