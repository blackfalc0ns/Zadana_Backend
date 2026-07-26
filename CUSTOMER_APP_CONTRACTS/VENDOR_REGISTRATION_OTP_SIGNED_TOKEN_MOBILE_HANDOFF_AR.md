# Handoff — تسجيل التاجر + OTP (Signed Token)

تاريخ التحديث: 2026-07-26  
الحالة: `implemented`  
الجمهور: لوحة التاجر (Vendor Panel) / أي عميل موبايل للتاجر

---

## الخلاصة

بعد `register` بكلمة مرور **ما بيتسجلش حساب في السيرفر** لحد ما ينجح `verify-otp`.

- الإيميل والموبايل وبيانات التسجيل **مش بيتكتبوا في DB** قبل التحقق
- السيرفر بيرجع `registrationToken` (JWT)
- لو التاجر قفل الصفحة قبل OTP → كأنّه ما سجّلش أصلًا
- Login قبل OTP = حساب غير موجود

> تسجيل التاجر بـ **Google** كما هو: ينشئ الحساب فورًا (إيميل مؤكد) و**ما يحتاجش** `registrationToken`.

---

## Endpoints

| الخطوة | Method + Path |
| --- | --- |
| تسجيل | `POST /api/vendors/register` |
| تأكيد OTP | `POST /api/vendors/auth/verify-otp` |
| إعادة إرسال OTP | `POST /api/vendors/auth/resend-otp` |
| Login | `POST /api/vendors/auth/login` |

---

## 1) Register (كلمة مرور)

### Response 200 (قبل التحقق)

```json
{
  "tokens": null,
  "user": {
    "id": "temporary-guid-from-token",
    "fullName": "...",
    "email": "vendor@example.com",
    "phone": "0501234567",
    "role": "Vendor"
  },
  "isVerified": false,
  "message": "...",
  "registrationToken": "<JWT — احفظه>"
}
```

### المطلوب من الواجهة

1. احفظ `registrationToken` (مثلًا `sessionStorage`).
2. افتح شاشة OTP.
3. لا تستخدم `user.id` لأي authenticated API قبل نجاح verify.

---

## 2) Verify OTP

```json
{
  "identifier": "vendor@example.com",
  "otpCode": "1234",
  "registrationToken": "<من register>"
}
```

عند النجاح: `tokens` + `isVerified: true` + `message: "تم إنشاء الحساب بنجاح."`  
→ اعرض الرسالة، احفظ التوكنات، وامسح `registrationToken` المحلي.

### الأخطاء

خطأ API يرجع `detail` و **`message`** (نفس النص) + `errorCode`.

---

## 3) Resend OTP

```json
{
  "identifier": "vendor@example.com",
  "registrationToken": "<التوكن الحالي>"
}
```

الرد يرجع **`registrationToken` جديد** — لازم تستبدله فورًا.

Cooldown: 60 ثانية → `OTP_COOLDOWN`.

---

## 4) Google Signup

`POST /api/vendors/register` مع `googleIdToken`:

- ينشئ الحساب مباشرة
- يرجع tokens / جلسة جاهزة
- **لا** مسار OTP ولا `registrationToken`

---

## 5) Login قبل إكمال OTP

- خطأ حساب غير موجود
- الإيميل/الموبايل مش محجوزين → يقدر يعيد التسجيل

لو الحساب **مكتمل** فعلًا ورجّع `USER_ALREADY_EXISTS` → وجّه لشاشة Login (مش OTP).

---

## صلاحيات زمنية

| العنصر | المدة |
| --- | --- |
| جلسة التسجيل (`registrationToken`) | 24 ساعة |
| صلاحية OTP | 5 دقائق |
| Cooldown إعادة الإرسال | 1 دقيقة |

---

## Checklist

- [ ] `register` يحفظ `registrationToken`
- [ ] `verify-otp` يبعت التوكن
- [ ] `resend-otp` يبعت التوكن ويستبدل المحفوظ
- [ ] Google signup بدون OTP
- [ ] `USER_ALREADY_EXISTS` → Login
- [ ] ممنوع authenticated APIs قبل `isVerified: true`

---

## مرجع مشترك

`REGISTRATION_OTP_EMAIL_DEFER_HANDOFF_AR.md`
