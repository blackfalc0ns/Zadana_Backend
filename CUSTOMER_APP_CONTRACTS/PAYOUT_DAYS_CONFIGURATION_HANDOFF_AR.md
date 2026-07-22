# إعداد أيام التحويل المتاحة

## الحالة

- `implemented`
- مصدر الحقيقة لأيام التحويل هو إعداد الإدارة العام، وليس قائمة ثابتة في التطبيق.
- القيمة الافتراضية بعد الترحيل: `Monday` و`Thursday`.

## قيم اليوم

ترسل وتستقبل الـ API أسماء الأيام بصيغة PascalCase التالية:

`Sunday`, `Monday`, `Tuesday`, `Wednesday`, `Thursday`, `Friday`, `Saturday`.

لا تعرض التطبيقات إلا القيم الواردة في `availablePayoutDays`؛ قد تتغير القائمة في أي وقت من لوحة الإدارة.

## تطبيق المندوب

### قراءة تفضيل يوم التحويل

`GET /api/drivers/wallet/payout-preference`

الاستجابة:

```json
{
  "payoutDay": "Thursday",
  "availablePayoutDays": ["Sunday", "Thursday"]
}
```

### تعديل تفضيل يوم التحويل

`PUT /api/drivers/wallet/payout-preference`

```json
{
  "payoutDay": "Sunday"
}
```

الاستجابة الناجحة بنفس الشكل السابق. يجب أن يعيد التطبيق تحميل التفضيل عند فتح صفحة المحفظة/التحويل، ويعرض فقط الأيام الموجودة في `availablePayoutDays`.

### أخطاء مهمة

- `INVALID_PAYOUT_DAY`: الاسم ليس يومًا صحيحًا من أيام الأسبوع.
- `PAYOUT_DAY_DISABLED`: اليوم صحيح، لكنه غير مفعّل حاليًا من الإدارة. أعد تحميل التفضيل واعرض الأيام المتاحة.

## تطبيق التاجر

نفس السلوك متاح للتاجر:

- `GET /api/vendors/profile/payout-preference`
- `PUT /api/vendors/profile/payout-preference`

استجابة `PUT` مغلفة في `data`، أما بيانات التفضيل فهي:

```json
{
  "payoutDay": "Thursday",
  "availablePayoutDays": ["Sunday", "Thursday"]
}
```

## أثر تغيير الإدارة

عند حذف الإدارة يومًا مفعّلًا:

- يعاد تعيين كل تاجر ومندوب اختار ذلك اليوم في نفس عملية الحفظ.
- الاختيار البديل ثابت: `Monday` إن كان مفعّلًا، وإلا أول يوم مفعّل بحسب ترتيب أيام الأسبوع.
- لا يفترض التطبيق أن التفضيل المخزن محليًا ما زال صالحًا؛ عند خطأ `PAYOUT_DAY_DISABLED` يعيد قراءة الـ GET ويحدّث الواجهة.

لا يمكن للإدارة حفظ قائمة فارغة من أيام التحويل.

## إنشاء طلب سحب المندوب بأمان

### قراءة الحدود والحالة قبل عرض نموذج السحب

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

القيم تأتي من إعدادات السيرفر وقد تتغير بين البيئات. يعطّل التطبيق زر الطلب عندما تكون `hasActiveWithdrawal = true` أو يصل `requestsCreatedToday` إلى `maximumRequestsPerDay`، مع بقاء تحقق الخادم هو مصدر الحقيقة.

### إنشاء الطلب

`POST /api/drivers/wallet/withdrawals`

أرسل مفتاحًا فريدًا ثابتًا لكل ضغطة/محاولة من التطبيق، واحتفظ به عند إعادة المحاولة بسبب انقطاع الشبكة. يمكن إرساله في `Idempotency-Key` أو في الجسم:

```json
{
  "paymentMethodId": "b7d50fe9-99f9-4fdf-b315-205b765fdb65",
  "amount": 250,
  "idempotencyKey": "mobile-0d8747ca-34b5-40f0-a70f-b0578d998ceb"
}
```

