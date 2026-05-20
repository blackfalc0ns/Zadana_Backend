# تعديلات الموبايل المطلوبة: OTP + صورة البروفايل

## 1. التسجيل وتفعيل الحساب بالـ OTP

بعد `register` الحساب بيتعمل وبيتبعت OTP على الإيميل، لكن الباك إند **مش هيرجع tokens** قبل تفعيل الحساب.

### Register response قبل التفعيل

```json
{
  "tokens": null,
  "user": {
    "id": "...",
    "fullName": "...",
    "email": "...",
    "phone": "...",
    "role": "Customer",
    "profilePhotoUrl": null
  },
  "isVerified": false,
  "message": "Your email address is not verified yet..."
}
```

### المطلوب في الموبايل

- بعد `register` لا تدخل المستخدم على التطبيق.
- افتح شاشة OTP مباشرة.
- خزّن `email` أو `identifier` مؤقتًا فقط لاستخدامه في `verify-otp`.
- لا تعتمد على tokens من `register` لأنها هتكون `null`.

### Verify OTP

```http
POST /api/customers/auth/verify-otp
```

```json
{
  "identifier": "user@email.com",
  "otpCode": "1234"
}
```

بعد نجاح `verify-otp` هترجع `tokens`، وساعتها فقط خزّنها وادخل المستخدم للتطبيق.

نفس الفكرة للتاجر والمندوب:

```http
POST /api/vendors/auth/verify-otp
POST /api/drivers/auth/verify-otp
```

## 2. تسجيل الدخول

لو المستخدم حاول يعمل `login` قبل تفعيل الإيميل، الباك إند هيرجع Unauthorized برسالة إن الحساب غير مفعل.

### المطلوب في الموبايل

- لو `login` رجع 401 ورسالة email not verified، افتح شاشة OTP.
- استخدم نفس الإيميل/رقم الهاتف كـ `identifier`.
- ممكن تعرض زر "إعادة إرسال الكود".

### Resend OTP

```http
POST /api/customers/auth/resend-otp
```

```json
{
  "identifier": "user@email.com"
}
```

## 3. صورة البروفايل

كل responses الخاصة بالمستخدم بقت بترجع:

```json
"profilePhotoUrl": "https://..."
```

موجودة في:

- `login`
- `verify-otp`
- `resend-otp`
- `GET /me`
- تحديث بيانات المستخدم
- تحديث صورة البروفايل

## 4. رفع صورة البروفايل

ارفع الصورة أولًا باستخدام endpoint الملفات.

```http
POST /api/files/upload
Authorization: Bearer ACCESS_TOKEN
Content-Type: multipart/form-data
```

Form data:

```text
file: image file
directory: uploads/users/profile
```

Response:

```json
{
  "url": "https://..."
}
```

## 5. إضافة أو تغيير صورة البروفايل

بعد ما ترفع الصورة وتاخد `url`، ابعته هنا.

### Customer

```http
PUT /api/customers/auth/me/profile-photo
Authorization: Bearer ACCESS_TOKEN
```

```json
{
  "profilePhotoUrl": "https://..."
}
```

### Vendor

```http
PUT /api/vendors/auth/me/profile-photo
```

### Driver

```http
PUT /api/drivers/auth/me/profile-photo
```

Response بيرجع `CurrentUserDto` محدث وفيه `profilePhotoUrl`.

## 6. حذف صورة البروفايل

### Customer

```http
DELETE /api/customers/auth/me/profile-photo
Authorization: Bearer ACCESS_TOKEN
```

### Vendor

```http
DELETE /api/vendors/auth/me/profile-photo
```

### Driver

```http
DELETE /api/drivers/auth/me/profile-photo
```

بعد الحذف `profilePhotoUrl` هيرجع `null`.

## 7. ملاحظات مهمة للموبايل

- ممنوع إدخال المستخدم للتطبيق قبل `verify-otp`.
- لا تخزن tokens لو `tokens == null`.
- عند نجاح `verify-otp` خزّن `accessToken` و `refreshToken`.
- لو أي endpoint محمي رجع 401 بسبب عدم تفعيل الإيميل، رجّع المستخدم لشاشة OTP.
- صورة البروفايل لازم تكون URL حقيقي `http` أو `https`، مش `file://` ولا `blob:`.
