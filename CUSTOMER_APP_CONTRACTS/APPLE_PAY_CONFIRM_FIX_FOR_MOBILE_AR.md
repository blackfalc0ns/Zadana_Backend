# Apple Pay — إصلاح مطلوب من تطبيق العميل (Flutter)

تاريخ: 2026-07-28  
الحالة: **مطلوب تنفيذ على الموبايل**  
السبب: بعد Done من ورقة Apple Pay يظهر «خطأ غير متوقع» والسلة ما تفضّاش.

---

## تشخيص من اللوجات

اللي شغال:
1. `POST /api/orders` بـ `payment_method: apple_pay` → 200
2. الاستجابة فيها `payment.provider_config` + `iframe_url: RenderMoyasarForm`
3. ورقة Apple Pay تظهر والمستخدم يدفع وتقول Done

اللي **ناقص**:
- بعد Done **ما فيش** طلب `POST /api/payments/moyasar/confirm`
- عشان كده زدانة ما تعرفش إن الدفع نجح، السلة تفضل، والتطبيق يرمي خطأ عام

> **Done من Apple Pay ≠ مدفوع عند زدانة.**

---

## التعديل المطلوب (خطوة واحدة)

بعد نجاح Moyasar SDK ورجوع `moyasar_payment_id`، نادِ فورًا:

```http
POST /api/payments/moyasar/confirm
Authorization: Bearer <customer_access_token>
Content-Type: application/json
Accept-Language: ar

{
  "id": "<moyasar_payment_id>"
}
```

Aliases مقبولة للجسم:
- `id`
- `provider_payment_id`
- `providerPaymentId`

### مهم
- `id` هنا = معرّف دفعة **Moyasar** (اللي يرجع من الـ SDK بعد Done)
- **مش** `order.id`
- **مش** `payment.id` بتاع زدانة من استجابة إنشاء الطلب

---

## التسلسل الصحيح كاملًا

```text
1) POST /api/orders  { payment_method: "apple_pay", ... }
2) ابنِ Apple Pay من payment.provider_config
3) مرّر metadata كما هي (order_id, payment_id, order_number)
4) المستخدم يكمل ورقة Apple Pay → Done
5) خذ moyasar payment id من نتيجة الـ SDK
6) POST /api/payments/moyasar/confirm  { id: moyasar_payment_id }
7) لو paid → شاشة نجاح الطلب + حدّث السلة/الطلبات
8) لو فشل → اعرض رسالة الـ API (مش "خطأ غير متوقع")
```

---

## مثال استجابة إنشاء الطلب (مختصر)

```json
{
  "order": {
    "id": "c4219ceb-0589-4c13-9083-f75ed32b3ef2",
    "status": "pending",
    "payment_method": "apple_pay",
    "payment_status": "pending"
  },
  "payment": {
    "id": "64149ac0-4b86-4d0a-8d5a-0a0337ea3169",
    "provider": "moyasar",
    "iframe_url": "RenderMoyasarForm",
    "confirmation_mode": "provider_payment_id",
    "customer_action": "render_payment_form",
    "provider_config": {
      "publishableKey": "pk_live_...",
      "amount": 14197,
      "currency": "SAR",
      "description": "Order ORD-...",
      "callbackUrl": "https://api.zadna0.com/api/payments/moyasar/verify",
      "methods": ["applepay"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "c4219ceb-0589-4c13-9083-f75ed32b3ef2",
        "payment_id": "64149ac0-4b86-4d0a-8d5a-0a0337ea3169",
        "order_number": "ORD-..."
      }
    }
  }
}
```

### قواعد من الاستجابة
- `iframe_url = "RenderMoyasarForm"` **مش رابط** — تلميح action فقط
- ابنِ الدفع من `provider_config`
- `amount` بالهللة (141.97 ر.س → 14197)
- مرّر `metadata` كاملة لـ Moyasar

---

## مثال كود Flutter (منطقي)

```dart
// بعد نجاح Apple Pay / Moyasar SDK:
final moyasarPaymentId = moyasarResult.id; // من SDK

final response = await dio.post(
  '/api/payments/moyasar/confirm',
  data: {'id': moyasarPaymentId},
);

// فقط بعد نجاح التأكيد:
// - افتح شاشة نجاح الطلب
// - أعد تحميل السلة / الطلبات
```

لا تعتمد على `callbackUrl` كمسار نجاح أساسي في Apple Pay native.  
`GET /api/payments/moyasar/verify?id=...` بديل للـ web؛ الموبايل يفضّل `POST /confirm`.

---

## تحقق من الإصلاح (Dio logs)

بعد التعديل لازم يظهر بالترتيب:

1. `POST /api/orders` → 200  
2. `POST /api/payments/moyasar/confirm` → 200 و`paymentStatus/paid`  
3. السلة فاضية أو بدون منتجات الطلب  
4. الطلب يظهر كمدفوع / بانتظار قبول التاجر  

لو الخطوة 2 مش موجودة → الإصلاح لسه ناقص.

---

## ممنوع

- اعتبار Done = نجاح نهائي
- عرض «تم الدفع» قبل رد زدانة
- فتح `iframe_url` كـ URL
- تجاهل فشل confirm برسالة عامة بدون لوج

---

## مرجع أوسع

التفاصيل الكاملة لإعداد Apple Pay وMoyasar:  
`CUSTOMER_APP_CONTRACTS/APPLE_PAY_MOYASAR_MOBILE_HANDOFF_AR.md`