- إعادة نفس المفتاح مع نفس المبلغ وطريقة السحب تعيد نفس الطلب ولا تخصم/تحجز مرتين.
- إعادة نفس المفتاح ببيانات مختلفة ترفض بـ `WITHDRAWAL_IDEMPOTENCY_KEY_REUSED`.
- لا يسمح بأكثر من طلب حالته `Pending` أو `Processing` لنفس المندوب؛ الخطأ `DRIVER_ACTIVE_WITHDRAWAL_EXISTS`.
- أخطاء الحدود: `INVALID_WITHDRAWAL_AMOUNT` و`DRIVER_DAILY_WITHDRAWAL_LIMIT_REACHED`.
- الخادم يلتقط يوم التحويل وبيانات الحساب البنكي داخل الطلب وقت إنشائه؛ تعديلهما لاحقًا لا يغيّر وجهة طلب قائم.

### إلغاء الطلب من المندوب

`POST /api/drivers/wallet/withdrawals/{withdrawalId}/cancel`

مسموح فقط للطلب الخاص بالمندوب وهو `Pending` وقبل دخول الإدارة في معالجته. الإعادة بعد نجاح الإلغاء آمنة وتعيد نفس الحالة. إذا بدأ مسار المالية يرجع `DRIVER_WITHDRAWAL_CANNOT_CANCEL`.

حالات الطلب التي يجب أن يدعمها التطبيق:

`Pending`, `Processing`, `Paid`, `Failed`, `Cancelled`, `Returned`.

`Returned` تعني أن البنك أعاد الحوالة بعد تسجيلها كمدفوعة؛ يعيد الخادم المبلغ إلى رصيد المحفظة ويرسل إشعارًا للمندوب لمراجعة بيانات الحساب.

## تجهيز سحب المندوب في وضع التسوية اليدوي

عند تفعيل `settlementProcessingMode = Manual`، تعتمد الإدارة طلب السحب أولاً ثم تسجل التحويل البنكي والإثبات في خطوة منفصلة. لا تعتبر خطوة الاعتماد تحويلاً مكتملًا.

### تجهيز التحويل

`POST /api/admin/wallets/withdrawals/{withdrawalId}/process`

```json
{
  "isApproved": true
}
```

لا ترسل `transferReference` في هذه الخطوة عند الوضع اليدوي؛ المرجع والإثبات مطلوبان عند التأكيد فقط.

الاستجابة الناجحة في الوضع اليدوي:

```json
{
  "withdrawalId": "b6b2d034-7c9b-4f30-a4cc-d859f1c5c753",
  "withdrawalStatus": "Processing",
  "payoutId": "1233f3d4-9d26-4e91-bfe2-ec25fd8408f5",
  "payoutStatus": "Pending",
  "manualWorkflowRequired": true,
  "manualClaimEndpoint": "/api/admin/payouts/1233f3d4-9d26-4e91-bfe2-ec25fd8408f5/manual-claim",
  "manualBankSubmissionEndpoint": "/api/admin/payouts/1233f3d4-9d26-4e91-bfe2-ec25fd8408f5/manual-bank-submission",
  "manualConfirmationEndpoint": "/api/admin/payouts/1233f3d4-9d26-4e91-bfe2-ec25fd8408f5/confirm-manual",
  "transferReference": null,
  "failureReason": null
}
```

احتفظ بـ `payoutId`؛ إعادة إرسال طلب التجهيز لن تنشئ تسوية أو payout أو hold جديدًا، بل تعيد نفس المعرّف.

### قفل الدفعة قبل الدخول إلى البنك

`POST /api/admin/payouts/{payoutId}/manual-claim`

هذه الخطوة تحجز الدفعة لأدمن واحد. لا تدخل إلى بوابة البنك قبل نجاحها.

### تسجيل إرسال التحويل إلى البنك

`POST /api/admin/payouts/{payoutId}/manual-bank-submission`

```json
{
  "bankSubmissionReference": "BANK-SUBMISSION-123"
}
```

تسجل هذه الخطوة أن التحويل أُدخل في بوابة البنك. بعد ذلك لا يمكن إلغاء الدفعة أو إعادة تشغيلها تلقائيًا؛ يجب تأكيدها أو تسويتها/عكسها.

### رفع إثبات التحويل بشكل آمن

`POST /api/admin/payouts/{payoutId}/proofs`

يرسل الطلب بصيغة `multipart/form-data` بعد تسجيل الإرسال البنكي، وبالقيم التالية:

- `kind`: القيمة الثابتة `ManualTransfer`.
- `file`: ملف PDF أو JPEG أو PNG أو WebP. الحد الأقصى 10 MB للـ PDF و5 MB للصور.

