# إغلاق حساب المندوب (يظهر كحذف) — Handoff للموبايل

## الحالة

- الباك إند: `implemented`
- المطلوب من تطبيق المندوب: زر «حذف الحساب» في الإعدادات/الملف الشخصي مع تأكيد قوي.

## المبدأ

إغلاق حساب (أرشفة + إخفاء هوية) يظهر للمندوب كـ**حذف حساب**. المحفظة وطلبات السحب وسجل التوصيل تبقى للإدارة.

## Endpoint

`POST /api/drivers/me/close-account`

Authorization: Bearer (DriverOnly)

### Body

```json
{
  "confirmation": "DELETE",
  "password": "كلمة_المرور",
  "reason": "اختياري"
}
```

### نجاح `200`

```json
{
  "message": "تم حذف الحساب.|Account deleted.",
  "closed": true
}
```

بعد النجاح: امسح التوكنات وارجع لشاشة الدخول.

## قيود قبل الإغلاق (مهم)

| errorCode | الشرط | سلوك الواجهة |
|---|---|---|
| `ACCOUNT_CLOSE_ACTIVE_ASSIGNMENT` | يوجد تعيين توصيل نشط (عرض معلّق أو مهمة جارية) | امنع الحذف ووجّه لشاشة الطلب الحالي |
| `ACCOUNT_CLOSE_OPEN_DISPUTE` | يوجد نزاع/بلاغ مفتوح مرتبط بالمندوب أو بطلباته | امنع الحذف ووجّه للدعم/النزاعات |
| `ACCOUNT_CLOSE_ACTIVE_WITHDRAWAL` | يوجد سحب `Pending` أو `Processing` | امنع الحذف ووجّه لقائمة السحوبات |
| `ACCOUNT_CLOSE_ACTIVE_SETTLEMENT` | توجد تسوية/تحويل قيد المعالجة | امنع الحذف ووجّه للمحفظة/التسويات |
| `ACCOUNT_CLOSE_ACTIVE_HOLD` | يوجد مبلغ محجوز على المحفظة | امنع الحذف |
| `ACCOUNT_CLOSE_WALLET_BALANCE` | يوجد رصيد حالي أو معلّق في المحفظة | امنع الحذف واطلب سحب/تصفير الرصيد |
| `ACCOUNT_CLOSE_COD_OUTSTANDING` | `codOwedBalance > 0` | امنع الحذف واعرض أن تسوية COD مطلوبة |
| `ACCOUNT_CLOSE_CONFIRMATION_REQUIRED` | confirmation ≠ DELETE | أظهر حقل DELETE |
| `ACCOUNT_CLOSE_INVALID_PASSWORD` | كلمة مرور خاطئة | أعد المحاولة |

افحص مسبقًا من المهمة الحالية + `GET /api/drivers/wallet` و`GET /api/drivers/wallet/withdrawals` عطّل الزر إن وُجد قيد.

## بعد الإغلاق — محاولة الدخول

`errorCode`: `ACCOUNT_CLOSED` — «تم حذف هذا الحساب.»

## واجهة مقترحة

1. زر **حذف الحساب** في الإعدادات.
2. إن وُجد طلب توصيل نشط أو نزاع مفتوح أو سحب/تسوية نشطة أو رصيد محفظة أو COD: اعرض سبب المنع بدل المودال.
3. مودال: تحذير + `DELETE` + كلمة المرور.
4. بعد النجاح: logout كامل.

## اختبارات قبول

1. طلب توصيل نشط / عرض معلّق → رفض `ACCOUNT_CLOSE_ACTIVE_ASSIGNMENT`.
2. نزاع/بلاغ مفتوح → رفض `ACCOUNT_CLOSE_OPEN_DISPUTE`.
3. سحب Pending → رفض `ACCOUNT_CLOSE_ACTIVE_WITHDRAWAL`.
4. تسوية شغّالة → رفض `ACCOUNT_CLOSE_ACTIVE_SETTLEMENT`.
5. رصيد محفظة → رفض `ACCOUNT_CLOSE_WALLET_BALANCE`.
6. COD > 0 → رفض `ACCOUNT_CLOSE_COD_OUTSTANDING`.
7. نجاح بدون قيود → إغلاق + logout.
8. Login بعد الإغلاق → `ACCOUNT_CLOSED`.
