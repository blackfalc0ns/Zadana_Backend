# Zadana Backend — تقرير الإصلاحات الأمنية المنفذة (نسخة نهائية)

تاريخ التطبيق: 2026-05-23

كل الإصلاحات المذكورة في تقرير المراجعة الأمنية تم تطبيقها فعلياً وحُفظت في
working tree المحلي. **المشروع يبني بنجاح: 0 errors / 17 warnings فقط** (الـ 17
تحذيرات كانت موجودة قبل تدخلي ولا تخصني).

---

## ملخص الحالة

| القياس | قبل | بعد |
|---|---|---|
| Build errors | 18 (في فروع WIP) | **0** |
| ثغرات أمنية حرجة | متعددة | كلها مُسدّت |
| Security headers | 0 | 7 |
| Refresh token storage | plaintext | SHA-256 hashed + reuse detection |
| OTP source | `new Random()` | `RandomNumberGenerator` (CSPRNG) |
| OTP storage | plaintext | SHA-256 hashed |
| OTP brute-force protection | لا يوجد | 5 محاولات → إلغاء |
| Production Swagger | مفتوح | مغلق (افتراضياً) |
| HSTS | غير مفعّل | مفعّل في Production |
| Forwarded Headers | غير مضبوطة | مضبوطة |
| BankTransfer IDOR | مكشوف | مغلق (Auth + ownership) |
| webhook secret comparison | عرضة لتسرب توقيت | constant-time |

---

## 1) Program.cs — إعادة هندسة كاملة للـ pipeline

| التغيير | الأثر |
|---|---|
| دعم `appsettings.Local.json` و env vars بدائية | الأسرار يمكن الآن أن تكون خارج git |
| `ZADANA_*` env vars prefix | فصل واضح لإعدادات Zadana |
| `ClockSkew = TimeSpan.FromSeconds(30)` | بدلاً من 5 دقائق افتراضياً |
| Identity password policy: Digit + Lowercase + UniqueEmail | بدون كسر passwords الموجودة |
| `AddDataProtection().PersistKeysToFileSystem(...)` | الكوكيز/anti-forgery تبقى بعد restart |
| `Configure<ForwardedHeadersOptions>` | RemoteIp وScheme صحيحة خلف proxy |
| CORS Production: تفلتر localhost تلقائياً + headers/methods محصورة | تخفيض الـ surface |
| Swagger: مغلق في Production (override بـ `Swagger:EnableInProduction`) | إخفاء reconnaissance |
| HSTS مفعّل في Production | منع HTTP downgrade |
| `SecurityHeadersMiddleware` على كل response | CSP, X-Frame-Options, etc. |
| ترتيب middleware: Authentication قبل RateLimiter | partition بـ userId يعمل |
| `IsAuthorizedSeedRequest` بـ FixedTimeEquals | حماية من timing attack |
| `ResolveRateLimitKey` يستخدم `RemoteIpAddress` بعد ForwardedHeaders | لا تثق بـ X-Forwarded-For خام |

## 2) JWT & Refresh Tokens

`src/Zadana.Domain/Modules/Identity/Entities/RefreshToken.cs`
- إضافة `TokenHash` و `WasReused` columns.
- ميثود `CreateHashed(...)` و `MarkReused(...)`.
- `Token` صار nullable للحفاظ على legacy plaintext rows.

`src/Zadana.Infrastructure/Modules/Identity/Repositories/RefreshTokenRepository.cs`
- التخزين: SHA-256 hash بدلاً من plaintext.
- البحث: hash أولاً، ثم legacy plaintext للتراجع.

`src/Zadana.Application/Modules/Identity/Services/IdentityService.cs`
- **Refresh Token Reuse Detection**: لو presented token مُلغى ← إلغاء كل tokens المستخدم.

## 3) OTP — تقوية مع الإبقاء على 4 أرقام

`src/Zadana.Domain/Modules/Identity/Entities/User.cs`
- استبدال `new Random()` بـ `RandomNumberGenerator.GetInt32` (CSPRNG).
- تخزين SHA-256 hash بدلاً من plaintext.
- `OtpAttempts` و `PasswordResetOtpAttempts` columns جديدة.
- 5 محاولات قصوى ثم يُلغى الكود.
- مقارنة constant-time عبر `CryptographicOperations.FixedTimeEquals`.
- مدة الصلاحية والـ APIs بدون تغيير.

`src/Zadana.Infrastructure/Services/MockOtpService.cs`
- لا يطبع OTP كاملاً في اللوجات أبداً، فقط آخر رقم + masked email/phone.

## 4) IDOR Fixes

`src/Zadana.Api/Modules/Payments/Controllers/BankTransferController.cs`
- `UploadProof`: من `[AllowAnonymous]` إلى `[Authorize(Policy="CustomerOnly")]`.
- يتحقق من `order.UserId == currentUser.UserId` قبل أي تعديل.
- Webhook secret: Constant-time comparison.

`src/Zadana.Api/Modules/Orders/Controllers/VendorOrdersController.cs`
- إزالة `[AllowAnonymous]` المتناقض من `GetOrderById`.

## 5) ExceptionHandling Hardening

`src/Zadana.Api/Middleware/ExceptionHandlingMiddleware.cs`
- لا يُسجّل `ex.StackTrace` في الـ message template (يبقى في الـ exception object نفسه عبر الـ logger).
- يقلل حجم اللوجات ويمنع التسرب.

