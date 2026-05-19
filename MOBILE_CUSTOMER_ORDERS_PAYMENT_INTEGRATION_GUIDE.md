# دليل ربط تطبيق العميل مع الدفع عبر Moyasar

آخر تحديث: 2026-05-19

هذا الملف هو المرجع الحالي لتطبيق العميل بعد إزالة منطق Paymob من مسار الدفع. الربط الآن يعتمد على Moyasar فقط للطلبات المدفوعة بالبطاقة، مع بقاء الكاش والتحويل البنكي كما هما.

## الخلاصة

- لا تستخدم أي endpoint أو iframe خاص بـ Paymob.
- لا تفتح `payment.iframe_url` كرابط. في الربط الحالي قيمته تكون `RenderMoyasarForm` كإشارة للتطبيق أن يرسم نموذج Moyasar.
- اقرأ إعدادات Moyasar من `payment.provider_config` القادمة من الباك إند.
- التطبيق يستخدم `publishableKey` فقط. أي `SecretKey` أو `WebhookSecret` يظل على السيرفر فقط.
- لا تعتبر الطلب مدفوعا إلا بعد تأكيد الباك إند، ثم اعمل refresh للطلب.

## مصادر Moyasar الرسمية

- Card Payment Basic Integration: https://docs.moyasar.com/guides/card-payments/basic-integration/
- Form Configuration: https://docs.moyasar.com/guides/references/form-configuration/
- API Authentication: https://docs.moyasar.com/api/authentication/
- Webhooks: https://docs.moyasar.com/guides/dashboard/setting-up-webhooks/

المهم من المصادر:

- Moyasar Form يحتاج `publishable_api_key`, `amount`, `currency`, `description`, `callback_url`, `methods`.
- `amount` يكون بالوحدة الصغيرة للعملة، يعني `125 SAR` ترسل `12500`.
- بعد الدفع يرجع Moyasar إلى `callback_url` ويضيف `id` الخاص بالدفع.
- الباك إند فقط هو الذي يجلب الدفع من Moyasar ويتأكد من `status`, `amount`, `currency`, و `metadata`.
- Webhook endpoint يجب أن يكون HTTPS ومعه Secret Token.

## ملفات الباك إند المرتبطة

- `src/Zadana.Api/Modules/Orders/Controllers/OrdersController.cs`
- `src/Zadana.Api/Modules/Payments/Controllers/MoyasarPaymentsController.cs`
- `src/Zadana.Application/Modules/Checkout/Commands/PlaceCheckoutOrder/PlaceCheckoutOrderCommand.cs`
- `src/Zadana.Application/Modules/Payments/Commands/RetryCardPayment/RetryCardPaymentCommand.cs`
- `src/Zadana.Infrastructure/Services/Payments/MoyasarPaymentGateway.cs`
- `src/Zadana.Infrastructure/Settings/MoyasarSettings.cs`

## إعدادات السيرفر

القسم المستخدم في `appsettings`:

```json
{
  "Moyasar": {
    "Enabled": true,
    "BaseUrl": "https://api.moyasar.com/v1/",
    "PublishableKey": "pk_live_or_test",
    "SecretKey": "sk_live_or_test",
    "WebhookSecret": "secret-from-moyasar-dashboard",
    "CallbackUrl": "https://your-api.com/api/payments/moyasar/verify",
    "EnabledMethods": ["creditcard"],
    "SupportedNetworks": ["mada", "visa", "mastercard"],
    "Currency": "SAR"
  }
}
```

في الإنتاج يفضل تمرير القيم من environment variables أو user secrets:

- `Moyasar__Enabled=true`
- `Moyasar__PublishableKey=...`
- `Moyasar__SecretKey=...`
- `Moyasar__WebhookSecret=...`
- `Moyasar__CallbackUrl=https://your-api.com/api/payments/moyasar/verify`

## Endpoints التي يستخدمها تطبيق العميل

كل endpoints الخاصة بالعميل تحتاج:

```http
Authorization: Bearer <token>
X-Device-Id: <stable-device-id>
Accept-Language: ar
```

