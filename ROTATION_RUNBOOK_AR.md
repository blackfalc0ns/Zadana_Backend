# Runbook لتدوير الأسرار المسرّبة

كل سر هنا كان موجوداً في git history. اعتبره مكشوفاً للمهاجمين. اتبع الترتيب
التالي بالضبط.

---

## ✅ ما تم تنفيذه تلقائياً (بدون تدخل يدوي خارجي)

| المفتاح | القيمة الجديدة | المكان |
|---|---|---|
| `JwtSettings__Secret` | base64 64 byte عشوائي | `deploy/web.config.production` |
| `BankTransfer__WebhookSecret` | hex 32 byte عشوائي | `deploy/web.config.production` |
| `Seeding__ManagementKey` | hex 32 byte عشوائي | `deploy/web.config.production` |

تم توليدها وحفظها في `deploy/.generated-secrets.txt` (مُستثنى من git).

> **التأثير**:
> - JWT الجديد سيُلغي كل الجلسات النشطة (المستخدمون يحتاجون login جديد). هذا
>   مقصود.
> - BankTransfer webhook سيرفض callbacks من البنك إلى أن تُحدِّث القيمة لدى
>   البنك أيضاً.

---

## 🔴 المفاتيح التي تحتاج تدوير من لوحات المزودين (دقائق لكل واحد)

### 1) MS SQL — databaseasp.net

**ما يجب تدويره**: كلمة مرور المستخدم `db47624` (`4Pq!?g7LiD_3`).

**الخطوات**:
1. ادخل https://www.databaseasp.net/Account/SignIn
2. اختر database `db47624`
3. Tab "Manage" → "Change Password"
4. ولّد كلمة مرور قوية (≥ 20 حرف، أرقام/حروف/رموز)
5. احفظها مؤقتاً في password manager
6. حدّث `deploy/web.config.production`:
   ```xml
   <environmentVariable
     name="ConnectionStrings__DefaultConnection"
     value="Server=db47624.public.databaseasp.net,1433;Database=db47624;User Id=db47624;Password=<NEW_PASSWORD>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connection Timeout=30;" />
   ```

**ملاحظة**: TrustServerCertificate=True هنا مقبول لأن databaseasp.net يستخدم
شهادة self-signed. الأمان الفعلي يأتي من `Encrypt=True`.

---

### 2) Moyasar — لوحة Moyasar

**ما يجب تدويره**:
- `Moyasar__SecretKey` (`sk_test_jeibBfSWQV1x7xuiCVZ1UB7ugqkYJ4BEud8dBA2z`)
- `Moyasar__WebhookSecret` (`whsec_Zadana2026MoyasarWebhook_xK9mP4qR7vL2`)
- `Moyasar__PublishableKey` (`pk_test_RKBfNqBesLkMw4gLcfd8qWMeeu9hxCKMGeYr3Jx1`)

**الخطوات**:
1. https://dashboard.moyasar.com/login
2. Settings → API Keys
3. اضغط "Rotate" بجانب كل key (publishable + secret)
4. Settings → Webhooks → Edit endpoint → "Regenerate Signing Secret"
5. حدّث الـ 3 قيم في `deploy/web.config.production`:
   ```xml
   <environmentVariable name="Moyasar__SecretKey" value="<NEW>" />
   <environmentVariable name="Moyasar__WebhookSecret" value="<NEW>" />
   <environmentVariable name="Moyasar__PublishableKey" value="<NEW>" />
   ```

> ⚠️ المفتاح `pk_test_*` و `sk_test_*` يدلان على وضع **test**. قبل الإنتاج
> الفعلي، انتقل إلى وضع **live** واطلب live keys من Moyasar.

---

### 3) Resend — لوحة Resend

**ما يجب تدويره**: `ResendSettings__ApiKey` (`re_Aqw1mqeR_EvyaPaFbGs7P3UZQ6qU4VsPm`).

**الخطوات**:
1. https://resend.com/api-keys
2. اضغط "Revoke" على المفتاح القديم
3. "Create API Key" → اسمه `zadana-prod`
4. اختر صلاحيات: Send emails فقط
5. انسخ القيمة (لن تظهر مرة ثانية)
6. حدّث `deploy/web.config.production`:
   ```xml
   <environmentVariable name="ResendSettings__ApiKey" value="re_NEW_VALUE" />
   ```

---

### 4) ImageKit — لوحة ImageKit

