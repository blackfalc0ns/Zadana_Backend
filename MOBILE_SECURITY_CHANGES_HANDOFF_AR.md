# Zadana — تغييرات الأمان وتأثيرها على الموبايل

تاريخ النشر: 2026-05-23 (محدّث)
الجمهور: مبرمج Customer / Driver / Vendor mobile apps

---

## 🔴 ملخص التغييرات الكاسرة (Breaking)

| # | المسار | التغيير | الإجراء المطلوب |
|---|---|---|---|
| 1 | `POST /api/orders/{orderId}/bank-transfer-proof` | يحتاج JWT للعميل | أضف `Authorization: Bearer <jwt>` |
| 2 | `POST /api/files/upload` (لمسارات التسجيل الحساسة) | يحتاج Registration Upload Token | اطلب token قبل الرفع |
| 3 | تدوير JWT Secret على الخادم | كل refresh tokens القديمة بطلت | أول `/auth/refresh` بعد النشر سيرجع 401 → افتح login screen |

كل التغييرات الأخرى (Refresh reuse-detection, OTP lockout, Password policy,
ClockSkew, Security Headers...) شفافة للموبايل.

---

## 1️⃣ BankTransfer Proof Upload (نفس التغيير السابق)

### قبل
```http
POST /api/orders/{orderId}/bank-transfer-proof
Content-Type: application/json

{ "receiptFileUrl": "...", "bankReference": "...", ... }
```

### بعد
```http
POST /api/orders/{orderId}/bank-transfer-proof
Authorization: Bearer <customer_access_token>
Content-Type: application/json

{ "receiptFileUrl": "...", "bankReference": "...", ... }
```

### حالات الخطأ
| الحالة | status | code |
|---|---|---|
| لا يوجد JWT | 401 | `USER_NOT_AUTHENTICATED` |
| JWT لمستخدم غير صاحب الطلب | 404 | `NotFound` |
| JWT لدور خطأ (Driver/Vendor) | 403 | `Forbidden` |

---

## 2️⃣ رفع وثائق التسجيل (جديد) — هام لمبرمج تطبيقات السائقين والفندورز

### المسارات المتأثرة

هذه الـ "directories" في الـ form-data لرفع الملفات:

| Directory | المثال |
|---|---|
| `drivers/national-id` | صورة بطاقة الهوية للسائق |
| `drivers/license` | رخصة قيادة |
| `drivers/vehicle` | استمارة السيارة |
| `drivers/profile` | صورة شخصية للسائق |
| `uploads/vendors/commercial-register` | سجل تجاري |
| `uploads/vendors/tax-certificates` | شهادة ضريبية |
| `uploads/vendors/licenses` | تراخيص الفندور |

> الـ uploads العامة مثل `uploads/vendors/logos`, `uploads/catalog/categories`,
> `uploads/catalog/brands`, `uploads/catalog/products` **لم تتغيّر** ولا تحتاج
> token.

### الـ Flow الجديد

#### الخطوة 1 — اطلب Registration Upload Token

```http
POST /api/registration-upload-tokens/issue
Content-Type: application/json
X-Device-Id: <مُعرّف الجهاز للموبايل>

{ "deviceId": "<اختياري؛ نفس X-Device-Id>" }
```

**Response 200**:
```json
{
  "token": "cmVnaXN0cmF0aW9uX3VwbG9hZHwxNzMyM...IY9pP1kS",
  "expiresAtUtc": "2026-05-23T19:25:00Z",
  "headerName": "X-Registration-Upload-Token"
}
```

- مدة صلاحية الـ token: **15 دقيقة**.
- يكفي token واحد لكل عملية تسجيل (يقدر يرفع عدة ملفات بنفس الـ token).
- لو انتهى صلاحيته أثناء التسجيل، اطلب واحد جديد.

#### الخطوة 2 — استخدم الـ token عند الرفع

```http
POST /api/files/upload
X-Registration-Upload-Token: <التوكن من الخطوة 1>
Content-Type: multipart/form-data

file: <البايتات>
directory: drivers/national-id
```

**Response 200**:
```json
{ "url": "https://ik.imagekit.io/.../national-id-front.jpg" }
```

#### الخطوة 3 — أكمل الـ registration كالعادة

استخدم الـ URLs المُرجَعة في `POST /api/drivers/register` أو
`POST /api/vendors/register` كالعادة. الـ JWT ينشأ بعد التسجيل.

### حالات الخطأ

| الحالة | status | error code |
|---|---|---|
| لا يوجد header `X-Registration-Upload-Token` | 401 | `TOKEN_MISSING` |
| التوكن فارغ أو شكله غلط | 401 | `TOKEN_MALFORMED` |
| التوكن صالح شكلياً لكن التوقيع غلط | 401 | `TOKEN_INVALID_SIGNATURE` |
| التوكن انتهت صلاحيته (> 15 دقيقة) | 401 | `TOKEN_EXPIRED` |
| التوكن لغرض آخر | 401 | `TOKEN_WRONG_PURPOSE` |
| المسار `directory` غير معروف أو فيه `..` | 400 | `INVALID_UPLOAD_DIRECTORY` |

### مثال Dart (Flutter)

