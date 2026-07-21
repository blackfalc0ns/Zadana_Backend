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

## متطلبات التشغيل قبل النشر

- طبّق migrations حتى `20260721195706_CompleteManualSettlementOperationalHardening` بعد أخذ نسخة احتياطية من قاعدة البيانات.
- في الإنتاج اضبط `DataProtection__KeysPath` على volume دائم ومشترك بين كل نسخ الـ API، مثل `/var/lib/zadana/data-protection-keys`. لا تستخدم مسارًا داخل image أو مجلدًا يُستبدل عند إعادة النشر؛ إذ سيمنع ذلك فك تشفير إثباتات التحويل القديمة.
- احصر صلاحيات مسار المفاتيح على مستخدم تشغيل الـ API، وتحقق من إمكان الكتابة فيه قبل تشغيل الخدمة.
