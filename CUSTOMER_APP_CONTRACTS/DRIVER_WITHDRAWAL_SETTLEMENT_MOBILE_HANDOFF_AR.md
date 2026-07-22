# تسويات وسحب المندوب — Handoff لتطبيق الموبايل

## الحالة

- الباك إند: `implemented`
- المطلوب من تطبيق المندوب: تنفيذ واجهة اختيار يوم التحويل، إنشاء طلب السحب الآمن، عرض الحالات، وإلغاء الطلب قبل بدء معالجة المالية، **بالإضافة إلى عرض COD المستحق ومزامنة حالة التشغيل عند تجاوز حد الدين**.
- التحويل البنكي ورفع الإثبات وتسجيل المرتجع تتم من لوحة الإدارة فقط، وليست ضمن تطبيق المندوب.

## القواعد الوظيفية

1. المندوب يختار يوم تحويل من الأيام التي تفعلها الإدارة ديناميكيًا.
2. لا تعرض قائمة ثابتة مثل الاثنين والخميس؛ استخدم دائمًا `availablePayoutDays` القادمة من الخادم.
3. يسمح بطلب سحب نشط واحد فقط بحالة `Pending` أو `Processing`.
4. الخادم يثبت يوم التحويل وطريقة السحب داخل الطلب وقت إنشائه؛ تعديل التفضيل أو الحساب لاحقًا لا يغير الطلب الحالي.
5. يمكن للمندوب إلغاء الطلب فقط وهو `Pending` وقبل أن تبدأ الإدارة معالجته.
6. لا يعتبر الطلب مدفوعًا إلا عندما تصبح حالته `Paid`.
7. الحالة `Returned` تعني أن البنك أعاد الحوالة وأن المبلغ عاد إلى رصيد المحفظة.
8. **COD:** إذا كان `codOwedBalance > 0` لا يمكن إنشاء طلب سحب (`DRIVER_COD_DEBT_NOT_SETTLED`). تسوية COD من الإدارة فقط — التطبيق يعرض المبلغ ويوجّه المندوب دون تنفيذ تسوية من الموبايل.
9. **حظر استقبال الطلبات:** عند تجاوز `codOwedBalance` للحد المسموح (افتراضيًا 500 ر.س من إعدادات المنصة) يمنع الخادم استقبال العروض/التفعيل؛ اعرض `RestrictionMessageAr` من حالة التشغيل ولا تعتمد على حساب محلي.

## 1. شاشة المحفظة

### ملخص المحفظة

`GET /api/drivers/wallet`

استخدم القيم القادمة من الخادم كما هي، خصوصًا:

- `currentBalance`
- `availableToWithdraw` (حاليًا يساوي `netWithdrawable` من الخادم)
- `pendingBalance` (يشمل حجوزات السحب النشطة)
- `codOwedBalance`
- `netWithdrawable`
- `todayEarnings`, `weekEarnings`, `monthEarnings`
- `recentTransactions` (آخر 10 معاملات — للمعاينة السريعة فقط)
- `withdrawalSummary` → `{ pendingCount, pendingAmount, totalRequests }`
- `paymentMethods`
- `payoutDay`

لا تحسب الرصيد المتاح محليًا؛ `netWithdrawable` هو مصدر الحقيقة لحد السحب.

### مثال استجابة ملخص المحفظة

```json
{
  "currentBalance": 1250.50,
  "availableToWithdraw": 700.50,
  "pendingBalance": 250.00,
  "codOwedBalance": 300.00,
  "netWithdrawable": 700.50,
  "todayEarnings": 85.00,
  "weekEarnings": 420.00,
  "monthEarnings": 1800.00,
  "recentTransactions": [],
  "paymentMethods": [],
  "withdrawalSummary": {
    "pendingCount": 1,
    "pendingAmount": 250.00,
    "totalRequests": 4
  },
  "payoutDay": "Thursday"
}
```

### عرض COD في الواجهة