`X-Device-Id` مهم في الدفع لأن الباك إند يستخدمه في تنظيف الكارت بعد تأكيد الدفع.

### 1. ملخص الدفع

```http
GET /api/checkout/summary?payment_method=card
```

استخدمه قبل إتمام الطلب للتأكد أن `card` متاح داخل `payment_methods`.

### 2. إنشاء طلب بطاقة

```http
POST /api/orders
```

Body:

```json
{
  "vendor_id": "11111111-1111-1111-1111-111111111111",
  "address_id": "22222222-2222-2222-2222-222222222222",
  "delivery_slot_id": "asap",
  "payment_method": "card",
  "promo_code": null,
  "notes": null
}
```

Response عند الدفع بالبطاقة:

```json
{
  "message": "تم إنشاء الطلب بنجاح",
  "order": {
    "id": "33333333-3333-3333-3333-333333333333",
    "created_at": "2026-05-19T10:00:00Z",
    "status": "pending_payment",
    "payment_method": "card",
    "payment_status": "pending",
    "total_price": 125.0
  },
  "payment": {
    "id": "44444444-4444-4444-4444-444444444444",
    "provider": "moyasar",
    "status": "pending",
    "iframe_url": "RenderMoyasarForm",
    "provider_reference": "",
    "provider_config": {
      "publishableKey": "pk_test_or_live",
      "amount": 12500,
      "currency": "SAR",
      "description": "Order ORD-000001",
      "callbackUrl": "https://your-api.com/api/payments/moyasar/verify",
      "methods": ["creditcard"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "33333333-3333-3333-3333-333333333333",
        "payment_id": "44444444-4444-4444-4444-444444444444",
        "order_number": "ORD-000001"
      }
    }
  }
}
```

### 3. Retry Payment

```http
POST /api/orders/{orderId}/retry-payment
```

استخدمه فقط عندما يرجع الطلب:

```json
{
  "can_retry_payment": true
}
```

Response نفس شكل `payment` في إنشاء الطلب:

```json
{
  "message": "payment retry session created successfully",
  "payment": {
    "id": "55555555-5555-5555-5555-555555555555",
    "provider": "moyasar",
    "status": "pending",
    "iframe_url": "RenderMoyasarForm",
    "provider_reference": "",
    "provider_config": {
      "publishableKey": "pk_test_or_live",
      "amount": 12500,
      "currency": "SAR",
      "description": "Order ORD-000001",
      "callbackUrl": "https://your-api.com/api/payments/moyasar/verify",
      "methods": ["creditcard"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "33333333-3333-3333-3333-333333333333",
        "payment_id": "55555555-5555-5555-5555-555555555555",
        "order_number": "ORD-000001",
        "retry_of": "44444444-4444-4444-4444-444444444444"
      }
    }
  }
}
```

## Rendering Moyasar Form داخل التطبيق

لا تمرر `provider_config` كما هو مباشرة إلى `Moyasar.init` لأن الباك إند يرجعه بصيغة camelCase، بينما Moyasar Form يستخدم بعض المفاتيح بصيغة snake_case.

استخدم هذا mapping:

| Backend field | Moyasar field |
| --- | --- |
| `publishableKey` | `publishable_api_key` |
| `callbackUrl` | `callback_url` |
| `supportedNetworks` | `supported_networks` |
| `amount` | `amount` |
| `currency` | `currency` |
| `description` | `description` |
| `methods` | `methods` |
| `metadata` | `metadata` |

HTML مناسب للـ WebView:

