# تعديلات تأكيد دفع Moyasar

آخر تحديث: 2026-05-19

## المشكلة

العميل كان يدفع داخل Moyasar والبوابة تظهر أن الدفع تم، لكن في الباك إند الطلب يظل:

```json
{
  "payment_status": "pending",
  "status": "pending_payment"
}
```

السبب أن نجاح الدفع داخل Moyasar لا يكفي لتغيير حالة الطلب داخل Zadana. لازم الباك إند يجلب عملية الدفع من Moyasar باستخدام `SecretKey` ويتأكد من:

- `status`
- `amount`
- `currency`
- `metadata.order_id`

لو الـ WebView لم يكمل navigation إلى callback أو المستخدم قفل شاشة الدفع بدري، endpoint القديم `/api/payments/moyasar/verify` لا يتم استدعاؤه، وبالتالي الطلب يفضل معلق.

## الحل الجديد

تم إضافة endpoint مباشر لتأكيد الدفع من تطبيق العميل بعد ما Moyasar يرجع provider payment id:

```http
POST /api/payments/moyasar/confirm
Content-Type: application/json
X-Device-Id: <same-device-id-used-for-checkout>
```

Body:

```json
{
  "id": "<moyasar_payment_id>"
}
```

الحقول المقبولة:

- `id`
- `provider_payment_id`
- `providerPaymentId`

ممنوع إرسال `paymentId` هنا لأنه ممكن يتلخبط مع `payment.id` المحلي الخاص بـ Zadana. المطلوب هو Moyasar provider payment id فقط.

## Response المتوقع عند نجاح التأكيد

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

التطبيق يعرض نجاح الدفع فقط إذا:

```json
"paymentStatus": "paid"
```

لو رجع `pending`، التطبيق لا يعرض نجاح نهائي ويعمل retry confirm أو refresh للطلب.

## حماية إضافية ضد وصول الطلب للتاجر قبل الدفع

تم سد فجوة إضافية: لو لأي سبب تقني order اتغيرت حالته إلى `PendingVendorAcceptance` بينما دفع البطاقة ما زال غير مؤكد، الطلب لا يظهر للتاجر ولا يقدر التاجر يتعامل معه.

القواعد الجديدة:

- طلبات `Card` لا تظهر في `GET /api/vendor/orders` إلا إذا كان `PaymentStatus`:
  - `Paid`
  - `Refunded`
  - `PartiallyRefunded`
- `GET /api/vendor/orders/{orderId}` يرجع `404` للتاجر لو الطلب بطاقة والدفع غير مؤكد.
- endpoints التاجر مثل accept/reject/preparing/ready ترفض الطلب بطاقة غير مدفوع بكود:

```json
{
  "code": "ORDER_PAYMENT_NOT_CONFIRMED"
}
```

- أي vendor notification يتم تجاهله لو الطلب بطاقة والدفع لم يتأكد بعد.

الهدف: حتى لو حصل race condition أو status اتقدمت بالغلط، fulfillment عند التاجر لا يبدأ قبل تأكيد الدفع من Zadana.

## Automatic Bank Transfer برقم الحساب

تم تعديل فلو `payment_method = bank` ليكون جاهزا للتأكيد التلقائي بدل رفع إثبات يدوي فقط.

### إعدادات السيرفر

أضف بيانات حساب المنصة في `BankTransfer`:

```json
{
  "BankTransfer": {
    "Enabled": true,
    "ProviderName": "BankTransfer",
    "BankName": "Your Bank",
    "AccountHolderName": "Zadana Platform",
    "Iban": "SA...",
    "AccountNumber": "...",
    "WebhookSecret": "secret-from-bank-provider",
    "ExpirationMinutes": 1440
  }
}
```

### إنشاء طلب bank

عند إنشاء طلب بطريقة `bank`، الطلب يظل:

```json
{
  "status": "pending",
  "payment_status": "pending"
}
```

وفي قاعدة البيانات يكون `OrderStatus = PendingBankConfirmation`، ولا يظهر للتاجر.

Response يحتوي `payment.provider_config` لعرض بيانات التحويل للعميل:

```json
{
  "payment": {
    "provider": "banktransfer",
    "status": "pending",
    "iframe_url": "ShowBankTransferInstructions",
    "provider_reference": "ZDN...",
    "provider_config": {
      "bankName": "Your Bank",
      "accountHolderName": "Zadana Platform",
      "iban": "SA...",
      "accountNumber": "...",
      "reference": "ZDN...",
      "amount": 125.0,
      "currency": "SAR",
      "expiresAtUtc": "2026-05-20T10:00:00Z",
      "webhookDriven": true
    }
  }
}
```

### تأكيد التحويل تلقائيا

مزود البنك أو Virtual IBAN provider يستدعي:

```http
POST /api/payments/bank-transfer/webhook
X-BankTransfer-Secret: <BankTransfer:WebhookSecret>
Content-Type: application/json
```

```json
{
  "reference": "ZDN...",
  "transactionId": "bank-transaction-id",
  "amount": 125.0,
  "currency": "SAR",
  "status": "paid",
  "paidAtUtc": "2026-05-19T10:10:00Z"
}
```