- اعرض `codOwedBalance` كبطاقة/تنبيه منفصل في شاشة المحفظة.
- إذا `codOwedBalance > 0`: عطّل نموذج السحب مسبقًا واعرض أن تسوية COD مطلوبة قبل السحب.
- **لا يوجد endpoint للمندوب لتسجيل تسوية COD**؛ التسوية تتم من الإدارة (`cod-remittance`). الموبايل يعرض المبلغ ورسالة توجيه فقط (مثل: «تواصل مع الإدارة لتسليم المبالغ» أو حسب سياسة التشغيل).

### سجل المعاملات (pagination)

`GET /api/drivers/wallet/transactions?page=1&pageSize=20`

```json
{
  "items": [
    {
      "id": "guid",
      "type": "Credit",
      "direction": "IN",
      "amount": 50.00,
      "description": "Driver payout for order ...",
      "referenceType": "JournalLine",
      "referenceId": "guid",
      "createdAtUtc": "2026-07-22T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42
}
```

- استخدم `recentTransactions` من ملخص المحفظة للبطاقة السريعة، و`transactions` للشاشة الكاملة مع pagination.
- لا تفسّر `type`/`direction` محليًا بقيم ثابتة؛ اعرض `description` و`amount` مع locale مناسب.

### إعدادات إنشاء طلب السحب

استدعِ هذا endpoint عند فتح نموذج السحب وقبل كل محاولة إرسال:

`GET /api/drivers/wallet/withdrawal-settings`

```json
{
  "minimumAmount": 10,
  "maximumAmount": 50000,
  "maximumRequestsPerDay": 3,
  "requestsCreatedToday": 1,
  "hasActiveWithdrawal": false,
  "currencyCode": "SAR",
  "payoutDay": "Thursday",
  "availablePayoutDays": ["Monday", "Thursday"]
}
```

سلوك الواجهة:

- تحقق أن المبلغ بين `minimumAmount` و`maximumAmount`.
- عطّل زر إرسال الطلب إذا كانت `hasActiveWithdrawal = true`.
- عطّل الزر إذا كان `requestsCreatedToday >= maximumRequestsPerDay`.
- اعرض العملة من `currencyCode`.
- هذه القيود لتحسين التجربة فقط؛ تعامل دائمًا مع رفض الخادم لأنه مصدر الحقيقة.

## 2. اختيار يوم التحويل

### قراءة التفضيل

`GET /api/drivers/wallet/payout-preference`

```json
{
  "payoutDay": "Thursday",
  "availablePayoutDays": ["Monday", "Thursday"]
}
```

### تعديل التفضيل

`PUT /api/drivers/wallet/payout-preference`

```json
{
  "payoutDay": "Monday"
}
```

قيم الأيام المدعومة:

```text
Sunday
Monday
Tuesday
Wednesday
Thursday
Friday
Saturday
```

المطلوب في الواجهة:

- اعرض ترجمة عربية لاسم اليوم، لكن أرسل القيمة الإنجليزية كما استلمتها.
- اعرض فقط الأيام الموجودة في `availablePayoutDays`.
- بعد نجاح التعديل، استبدل البيانات المحلية بالاستجابة الجديدة.
- عند `PAYOUT_DAY_DISABLED` أعد استدعاء GET لأن الإدارة ربما عدلت الأيام أثناء فتح الشاشة.

## 3. إنشاء طلب سحب

`POST /api/drivers/wallet/withdrawals`

```json
{
  "paymentMethodId": "b7d50fe9-99f9-4fdf-b315-205b765fdb65",
  "amount": 250,
  "idempotencyKey": "mobile-0d8747ca-34b5-40f0-a70f-b0578d998ceb"
}
```

### Idempotency إلزامي في التطبيق

- أنشئ UUID جديدًا عندما يضغط المستخدم «تأكيد طلب السحب» لأول مرة.
- احتفظ بنفس UUID عند إعادة المحاولة بسبب timeout أو انقطاع الشبكة.
- لا تنشئ UUID جديدًا إلا عندما يبدأ المستخدم طلبًا جديدًا فعليًا.
- يمكن إرسال المفتاح في `idempotencyKey` داخل الجسم كما في المثال، أو في header باسم `Idempotency-Key`.
- يفضل استخدام الجسم لتوحيد التنفيذ بين Android وiOS.

النتيجة:

