# Zadana — تغييرات الأمان لتطبيق العميل (المستخدم)

تاريخ آخر تحديث: 2026-05-23 (الجولة الثالثة)
الجمهور: مبرمج تطبيق Customer / User

> هذا الملف خاص بتطبيق العميل فقط. لو تعمل على تطبيق المندوب، راجع
> `MOBILE_DRIVER_SECURITY_HANDOFF_AR.md`.

---

## 🔴 ملخص كل التغييرات اللي تخص تطبيق العميل

| # | المسار / الميزة | نوع التغيير | الإجراء المطلوب |
|---|---|---|---|
| 1 | `POST /api/orders/{orderId}/bank-transfer-proof` | ✋ كاسر | أضف `Authorization: Bearer <jwt>` |
| 2 | `POST /api/cart/items` و mutations الأخرى للضيف | ✋ كاسر | استدعِ `guest-token` ثم أضف `X-Device-Signature` |
| 3 | `POST /api/customers/auth/register` | ✋ جديد | أضف Cloudflare Turnstile token |
| 4 | `POST /api/customers/auth/forgot-password` | ✋ جديد | أضف Cloudflare Turnstile token |
| 5 | تدوير JWT Secret على الخادم | 🔄 مرة واحدة | أول 401 → افتح login screen |
| 6 | OTP Lockout / Password / Refresh | 🟢 شفاف | تحسينات UX |
| 7 | Logout يلغي JWT فوراً | 🟢 شفاف | يحتاج fallback handling فقط |

---

## 1️⃣ BankTransfer Proof Upload (Breaking)

### قبل
```http
POST /api/orders/{orderId}/bank-transfer-proof
Content-Type: application/json

{ "receiptFileUrl": "...", "bankReference": "...", ... }
```

### بعد
```http
POST /api/orders/{orderId}/bank-transfer-proof
Authorization: Bearer <customer_access_token>   ← مضاف
Content-Type: application/json

{ "receiptFileUrl": "...", "bankReference": "...", ... }
```

### حالات الخطأ
| الحالة | status | code |
|---|---|---|
| لا يوجد JWT | 401 | `USER_NOT_AUTHENTICATED` |
| JWT لمستخدم غير صاحب الطلب | 404 | `NotFound` |
| JWT لدور خطأ (Driver/Vendor) | 403 | `Forbidden` |

> نُرجع 404 (وليس 403) عمداً عند طلب لمستخدم آخر، لئلا نكشف وجود الطلب.

---

## 2️⃣ Cart Guest Signing (Breaking) — جديد

### المشكلة قبل
أي شخص يخمّن `X-Device-Id` لضيف آخر يقدر يعدّل سلته.

### الحل الجديد
الضيف يحصل على HMAC signature في بداية session ويرسلها مع كل mutation.

### الـ Flow

#### الخطوة 1 — اطلب signature (مرة واحدة لكل device id)
```http
POST /api/cart/guest-token
Content-Type: application/json

{ "deviceId": "<uuid-thabet-للجهاز>" }
```

**Response 200**:
```json
{
  "deviceId": "<uuid>",
  "signature": "abc123...",
  "deviceHeaderName": "X-Device-Id",
  "signatureHeaderName": "X-Device-Signature"
}
```

> الـ signature ثابتة لكل deviceId. احفظها في secure storage على الجهاز
> ولا تطلبها كل مرة. تظل صالحة طوال عمر الـ deviceId.

#### الخطوة 2 — استخدمها في كل mutation للسلة

| العملية | يحتاج signature؟ |
|---|---|
| `GET /api/cart` | ❌ (read-only) |
| `GET /api/cart/vendors` | ❌ |
| `POST /api/cart/items` | ✅ |
| `PATCH /api/cart/items/{id}` | ✅ |
| `DELETE /api/cart/items/{id}` | ✅ |
| `DELETE /api/cart` | ✅ |

```http
POST /api/cart/items
X-Device-Id: <uuid>
X-Device-Signature: <signature>
Content-Type: application/json

{ "productId": "...", "quantity": 1 }
```

> العميل **المُسجَّل** (لديه JWT) لا يحتاج signature إطلاقاً. الحاجة فقط
> للضيوف.

### حالات الخطأ
| الحالة | status | code |
|---|---|---|
| mutation بدون signature | 401 | `GUEST_CART_SIGNATURE_REQUIRED` |
| signature خاطئة | 401 | `GUEST_CART_SIGNATURE_REQUIRED` |
| `deviceId` ناقص في طلب التوقيع | 400 | `DEVICE_ID_REQUIRED` |

### مثال Dart (Flutter)

```dart
class GuestCartSession {
  final Dio dio;
  final SecureStorage storage;

  Future<({String deviceId, String signature})> ensureSignature() async {
    var deviceId = await storage.read('deviceId');
    var signature = await storage.read('deviceSignature');

    if (deviceId != null && signature != null) {
      return (deviceId: deviceId, signature: signature);
    }

    deviceId ??= const Uuid().v4();
    final resp = await dio.post(
      '/api/cart/guest-token',
      data: {'deviceId': deviceId},
    );
    signature = resp.data['signature'] as String;

    await storage.write('deviceId', deviceId);
    await storage.write('deviceSignature', signature);
    return (deviceId: deviceId, signature: signature);
  }

  Future<void> addCartItem(String productId, int quantity) async {
    final session = await ensureSignature();
    await dio.post(
      '/api/cart/items',
      data: {'productId': productId, 'quantity': quantity},
      options: Options(headers: {
        'X-Device-Id': session.deviceId,
        'X-Device-Signature': session.signature,
      }),
    );
  }
}
```