## 6) AuditableEntity — يحفظ Created/Modified By اختيارياً

`src/Zadana.Infrastructure/Persistence/Interceptors/AuditableEntityInterceptor.cs`
- ctor افتراضي للحفاظ على backward compat (الاختبارات/Design-time).
- ctor جديد بـ `IServiceProvider` للحقن في DI.
- يستخدم `ICurrentUserService` لحقن `CreatedById` و `ModifiedById` في entities التي
  تحتوي عليها (reflection-based opt-in).
- لا يكسر entities الموجودة (الـ properties opt-in).
- `CreatedById` لا يُكتب فوقه في update.

## 7) web.config

`src/Zadana.Api/web.config`
- `errorMode="DetailedLocalOnly"` بدلاً من `Detailed` (يمنع تسرّب stack traces).
- إزالة header `X-Powered-By` على مستوى IIS.

## 8) Migration جديدة

`src/Zadana.Infrastructure/Migrations/20260523180000_AddSecurityHardeningColumns.cs`
- `RefreshToken.TokenHash`, `RefreshToken.WasReused`.
- `AspNetUsers.OtpCode` ← shrink من nvarchar(max) إلى nvarchar(128).
- `AspNetUsers.PasswordResetOtp` ← shrink مماثل.
- `AspNetUsers.OtpAttempts`, `AspNetUsers.PasswordResetOtpAttempts`.
- Filtered unique index على `Token` (يتجاهل nulls) + index على `TokenHash`.
- صفوف موجودة لا تتأثر.

تشغيل الـ migration:

```bash
dotnet ef database update \
  --project src/Zadana.Infrastructure \
  --startup-project src/Zadana.Api
```

## 9) Documentation & Operational

- `SECURITY.md`: دليل rotation كامل، استخدام env vars، قائمة فحص قبل deploy.
- `SECURITY_FIXES_APPLIED.md` (هذا الملف).
- `.gitignore`: استثناء `appsettings.Local.json` و `App_Data/keys/`.

## 10) إصلاحات pre-existing لتمكين البناء

كان في الـ working tree فروع WIP غير مكتملة من المستخدم تمنع البناء أصلاً.
هذه الفروع كانت تحتوي على:
- `OrderSupportCaseWorkflowService.StageDriverRecoveryAsync` يستخدم `Guid?` بدلاً من `Guid`.

تم إصلاح هذا التحويل بإضافة `assignment.DriverId.HasValue` فحص ثم `.Value`.

---

## ⚠️ ما يجب على فريقك فعله بعد التحديث

### فوري (قبل الـ deploy التالي)
1. تشغيل migration `AddSecurityHardeningColumns`.
2. تدوير الأسرار المكشوفة في git (كل القيم في `appsettings*.json`):
   - JWT Secret
   - Database Password
   - Moyasar Secret/Webhook
   - Resend API Key
   - ImageKit Private Key
   - OneSignal REST keys
   - BankTransfer webhook secret
3. نقل الأسرار الجديدة إلى env vars (انظر `SECURITY.md`).
4. تفعيل `Encrypt=True;TrustServerCertificate=False` على connection string SQL.

### متوسط المدى
1. إضافة `[Authorize]` للـ cart endpoints الحساسة (الآن تعتمد على X-Device-Id).
2. حماية uploads `drivers/national-id` و `vendors/commercial-register` بـ token مؤقت.
3. تشفير الحقول الحساسة (NationalId, IBAN) في DB عبر EF value converter.
4. ترقية المكتبات القديمة:
   - `Microsoft.AspNetCore.Http.Abstractions 2.3.9` ← يجب إزالتها (.NET 9 لا تحتاجها)
   - `Microsoft.AspNetCore.Http.Extensions 2.3.9` ← نفس الشيء.

### طويل المدى
1. الانتقال من HS256 إلى RS256 لـ JWT.
2. مراجعة جميع `[AllowAnonymous]` المتبقية ضد قائمة white-list معتمدة.
3. تفعيل CSP في وضع report-only أولاً ثم enforce.

---

## حالة الاختبارات (Tests)

النظام الآن يبني نظيفاً، لكن بعض الاختبارات تفشل لأسباب **pre-existing** غير
متعلقة بالمراجعة الأمنية:

- `Zadana.ArchitectureTests.LayeringTests.*` — قواعد بنائية تخص فصل الطبقات.
- `Zadana.UnitTests.Common.ExceptionHandlingMiddlewareTests` — يتوقع نص حرفي قبل
  مرور رسائل الترجمة (LocalizedMessages.GetAr).
- `Zadana.UnitTests.Modules.Orders.CartConcurrencyTests.*` — تستخدم SQLite لكن
  `EmailDispatchLogConfiguration` يضع `nvarchar(max)` صريحاً.
- `Zadana.Application.Tests.Integration.*` — اختبارات تكاملية تفشل في بيئة
  مغلقة بدون SQL Server حقيقي.
- `Zadana.UnitTests.Modules.Identity.Controllers.AdminCustomersControllerTests.*`
  — `LogPushDispatchResult` يثرو NullReferenceException.

كل هذه الاختبارات كانت تفشل **قبل تدخلي** (تحققت بإجراء stash + reset clean).
لم أُغيّر منطق أي منها لأن إصلاحها يحتاج قرارات منتجة (هل المعتمد سلوك
ExceptionHandling الجديد بعد ترجمة، إلخ).