- تكرار نفس المفتاح مع نفس المبلغ والطريقة يعيد نفس الطلب ولا ينشئ حجزًا ثانيًا.
- استخدام نفس المفتاح مع مبلغ أو طريقة مختلفة يرجع `WITHDRAWAL_IDEMPOTENCY_KEY_REUSED`.

### الاستجابة

```json
{
  "id": "2e30501c-0e13-48d5-889d-7bbaf77dcf90",
  "amount": 250,
  "status": "Pending",
  "transferReference": null,
  "failureReason": null,
  "createdAtUtc": "2026-07-22T11:30:00Z",
  "processedAtUtc": null,
  "paymentMethod": {
    "id": "b7d50fe9-99f9-4fdf-b315-205b765fdb65",
    "type": "BankAccount",
    "accountHolderName": "اسم صاحب الحساب",
    "providerName": "اسم البنك",
    "maskedLabel": "اسم البنك ****9012",
    "isPrimary": true,
    "isVerified": true
  },
  "payoutId": null,
  "providerName": null,
  "providerTransferId": null,
  "payoutDay": "Thursday"
}
```

بعد النجاح:

- امسح `idempotencyKey` المؤقت فقط بعد حفظ `id` الخاص بالطلب.
- حدّث ملخص المحفظة وقائمة طلبات السحب.
- اعرض أن التحويل سيتم في `payoutDay` المخزن داخل الطلب.

## 4. قائمة طلبات السحب

