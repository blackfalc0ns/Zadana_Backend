# ظهور مرجع التحويل وإثبات التحويل — Handoff لتطبيق المندوب

## الغرض

هذا الملف يكمّل [`DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md`](./DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md) ويحدّد **متى** يظهر للمندوب مرجع التحويل وملف إثبات التحويل، و**متى** تتغير الحالة إلى `Processing`.

> **ملخص سريع:** بعد `Paid` يرى المندوب `transferReference`، وإن وُجد إثبات مرفوع من الإدارة يظهر `hasTransferProof` مع إمكانية التحميل من endpoint مخصص.

## جدول الظهور

| المحتوى | المندوب | التاجر (لوحة التاجر) |
|---|---|---|
| ملف إثبات التحويل | بعد `Paid` إن وُجد (`hasTransferProof`) | يُعرض ويُحمَّل بعد `paid` |
| `transferReference` | فقط عند `Paid` | عند `paid` (تنبيه + جدول التسويات) |
| حالة `Processing` | بعد `manual-bank-submission` أو الإرسال التلقائي للبنك | يظهر `processing` في المالية أثناء التحويل |
| `providerTransferId` | فقط عند `Paid` (اختياري، لا تعرضه كاملًا) | غير مطلوب في لوحة التاجر |
| IBAN / رقم حساب كامل | **لا** — استخدم `maskedLabel` فقط | — |

## دورة الحالة من منظور المندوب

```
Pending  →  Processing  →  Paid
   ↑            │              │
   │            │              └─ transferReference + إثبات (إن وُجد)
   │            └─ بعد تسجيل الإرسال البنكي (إدارة)
   └─ يمكن الإلغاء
```

### `Pending`

- الطلب قيد المراجعة أو تم تحضيره للتحويل من الإدارة **دون** تسجيل إرسال بنكي بعد.
- `transferReference` = `null`
- `hasTransferProof` = `false`
- زر الإلغاء **متاح**.

### `Processing`

- تبدأ **بعد** أن تسجّل الإدارة إرسال الحوالة للبنك (`manual-bank-submission`) أو بعد الإرسال التلقائي عبر البوابة.
- `transferReference` = `null`
- `hasTransferProof` = `false`
- زر الإلغاء **مخفي**.
- إشعار: `wallet.withdrawal_processing`.

### `Paid`

- التحويل اكتمل وتأكدت الإدارة الدفع.
- `transferReference` يُرجَع **فقط** هنا.
- `hasTransferProof` / `transferProofFileName` يُرجَعان إن رفعت الإدارة إثباتًا.
- إشعار: `wallet.withdrawal_paid` وقد يحتوي `transferReference` و`hasTransferProof`.

## حقول DTO في API المندوب

Endpoints:

- `GET /api/drivers/wallet/withdrawals`
- `POST /api/drivers/wallet/withdrawals`
- `GET /api/drivers/wallet/withdrawals/{id}/transfer-proof` — تحميل ملف الإثبات (Paid فقط)

### قواعد الحقول

| الحقل | `Pending` / `Processing` | `Paid` |
|---|---|---|
| `transferReference` | `null` | قيمة نصية أو `null` |
| `providerTransferId` | `null` | قيمة أو `null` — **لا تعرض كاملًا** |
| `hasTransferProof` | `false` | `true` إن وُجد إثبات |
| `transferProofFileName` | `null` | اسم الملف إن وُجد |
| `payoutId` | قد يظهر بعد الربط | يظهر |

### مثال — `Paid` مع إثبات

```json
{
  "id": "2e30501c-0e13-48d5-889d-7bbaf77dcf90",
  "amount": 250,
  "status": "Paid",
  "transferReference": "TRX-20260722-001",
  "providerTransferId": "GW-998877",
  "hasTransferProof": true,
  "transferProofFileName": "transfer-proof.pdf",
  "processedAtUtc": "2026-07-22T14:05:00Z"
}
```

## تحميل إثبات التحويل

`GET /api/drivers/wallet/withdrawals/{id}/transfer-proof`

Authorization: Bearer (DriverOnly)

- متاح فقط إذا الطلب يخص المندوب الحالي وحالته `Paid` ويوجد إثبات.
- الرد: ملف (`Content-Type` حسب المرفق) مع اسم الملف.
- إن لم يوجد إثبات أو الحالة ليست `Paid` → `404`.

## الإشعارات و SignalR

| الحدث | متى يُرسل | ما يعرضه التطبيق |
|---|---|---|
| `wallet.withdrawal_processing` | بعد تسجيل الإرسال البنكي | «جاري تحويل السحب» — بدون مرجع/إثبات |
| `wallet.withdrawal_paid` | بعد اكتمال الدفع | اعرض `transferReference`؛ إن `hasTransferProof` وجّه لتحميل الإثبات من تفاصيل السحب |
| `wallet.withdrawal_failed` | فشل التحويل | `failureReason` |
| `wallet.withdrawal_returned` | إرجاع من البنك | سبب المرتجع + تحديث المحفظة |

Payload في `wallet.withdrawal_paid` قد يتضمن:

- `withdrawalId`, `amount`, `status`
- `transferReference`
- `hasTransferProof`
- `payoutId`
- `targetUrl` — غالبًا `/wallet/withdrawals/{withdrawalId}`

بعد أي حدث: **refresh صامت** للمحفظة والطلب من الخادم.

## سلوك الواجهة المطلوب

1. في قائمة/تفاصيل السحوبات: اعرض `transferReference` **فقط** إذا `status === "Paid"`.
2. إن `hasTransferProof === true`: أظهر زر **تحميل إثبات التحويل** يستدعي  
   `GET /api/drivers/wallet/withdrawals/{id}/transfer-proof`.
3. عند `Processing`: badge «جاري التحويل» بدون مرجع ولا إثبات.
4. لا تعرض `providerTransferId` كاملًا؛ استخدم `transferReference` كمرجع للمستخدم.
5. لا تعرض IBAN كامل؛ `paymentMethod.maskedLabel` فقط.
6. عند `wallet.withdrawal_paid`: أعد تحميل الطلب ثم اعرض المرجع وزر الإثبات إن وُجد.

## اختبارات قبول مختصرة

1. طلب `Pending`: لا `transferReference` ولا زر إثبات.
2. بعد `Processing`: المرجع والإثبات مخفيان.
3. بعد `Paid` بدون إثبات: يظهر `transferReference` و`hasTransferProof=false`.
4. بعد `Paid` مع إثبات: `hasTransferProof=true` والتحميل ينجح.
5. تحميل إثبات لطلب غير Paid أو لمندوب آخر → `404`.

## مراجع

- Handoff كامل للسحب: [`DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md`](./DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md)
- Backend: `DriverWalletController.MapWithdrawalDto`, `DownloadWithdrawalTransferProof`
