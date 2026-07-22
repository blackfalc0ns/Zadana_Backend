# إغلاق حساب التاجر (يظهر كحذف) — Handoff للـ vendor-panel

## الحالة

- الباك إند: `implemented`
- المطلوب من لوحة التاجر: زر «حذف الحساب» في إعدادات الملف الشخصي (للمالك فقط) مع تأكيد قوي، ثم مسح الجلسة والعودة لتسجيل الدخول.

## المبدأ

هذا **إغلاق حساب** وليس حذفًا فعليًا من قاعدة البيانات. الطلبات والتسويات والـ ledger تبقى للإدارة. للمستخدم يظهر أن الحساب **تم حذفه**.

## Endpoint

`POST /api/vendor/account/close`

Authorization: Bearer (VendorOnly) — **المالك فقط** (ليس staff).

### Body

```json
{
  "confirmation": "DELETE",
  "password": "كلمة_المرور",
  "reason": "اختياري"
}
```

- `confirmation` يجب أن يكون بالضبط `DELETE` (حروف كبيرة).
- `password` مطلوب للتأكيد.
- `reason` اختياري.

### نجاح `200`

```json
{
  "message": "تم حذف الحساب.|Account deleted.",
  "closed": true
}
```

بعد النجاح:
1. امسح access/refresh tokens محليًا.
2. امسح أي cache للملف الشخصي/الجلسة.
3. انتقل لشاشة تسجيل الدخول.
4. لا تحاول refresh-token بعد الإغلاق.

## رسائل الخطأ الشائعة

| errorCode | المعنى | سلوك الواجهة |
|---|---|---|
| `ACCOUNT_CLOSE_OWNER_ONLY` | المستخدم staff وليس المالك | أخفِ الزر أو امنع الإجراء |
| `ACCOUNT_CLOSE_ACTIVE_ORDERS` | يوجد طلبات غير مكتملة للمتجر | امنع الحذف ووجّه لقائمة الطلبات |
| `ACCOUNT_CLOSE_OPEN_DISPUTE` | يوجد نزاع/بلاغ مفتوح على طلبات المتجر | امنع الحذف ووجّه للنزاعات |
| `ACCOUNT_CLOSE_ACTIVE_SETTLEMENT` | تسوية/تحويل قيد المعالجة | امنع الحذف ووجّه للمالية |
| `ACCOUNT_CLOSE_ACTIVE_HOLD` | مبالغ محجوزة على المحفظة | امنع الحذف |
| `ACCOUNT_CLOSE_CONFIRMATION_REQUIRED` | لم يُكتب DELETE | أظهر حقل التأكيد |
| `ACCOUNT_CLOSE_PASSWORD_REQUIRED` | كلمة المرور فارغة | ركّز على حقل كلمة المرور |
| `ACCOUNT_CLOSE_INVALID_PASSWORD` | كلمة مرور خاطئة | أعد المحاولة |
| `ACCOUNT_ALREADY_CLOSED` | الحساب مغلق مسبقًا | اعتبره نجاحًا وlogout |

## بعد الإغلاق — محاولة الدخول

`POST` login للتاجر يعيد:

- `errorCode`: `ACCOUNT_CLOSED`
- الرسالة: «تم حذف هذا الحساب.»

اعرضها كنص نهائي دون زر «إعادة تفعيل».

## واجهة الإعدادات

1. زر أحمر/تحذيري: **حذف الحساب** (للمالك فقط).
2. مودال تحذير يوضح:
   - لن تقدر تسجّل الدخول بهذا الحساب.
   - البيانات الشخصية تُحذف من العرض.
   - سجل الطلبات والتسويات يبقى للمنصة فقط.
3. حقل نص: اكتب `DELETE`
4. حقل كلمة المرور
5. زر تأكيد معطل حتى يكتمل الشرطان

## اختبارات قبول

1. staff يحاول الحذف → `ACCOUNT_CLOSE_OWNER_ONLY`.
2. طلبات غير مكتملة → `ACCOUNT_CLOSE_ACTIVE_ORDERS`.
3. نزاع/بلاغ مفتوح → `ACCOUNT_CLOSE_OPEN_DISPUTE`.
4. تسوية processing → `ACCOUNT_CLOSE_ACTIVE_SETTLEMENT`.
5. تأكيد خاطئ (بدون DELETE) → رفض.
6. كلمة مرور خاطئة → رفض بدون إغلاق.
7. نجاح → logout فوري ورسالة حذف.
8. محاولة login بعد الإغلاق → `ACCOUNT_CLOSED`.