`GET /api/drivers/wallet/withdrawals?page=1&pageSize=20`

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0
}
```

دعم pagination وعدم افتراض أن كل الطلبات ترجع في أول صفحة.

### الحالات وترجمتها المقترحة

| قيمة API | النص العربي | سلوك الواجهة |
|---|---|---|
| `Pending` | بانتظار المراجعة | إظهار زر الإلغاء |
| `Processing` | جاري التحويل | إخفاء الإلغاء |
| `Paid` | تم التحويل | عرض مرجع التحويل إن وجد |
| `Failed` | تعذر التحويل | عرض `failureReason` إن وجد |
| `Cancelled` | ملغي | حالة نهائية |
| `Returned` | مرتجع من البنك | توضيح أن المبلغ عاد للمحفظة وعرض السبب |

لا تعرض `providerTransferId` كاملًا إذا لم يكن مطلوبًا للمستخدم. مرجع المستخدم الأساسي هو `transferReference`.

## 5. إلغاء طلب السحب

`POST /api/drivers/wallet/withdrawals/{withdrawalId}/cancel`

لا يحتاج request body.

سلوك الواجهة:

- اعرض الزر فقط عندما تكون الحالة `Pending`.
- اطلب تأكيد المستخدم قبل الإلغاء.
- عطّل الزر أثناء الطلب لمنع الضغط المكرر.
- الاستدعاء idempotent إذا كان الطلب أصبح `Cancelled` بالفعل.
- بعد النجاح حدّث المحفظة والقائمة من الخادم.
- عند `DRIVER_WITHDRAWAL_CANNOT_CANCEL` أعد تحميل الطلب؛ غالبًا بدأت الإدارة معالجته.

## 6. طرق السحب

استخدم طرق السحب الموجودة في ملخص المحفظة أو:

`GET /api/drivers/wallet/payment-methods`

طلبات السحب الحالية تدعم **حساب بنكي سعودي (IBAN)** فقط. النوع المدعوم في API: `BankAccount`. لا ترسل أو تعرض رقم الحساب كاملًا بعد الحفظ؛ استخدم `maskedLabel`.

إذا لم توجد طريقة أساسية (`isPrimary = true`) **ومُعتمدة** (`isVerified = true`)، وجّه المندوب إلى إضافة/اعتماد طريقة السحب قبل فتح نموذج الطلب.

### إنشاء طريقة سحب

`POST /api/drivers/wallet/payment-methods`

```json
{
  "type": "BankAccount",
  "accountHolderName": "اسم صاحب الحساب",
  "accountIdentifier": "SA0380000000608010167519",
  "providerName": "البنك الأهلي",
  "isPrimary": true
}
```

- `accountIdentifier` يجب أن يكون IBAN سعودي صالح (24 حرفًا يبدأ بـ `SA`).
- **الاستجابة `202 Accepted`** — الطلب يدخل **موافقة إدارية** ولا يُفعَّل فورًا:

```json
{
  "approvalRequestId": "guid",
  "message": "..."
}
```

اعرض للمستخدم «بانتظار موافقة الإدارة» ولا تعتبر الطريقة جاهزة للسحب حتى تظهر في `GET /wallet` أو `GET /payment-methods` بـ `isVerified: true`.

### تعديل طريقة

`PUT /api/drivers/wallet/payment-methods/{id}`

```json
{
  "type": "BankAccount",
  "accountHolderName": "اسم صاحب الحساب",
  "accountIdentifier": "SA0380000000608010167519",
  "providerName": "البنك الأهلي"
}
```

يرجع أيضًا **`202 Accepted`** (موافقة إدارية).

### حذف طريقة

`DELETE /api/drivers/wallet/payment-methods/{id}`

- يرجع **`202 Accepted`** إذا لم تكن مرتبطة بسجل سحب.
- إذا مرتبطة بطلبات سحب: `DRIVER_PAYOUT_METHOD_IN_USE`.

### تعيين أساسي

`POST /api/drivers/wallet/payment-methods/{id}/make-primary`

- يرجع **`202 Accepted`** (موافقة إدارية).

### DTO طريقة السحب

```json
{
  "id": "guid",
  "type": "BankAccount",
  "accountHolderName": "اسم صاحب الحساب",
  "providerName": "البنك الأهلي",
  "maskedLabel": "البنك الأهلي ****7519",
  "isPrimary": true,
  "isVerified": true
}
```

- لا تعرض `accountIdentifier` بعد الحفظ.
- `isVerified = false` → لا تستخدم الطريقة في نموذج السحب حتى الاعتماد.

## 7. الأخطاء التي يجب معالجتها

الخادم يعيد `errorCode` داخل Problem Details في أغلب أخطاء `400` و`409`. بعض أخطاء التحقق المباشرة قد تأتي في `error`.

اقرأ الكود بهذا الترتيب:

```text
response.errorCode ?? response.error ?? response.code
```

| الكود | رسالة/إجراء التطبيق |
|---|---|
| `INVALID_WITHDRAWAL_AMOUNT` | اعرض الحدود الجديدة بعد إعادة قراءة `withdrawal-settings` |
| `DRIVER_DAILY_WITHDRAWAL_LIMIT_REACHED` | تم الوصول للحد اليومي |
| `DRIVER_ACTIVE_WITHDRAWAL_EXISTS` | يوجد طلب سحب قيد المراجعة أو التحويل |
| `WITHDRAWAL_IDEMPOTENCY_KEY_REUSED` | أنشئ مفتاحًا جديدًا فقط لطلب جديد، وليس لإعادة محاولة نفس الطلب |
| `DRIVER_PAYOUT_METHOD_REQUIRED` | يجب إضافة طريقة سحب أساسية |
| `DRIVER_PAYOUT_METHOD_NOT_FOUND` | الطريقة حُذفت أو لا تخص المندوب؛ أعد تحميل الطرق |
| `DRIVER_BANK_ACCOUNT_REQUIRED` | طريقة السحب يجب أن تكون حسابًا بنكيًا |
| `DRIVER_BANK_IBAN_INVALID` | بيانات الآيبان تحتاج إلى تعديل واعتماد |
| `INSUFFICIENT_WITHDRAWABLE_BALANCE` | أعد تحميل رصيد المحفظة |
| `DRIVER_COD_DEBT_NOT_SETTLED` | `codOwedBalance > 0` — اعرض المبلغ ووجّه لتسوية COD (من الإدارة) قبل السحب |
| `DRIVER_COD_BLOCKED` | تجاوز حد COD — امنع التفعيل/قبول العروض واعرض `RestrictionMessageAr` من حالة التشغيل |
| `PAYOUT_DAY_DISABLED` | أعد تحميل الأيام المتاحة |
| `INVALID_PAYOUT_DAY` | يوم غير صالح — استخدم قيم الأسبوع الإنجليزية فقط |
| `DRIVER_WITHDRAWAL_CANNOT_CANCEL` | الطلب دخل معالجة المالية ولا يمكن إلغاؤه |
| `WITHDRAWAL_IDEMPOTENCY_KEY_TOO_LONG` | المفتاح أطول من 160 حرفًا — أنشئ UUID قياسي |
| `DRIVER_PAYOUT_METHOD_IN_USE` | لا يمكن حذف طريقة مرتبطة بسحوبات |
| `INVALID_DRIVER_PAYOUT_METHOD_TYPE` | النوع غير مدعوم — استخدم `BankAccount` |
| `INVALID_ACCOUNT_HOLDER_NAME` / `INVALID_ACCOUNT_IDENTIFIER` | حقول مطلوبة أو IBAN غير صالح |
| `DRIVER_WALLET_ACCESS_BLOCKED` | المحفظة غير متاحة لهذا الحساب |
| `RATE_LIMIT_EXCEEDED` | امنع المحاولات السريعة واعرض إعادة المحاولة لاحقًا |

عند `401` أعد تسجيل الدخول، وعند `403` لا تعرض رسالة شبكة؛ اعرض أن الحساب غير مخول للعملية.

## 8. التحديثات الفورية والإشعارات

يدعم الباك إند إشعارات تحديث المحفظة بالعربية والإنجليزية (Inbox + Push + SignalR). عند وصول Push/Notification من النوع:

```text
driver_wallet_updated
```

وفي اتصال SignalR اسم الرسالة هو:

```text
ReceiveDriverWalletUpdated
```

### أحداث السحب والتسوية

| `event` / `eventName` | متى يُرسل | عنوان عربي | عنوان إنجليزي |
|---|---|---|---|
| `wallet.withdrawal_submitted` | بعد `POST /withdrawals` | استلمنا طلب السحب | Withdrawal request submitted |
| `wallet.withdrawal_cancelled` | بعد `POST /withdrawals/{id}/cancel` | ألغينا طلب السحب | Withdrawal cancelled |
| `wallet.withdrawal_processing` | بعد موافقة الإدارة وبدء التحويل | جاري تحويل السحب | Withdrawal transfer started |
| `wallet.withdrawal_paid` | بعد اكتمال التحويل البنكي | حوّلنا مبلغ السحب | Withdrawal paid |
| `wallet.withdrawal_failed` | عند فشل التحويل البنكي | فشل تحويل السحب | Withdrawal transfer failed |
| `wallet.withdrawal_returned` | عند إرجاع الحوالة من البنك | تم إرجاع الحوالة البنكية | Bank transfer returned |
| `wallet.withdrawal_rejected` | عند رفض الإدارة للطلب | رفضنا طلب السحب | Withdrawal rejected |
| `wallet.admin_adjustment` | عند تعديل رصيد المحفظة من الإدارة | عدّلنا رصيد المحفظة | Wallet balance adjusted |

> **ملاحظة:** `wallet.withdrawal_paid` و`wallet.withdrawal_failed` و`wallet.withdrawal_returned` تُرسل من مسار التحويل البنكي (Orchestrator). `wallet.withdrawal_processing` و`wallet.withdrawal_rejected` تُرسل من قرار الإدارة.

### حقول payload المشتركة

في payload الإشعار/SignalR قد تجد:

- `screen`: `"wallet"` — افتح شاشة المحفظة أو حدّثها
- `event` / `eventName`: أحد الأحداث أعلاه
- `targetUrl`: مسار التنقل عند الضغط (Inbox/Push)
- `withdrawalId`: معرف الطلب لتحديث التفاصيل (موجود في كل أحداث السحب)
- `amount`, `status`: قيمة الطلب وحالته
- `transferReference`: مرجع التحويل عند الدفع
- `failureReason`: سبب الرفض/الفشل عند `rejected` / `failed` / `returned`
- `payoutId`: معرف التسوية البنكية عند `processing` / `paid` / `returned`
- `popupType`: `driver_wallet_updated`

نفّذ refresh صامتًا لملخص المحفظة والطلب المعني بدل تعديل الأرصدة محليًا.

### سلوك الواجهة حسب الحدث

- في `wallet.withdrawal_cancelled`: حدّث الطلب إلى `Cancelled` وأزل hold من ملخص المحفظة بعد refresh.
- في `wallet.withdrawal_returned`: اعرض تنبيهًا واضحًا بأن المبلغ عاد للمحفظة وأن بيانات الحساب البنكي تحتاج للمراجعة قبل طلب جديد.
- في `wallet.withdrawal_rejected` أو `failed`: اعرض `failureReason` بعد إعادة تحميل الطلب.
- في `wallet.withdrawal_paid`: اعرض `transferReference` إن وُجد.

### نص Push/Inbox

كل إشعار يحمل:

- `titleAr` / `titleEn`
- `bodyAr` / `bodyEn`

التطبيق يعرض النص حسب لغة المستخدم؛ لا تعتمد على نص Push وحده بدون `event` و`withdrawalId`.

### التنقل عند الضغط على الإشعار

| `targetUrl` | السلوك المطلوب |
|---|---|
| `/wallet/withdrawals/{withdrawalId}` | افتح شاشة/مودال تفاصيل طلب السحب وحدّث حالته من الخادم |
| `/wallet` | افتح ملخص المحفظة (مثل `wallet.admin_adjustment`) |

قواعد التطبيق:

1. عند فتح Inbox أو Push: اقرأ `targetUrl` أولًا، ثم `withdrawalId` كاحتياط.
2. إذا `targetUrl` يبدأ بـ `/wallet/withdrawals/` → navigates to withdrawal detail route.
3. إذا وصل `ReceiveDriverWalletUpdated` بدون فتح شاشة → refresh صامت فقط.
4. لا تعدّل الأرصدة محليًا؛ أعد تحميل المحفظة/الطلب بعد التنقل.

## 9. COD ومزامنة حالة التشغيل

هذا القسم يكمّل المحفظة ولا يضيف endpoints سحب جديدة.

### مصادر حالة COD

| Endpoint | الاستخدام |
|---|---|
| `GET /api/drivers/wallet` | `codOwedBalance`, `netWithdrawable` |
| `GET /api/drivers/me/status` | `operationalStatus.canReceiveOffers`, `canGoAvailable`, `restrictionMessageAr` |
| `GET /api/drivers/home` | `operationalStatus` + الشاشة الرئيسية |
| `PUT /api/drivers/me/availability` | عند تفعيل «متاح» مع COD فوق الحد → `DRIVER_COD_BLOCKED` |

### سلوك الواجهة عند حظر COD

- إذا `canReceiveOffers = false` و`restrictionMessageAr` يذكر COD: اعرض banner على الرئيسية والمحفظة.
- عطّل زر «متاح للعمل» أو اعرض سبب الرفض من الرسالة العربية.
- عند محاولة قبول عرض توصيل COD والرفض بـ `DRIVER_COD_BLOCKED`: اعرض نفس الرسالة ووجّه للمحفظة.
- **لا تحسب الحظر محليًا**؛ الخادم يقارن `codOwedBalance` بحد المنصة (افتراضي 500 ر.س).

### الفرق بين COD للسحب وCOD للتشغيل

| الحالة | الشرط | التأثير |
|---|---|---|
| منع السحب | `codOwedBalance > 0` | `DRIVER_COD_DEBT_NOT_SETTLED` |
| حظر الطلبات | `codOwedBalance >= threshold` | `DRIVER_COD_BLOCKED` + `canReceiveOffers = false` |

قد يكون للمندوب COD مستحق أقل من الحد: يسمح باستقبال الطلبات لكن **لا يسمح بالسحب** حتى تصفير `codOwedBalance`.

## 10. حالات التحميل ومنع التكرار في الواجهة

- استخدم loading مستقل لكل عملية: تحميل المحفظة، إرسال الطلب، تعديل اليوم، والإلغاء.
- امنع double tap على أزرار mutations.
- لا تعتبر timeout فشلًا نهائيًا في إنشاء السحب؛ أعد المحاولة بنفس idempotency key.
- لا تخصم الرصيد محليًا بعد إنشاء الطلب؛ أعد قراءة المحفظة من الخادم.
- لا تحفظ `availablePayoutDays` كإعداد دائم؛ أعد تحميلها عند فتح الشاشة.

## 11. اختبارات القبول المطلوبة للموبايل

1. يعرض التطبيق الأيام التي ترجع من API فقط.
2. تعديل يوم التحويل ينعكس بعد إعادة فتح الشاشة.
3. إرسال نفس طلب السحب مرتين بنفس المفتاح يعرض طلبًا واحدًا فقط.
4. timeout ثم retry بنفس المفتاح لا ينشئ طلبًا ثانيًا.
5. يمنع التطبيق إنشاء طلب جديد مع وجود `Pending` أو `Processing`، ويتعامل أيضًا مع رفض الخادم.
6. إلغاء طلب `Pending` ينجح ويحدّث الرصيد والقائمة، ويستقبل Push/Inbox بـ `wallet.withdrawal_cancelled`.
7. محاولة إلغاء `Processing` تعيد تحميل الحالة وتخفي زر الإلغاء.
8. ظهور `Paid` يعرض مرجع التحويل.
9. ظهور `Returned` يعرض سبب المرتجع ويؤكد رجوع المبلغ للمحفظة.
10. تغيير الإدارة للأيام أثناء فتح الشاشة يعالج `PAYOUT_DAY_DISABLED` ويعيد تحميل القائمة.
11. التطبيق يعرض رسائل صحيحة عند الرصيد غير الكافي أو وجود COD مستحق.
12. جميع المبالغ تعرض بعملة `SAR` وبصيغة locale مناسبة دون تغيير القيمة المرسلة للـ API.
13. إضافة/تعديل/حذف/تعيين أساسي لطريقة سحب يعرض «بانتظار الموافقة» عند `202 Accepted` ولا يفعّل السحب قبل `isVerified`.
14. `codOwedBalance > 0` يمنع فتح نموذج السحب ويعرض رسالة `DRIVER_COD_DEBT_NOT_SETTLED` عند المحاولة.
15. `codOwedBalance >= threshold` يعطل استقبال العروض/التفعيل ويعرض `restrictionMessageAr`.
16. Push/SignalR بـ `wallet.withdrawal_*` يحدّث المحفظة والطلب دون تعديل أرصدة محليًا.
17. سجل المعاملات يدعم pagination ولا يعتمد على `recentTransactions` فقط.

## 12. خارج نطاق تطبيق المندوب

لا تنفذ في تطبيق المندوب أيًا من الآتي:

- اعتماد أو رفض الطلب.
- حجز التحويل لأدمن.
- تسجيل إرسال الحوالة في البنك.
- رفع إثبات التحويل أو إثبات المرتجع.
- تأكيد الدفع أو تسجيل المرجع البنكي من جهة المندوب.
- تشغيل أو إعادة محاولة بوابة الدفع.
- **تسجيل تسوية COD (cod-remittance)** — تتم من لوحة الإدارة فقط.

هذه العمليات محمية بصلاحيات الإدارة المالية وتنفذ من لوحة الإدارة فقط.

## 13. مرجع سريع — Endpoints

| Method | Path | الغرض |
|---|---|---|
| GET | `/api/drivers/wallet` | ملخص المحفظة |
| GET | `/api/drivers/wallet/transactions` | سجل المعاملات |
| GET | `/api/drivers/wallet/withdrawal-settings` | قيود نموذج السحب |
| GET/PUT | `/api/drivers/wallet/payout-preference` | يوم التحويل |
| GET/POST/PUT/DELETE | `/api/drivers/wallet/payment-methods` | طرق السحب |
| POST | `/api/drivers/wallet/payment-methods/{id}/make-primary` | تعيين أساسي |
| POST | `/api/drivers/wallet/withdrawals` | إنشاء سحب |
| GET | `/api/drivers/wallet/withdrawals` | قائمة السحوبات |
| POST | `/api/drivers/wallet/withdrawals/{id}/cancel` | إلغاء |
| GET | `/api/drivers/me/status` | حالة التشغيل + COD block |
| GET | `/api/drivers/home` | الرئيسية + operationalStatus |
| PUT | `/api/drivers/me/availability` | تفعيل/إيقاف التوفر |
