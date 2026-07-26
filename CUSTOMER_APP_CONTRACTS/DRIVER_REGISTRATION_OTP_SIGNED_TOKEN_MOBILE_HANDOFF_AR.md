# Handoff Flutter — تسجيل المندوب + OTP (Signed Token)

تاريخ التحديث: 2026-07-26  
الحالة: `implemented`  
الجمهور: تطبيق المندوب فقط

---

## الخلاصة

بعد `register` **ما بيتسجلش حساب مندوب في السيرفر** لحد ما ينجح `verify-otp`.

- الإيميل والموبايل وبيانات التسجيل **مش بيتكتبوا في DB** قبل التحقق
- السيرفر بيرجع `registrationToken` (JWT)
- لو المندوب قفل التطبيق قبل OTP → كأنّه ما سجّلش أصلًا
- Login قبل OTP = حساب غير موجود

> ملاحظة: `register` على `/api/drivers/register` بينما `verify-otp` / `resend-otp` على `/api/drivers/auth/...`

---

## Endpoints

| الخطوة | Method + Path |
| --- | --- |
| تسجيل | `POST /api/drivers/register` |
| تأكيد OTP | `POST /api/drivers/auth/verify-otp` |
| إعادة إرسال OTP | `POST /api/drivers/auth/resend-otp` |
| Login | `POST /api/drivers/auth/login` |

Rate limit: سياسة `Auth`.

---

## 1) Register

### Request

```json
{
  "fullName": "Ahmed Driver",
  "email": "driver@example.com",
  "phone": "0501234567",
  "password": "P@ssword1234",
  "vehicleType": "Car",
  "nationalId": "1234567890",
  "licenseNumber": "DL-12345",
  "nationalIdExpiryDate": "2028-01-01T00:00:00Z",
  "driverLicenseExpiryDate": "2028-01-01T00:00:00Z",
  "vehicleLicenseNumber": "V-9988",
  "vehicleLicenseExpiryDate": "2028-01-01T00:00:00Z",
  "address": "Riyadh",
  "region": "RIYADH",
  "city": "RIYADH",
  "nationalIdFrontImageUrl": "https://...",
  "nationalIdBackImageUrl": "https://...",
  "licenseImageUrl": "https://...",
  "vehicleImageUrl": "https://...",
  "personalPhotoUrl": "https://..."
}
```

> `region` و `city` **مطلوبين** (أكواد الجغرافيا التشغيلية).

### Response 200

```json
{
  "tokens": null,
  "user": {
    "id": "temporary-guid-from-token",
    "fullName": "Ahmed Driver",
    "email": "driver@example.com",
    "phone": "0501234567",
    "role": "Driver",
    "mustChangePassword": false,
    "profilePhotoUrl": "https://..."
  },
  "isVerified": false,
  "message": "...",
  "driverStatus": null,
  "registrationToken": "<JWT — احفظه>"
}
```

### المطلوب من Flutter

1. احفظ `registrationToken` محليًا لحد ما تخلص شاشة OTP.
2. روح لشاشة إدخال OTP.
3. **متستخدمش** `user.id` لأي authenticated API قبل نجاح verify.
4. امنع double-submit على زر التسجيل.

### أخطاء شائعة

| Code | المعنى |
| --- | --- |
| `USER_ALREADY_EXISTS` | إيميل أو موبايل لحساب مكتمل بالفعل |
| `DRIVER_SERVICE_AREA_REQUIRED` | `region` / `city` ناقصين |
| validation / geography errors | منطقة/مدينة غير تشغيلية |

---

## 2) Verify OTP

### Request

```json
{
  "identifier": "driver@example.com",
  "otpCode": "1234",
  "registrationToken": "<نفس التوكن من register>"
}
```

> `identifier` = الإيميل أو الموبايل.  
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
    "fullName": "Ahmed Driver",
    "email": "driver@example.com",
    "phone": "0501234567",
    "role": "Driver",
    "mustChangePassword": false
  },
  "isVerified": true,
  "message": "تم إنشاء الحساب بنجاح.",
  "driverStatus": {
    "...": "قد يظهر حسب حالة المندوب بعد الإنشاء"
  },
  "registrationToken": null
}
```

### المطلوب من Flutter

1. اعرض `message` عند النجاح.
2. احفظ `tokens`.
3. امسح `registrationToken` المحلي.
4. وجّه حسب `driverStatus` / حالة المراجعة (لو التطبيق بيعتمد عليها بعد التسجيل).

### الأخطاء

خطأ API يرجع `detail` و **`message`** (نفس النص) + `errorCode`.

### أخطاء شائعة

| Code | المعنى | Action |
| --- | --- | --- |
| `INVALID_OTP` | كود غلط أو منتهي | إعادة محاولة / Resend |
| `USER_NOT_FOUND` | توكن فاسد/منتهي أو identifier مش مطابق | ارجع لـ register من جديد |
| `USER_ALREADY_EXISTS` | حد سجّل بنفس البيانات أثناء الانتظار | وجّه لـ login |

---

## 3) Resend OTP

### Request

```json
{
  "identifier": "driver@example.com",
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

1. **استبدل** `registrationToken` بالجديد فورًا.
2. `verify-otp` بعد كده يستخدم التوكن الجديد فقط.
3. Cooldown: **60 ثانية** → `OTP_COOLDOWN` لو اتبعت بدري.

### أخطاء شائعة

| Code | المعنى |
| --- | --- |
| `OTP_COOLDOWN` | استنى قبل إعادة الإرسال |
| `USER_NOT_FOUND` | مفيش توكن أو توكن منتهي |
| `INVALID_OTP` | جلسة التسجيل انتهت (TTL) |

---

## 4) Login قبل إكمال OTP

لو المندوب سجّل وماكمّلش OTP، وبعدين حاول login:

- السيرفر بيرجع خطأ حساب غير موجود
- **مش** `ACCOUNT_NOT_VERIFIED`
- الإيميل/الموبايل مش محجوزين → يقدر يعيد `register` بنفس البيانات

---

## قواعد التخزين في التطبيق

| المفتاح | متى تحفظه | متى تمسحه |
| --- | --- | --- |
| `registrationToken` | بعد `register` / بعد كل `resend-otp` | بعد verify ناجح أو إلغاء شاشة OTP |
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

- [ ] `POST /api/drivers/register` يحفظ `registrationToken`
- [ ] شاشة OTP تبعت التوكن على `POST /api/drivers/auth/verify-otp`
- [ ] `resend-otp` على `/api/drivers/auth/resend-otp` ويستبدل التوكن
- [ ] ممنوع authenticated APIs قبل `isVerified: true`
- [ ] لو التوكن ضاع/انتهى → رجّع لـ register من الأول
- [ ] معالجة `OTP_COOLDOWN` بـ timer على زر Resend
- [ ] تأكد `region` + `city` قبل ما تبعت register

---

## مراجع إضافية

- سلوك الباك إند العام:  
  `REGISTRATION_OTP_EMAIL_DEFER_HANDOFF_AR.md`