---

## 3️⃣ CAPTCHA على Register و Forgot-Password — جديد

التطبيقات اللي تخدم عملاء حقيقيين تحتاج Cloudflare Turnstile (مجاني، أسرع
من reCAPTCHA، أكثر privacy-friendly).

### المسارات المتأثرة (في تطبيق العميل)

| المسار | يحتاج CAPTCHA؟ |
|---|---|
| `POST /api/customers/auth/register` | ✅ |
| `POST /api/customers/auth/forgot-password` | ✅ |
| `POST /api/customers/auth/login` | ❌ (rate limit يكفي) |
| `POST /api/customers/auth/verify-otp` | ❌ |
| `POST /api/customers/auth/reset-password` | ❌ (مع OTP) |

### الـ Flow

#### الخطوة 1 — أضف Cloudflare Turnstile widget
```html
<!-- في شاشة التسجيل / forgot-password -->
<div class="cf-turnstile"
     data-sitekey="<CLOUDFLARE_SITE_KEY>"
     data-callback="onCaptchaSuccess"></div>
```

في Flutter: استخدم package مثل `flutter_turnstile` أو `webview_flutter` لعرض
الـ widget.

#### الخطوة 2 — أرسل token مع الـ request
```http
POST /api/customers/auth/register
Content-Type: application/json
X-Bot-Challenge-Token: <token من Turnstile>

{
  "fullName": "...",
  "email": "...",
  "phone": "...",
  "password": "..."
}
```

> أو احقن `captchaToken` في body الـ JSON إن كان أسهل.

### حالات الخطأ
| الحالة | status | code |
|---|---|---|
| لا يوجد token | 400 | `BOT_CHALLENGE_FAILED` (`MISSING_TOKEN`) |
| token غير صالح | 400 | `BOT_CHALLENGE_FAILED` (`INVALID_TOKEN`) |
| فشل اتصال بـ Cloudflare | 400 | `BOT_CHALLENGE_FAILED` (`VERIFY_HTTP_ERROR`) |

> ⚠️ في staging/dev قد يكون الـ Turnstile غير مفعّل (مفتاحه غير مُعرَّف في
> الخادم). في تلك الحالة **لن** يطلب الـ API token. التطبيق يجب أن:
> - يحاول قراءة الـ Cloudflare site key من config على الخادم.
> - لو غير مفعّل (مثلاً عبر endpoint feature-flags)، تخطى الـ widget.
> - أو ببساطة: أرسل token دائماً، الخادم يتجاهله إذا لم يكن مفعَّلاً.

---

## 4️⃣ JWT Secret Rotation (يوم نشر الباك إند)

عند نشر التحديث:
- كل access tokens القديمة ستفشل (التوقيع تغيّر).
- كل refresh tokens القديمة ستفشل أيضاً.
- المستخدم يفتح التطبيق → 401 → فشل refresh → login screen.

### تأثير على الكود
**صفر تغيير**. تأكد فقط أن:
- 401 → محاولة refresh تلقائية مرة واحدة.
- لو refresh فشل → امسح التوكن المحلي وافتح login.

### تأثير على UX
يوم النشر: كل العملاء يُسجَّل خروجهم. مقصود.

---

## 🟢 تغييرات شفافة (لا تحتاج تعديل كود إلا لتحسين UX)

### Logout يلغي JWT فوراً
قبل: بعد logout، الـ JWT يبقى صالح للاستخدام حتى انتهاء صلاحيته (60 دقيقة).
بعد: logout يضيف الـ JTI للـ revocation list فوراً → أي استخدام بعد ذلك = 401.

**تأثير على الموبايل**: لا شيء. أنت بالفعل تمسح التوكن بعد logout.

**سيناريو نادر**: لو طلب آخر كان "في الطريق" أثناء logout → سيرجع 401
`TOKEN_REVOKED`. تعامل معه كأي 401 عادي.

### Refresh Token Reuse Detection (محسّن)
لو سُرق refresh token وحاول السارق استخدامه:
- النظام يلغي **كل** refresh tokens للمستخدم
- **بالإضافة** يلغي **كل** access tokens النشطة فوراً (جديد!)
- السارق وأنت كلاكما تحتاج login جديد

**الحل**: استخدم mutex/lock حول refresh API call:

```dart
final _refreshLock = Lock();

Future<TokenPair> refreshTokens(String refreshToken) {
  return _refreshLock.synchronized(() async {
    return await api.refresh(refreshToken);
  });
}
```