الحالات المقبولة كنجاح:

- `paid`
- `settled`
- `confirmed`
- `completed`

بعد webhook ناجح:

- `PaymentStatus = Paid`
- `OrderStatus = PendingVendorAcceptance`
- يتم posting للـ ledger
- يتبعت الطلب للتاجر

ملاحظة مهمة: هذا الفلو جاهز لاستقبال webhook من مزود حقيقي. لا يوجد خصم تلقائي من حساب العميل بمجرد كتابة رقم الحساب؛ لازم بنك أو مزود Virtual IBAN/Open Banking يرسل إشعار التحويل الوارد.

## Flow الموبايل المطلوب

### 1. إنشاء الطلب

```http
POST /api/orders
```

الدفع بالبطاقة يرجع:

```json
{
  "payment": {
    "id": "zadana-local-payment-id",
    "provider": "moyasar",
    "status": "pending",
    "iframe_url": "RenderMoyasarForm",
    "provider_config": {
      "publishableKey": "pk_test_or_live",
      "amount": 12500,
      "currency": "SAR",
      "description": "Order ORD-000001",
      "callbackUrl": "https://your-api.com/api/payments/moyasar/verify",
      "methods": ["creditcard"],
      "supportedNetworks": ["mada", "visa", "mastercard"],
      "metadata": {
        "order_id": "order-id",
        "payment_id": "zadana-local-payment-id",
        "order_number": "ORD-000001"
      }
    }
  }
}
```

`payment.id` هنا هو id داخلي في Zadana، وليس Moyasar payment id.

### 2. رسم Moyasar Form

استخدم `provider_config` مع mapping التالي:

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

### 3. حفظ Moyasar payment id

داخل `on_completed(payment)`:

```js
on_completed: async function (payment) {
  const moyasarPaymentId = payment.id;

  // احفظه مؤقتا واستخدمه في confirm
  // لا تعتبر الطلب مدفوعا من هنا
}
```

مهم: `on_completed(payment)` ممكن يحصل قبل نتيجة 3DS/callback النهائية، لذلك هو ليس نجاح نهائي.

### 4. تأكيد الدفع مع الباك إند

بعد `on_completed(payment)` أو بعد وصول الـ WebView إلى:

```text
/api/payments/moyasar/verify?id=<moyasar_payment_id>
```

استدع:

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

### 5. Refresh الطلب

بعد confirm:

```http
GET /api/orders/{orderId}
```

أو:

```http
GET /api/orders/active
```

واستخدم القيم القادمة من السيرفر:

- `payment_status`
- `status`
- `can_retry_payment`
- `can_delete`
- `can_cancel`

## قواعد مهمة لتطبيق العميل

- لا تعتبر `on_completed(payment)` نجاح نهائي.
- لا تغير حالة الطلب محليا إلى paid.
- لا تعرض success إلا بعد رجوع `paymentStatus = "paid"` من Zadana.
- لا تستخدم `payment.id` المحلي في confirm.
- استخدم Moyasar `payment.id` فقط في confirm.
- بعد أي payment action اعمل refresh للطلب من الباك إند.
- لو المستخدم أغلق WebView ومعك Moyasar payment id، استدع confirm ثم refresh.
- لو لا يوجد Moyasar payment id، اعمل refresh فقط واترك retry/delete حسب flags.

## تعديلات الباك إند

تم تعديل:

- `MoyasarPaymentsController`
  - إضافة `POST /api/payments/moyasar/confirm`
  - توحيد confirm logic بين `verify` و `confirm`
  - webhook يحاول يعمل inline confirmation لو payload فيه provider payment id

- `ProcessPaymentWebhookCommand`
  - يرجع `ProviderPaymentId` في `PaymentWebhookProcessResultDto`

- `CardCheckoutDtos`
  - إضافة `ProviderPaymentId` اختياري في نتيجة webhook

- Tests
  - إضافة tests لقبول aliases:
    - `id`
    - `provider_payment_id`
    - `providerPaymentId`

## Verification

تم تشغيل:

```powershell
dotnet build src\Zadana.Api\Zadana.Api.csproj --no-restore -v minimal -p:UseSharedCompilation=false
```

النتيجة:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

وتم تشغيل:

```powershell
dotnet test tests\Zadana.UnitTests\Zadana.UnitTests.csproj --no-restore --filter "FullyQualifiedName~MoyasarWebhookTests|FullyQualifiedName~ConfirmCardPaymentCommandHandlerTests|FullyQualifiedName~MoyasarPaymentGateway" -p:UseSharedCompilation=false
```

النتيجة:

```text
Passed: 16
Failed: 0
```

## Rollout checklist

- Deploy آخر backend على production.
- تطبيق العميل يضيف نداء `/api/payments/moyasar/confirm`.
- تطبيق العميل يرسل Moyasar provider payment id فقط.
- تطبيق العميل يعمل refresh للطلب بعد confirm.
- Moyasar Dashboard webhook يظل مفعل على:

```text
https://your-api.com/api/payments/moyasar/webhook
```

- `Moyasar:WebhookSecret` في production يطابق secret الموجود في Moyasar Dashboard.
