# ظهور مرجع التحويل وحالة السحب — Handoff لتطبيق المندوب

## الغرض

هذا الملف يكمّل [`DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md`](./DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md) ويحدّد **متى** يظهر للمندوب مرجع التحويل، و**متى** تتغير الحالة إلى `Processing`، وما الذي **لا يُعرض أبدًا** في تطبيق المندوب.

> **ملخص سريع:** المندوب لا يرى ملف إثبات التحويل. يرى `transferReference` فقط بعد `Paid`. يرى `Processing` بعد تسجيل الإرسال البنكي من الإدارة، وليس عند مجرد «تحضير» الطلب.

## جدول الظهور

| المحتوى | المندوب | التاجر (لوحة التاجر) |
|---|---|---|
| ملف إثبات التحويل | **لا يُعرض أبدًا** | يُعرض ويُحمَّل بعد `paid` |
| `transferReference` | فقط عند `Paid` | عند `paid` (تنبيه + جدول التسويات) |
| حالة `Processing` | بعد `manual-bank-submission` أو الإرسال التلقائي للبنك | يظهر `processing` في المالية أثناء التحويل |
| `providerTransferId` | فقط عند `Paid` (اختياري، لا تعرضه كاملًا) | غير مطلوب في لوحة التاجر |
| IBAN / رقم حساب كامل | **لا** — استخدم `maskedLabel` فقط | — |

## دورة الحالة من منظور المندوب

```
Pending  →  Processing  →  Paid
   ↑            │              │
   │            │              └─ transferReference يظهر هنا فقط
   │            └─ بعد تسجيل الإرسال البنكي (إدارة)
   └─ يمكن الإلغاء
```

### `Pending`

- الطلب قيد المراجعة أو تم تحضيره للتحويل من الإدارة **دون** تسجيل إرسال بنكي بعد.
- `transferReference` = `null`
- `providerTransferId` = `null`
- زر الإلغاء **متاح**.

### `Processing`

- تبدأ **بعد** أن تسجّل الإدارة إرسال الحوالة للبنك (`manual-bank-submission`) أو بعد الإرسال التلقائي عبر البوابة.
- **لا** تبدأ عند مجرد موافقة الإدارة أو «تحضير» الطلب إذا لم يُسجَّل إرسال بنكي.
- `transferReference` = `null` (المرجع النهائي يظهر عند الدفع فقط)
- زر الإلغاء **مخفي**.
- إشعار Push/Inbox: `wallet.withdrawal_processing`.

### `Paid`

- التحويل اكتمل وتأكدت الإدارة الدفع (`confirm-manual` أو مسار تلقائي).
- `transferReference` يُرجَع في API **فقط** في هذه الحالة.
- `providerTransferId` يُرجَع **فقط** في هذه الحالة (لا تعرضه كاملًا للمستخدم؛ مرجع الواجهة هو `transferReference`).
- إشعار Push/Inbox: `wallet.withdrawal_paid` وقد يحتوي `transferReference`.

## حقول DTO في API المندوب

Endpoints المعنية:

- `POST /api/drivers/wallet/withdrawals`
- `GET /api/drivers/wallet/withdrawals`
- `GET /api/drivers/wallet/withdrawals/{id}` (إن وُجد)

### قواعد الحقول

| الحقل | `Pending` / `Processing` | `Paid` |
|---|---|---|
| `transferReference` | `null` | قيمة نصية أو `null` إن لم يُسجَّل |
| `providerTransferId` | `null` | قيمة أو `null` — **لا تعرض كاملًا** |
| `providerName` | قد يظهر | قد يظهر |
| `payoutId` | قد يظهر بعد الربط | يظهر |
| ملف إثبات | **لا يوجد endpoint للمندوب** | **لا يوجد endpoint للمندوب** |

### مثال — `Processing`

```json
{
  "id": "2e30501c-0e13-48d5-889d-7bbaf77dcf90",
  "amount": 250,
  "status": "Processing",
  "transferReference": null,
  "providerTransferId": null,
  "payoutId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### مثال — `Paid`

```json
{
  "id": "2e30501c-0e13-48d5-889d-7bbaf77dcf90",
  "amount": 250,
  "status": "Paid",
  "transferReference": "TRX-20260722-001",
  "providerTransferId": "GW-998877",
  "processedAtUtc": "2026-07-22T14:05:00Z"
}
```

## الإشعارات و SignalR

| الحدث | متى يُرسل | ما يعرضه التطبيق |
|---|---|---|
| `wallet.withdrawal_processing` | بعد تسجيل الإرسال البنكي وانتقال الحالة إلى `Processing` | «جاري تحويل السحب» — **بدون** مرجع تحويل |
| `wallet.withdrawal_paid` | بعد اكتمال الدفع | اعرض `transferReference` إن وُجد في payload |
| `wallet.withdrawal_failed` | فشل التحويل | `failureReason` |
| `wallet.withdrawal_returned` | إرجاع من البنك | سبب المرتجع + تحديث المحفظة |

Payload مشترك (قد يختلف حسب الحدث):

- `withdrawalId` — لتحديث شاشة التفاصيل
- `amount`, `status`
- `transferReference` — **فقط** في `wallet.withdrawal_paid` عند توفره
- `targetUrl` — غالبًا `/wallet/withdrawals/{withdrawalId}`

بعد أي حدث: **refresh صامت** للمحفظة والطلب من الخادم؛ لا تعدّل الأرصدة أو المراجع محليًا.

## سلوك الواجهة المطلوب

1. **لا** تضف زر «تحميل إثبات» أو أي مسار لملف PDF/صورة — غير متاح للمندوب.
2. في قائمة السحوبات وتفاصيل الطلب: اعرض `transferReference` **فقط** إذا `status === "Paid"`.
3. عند `Processing`: badge «جاري التحويل» بدون مرجع.
4. لا تعرض `providerTransferId` كاملًا؛ استخدم `transferReference` كمرجع للمستخدم.
5. لا تعرض IBAN أو رقم حساب كامل بعد الحفظ؛ `paymentMethod.maskedLabel` فقط.
6. عند استلام `wallet.withdrawal_processing`: حدّث الحالة إلى `Processing` وأخفِ الإلغاء.
7. عند استلام `wallet.withdrawal_paid`: أعد تحميل الطلب ثم اعرض المرجع إن وُجد.

## خارج النطاق (تذكير)

- رفع إثبات التحويل أو إثبات المرتجع.
- تأكيد الدفع أو تسجيل المرجع البنكي.
- أي endpoint لتحميل ملف إثبات.

كل ذلك من **لوحة الإدارة** فقط.

## اختبارات قبول مختصرة

1. طلب `Pending`: لا `transferReference` ولا زر إثبات.
2. بعد `wallet.withdrawal_processing`: الحالة `Processing`، المرجع ما زال مخفيًا.
3. بعد `Paid`: يظهر `transferReference` في التفاصيل والإشعار.
4. `GET /withdrawals` لا يعيد `transferReference` لطلبات `Pending` أو `Processing`.
5. لا يوجد في التطبيق أي استدعاء API لتحميل إثبات تحويل.

## مراجع

- Handoff كامل للسحب: [`DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md`](./DRIVER_WITHDRAWAL_SETTLEMENT_MOBILE_HANDOFF_AR.md)
- Backend: `DriverWalletController.MapWithdrawalDto`, `PayoutOrchestrator.NotifyLinkedDriverWithdrawalProcessingAsync`
