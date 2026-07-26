# Handoff Flutter — تسجيل العميل + OTP (Signed Token)

تاريخ التحديث: 2026-07-26  
الحالة: `implemented`  
الجمهور: تطبيق العميل فقط

---

## الخلاصة

بعد `register` **ما بيتسجلش حساب في السيرفر** لحد ما ينجح `verify-otp`.

- الإيميل والموبايل **مش بيتكتبوا في DB** قبل التحقق
- السيرفر بيرجع `registrationToken` (JWT)
- لو المستخدم قفل التطبيق قبل OTP → كأنّه ما سجّلش أصلًا
- Login قبل OTP = حساب غير موجود

---

## Endpoints

| الخطوة | Method + Path |
| --- | --- |
| تسجيل | `POST /api/customers/auth/register` |
| تأكيد OTP | `POST /api/customers/auth/verify-otp` |
| إعادة إرسال OTP | `POST /api/customers/auth/resend-otp` |
| Login | `POST /api/customers/auth/login` |

Rate limit: سياسة `Auth`.  
`register` عليه `BotChallenge`.

---

## 1) Register

### Request

```json
{
  "fullName": "Ahmed Ali",
  "email": "ahmed@example.com",
  "phone": "0501234567",
  "password": "P@ssword1234",
  "profilePhotoUrl": null,
  "addressLine": "Street 1",
  "label": "Home",
  "buildingNo": "12",
  "floorNo": "3",
  "apartmentNo": "5",
  "city": "Riyadh",
  "area": "Olaya",
  "latitude": 24.7136,
  "longitude": 46.6753
}
```

### Response 200

```json
{
  "tokens": null,
  "user": {
    "id": "temporary-guid-from-token",
    "fullName": "Ahmed Ali",
    "email": "ahmed@example.com",
    "phone": "0501234567",
    "role": "Customer",
    "mustChangePassword": false,
    "profilePhotoUrl": null
  },
  "isVerified": false,
  "message": "...",
  "registrationToken": "<JWT — احفظه>"
}
```

### المطلوب من Flutter

1. احفظ `registrationToken` محليًا (secure storage / memory حتى انتهاء شاشة OTP).
2. روح لشاشة إدخال OTP.
3. **متستخدمش** `user.id` لأي authenticated API قبل نجاح verify.

### أخطاء شائعة

| Code | المعنى |
| --- | --- |
| `USER_ALREADY_EXISTS` | إيميل أو موبايل موجود فعلًا في حساب مكتمل |
| `EMAIL_REQUIRED` / validation | بيانات ناقصة أو غير صالحة |

---

## 2) Verify OTP

### Request

```json
{
  "identifier": "ahmed@example.com",
  "otpCode": "1234",
  "registrationToken": "<نفس التوكن من register>"
}
```

> `identifier` = الإيميل أو الموبايل اللي اتسجل بيه.  
> `registrationToken` **إجباري** لمسار التسجيل الجديد.

### Response 200 (نجاح)

```json
{
  "tokens": {
    "accessToken": "...",
    "refreshToken": "..."
  },
  "user": {
    "id": "real-user-guid",
    "fullName": "Ahmed Ali",
    "email": "ahmed@example.com",
    "phone": "0501234567",
    "role": "Customer",
    "mustChangePassword": false
  },
  "isVerified": true,
  "message": "تم إنشاء الحساب بنجاح.",
  "registrationToken": null
}
```

### المطلوب من Flutter

1. اعرض `message` للمستخدم عند النجاح.
2. احفظ `tokens` وخلّي المستخدم logged in.
3. امسح `registrationToken` المحلي.
4. كمّل onboarding / home.

### الأخطاء

خطأ API (4xx/5xx) يرجع ProblemDetails وفيه:
- `detail` و **`message`** = نص الخطأ للعرض
- `errorCode` = كود ثابت (مثل `INVALID_OTP`)

### أخطاء شائعة

| Code | المعنى | Action |
| --- | --- | --- |
| `INVALID_OTP` | كود غلط أو منتهي | خلّي المستخدم يعيد المحاولة / يطلب resend |
| `USER_NOT_FOUND` | توكن فاسد/منتهي أو identifier مش مطابق | ارجع لشاشة التسجيل من جديد |
| `USER_ALREADY_EXISTS` | حد سجّل بنفس الإيميل/الموبايل أثناء الانتظار | اعرض رسالة ووجّه لـ login |

---

## 3) Resend OTP

### Request

```json
{
  "identifier": "ahmed@example.com",
  "registrationToken": "<التوكن الحالي>"
}
```

### Response 200

```json
{
  "tokens": null,
  "user": { "...": "..." },
  "isVerified": false,
  "message": "OTP resent successfully",
  "registrationToken": "<JWT جديد — لازم تستبدله>"
}
```

### المطلوب من Flutter

1. **استبدل** `registrationToken` المحفوظ بالجديد فورًا.
2. أي `verify-otp` بعد كده لازم يستخدم التوكن الجديد.
3. Cooldown: **60 ثانية** من آخر إرسال → لو اتبعت بدري هيرجع `OTP_COOLDOWN`.

### أخطاء شائعة

| Code | المعنى |
| --- | --- |
| `OTP_COOLDOWN` | استنى الوقت المتبقي |
| `USER_NOT_FOUND` | مفيش `registrationToken` أو توكن منتهي |
| `INVALID_OTP` | جلسة التسجيل منتهية (TTL) |

---

## 4) Login قبل إكمال OTP

لو المستخدم عمل register وماكمّلش OTP، وبعدين حاول login بنفس الإيميل/الباسورد:

- السيرفر بيرجع خطأ حساب غير موجود (`AccountNotFound`)
- **مش** هيرجع `ACCOUNT_NOT_VERIFIED`
- الإيميل/الموبايل مش محجوزين → يقدر يعمل register تاني بنفس البيانات

---

## قواعد التخزين في التطبيق

| المفتاح | متى تحفظه | متى تمسحه |
| --- | --- | --- |
| `registrationToken` | بعد `register` / بعد كل `resend-otp` | بعد verify ناجح أو خروج من شاشة OTP + TTL انتهى |
| `accessToken` / `refreshToken` | بعد verify ناجح فقط | logout عادي |

---

## صلاحيات زمنية

| العنصر | المدة |
| --- | --- |
| جلسة التسجيل (`registrationToken`) | 24 ساعة |
| صلاحية OTP | 5 دقائق |
| Cooldown إعادة الإرسال | 1 دقيقة |

---

## Checklist تنفيذ Flutter

- [ ] `register` يحفظ `registrationToken`
- [ ] شاشة OTP تبعت `registrationToken` في `verify-otp`
- [ ] `resend-otp` يبعت التوكن ويستبدل المحفوظ بالجديد
- [ ] ممنوع login/authenticated APIs قبل `isVerified: true`
- [ ] لو التوكن ضاع أو انتهى → رجّع المستخدم لـ register من الأول
- [ ] معالجة `OTP_COOLDOWN` بـ timer على زر Resend

---

## مرجع مشترك

سلوك الباك إند العام:  
`REGISTRATION_OTP_EMAIL_DEFER_HANDOFF_AR.md`