### OTP Lockout بعد 3 إنهاءات (محسّن)
- 5 محاولات OTP خاطئة = إنهاء كود واحد (كان بالفعل).
- **3 إنهاءات متتالية** = قفل الحساب 60 دقيقة من طلبات OTP جديدة (جديد).
- التحقق الناجح يُعيد تعيين العداد.

```http
POST /api/customers/auth/resend-otp
{ "email": "user@example.com" }
```
**Response 401 (إذا الحساب مقفول)**:
```json
{ "code": "OTP_ACCOUNT_LOCKED", "message": "..." }
```

**التحسين المُقترَح للـ UX**: لو رأيت `OTP_ACCOUNT_LOCKED`، اعرض:
> "تم قفل الحساب لمدة ساعة بسبب محاولات متعددة. حاول لاحقاً أو تواصل مع الدعم."

### Password Validation
عند التسجيل / تغيير كلمة المرور:
- 8 أحرف كحد أدنى
- حرف صغير على الأقل
- رقم على الأقل

```dart
final passwordRegex = RegExp(r'^(?=.*[a-z])(?=.*\d).{8,}$');
```

### ClockSkew تقلّص إلى 30 ثانية
كان 5 دقائق، صار 30 ثانية. تأكد أن ساعة الجهاز مزامنة.

### SlidingWindow Rate Limiter
كان FixedWindow (يسمح بـ burst في حدود النافذة)، صار SlidingWindow.

**تأثير**: لو ترسل طلبات كثيرة بسرعة، قد ترى `429 RATE_LIMIT_EXCEEDED` أكثر
شيوعاً. الحل: أضف retry-with-backoff في HTTP interceptor.

---

## 📋 قائمة فحص شاملة لتطبيق العميل

- [ ] `bank-transfer-proof` يرسل JWT.
- [ ] Cart guest flow: `guest-token` ثم `X-Device-Signature` على mutations.
- [ ] CAPTCHA token على register و forgot-password.
- [ ] Mutex/lock في refresh-token flow.
- [ ] Password validation محلياً.
- [ ] Retry-with-backoff للـ 429.
- [ ] التعامل مع `OTP_ACCOUNT_LOCKED` كرسالة خاصة.
- [ ] التعامل مع `TOKEN_REVOKED` كـ 401 عادي → login screen.
- [ ] 401 على أي endpoint → محاولة refresh مرة واحدة → login screen عند الفشل.

---

## ❓ سيناريوهات اختبار جاهزة

### 1) Cart Guest Hijacking Prevention
1. ضيف A: `POST /api/cart/guest-token` بـ deviceId=`A` → احصل signature `S_A`.
2. ضيف A يضيف عنصر بـ `X-Device-Id: A` + `X-Device-Signature: S_A` → 200.
3. (سلبي) محاكي يحاول `X-Device-Id: A` بدون signature → 401.
4. (سلبي) محاكي يستخدم signature خاطئة → 401.

### 2) CAPTCHA على Register
1. شاشة register تعرض Turnstile widget.
2. المستخدم يحلّ الـ challenge → token.
3. ارسل `POST /api/customers/auth/register` مع `X-Bot-Challenge-Token`.
4. (سلبي) ارسل بدون token → 400 `BOT_CHALLENGE_FAILED`.

### 3) BankTransfer Proof
1. login كعميل → JWT.
2. أنشئ طلب bank-transfer.
3. `POST /api/orders/{id}/bank-transfer-proof` مع JWT → 200.
4. (سلبي) أرسله بدون JWT → 401.
5. (سلبي) login بعميل آخر، أرسل لنفس الـ orderId → 404.

### 4) JWT Revocation
1. login → JWT_1.
2. logout.
3. حاول endpoint مع JWT_1 → 401 `TOKEN_REVOKED`.
4. تطبيق يفتح login screen.

### 5) Refresh Token Reuse
1. login، خزّن refresh1.
2. refresh بـ refresh1 → refresh2.
3. (race) refresh **مرة ثانية** بـ refresh1 → 401.
4. حاول refresh2 → 401 `USER_TOKENS_REVOKED` (كل التوكنات أُلغيت).
5. التطبيق يفتح login screen.

### 6) OTP Account Lockout
1. اطلب OTP → ادخل خطأ 5 مرات → كود مُلغى (إنهاء 1).
2. اطلب OTP جديد → ادخل خطأ 5 مرات → إنهاء 2.
3. اطلب OTP جديد → ادخل خطأ 5 مرات → إنهاء 3.
4. حاول `resend-otp` → 401 `OTP_ACCOUNT_LOCKED`.
5. انتظر 60 دقيقة (أو admin unlock) → عادي.

---

## ❌ تغييرات لا تخص تطبيق العميل

هذه التغييرات **لا تتعلق بتطبيق العميل**:
- Registration Upload Token (لرفع وثائق السائقين/الفندورز فقط).
- مسارات `drivers/national-id`, `drivers/license`, `drivers/vehicle`.

تجاهل أي ذكر لهم.

---

## نقطة اتصال

لو ظهرت سلوكيات غير متوقعة:
- شارك `traceId` من response.
- شارك `Method + Path + status + errorCode`.
- شارك التوقيت UTC.

سيتم رصد الطلب في `SystemLogEntries` للـ debugging.
