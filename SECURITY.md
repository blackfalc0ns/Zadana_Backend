# Zadana Backend — دليل الأمان وإدارة الأسرار

هذه الوثيقة توضح خطة الأمان والمعايير المطبقة في مشروع Zadana Backend بعد تطبيق المراجعة الأمنية.

---

## 1) الأسرار (Secrets)

### الأسرار التي تُعتبر مكشوفة (Compromised)

كل القيم التالية موجودة حالياً في `appsettings*.json` داخل git ويجب **تدويرها (rotate)**:

| المفتاح | الموقع | الإجراء |
|---|---|---|
| `ConnectionStrings:DefaultConnection` (Password) | جميع الملفات | تغيير كلمة مرور SQL وتفعيل `Encrypt=True` |
| `JwtSettings:Secret` | جميع الملفات | استبدال بقيمة عشوائية 64+ بايت لكل بيئة |
| `ResendSettings:ApiKey` (`re_Aqw1mq...`) | Production | إلغاء/تدوير المفتاح من لوحة Resend |
| `ImageKit:PrivateKey` | جميع الملفات | تدوير من لوحة ImageKit |
| `Moyasar:SecretKey`, `WebhookSecret`, `PublishableKey` | جميع الملفات | تدوير من لوحة Moyasar |
| `OneSignal:RestApiKey`, `DriverRestApiKey`, `AdminWebRestApiKey` | جميع الملفات | تدوير من لوحة OneSignal |
| `BankTransfer:WebhookSecret` | Production | تدوير من نظام البنك |

### الترتيب الجديد لمصادر الإعدادات

في `Program.cs` يقرأ التطبيق الإعدادات من المصادر التالية (الأخير يفوز):

1. `appsettings.json`
2. `appsettings.{Environment}.json` (Development / Production)
3. `appsettings.Local.json` ← **مُستثنى من git**؛ مكان الأسرار محلياً
4. User Secrets (Development فقط)
5. Environment Variables (مفضّل في Production)
6. Environment Variables بـ prefix `ZADANA_`

### مثال: تشغيل محلياً بأسرار غير مكشوفة

أنشئ `src/Zadana.Api/appsettings.Local.json` (لن يُرفع لـ git):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Encrypt=True;TrustServerCertificate=False;User Id=...;Password=..."
  },
  "JwtSettings": {
    "Secret": "<64 byte base64>"
  },
  "Moyasar": {
    "SecretKey": "sk_test_...",
    "WebhookSecret": "..."
  },
  "ResendSettings": {
    "ApiKey": "re_..."
  },
  "ImageKit": {
    "PrivateKey": "private_..."
  }
}
```

### مثال: تشغيل في Production عبر Environment Variables

```
ConnectionStrings__DefaultConnection=...
JwtSettings__Secret=...
Moyasar__SecretKey=...
Moyasar__WebhookSecret=...
ResendSettings__ApiKey=...
ImageKit__PrivateKey=...
OneSignal__RestApiKey=...
OneSignal__DriverRestApiKey=...
OneSignal__AdminWebRestApiKey=...
BankTransfer__WebhookSecret=...
DataProtection__KeysPath=/var/zadana/keys   # مسار مستديم لمفاتيح DataProtection
```

ملاحظة: استخدام `__` (double underscore) كفاصل في أسماء متغيرات البيئة عند استبدال `:`.

---

## 2) Authentication / Authorization

| الميزة | الوضع الحالي |
|---|---|
| JWT Issuer/Audience | يُتحقق منهما |
| ClockSkew | 30 ثانية (بدلاً من 5 دقائق الافتراضية) |
| Refresh Token | SHA-256 hashed قبل التخزين + reuse-detection |
| Password Hashing | ASP.NET Identity (PBKDF2) |
| Password Policy | 8 أحرف + رقم + حرف صغير |
| Account Lockout | 5 محاولات فاشلة → 15 دقيقة |

عند رصد **Refresh Token Reuse** (يعني token مُلغى يُستخدم مجدداً)، النظام يلغي **كل** refresh tokens النشطة لذلك المستخدم تلقائياً.

---

## 3) OTP

- 4 أرقام عشوائية عبر `RandomNumberGenerator.GetInt32` (CSPRNG).
- يُخزن SHA-256 hash للكود في DB، وليس الكود نفسه.
- 5 محاولات فاشلة كحد أقصى ثم يُلغى الكود تلقائياً.
- مدة الصلاحية: 5 دقائق (login OTP) و 15 دقيقة (password reset).
- لا يُكتب الكود الكامل أبداً في اللوجات، فقط fingerprint.

---

## 4) Rate Limiting

| Policy | الحد |
|---|---|
| `Auth` | 12 طلب/دقيقة |
| `FileUploads` | 20 طلب / 10 دقائق |
| `PaymentCallbacks` | 120 طلب/دقيقة |

المفتاح: `userId` للمستخدمين المُصادَقين، وإلا `RemoteIpAddress` (بعد UseForwardedHeaders).

---

## 5) Security Headers (مُطبَّقة على كل response)

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: no-referrer`
- `Permissions-Policy: geolocation=(), microphone=(), camera=(), payment=()`
- `Cross-Origin-Opener-Policy: same-origin`
- `Strict-Transport-Security` (Production فقط بـ UseHsts)
- `Content-Security-Policy` (مرن بما يكفي لـ Swagger)
- إخفاء `Server` و `X-Powered-By`