```html
<!doctype html>
<html lang="ar" dir="rtl">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <link rel="stylesheet" href="https://cdn.moyasar.com/mpf/1.15.0/moyasar.css" />
  <script src="https://cdn.moyasar.com/mpf/1.15.0/moyasar.js"></script>
</head>
<body>
  <div class="mysr-form"></div>
  <script>
    const cfg = window.__ZADANA_MOYASAR_CONFIG__;

    function notifyNativePaymentId(providerPaymentId, source) {
      if (!providerPaymentId) return;

      const message = JSON.stringify({
        type: 'moyasar_provider_payment_id',
        providerPaymentId,
        source
      });

      window.ReactNativeWebView?.postMessage(message);
      window.webkit?.messageHandlers?.moyasar?.postMessage(message);
    }

    Moyasar.init({
      element: '.mysr-form',
      amount: cfg.amount,
      currency: cfg.currency,
      description: cfg.description,
      publishable_api_key: cfg.publishableKey,
      callback_url: cfg.callbackUrl,
      supported_networks: cfg.supportedNetworks,
      methods: cfg.methods,
      metadata: cfg.metadata,
      language: 'ar',
      on_completed: async function (payment) {
        window.__ZADANA_MOYASAR_PAYMENT_ID__ = payment.id;
        notifyNativePaymentId(payment.id, 'on_completed');
        // احفظ payment.id محليا للتشخيص فقط.
        // لا تغير حالة الطلب إلى paid من التطبيق.
      },
      on_failure: function (error) {
        // اعرض رسالة فشل مناسبة ثم اسمح للمستخدم بالمحاولة مرة أخرى.
      }
    });
  </script>
</body>
</html>
```

## تأكيد الدفع مع الباك إند

قيمة Moyasar `payment.id` هي provider payment id. هذه ليست نفس قيمة Zadana `payment.id` الموجودة في response إنشاء الطلب.

التطبيق يجب أن يستدعي Zadana بعد وصول الـ WebView إلى `callbackUrl`. ويمكنه أيضا استدعاء نفس endpoint باستخدام id المحفوظ من `on_completed(payment)` لو المستخدم أغلق WebView قبل اكتمال callback:

```http
POST /api/payments/moyasar/confirm
Content-Type: application/json
X-Device-Id: <same-device-id-used-for-checkout>
```

```json
{
  "id": "<moyasar_payment_id>"
}
```

الحقول المقبولة في body هي `id` أو `provider_payment_id` أو `providerPaymentId`.

قواعد مهمة:

- `on_completed(payment)` ممكن يحصل قبل نتيجة 3DS/callback النهائية، لذلك response التأكيد قد يظل `pending`.
- استدعِ confirm مرة أخرى بعد أن يحتوي callback navigation على `?id=<moyasar_payment_id>`، أو عند إغلاق WebView ومعك id محفوظ.
- لا تعرض نجاح الدفع إلا عندما يرجع الباك إند `"paymentStatus": "paid"`.
- بعد confirm اعمل refresh باستخدام `GET /api/orders/{orderId}` أو `GET /api/orders/active`.

لو التطبيق Flutter أو React Native:

1. ابن صفحة HTML محلية أو route داخل WebView.
2. ضع `provider_config` داخل `window.__ZADANA_MOYASAR_CONFIG__`.
3. افتح WebView.
4. راقب navigation إلى `callbackUrl`.
5. عند ظهور `.../api/payments/moyasar/verify?id=<moyasar_payment_id>` أغلق شاشة الدفع أو اتركها تكمل ثم اعمل refresh من الباك إند.

## ماذا يحدث بعد callback

Moyasar يرجع إلى:

```http
GET /api/payments/moyasar/verify?id=<moyasar_payment_id>
```

هذا endpoint لا يحتاج token لأنه callback عام. الباك إند:

1. يجلب الدفع من Moyasar باستخدام `SecretKey`.
2. يتأكد من `amount` و `currency`.
3. يتأكد من `metadata.order_id`.
4. لو الحالة paid/captured:
   - يجعل `PaymentStatus = Paid`
   - ينقل الطلب إلى `PendingVendorAcceptance`
   - ينظف الكارت
   - يرسل إشعار للتاجر والعميل
5. لو الحالة failed/voided:
   - يجعل محاولة الدفع فاشلة
   - يظل الطلب قابل للـ retry/delete حسب flags الباك إند

Response المتوقع:

```json
{
  "message": "Payment confirmed successfully",
  "paymentId": "44444444-4444-4444-4444-444444444444",
  "paymentStatus": "paid",
  "userId": "66666666-6666-6666-6666-666666666666",
  "orderId": "33333333-3333-3333-3333-333333333333",
  "orderStatus": "pending_vendor_acceptance",
  "alreadyConfirmed": false
}
```

