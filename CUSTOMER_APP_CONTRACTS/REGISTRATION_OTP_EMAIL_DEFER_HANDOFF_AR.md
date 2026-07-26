# تسجيل الحساب — بدون حفظ بيانات قبل OTP (Signed Token)

تاريخ التحديث: 2026-07-26  
الحالة: `implemented`  
الجمهور: تطبيقات العميل / التاجر / المندوب

## الخلاصة

عند `register` (عميل / تاجر بكلمة مرور / مندوب):

- **لا يُنشأ صف في `AspNetUsers`**
- **لا تُحفظ الإيميل ولا الموبايل ولا أي بيانات تسجيل في قاعدة البيانات**
- السيرفر يرجّع `registrationToken` (JWT موقّع قصير العمر) يحمل بيانات التسجيل + هاش الـ OTP
- الحساب الحقيقي يُنشأ فقط بعد نجاح `verify-otp`

لو المستخدم ماكمّلش OTP → كأنّه ما سجّلش أصلًا (Login = حساب غير موجود، والإيميل/الموبايل مش محجوزين).

## التدفق

1. `POST .../register`  
   الرد: `isVerified: false`, `tokens: null`, **`registrationToken: "<jwt>"`**
2. العميل يدخل OTP
3. `POST .../auth/verify-otp` body:
   ```json
   {
     "identifier": "user@email.com",
     "otpCode": "1234",
     "registrationToken": "<jwt from register>"
   }
   ```
4. عند النجاح فقط: يُنشأ User + بيانات الدور، وتُصدر توكنات الدخول

## Resend OTP

`POST .../auth/resend-otp` يحتاج نفس الـ token:

```json
{
  "identifier": "user@email.com",
  "registrationToken": "<current jwt>"
}
```

الرد يرجع **`registrationToken` جديد** (لازم التطبيق يستبدله) لأن هاش الـ OTP اتغيّر.

Cooldown: دقيقة واحدة من آخر إرسال (مُضمّن في التوكن).

## ملاحظات للتطبيق

- احفظ `registrationToken` محليًا من رد `register` / `resend-otp` وأرسله مع `verify-otp` و `resend-otp`.
- `user.id` في رد التسجيل قبل التحقق مؤقت من داخل التوكن — لا تستخدمه لاستدعاءات authenticated.
- تسجيل التاجر بـ Google يظل ينشئ الحساب فورًا (الإيميل مؤكد من Google).
- صلاحية جلسة التسجيل: 24 ساعة (انتهاء الـ JWT).
- صلاحية OTP: 5 دقائق.

## ما لا يتغير

- شكل باقي حقول `AuthResponseDto` (`tokens`, `user`, `isVerified`, `message`)
- مسارات `verify-otp` / `resend-otp` (أُضيف حقل اختياري `registrationToken`)

## Handoff لكل تطبيق

- عميل: `CUSTOMER_REGISTRATION_OTP_SIGNED_TOKEN_MOBILE_HANDOFF_AR.md`
- مندوب: `DRIVER_REGISTRATION_OTP_SIGNED_TOKEN_MOBILE_HANDOFF_AR.md`
- تاجر: `VENDOR_REGISTRATION_OTP_SIGNED_TOKEN_MOBILE_HANDOFF_AR.md`
