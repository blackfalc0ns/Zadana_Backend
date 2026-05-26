# Zadana — تغييرات الأمان لتطبيق المندوب (السائق/Driver)

تاريخ آخر تحديث: 2026-05-23 (الجولة الثالثة)
الجمهور: مبرمج تطبيق Driver

> هذا الملف خاص بتطبيق المندوب فقط. لو تعمل على تطبيق العميل، راجع
> `MOBILE_CUSTOMER_SECURITY_HANDOFF_AR.md`.

---

## 🔴 ملخص كل التغييرات اللي تخص تطبيق المندوب

| # | المسار / الميزة | نوع التغيير | الإجراء المطلوب |
|---|---|---|---|
| 1 | `POST /api/files/upload` لوثائق التسجيل الحساسة | ✋ كاسر | اطلب Registration Upload Token قبل الرفع |
| 2 | `POST /api/drivers/auth/forgot-password` | ✋ جديد | أضف Cloudflare Turnstile token |
| 3 | تدوير JWT Secret على الخادم | 🔄 مرة واحدة | أول 401 → افتح login screen |
| 4 | OTP Lockout / Password / Refresh | 🟢 شفاف | تحسينات UX |
| 5 | Logout يلغي JWT فوراً | 🟢 شفاف | يحتاج fallback handling فقط |
| 6 | SignalR hubs بعد revocation | 🟢 شفاف | reconnect logic |

---

## 1️⃣ Registration Upload Token (Breaking) ⚠️ هام جداً

أهم تغيير لتطبيق المندوب.

### المشكلة قبل
أي شخص anonymous يقدر يرفع لـ `drivers/national-id` بدون authentication.

### المسارات المتأثرة في تطبيق المندوب

| Directory في form-data | المثال |
|---|---|
| `drivers/national-id` | صورة بطاقة الهوية (front + back) |
| `drivers/license` | رخصة قيادة |
| `drivers/vehicle` | استمارة السيارة |
| `drivers/profile` | صورة شخصية |

> **`drivers/proofs`** (إثبات التسليم) **لم يتغيّر** — يستخدم JWT المندوب
> العادي بعد تسجيل الدخول.

### الـ Flow الجديد

#### الخطوة 1 — اطلب Registration Upload Token (في بداية شاشة التسجيل)

```http
POST /api/registration-upload-tokens/issue
Content-Type: application/json
X-Device-Id: <مُعرّف الجهاز>

{ "deviceId": "<اختياري>" }
```

**Response 200**:
```json
{
  "token": "cmVnaXN0cmF0aW9uX3VwbG9hZHwxNzMyM...IY9pP1kS",
  "expiresAtUtc": "2026-05-23T19:25:00Z",
  "headerName": "X-Registration-Upload-Token"
}
```

- مدة الصلاحية: **15 دقيقة**.
- نفس الـ token يكفي لرفع كل وثائق التسجيل.
- لو انتهت صلاحيته، اطلب جديد.

#### الخطوة 2 — استخدم الـ token عند رفع الوثائق

```http
POST /api/files/upload
X-Registration-Upload-Token: <التوكن>
Content-Type: multipart/form-data

file: <البايتات>
directory: drivers/national-id
```

**Response 200**: `{ "url": "https://ik.imagekit.io/.../nid-front.jpg" }`

#### الخطوة 3 — أكمل تسجيل السائق

استخدم الـ URLs المُرجَعة في `POST /api/drivers/register`. الـ JWT للسائق
يُولَّد بعد التسجيل.

### حالات الخطأ الجديدة

| الحالة | status | error code |
|---|---|---|
| لا يوجد header | 401 | `TOKEN_MISSING` |
| التوكن فارغ أو مكسور | 401 | `TOKEN_MALFORMED` |
| توقيع غلط | 401 | `TOKEN_INVALID_SIGNATURE` |
| انتهت الصلاحية (> 15 دقيقة) | 401 | `TOKEN_EXPIRED` |
| المسار `directory` فيه `..` أو غير معروف | 400 | `INVALID_UPLOAD_DIRECTORY` |

### مثال Dart (Flutter)