بعد أي callback أو إغلاق WebView، التطبيق يستدعي:

```http
GET /api/orders/{orderId}
```

أو:

```http
GET /api/orders/active
```

ثم يحدث الواجهة بناء على القيم القادمة من السيرفر.

## Webhook الخاص بميسر

لا يستخدمه تطبيق العميل مباشرة.

إعداد Moyasar Dashboard:

- Endpoint: `https://your-api.com/api/payments/moyasar/webhook`
- HTTP Method: `POST`
- Secret Token: نفس قيمة `Moyasar:WebhookSecret`
- Events مفضلة:
  - `payment_paid`
  - `payment_faild`
  - `payment_refunded`
  - `payment_voided`
  - `payment_authorized`
  - `payment_captured`
  - `payment_verified`

الباك إند يسجل الـ webhook في `PaymentProviderEventInbox` ثم worker يؤكد الدفع بشكل idempotent.

## قواعد واجهة الطلبات

اعتمد على flags من الباك إند ولا تستنتج من status فقط:

- `can_retry_payment`
- `can_delete`
- `can_cancel`

### طلب بطاقة لم يتم دفعه

غالبا:

```json
{
  "status": "pending",
  "payment_status": "pending",
  "payment_method": "card",
  "can_retry_payment": true,
  "can_delete": true,
  "can_cancel": false
}
```

اعرض:

- إعادة محاولة الدفع
- حذف الطلب

لا تعرض:

- إلغاء الطلب

### طلب تم دفعه وينتظر التاجر

غالبا:

```json
{
  "status": "processing",
  "payment_status": "paid",
  "payment_method": "card",
  "can_retry_payment": false,
  "can_delete": false,
  "can_cancel": true
}
```

اعرض إلغاء الطلب فقط لو `can_cancel = true`.

## ممنوعات مهمة

- ممنوع استخدام `/api/payments/paymob/*`.
- ممنوع فتح `accept.paymob.com`.
- ممنوع تكوين payment token داخل التطبيق.
- ممنوع إرسال بيانات البطاقة إلى الباك إند.
- ممنوع تخزين `SecretKey` أو `WebhookSecret` داخل التطبيق.
- ممنوع اعتبار `on_completed` نجاح نهائي. النجاح النهائي يأتي من refresh بعد تأكيد الباك إند.
- ممنوع إنشاء order جديد عند retry. استخدم نفس `orderId`.

## أخطاء متوقعة

| Code | المعنى | تصرف التطبيق |
| --- | --- | --- |
| `PAYMENT_UNAVAILABLE` | Moyasar غير مفعل أو مفاتيحه ناقصة | اعرض رسالة وحافظ على الطلب |
| `PAYMENT_METHOD_NOT_SUPPORTED` | طريقة دفع غير مدعومة حاليا | ارجع لاختيار طريقة دفع أخرى |
| `ORDER_PAYMENT_RETRY_NOT_ALLOWED` | الطلب لم يعد يقبل retry | اعمل refresh واخفي الزر |
| `ORDER_ALREADY_PAID` | الدفع مؤكد بالفعل | اعمل refresh واخفي retry/delete |
| `PAYMENT_AMOUNT_MISMATCH` | مبلغ Moyasar لا يطابق الطلب | اعرض خطأ عام وبلغ الدعم |
| `PAYMENT_ORDER_MISMATCH` | metadata لا تطابق الطلب | اعرض خطأ عام وبلغ الدعم |

## Acceptance checklist

- إنشاء طلب `payment_method=card` يرجع `payment.provider = moyasar`.
- `payment.iframe_url` لا يستخدم كرابط.
- WebView يرسم Moyasar Form باستخدام `provider_config`.
- نجاح الدفع يجعل التطبيق يعمل refresh ولا يغير الحالة محليا.
- بعد النجاح يختفي retry/delete.
- عند فشل أو خروج المستخدم يظهر retry/delete حسب flags.
- retry payment يعمل على نفس `orderId`.
- لا يوجد أي Paymob URL أو token أو endpoint داخل تطبيق العميل.
- لا توجد مفاتيح سرية داخل تطبيق العميل.