```dart
// 1) اطلب التوكن
final tokenResp = await dio.post(
  '/api/registration-upload-tokens/issue',
  data: {'deviceId': deviceId},
  options: Options(headers: {'X-Device-Id': deviceId}),
);
final uploadToken = tokenResp.data['token'] as String;
final headerName = tokenResp.data['headerName'] as String; // X-Registration-Upload-Token

// 2) ارفع الملف
final formData = FormData.fromMap({
  'file': await MultipartFile.fromFile(localPath, filename: 'national-id.jpg'),
  'directory': 'drivers/national-id',
});
final uploadResp = await dio.post(
  '/api/files/upload',
  data: formData,
  options: Options(headers: {headerName: uploadToken}),
);
final imageKitUrl = uploadResp.data['url'] as String;

// 3) أكمل التسجيل بالـ URL
await dio.post('/api/drivers/register', data: {
  'fullName': '...',
  'nationalIdFrontImageUrl': imageKitUrl,
  // ... باقي البيانات
});
```

### كم المسارات التي تحتاج token؟
- مسار واحد فقط = drivers/national-id-front
- نفس الـ token الواحد يكفي لكل ملفات التسجيل (front + back + license + vehicle).
- اطلب token جديد فقط لو انتهت صلاحيته (15 دقيقة).

---

## 3️⃣ JWT Secret Rotation (لمّا الباك إند ينشر)

عند نشر التحديث الجديد على الخادم:

- **كل** access tokens النشطة الآن ستفشل (التوقيع تغيّر).
- **كل** refresh tokens النشطة ستفشل أيضاً.
- المستخدم يفتح التطبيق → أول API call → 401 → الموبايل يحاول refresh →
  refresh يفشل → الموبايل يفتح login screen.

### التأثير على الموبايل
**صفر تغيير في الكود** — هذا السلوك المتوقع لمّا يفشل refresh.

تأكد فقط أن:
- 401 على أي endpoint يحاول refresh تلقائياً.
- لو فشل refresh → امسح التوكن المحلي وافتح login screen.

### ملاحظة لتجربة المستخدم
يوم النشر، كل المستخدمين سيطلب منهم login جديد. هذا مقصود (نتيجة الـ rotation
الأمني). أبلغ فريق الـ marketing لو فيه إعلان وقت النشر.

---

## 🟢 تغييرات شفافة (لا تحتاج تدخل من الموبايل)

### Refresh Token Reuse Detection
لو أرسل المبرمج نفس refresh token مرتين متوازيتين (race condition) → النظام
يلغي **كل** جلسات المستخدم.

**الحل**: استخدم mutex/lock حول الـ refresh API call:

```dart
final _refreshLock = Lock();

Future<TokenPair> refreshTokens(String refreshToken) {
  return _refreshLock.synchronized(() async {
    // فقط استدعاء واحد في كل لحظة
    return await api.refresh(refreshToken);
  });
}
```

### OTP Lockout
4 أرقام كما هو، لكن بعد 5 محاولات فاشلة الكود يُلغى.

**التحسين المُقترَح**: لو ظهر `INVALID_OTP` 4 مرات، اعرض زر "أرسل كود جديد"
بشكل أوضح.

### Password Policy للتسجيل
تطلب الآن: 8 أحرف + رقم + حرف صغير.

**الحل**: حدّث validation محلياً:
```dart
RegExp(r'^(?=.*[a-z])(?=.*\d).{8,}$')
```
وإلا الـ API يرجع 400 مع `errorCode = "VALIDATION_ERROR"`.

### ClockSkew
انخفض من 5 دقائق إلى 30 ثانية.

**التأثير**: لو ساعة الجهاز فيها فرق كبير عن UTC، الـ JWT يُرفض بسرعة. عادة
الموبايلات تزامن تلقائياً، لذا غالباً صفر تأثير.

---

## 📋 قائمة فحص الموبايل

- [ ] تحديث `bank-transfer-proof` لإرسال JWT.
- [ ] إضافة flow الـ Registration Upload Token قبل رفع وثائق السائقين/الفندورز.
- [ ] قفل race conditions في refresh-token flow.
- [ ] تحديث validation كلمة المرور (digit + lowercase).
- [ ] اختبار سيناريو "5 محاولات OTP فاشلة" → عرض زر resend.
- [ ] التأكد أن 401 على أي endpoint يفتح login screen بعد فشل refresh.

---

## ❓ سيناريوهات اختبار جاهزة

### 1) Driver Registration Flow الجديد
1. اطلب `POST /api/registration-upload-tokens/issue` → احفظ token.
2. ارفع `drivers/national-id-front` بـ token → 200 OK.
3. ارفع `drivers/national-id-back` بنفس token → 200 OK.
4. ارفع `drivers/license` بنفس token → 200 OK.
5. أكمل `POST /api/drivers/register` بالـ URLs المُرجَعة.
6. (اختبار سلبي) ارفع بدون token → 401 `TOKEN_MISSING`.
7. (اختبار سلبي) انتظر 16 دقيقة ثم ارفع → 401 `TOKEN_EXPIRED`.

### 2) BankTransfer Proof
1. login كعميل، احصل على JWT.
2. أنشئ طلب bank-transfer.
3. `POST /api/orders/{orderId}/bank-transfer-proof` مع JWT → 200.
4. أرسله بدون JWT → 401.
5. login بعميل آخر، حاول إرسال لنفس الـ orderId → 404.

### 3) JWT Rotation Effect
1. login → احفظ tokens.
2. (نشر تحديث الباك إند).
3. حاول أي API call → 401.
4. حاول refresh → 401.
5. تطبيق يفتح login screen تلقائياً.

---

## نقاط اتصال

لو ظهرت سلوكيات غير متوقعة:
- شارك `traceId` من response.
- شارك `Method + Path + status + errorCode`.
- شارك التوقيت UTC.

سيتم رصد الطلب في `SystemLogEntries` للـ debugging.