```dart
class DriverRegistrationService {
  final Dio dio;
  String? _uploadToken;
  String? _tokenHeaderName;
  DateTime? _tokenExpiry;

  Future<String> _ensureUploadToken(String deviceId) async {
    if (_uploadToken != null &&
        _tokenExpiry != null &&
        _tokenExpiry!.isAfter(DateTime.now().toUtc().add(const Duration(seconds: 30)))) {
      return _uploadToken!;
    }

    final resp = await dio.post(
      '/api/registration-upload-tokens/issue',
      data: {'deviceId': deviceId},
      options: Options(headers: {'X-Device-Id': deviceId}),
    );

    _uploadToken = resp.data['token'] as String;
    _tokenHeaderName = resp.data['headerName'] as String;
    _tokenExpiry = DateTime.parse(resp.data['expiresAtUtc'] as String);
    return _uploadToken!;
  }

  Future<String> uploadDriverDocument({
    required String deviceId,
    required String localPath,
    required String directory,    // 'drivers/national-id' وما شابه
    required String filename,
  }) async {
    final token = await _ensureUploadToken(deviceId);
    final formData = FormData.fromMap({
      'file': await MultipartFile.fromFile(localPath, filename: filename),
      'directory': directory,
    });
    final resp = await dio.post(
      '/api/files/upload',
      data: formData,
      options: Options(headers: {_tokenHeaderName!: token}),
    );
    return resp.data['url'] as String;
  }

  Future<void> registerDriver({...}) async {
    final deviceId = await _getDeviceId();

    final nationalIdFrontUrl = await uploadDriverDocument(
      deviceId: deviceId,
      localPath: '...',
      directory: 'drivers/national-id',
      filename: 'nid-front.jpg',
    );

    final nationalIdBackUrl = await uploadDriverDocument(
      deviceId: deviceId,
      localPath: '...',
      directory: 'drivers/national-id',
      filename: 'nid-back.jpg',
    );

    final licenseUrl = await uploadDriverDocument(
      deviceId: deviceId,
      localPath: '...',
      directory: 'drivers/license',
      filename: 'license.jpg',
    );

    await dio.post('/api/drivers/register', data: {
      'fullName': '...',
      'nationalIdFrontImageUrl': nationalIdFrontUrl,
      'nationalIdBackImageUrl': nationalIdBackUrl,
      'licenseImageUrl': licenseUrl,
    });
  }
}
```

### نصيحة UX
ابدأ شاشة التسجيل بطلب الـ token تلقائياً في الـ background. السائق ما لازم
يلاحظ شيئاً.

### إعادة رفع وثيقة بعد التسجيل
لو الإدارة رفضت وثيقة وطلبت إعادة رفعها:
- السائق الآن مُسجَّل ولديه JWT
- استخدم `Authorization: Bearer <jwt>` (لا حاجة لـ Registration Upload Token)
- نفس الـ endpoint `POST /api/files/upload`

---

## 2️⃣ CAPTCHA على Forgot-Password — جديد

### المسارات المتأثرة (في تطبيق المندوب)

| المسار | يحتاج CAPTCHA؟ |
|---|---|
| `POST /api/drivers/auth/forgot-password` | ✅ |
| `POST /api/drivers/auth/login` | ❌ |
| `POST /api/drivers/auth/verify-otp` | ❌ |
| `POST /api/drivers/auth/reset-password` | ❌ |
| `POST /api/drivers/register` | ❌ (محمي بـ Registration Token) |

### الـ Flow

#### أضف Cloudflare Turnstile widget في شاشة forgot-password
في Flutter: استخدم `flutter_turnstile` أو `webview_flutter`.

#### أرسل token مع الـ request
```http
POST /api/drivers/auth/forgot-password
Content-Type: application/json
X-Bot-Challenge-Token: <token>

{ "email": "driver@example.com" }
```

### حالات الخطأ
| الحالة | status | code |
|---|---|---|
| لا يوجد token | 400 | `BOT_CHALLENGE_FAILED` (`MISSING_TOKEN`) |
| token غير صالح | 400 | `BOT_CHALLENGE_FAILED` (`INVALID_TOKEN`) |

