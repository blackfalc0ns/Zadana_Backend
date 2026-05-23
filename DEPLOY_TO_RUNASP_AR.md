# دليل النشر على runasp.net — Zadana Backend

تاريخ التحديث: 2026-05-23

هذا الدليل يشرح كيف تنشر التطبيق على runasp.net مع تأمين الأسرار خارج git.

---

## 🎯 الخطة بإيجاز

1. تعمل publish محلياً.
2. ترفع ملفات الـ publish إلى runasp.net.
3. ترفع `web.config` الإنتاجي (الذي يحتوي على الأسرار) **مرة واحدة فقط** يدوياً عبر File Manager في runasp.net.
4. تشغّل migrations على DB.
5. Restart Application Pool.

---

## الخطوة 1 — تشغيل publish محلياً

من PowerShell في مجلد المشروع:

```powershell
dotnet publish src/Zadana.Api/Zadana.Api.csproj -c Release -o ./publish
```

ستجد ملفات النشر في `./publish/`.

---

## الخطوة 2 — تجهيز web.config الإنتاجي

افتح ملف `deploy/web.config.production` (موجود محلياً، لن يُرفع لـ git):

- إذا كانت أسرار قاعدة البيانات / Moyasar / Resend / ImageKit / OneSignal **هي نفسها القديمة المسرّبة**، الملف جاهز كما هو ولكنه يحتاج rotation فوري بعد النشر (راجع `SECURITY.md`).
- إذا قمت بتدويرها (recommended)، عدّل القيم الجديدة في الملف.

> ملاحظة: لتوليد JWT Secret جديد عشوائي:
> ```powershell
> pwsh scripts/rotate-secrets.ps1 -Format env
> ```

---

## الخطوة 3 — رفع ملفات الـ publish

### الخيار A: عبر FTP / SFTP من runasp.net
1. لوحة runasp.net → File Manager.
2. ادخل مجلد التطبيق (عادةً `wwwroot/` أو `httpdocs/`).
3. ارفع كل محتوى مجلد `./publish/` (لكن **ليس** `web.config`).

### الخيار B: ZIP deploy
1. اضغط محتوى `./publish/` في ZIP (لا تُضمّن `web.config` فيه).
2. ارفع الـ ZIP عبر File Manager.
3. Extract في مجلد التطبيق.

> ⚠️ احذف `web.config` الذي يأتي مع الـ publish (يحتوي على placeholders فقط).

---

## الخطوة 4 — رفع web.config الإنتاجي يدوياً

هذه الخطوة الحاسمة لأمان الأسرار:

1. افتح File Manager → مجلد التطبيق.
2. احذف `web.config` الموجود (إن وُجد).
3. ارفع `deploy/web.config.production` من جهازك المحلي وسمّه `web.config`.
4. تأكد أن الـ `<environmentVariables>` فيه القيم الفعلية للأسرار.

> ⚠️ هذا الملف **لا يُسلَّم لـ git أبداً**. مكانه فقط على الخادم.

---

## الخطوة 5 — تشغيل Database Migrations

من جهازك المحلي:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=db47624.public.databaseasp.net,1433;Database=db47624;User Id=db47624;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;"

dotnet ef database update `
    --project src/Zadana.Infrastructure `
    --startup-project src/Zadana.Api
```

أو شغّل سكربت SQL المُولَّد:

```powershell
dotnet ef migrations script --idempotent `
    --project src/Zadana.Infrastructure `
    --startup-project src/Zadana.Api `
    --output deploy/migrations.sql
```

ثم نفّذ `deploy/migrations.sql` على قاعدة البيانات الإنتاجية عبر:
- لوحة databaseasp.net → SQL Editor → نفّذ السكربت.

---

## الخطوة 6 — Restart Application Pool

1. لوحة runasp.net → Application.
2. زر **Restart Application Pool** (أو Stop ثم Start).

---

## الخطوة 7 — التحقق من نجاح النشر

### 7.1 صحة الخدمة
```powershell
curl https://zadana.runasp.net/health
```
يجب أن تحصل على `200 OK` و JSON يقول `"status":"Healthy"`.

### 7.2 Security Headers
```powershell
curl -I https://zadana.runasp.net/health
```
يجب أن ترى:
- `Strict-Transport-Security: max-age=...`
- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `Content-Security-Policy: ...`

### 7.3 Swagger مغلق
```powershell
curl -I https://zadana.runasp.net/swagger
```
يجب أن تحصل على `404` أو redirect (Swagger مغلق في Production).

### 7.4 Login يعمل
أرسل POST لـ `/api/customer-auth/login` بحساب اختبار. يجب أن يرجع access + refresh tokens.

---

## ⚠️ ماذا لو فشل البدء؟

التطبيق يطبع أخطاء تشخيصية في `stdout` log. اعرضه من File Manager:
- `logs/stdout_*.log`

أكثر الأخطاء شيوعاً:

| الخطأ | الحل |
|---|---|
| `JwtSettings:Secret must be at least 32 bytes` | استبدل JWT Secret بـ قيمة 64 بايت من `rotate-secrets.ps1` |
| `Connection string ... Encrypt=False` | غيّر `Encrypt=False` إلى `Encrypt=True` في web.config |
| `the following required secrets are not configured` | تأكد أن environmentVariables في web.config يحوي كل المفاتيح |
| `JWT Secret is not configured` | تأكد من ASPNETCORE_ENVIRONMENT=Production و web.config صحيح |

---

## 🔁 تحديث التطبيق لاحقاً

عند نشر تحديث جديد:

1. شغّل `dotnet publish` محلياً.
2. ارفع كل ملفات `./publish/` **ما عدا web.config**.
3. لا تلمس `web.config` الإنتاجي — يبقى كما هو.
4. إذا كان فيه migration جديد، شغّله من DB editor.
5. Restart Application Pool.

> الفائدة: web.config (وأسراره) محفوظ على الخادم. التحديثات لا تُسرّب الأسرار.

---

## 🔄 تدوير سر معيّن لاحقاً

مثال: أردت تدوير Moyasar Secret.

1. Moyasar Dashboard → API Keys → Rotate.
2. لوحة runasp.net → File Manager → افتح `web.config`.
3. عدّل قيمة `Moyasar__SecretKey` للقيمة الجديدة.
4. احفظ.
5. Restart Application Pool.

التطبيق يلتقط القيمة الجديدة فوراً بعد الـ restart.

---

## 📋 قائمة فحص نهائية قبل الـ Go-Live

- [ ] `deploy/web.config.production` فيه كل الأسرار الجديدة (بعد rotation).
- [ ] `ConnectionStrings__DefaultConnection` فيه `Encrypt=True`.
- [ ] `JwtSettings__Secret` قيمته من `rotate-secrets.ps1` (طولها ≥ 32 بايت).
- [ ] migrations مُطبقة على DB الإنتاجي.
- [ ] `web.config` على الخادم (وليس في git).
- [ ] `/health` يرد 200.
- [ ] `/swagger` يرد 404.
- [ ] Login + OTP + bank-transfer-proof يعملون.
- [ ] Mobile dev اختبر التغيير في `MOBILE_SECURITY_CHANGES_HANDOFF_AR.md`.

---

## 🚨 طوارئ: لو الأسرار سُرّبت بعد النشر

1. **Rotate فوراً** من لوحة المزود (Moyasar / Resend / ImageKit / DB / OneSignal).
2. عدّل web.config على الخادم بالقيم الجديدة.
3. Restart Application Pool.
4. راجع `SystemLogEntries` في DB لمعرفة هل اسُتخدمت الأسرار القديمة بشكل ضار.