---

## 6) CORS

- **Development/Testing**: localhost / 127.0.0.1 مسموح بمرونة.
- **Production**: فقط `https://` origins من قائمة `Cors:AllowedOrigins`؛ localhost يُفلتر تلقائياً.
- Headers و Methods محصورة (لا `AllowAnyHeader` في production).

---

## 7) DataProtection

- Keys تُحفظ على القرص في `DataProtection:KeysPath` (env: `DataProtection__KeysPath`).
- افتراضي: `<ContentRoot>/App_Data/keys`.
- في Container/Cloud: ضع المسار على volume مستديم لتجنّب فقدان الـ cookies / antiforgery عند restart.

---

## 8) Webhooks

- Moyasar webhook: التحقق بـ `CryptographicOperations.FixedTimeEquals` على `X-Moyasar-Signature`.
- BankTransfer webhook: التحقق بـ `CryptographicOperations.FixedTimeEquals` على `X-BankTransfer-Secret`.
- Seeding endpoints: التحقق بـ constant-time على `X-Seeding-Key`.

---

## 9) IDOR & Authorization

- `BankTransferController.UploadProof` صار يتطلب JWT ويتحقق من ملكية الطلب.
- `VendorOrdersController.GetOrderById` لم يعد `[AllowAnonymous]`.
- `OrderTrackingHub` (SignalR) يتحقق من ownership لكل دور قبل subscribe.
- `RequireAccessFilter` يتحقق من `permission_version` claim لإلغاء الصلاحيات فوراً.

---

## 10) Swagger

- Production: مغلق افتراضياً.
- لتفعيله مؤقتاً (staging-like): `Swagger:EnableInProduction=true`.

---

## 11) خطوات التدوير الموصى بها (Rotation Runbook)

1. **JWT Secret**:
   - ولّد قيمة عشوائية: `[Convert]::ToBase64String((1..64 | %{Get-Random -Maximum 256}))`
   - ضعها في env var `JwtSettings__Secret`.
   - انتبه: تدوير الـ secret يُلغي كل JWT تكنات النشطة (المستخدمون يحتاجون login جديد).

2. **DB Password**:
   - غيّر من لوحة DBaaS، حدّث `ConnectionStrings__DefaultConnection`.
   - فعّل `Encrypt=True;TrustServerCertificate=False`.

3. **Moyasar / Resend / ImageKit / OneSignal**:
   - من لوحات المزود اطلب `rotate key`، انسخ الجديد للـ env var.
   - الـ webhooks ستلتقط الـ secret الجديد عند إعادة تشغيل التطبيق.

---

## 12) قائمة فحص قبل الـ Deploy

- [ ] `appsettings.Production.json` خالي من أي secret حقيقي (placeholder فقط).
- [ ] جميع متغيرات البيئة المذكورة أعلاه مضبوطة.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] HTTPS مُفعّل، الشهادة سارية.
- [ ] `DataProtection__KeysPath` يشير إلى volume مستديم.
- [ ] الـ DB connection string يستخدم `Encrypt=True`.
- [ ] الـ migration `AddSecurityHardeningColumns` تم تطبيقها على DB.
- [ ] الـ logs لا تحتوي على OTP / passwords / tokens.