> ⚠️ في staging/dev الـ Turnstile قد يكون غير مفعّل. التطبيق:
> - يحاول قراءة الـ Cloudflare site key من config.
> - لو غير مفعّل، تخطى الـ widget.
> - أو ببساطة: أرسل token دائماً، الخادم يتجاهله إذا غير مفعَّل.

---

## 3️⃣ JWT Secret Rotation (يوم النشر)

عند نشر التحديث:
- كل access tokens القديمة ستفشل.
- كل refresh tokens القديمة ستفشل.
- المندوب يفتح التطبيق → 401 → فشل refresh → login screen.

### تأثير على الكود
**صفر تغيير**.

### تأثير على UX
يوم النشر: كل المندوبين يُسجَّل خروجهم. مقصود.

---

## 🟢 تغييرات شفافة (تحسينات UX)

### Logout يلغي JWT فوراً
قبل: JWT يبقى صالح حتى انتهاء صلاحيته (60 دقيقة) بعد logout.
بعد: logout يضيف JTI لـ revocation list فوراً.

**تأثير عملي**: لو طلب آخر "في الطريق" أثناء logout → 401 `TOKEN_REVOKED`.
تعامل معه كأي 401 عادي.

### Refresh Token Reuse Detection (محسّن)
لو سُرق refresh token:
- النظام يلغي **كل** refresh tokens للمندوب
- **بالإضافة** يلغي **كل** access tokens النشطة فوراً (جديد!)
- السارق وأنت كلاكما تحتاجان login جديد

**الحل**: استخدم mutex حول refresh API call:

```dart
final _refreshLock = Lock();

Future<TokenPair> refreshTokens(String refreshToken) {
  return _refreshLock.synchronized(() async {
    return await api.refresh(refreshToken);
  });
}
```

### OTP Account Lockout (محسّن)
- 5 محاولات OTP خاطئة = إنهاء كود (موجود سابقاً).
- **3 إنهاءات متتالية = قفل الحساب 60 دقيقة** من طلبات OTP جديدة (جديد).

```http
POST /api/drivers/auth/resend-otp
{ "email": "driver@example.com" }
```
**Response 401**:
```json
{ "code": "OTP_ACCOUNT_LOCKED", "message": "..." }
```

**تحسين UX**: اعرض رسالة واضحة:
> "تم قفل الحساب لمدة ساعة بسبب محاولات متعددة. حاول لاحقاً أو تواصل مع الدعم."

### Password Validation
- 8 أحرف + حرف صغير + رقم.

```dart
final passwordRegex = RegExp(r'^(?=.*[a-z])(?=.*\d).{8,}$');
```

### ClockSkew = 30 ثانية
ساعة الجهاز يجب أن تكون مزامنة.

### SignalR Hubs بعد Revocation
لو سُرق JWT المندوب وحدث reuse-detection:
- كل JWTs المندوب تُلغى فوراً
- SignalR hubs (`/hubs/order-tracking`, `/hubs/notifications`) ستقطع الاتصال
- الموبايل لازم يحاول reconnect → سيفشل بـ 401 → login screen

```dart
// مثال SignalR client مع auto-reconnect
final connection = HubConnectionBuilder()
    .withUrl(hubUrl, options: HttpConnectionOptions(
      accessTokenFactory: () async => accessToken,
    ))
    .withAutomaticReconnect()
    .build();

connection.onclose((error) {
  // لو الخطأ 401 → افتح login
});
```

### SlidingWindow Rate Limiter
ترقية من FixedWindow إلى SlidingWindow → 429 أكثر شيوعاً عند burst.
أضف retry-with-backoff.

---

## 📌 خصوصية تطبيق المندوب

### `[Authorize(Policy="DriverOnly")]` — لم تتغيّر
كل endpoints المندوب الموجودة (المهام، اللوكيشن، الويلت، إلخ) تستخدم JWT
العادي وما تغيّرت.

### `drivers/proofs` — لم يتغيّر
يستخدم JWT المندوب (ليس Registration Upload Token).

