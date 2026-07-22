# تسويات وسحب المندوب — Handoff لتطبيق الموبايل

## الحالة

- الباك إند: `implemented`
- المطلوب من تطبيق المندوب: تنفيذ واجهة اختيار يوم التحويل، إنشاء طلب السحب الآمن، عرض الحالات، وإلغاء الطلب قبل بدء معالجة المالية.
- التحويل البنكي ورفع الإثبات وتسجيل المرتجع تتم من لوحة الإدارة فقط، وليست ضمن تطبيق المندوب.

## القواعد الوظيفية

1. المندوب يختار يوم تحويل من الأيام التي تفعلها الإدارة ديناميكيًا.
2. لا تعرض قائمة ثابتة مثل الاثنين والخميس؛ استخدم دائمًا `availablePayoutDays` القادمة من الخادم.
3. يسمح بطلب سحب نشط واحد فقط بحالة `Pending` أو `Processing`.
4. الخادم يثبت يوم التحويل وطريقة السحب داخل الطلب وقت إنشائه؛ تعديل التفضيل أو الحساب لاحقًا لا يغير الطلب الحالي.
5. يمكن للمندوب إلغاء الطلب فقط وهو `Pending` وقبل أن تبدأ الإدارة معالجته.
6. لا يعتبر الطلب مدفوعًا إلا عندما تصبح حالته `Paid`.
7. الحالة `Returned` تعني أن البنك أعاد الحوالة وأن المبلغ عاد إلى رصيد المحفظة.

## 1. شاشة المحفظة

### ملخص المحفظة

`GET /api/drivers/wallet`

استخدم القيم القادمة من الخادم كما هي، خصوصًا:

- `currentBalance`
- `availableToWithdraw`
- `pendingBalance`
- `codOwedBalance`
- `netWithdrawable`
- `withdrawalSummary`
- `paymentMethods`
- `payoutDay`

لا تحسب الرصيد المتاح محليًا؛ `netWithdrawable` هو مصدر الحقيقة.

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

طلبات السحب الحالية تدعم الحساب البنكي السعودي الصحيح. لا ترسل أو تعرض رقم الحساب كاملًا بعد الحفظ؛ استخدم `maskedLabel`.

إذا لم توجد طريقة أساسية صالحة، وجّه المندوب إلى إضافة/اعتماد طريقة السحب قبل فتح نموذج الطلب.

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
| `DRIVER_COD_DEBT_NOT_SETTLED` | يجب تسوية مبالغ الدفع عند الاستلام أولًا |
| `PAYOUT_DAY_DISABLED` | أعد تحميل الأيام المتاحة |
| `DRIVER_WITHDRAWAL_CANNOT_CANCEL` | الطلب دخل معالجة المالية ولا يمكن إلغاؤه |
| `RATE_LIMIT_EXCEEDED` | امنع المحاولات السريعة واعرض إعادة المحاولة لاحقًا |

عند `401` أعد تسجيل الدخول، وعند `403` لا تعرض رسالة شبكة؛ اعرض أن الحساب غير مخول للعملية.

## 8. التحديثات الفورية والإشعارات

يدعم الباك إند إشعارات تحديث المحفظة. عند وصول Push/Notification من النوع:

```text
driver_wallet_updated
```

وفي اتصال SignalR اسم الرسالة هو:

```text
ReceiveDriverWalletUpdated
```

أو event متعلق بالسحب مثل:

```text
wallet.withdrawal_submitted
wallet.withdrawal_processing
wallet.withdrawal_paid
wallet.withdrawal_failed
wallet.withdrawal_returned
```

نفّذ refresh صامتًا لملخص المحفظة والطلب المعني بدل تعديل الأرصدة محليًا.

في `wallet.withdrawal_returned` اعرض تنبيهًا واضحًا بأن المبلغ عاد للمحفظة وأن بيانات الحساب البنكي تحتاج للمراجعة قبل طلب جديد.

## 9. حالات التحميل ومنع التكرار في الواجهة

- استخدم loading مستقل لكل عملية: تحميل المحفظة، إرسال الطلب، تعديل اليوم، والإلغاء.
- امنع double tap على أزرار mutations.
- لا تعتبر timeout فشلًا نهائيًا في إنشاء السحب؛ أعد المحاولة بنفس idempotency key.
- لا تخصم الرصيد محليًا بعد إنشاء الطلب؛ أعد قراءة المحفظة من الخادم.
- لا تحفظ `availablePayoutDays` كإعداد دائم؛ أعد تحميلها عند فتح الشاشة.

## 10. اختبارات القبول المطلوبة للموبايل

1. يعرض التطبيق الأيام التي ترجع من API فقط.
2. تعديل يوم التحويل ينعكس بعد إعادة فتح الشاشة.
3. إرسال نفس طلب السحب مرتين بنفس المفتاح يعرض طلبًا واحدًا فقط.
4. timeout ثم retry بنفس المفتاح لا ينشئ طلبًا ثانيًا.
5. يمنع التطبيق إنشاء طلب جديد مع وجود `Pending` أو `Processing`، ويتعامل أيضًا مع رفض الخادم.
6. إلغاء طلب `Pending` ينجح ويحدّث الرصيد والقائمة.
7. محاولة إلغاء `Processing` تعيد تحميل الحالة وتخفي زر الإلغاء.
8. ظهور `Paid` يعرض مرجع التحويل.
9. ظهور `Returned` يعرض سبب المرتجع ويؤكد رجوع المبلغ للمحفظة.
10. تغيير الإدارة للأيام أثناء فتح الشاشة يعالج `PAYOUT_DAY_DISABLED` ويعيد تحميل القائمة.
11. التطبيق يعرض رسائل صحيحة عند الرصيد غير الكافي أو وجود COD مستحق.
12. جميع المبالغ تعرض بعملة `SAR` وبصيغة locale مناسبة دون تغيير القيمة المرسلة للـ API.

## 11. خارج نطاق تطبيق المندوب

لا تنفذ في تطبيق المندوب أيًا من الآتي:

- اعتماد أو رفض الطلب.
- حجز التحويل لأدمن.
- تسجيل إرسال الحوالة في البنك.
- رفع إثبات التحويل أو إثبات المرتجع.
- تأكيد الدفع أو تسجيل المرجع البنكي من جهة المندوب.
- تشغيل أو إعادة محاولة بوابة الدفع.

هذه العمليات محمية بصلاحيات الإدارة المالية وتنفذ من لوحة الإدارة فقط.