مثال على الاستجابة:

```json
{
  "id": "dd15d84e-9a82-48e6-aad0-03cf5a1e16d5",
  "payoutId": "1233f3d4-9d26-4e91-bfe2-ec25fd8408f5",
  "kind": "ManualTransfer",
  "fileName": "bank-receipt.pdf",
  "isFinalized": false
}
```

لا يُنشئ الخادم رابطًا عامًا لملف الإثبات. بعد التأكيد يمكن لأدمن المالية تنزيله فقط من:

`GET /api/admin/payouts/{payoutId}/proofs/{proofAttachmentId}`

### تأكيد التحويل البنكي بعد تنفيذه خارج المنصة

`POST /api/admin/payouts/{payoutId}/confirm-manual`

```json
{
  "transferReference": "BANK-REFERENCE-123",
  "proofAttachmentId": "dd15d84e-9a82-48e6-aad0-03cf5a1e16d5"
}
```

تتطلب هذه الخطوة مرجع البنك و`proofAttachmentId` الناتج من خطوة الرفع الآمنة، وتأتي بعد claim ثم bank submission. لا تعرض للسائق حالة `Paid` قبل نجاحها. افتراضيًا يجب أن يكون الأدمن المؤكد مختلفًا عن الأدمن الذي سجل الإرسال البنكي.

### الوضع التلقائي بلا بوابة تحويل

إذا كان الوضع `Automatic` ولا توجد بوابة payout مهيأة، يعيد الخادم `PAYOUT_GATEWAY_UNAVAILABLE` ولا ينشئ payout ولا يغيّر طلب السحب إلى مدفوع، حتى إن أرسل العميل `transferReference`. للتحويل الخارجي يجب أن تحوّل الإدارة الوضع إلى `Manual` ثم تستخدم خطتي التجهيز والتأكيد أعلاه.

## تسجيل حوالة مرتجعة من البنك

إذا أعاد البنك حوالة سبق تأكيدها كمدفوعة، تفتح الإدارة طلب المندوب من صفحة طلبات السحب وتختار «تسجيل مرتجع بنكي». المسار التقني هو:

1. رفع إثبات مستقل إلى `POST /api/admin/payouts/{payoutId}/proofs` مع `kind = ReturnedFunds`.
2. إرسال `POST /api/admin/payouts/{payoutId}/record-return`:

```json
{
  "returnReference": "BANK-RETURN-123",
  "proofAttachmentId": "3f21b746-6d08-4ae6-8d15-c11cfbc99f07",
  "reason": "Beneficiary account rejected the transfer"
}
```

ينشئ الخادم سجل عكس مستقل لا يعدّل إثبات الدفع الأصلي، يغيّر حالة طلب المندوب إلى `Returned`، يعيد المبلغ إلى رصيد المحفظة بقيد محاسبي معاكس، ويرسل إشعارًا عربيًا/إنجليزيًا للمندوب. إعادة نفس العملية على payout أصبح `Reversed` آمنة ولا تعيد الرصيد مرتين.

## متطلبات التشغيل قبل النشر

- طبّق migrations حتى `20260722103228_HardenDriverWithdrawalSettlementWorkflow` بعد أخذ نسخة احتياطية من قاعدة البيانات.
- الـ migration يرحّل يوم التحويل للطلبات القديمة، ويلغي الطلبات النشطة المكررة وحجوزاتها قبل إنشاء قيد يمنع التكرار، ثم يضيف مفاتيح idempotency وتدقيق الأدمن.
- شغّل الـ migration قبل تشغيل نسخة الـ API الجديدة؛ النسخة الجديدة توسّع حقول حساب سحب المندوب ثم تشفّر القيم القديمة تلقائيًا في مهمة الـ backfill عند التشغيل.
- في الإنتاج اضبط `DataProtection__KeysPath` على volume دائم ومشترك بين كل نسخ الـ API، مثل `/var/lib/zadana/data-protection-keys`. لا تستخدم مسارًا داخل image أو مجلدًا يُستبدل عند إعادة النشر؛ إذ سيمنع ذلك فك تشفير إثباتات التحويل القديمة.
- احصر صلاحيات مسار المفاتيح على مستخدم تشغيل الـ API، وتحقق من إمكان الكتابة فيه قبل تشغيل الخدمة.