### Driver Account Appeals — لم تتغيّر
`POST /api/drivers/account-support/appeals` لا يزال anonymous + rate-limited.

### Real-time tracking (SignalR) — لم تتغيّر سلوكياً
كل hubs تستخدم JWT المندوب. لكن قد ينقطع الاتصال إذا حدث revocation.

---

## 📋 قائمة فحص شاملة لتطبيق المندوب

- [ ] Registration Upload Token قبل رفع وثائق السائق.
- [ ] Caching للـ token (15 دقيقة) لتجنب طلبه قبل كل ملف.
- [ ] CAPTCHA token على forgot-password.
- [ ] Mutex/lock في refresh-token flow.
- [ ] Password validation محلياً.
- [ ] Retry-with-backoff للـ 429.
- [ ] التعامل مع `OTP_ACCOUNT_LOCKED`.
- [ ] التعامل مع `TOKEN_REVOKED` و `USER_TOKENS_REVOKED` كـ 401.
- [ ] إعادة الاتصال بـ SignalR بعد 401 → login screen.

---

## ❓ سيناريوهات اختبار جاهزة

### 1) Driver Registration Flow الكامل
1. اطلب `POST /api/registration-upload-tokens/issue` → احفظ token.
2. ارفع `drivers/national-id` (front) بـ token → 200.
3. ارفع `drivers/national-id` (back) بنفس token → 200.
4. ارفع `drivers/license` بنفس token → 200.
5. ارفع `drivers/vehicle` بنفس token → 200.
6. ارفع `drivers/profile` بنفس token → 200.
7. أكمل `POST /api/drivers/register` بالـ URLs.
8. (سلبي) ارفع بدون header → 401 `TOKEN_MISSING`.
9. (سلبي) انتظر 16 دقيقة ثم ارفع → 401 `TOKEN_EXPIRED`.

### 2) CAPTCHA على Forgot-Password
1. شاشة forgot-password تعرض Turnstile widget.
2. حلّ challenge → token.
3. ارسل request مع `X-Bot-Challenge-Token` → 200.
4. (سلبي) بدون token → 400 `BOT_CHALLENGE_FAILED`.

### 3) Document Re-upload بعد Rejection
1. السائق مُسجَّل ولديه JWT.
2. الإدارة رفضت `national-id` وطلبت إعادة رفعها.
3. التطبيق يستخدم JWT العادي (ليس Registration Token):
   ```http
   POST /api/files/upload
   Authorization: Bearer <driver_jwt>
   Content-Type: multipart/form-data

   file: ...
   directory: drivers/national-id
   ```
4. → 200 OK.

### 4) JWT Revocation
1. login → JWT_1.
2. logout.
3. حاول endpoint مع JWT_1 → 401 `TOKEN_REVOKED`.
4. تطبيق يفتح login screen.

### 5) Refresh Token Reuse
1. login، خزّن refresh1.
2. refresh بـ refresh1 → refresh2.
3. (race) refresh **مرة ثانية** بـ refresh1 → 401.
4. حاول refresh2 → 401 `USER_TOKENS_REVOKED`.
5. SignalR hubs المتصلة تنقطع.
6. التطبيق يفتح login screen.

### 6) OTP Account Lockout
1. اطلب OTP → خطأ 5 مرات → كود مُلغى (إنهاء 1).
2. اطلب OTP جديد → خطأ 5 → إنهاء 2.
3. اطلب OTP جديد → خطأ 5 → إنهاء 3.
4. حاول `resend-otp` → 401 `OTP_ACCOUNT_LOCKED`.

---

## ❌ تغييرات لا تخص تطبيق المندوب

- `POST /api/orders/{orderId}/bank-transfer-proof` → تطبيق العميل فقط.
- Cart guest signing → تطبيق العميل فقط.
- CAPTCHA على register → المندوب لا يحتاج (Registration Token يكفي).

---

## نقطة اتصال

لو ظهرت سلوكيات غير متوقعة:
- شارك `traceId` من response.
- شارك `Method + Path + status + errorCode`.
- شارك التوقيت UTC.

سيتم رصد الطلب في `SystemLogEntries` للـ debugging.