**ما يجب تدويره**:
- `ImageKit__PrivateKey` (`private_I+B7d2/bfoZkFllZCf07835bjb8=`)
- `ImageKit__PublicKey` (`public_1bswA0Vq66mBJQlYJxBAyPJm3dE=`)

**الخطوات**:
1. https://imagekit.io/dashboard/developer/api-keys
2. اضغط "Add New API Key" → سمّه `zadana-prod`
3. انسخ Public + Private keys
4. عُد إلى القائمة → احذف المفاتيح القديمة
5. حدّث `deploy/web.config.production`:
   ```xml
   <environmentVariable name="ImageKit__PublicKey"  value="public_NEW" />
   <environmentVariable name="ImageKit__PrivateKey" value="private_NEW" />
   ```

> الـ URL endpoint `https://ik.imagekit.io/fnyx4x87z` يبقى كما هو (ليس سرّاً).

---

### 5) OneSignal — لوحة OneSignal

**ما يجب تدويره**: REST API keys للـ 3 apps (`Customer`, `Driver`, `AdminWeb`).

**الخطوات لكل app على حدة**:
1. https://dashboard.onesignal.com → اختر الـ app
2. Settings → Keys & IDs
3. "Roll" بجانب REST API Key
4. انسخ القيمة الجديدة
5. حدّث القيم الثلاث في `deploy/web.config.production`:
   ```xml
   <environmentVariable name="OneSignal__RestApiKey"         value="<NEW>" />
   <environmentVariable name="OneSignal__DriverRestApiKey"   value="<NEW>" />
   <environmentVariable name="OneSignal__AdminWebRestApiKey" value="<NEW>" />
   ```

> الـ App IDs (`e557da4e-...`, `1eead1ea-...`, `c32e801d-...`) ليست أسراراً —
> تستخدمها الفرونت إند علناً.

---

### 6) Twilio (إن كنت تستخدمه فعلاً)

**ما يجب تدويره**: `TwilioSettings__AuthToken`.

**الخطوات**:
1. https://console.twilio.com → Account
2. "Auth Token" → "View" → "Reset Auth Token"
3. حدّث:
   ```xml
   <environmentVariable name="TwilioSettings__AuthToken" value="<NEW>" />
   ```

> القيم الحالية (`test_account_sid` / `test_auth_token`) placeholders فقط، لذا
> هذا غير عاجل.

---

## 📋 قائمة فحص التدوير

- [ ] MS SQL password
- [ ] Moyasar Secret Key
- [ ] Moyasar Webhook Secret
- [ ] Moyasar Publishable Key
- [ ] Resend API Key
- [ ] ImageKit Private Key + Public Key
- [ ] OneSignal Customer REST API Key
- [ ] OneSignal Driver REST API Key
- [ ] OneSignal Admin Web REST API Key
- [ ] (اختياري) Twilio Auth Token

بعد كل تدوير، **حدّث `deploy/web.config.production`** فوراً.

---

## 🧹 بعد التدوير: تنظيف git history

شغّل:
```powershell
pwsh scripts/purge-secrets-from-git.ps1 -DryRun       # للاختبار
pwsh scripts/purge-secrets-from-git.ps1               # التنفيذ الفعلي
```

السكربت يعمل backup تلقائياً ثم:
1. يستبدل كل سر مكشوف في history بـ `__REDACTED__`
2. يحذف مجلدات `temp_*` و `.tmp-*` من history كاملاً
3. يطبع تعليمات الـ force-push.

⚠️ بعد ما تنفذ force-push، كل المطورين لازم يعيدون clone للمشروع.

---

## 🚀 رفع التحديث على runasp.net

1. لوحة runasp.net → File Manager → افتح web.config على الخادم.
2. استبدل محتواه بمحتوى `deploy/web.config.production` المُحدَّث.
3. احفظ.
4. Application → Restart Application Pool.
5. اختبر:
   - `https://zadana.runasp.net/health` → 200 OK
   - حاول login → يجب يعمل (لكن المستخدمين القدامى سُجِّل خروجهم تلقائياً)
   - أي عملية دفع → يجب تعمل بمفاتيح Moyasar الجديدة

---

## 🎯 التحقق من نجاح التدوير

شغّل من جهازك المحلي:
```powershell
$response = curl https://zadana.runasp.net/health
$response.StatusCode    # يجب: 200
$response.Headers       # يجب يحوي Strict-Transport-Security
```

من جهازك راجع `SystemLogEntries` في DB — لا يجب أن تظهر أي محاولات استخدام
لمفاتيح قديمة بعد ساعة من التدوير. لو ظهرت، حقق فيها.
