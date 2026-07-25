# الدعم ووسائل التواصل — Handoff للموبايل (عميل / مندوب)

تاريخ التحديث: 2026-07-24  
الحالة: `implemented` في الباك إند  
الجمهور: تطبيق العميل + تطبيق المندوب (وأي شاشة عامة تحتاج بيانات التواصل)

## الخلاصة

الأدمن يدخل من لوحة المشرف:

- **التسويق → الدعم والتواصل**

التطبيق يجلب نفس البيانات من API عام **بدون توكن**.

```text
شاشة المساعدة / تواصل معنا / فوتر السوشيال
        ↓
GET /api/public/platform-contact
        ↓
اعرض الإيميل والرقم والروابط المتاحة فقط (غير null)
```

## Endpoint

```http
GET /api/public/platform-contact
```

| البند | القيمة |
|---|---|
| Auth | **غير مطلوب** (`AllowAnonymous`) |
| Method | `GET` |
| Base | `API_BASE_URL` مثال إنتاج: `https://api.zadna0.com` |
| Path كامل | `https://api.zadna0.com/api/public/platform-contact` |

لا ترسل `Authorization`. مناسب لشاشة «تواصل معنا» قبل وبعد تسجيل الدخول.

## Response `200`

```json
{
  "supportEmail": "support@zadna0.com",
  "supportPhone": "+9665xxxxxxxx",
  "whatsAppUrl": "https://wa.me/9665xxxxxxxx",
  "instagramUrl": "https://instagram.com/zadana",
  "twitterUrl": "https://x.com/zadana",
  "tikTokUrl": "https://www.tiktok.com/@zadana",
  "snapchatUrl": "https://www.snapchat.com/add/zadana",
  "facebookUrl": "https://facebook.com/zadana",
  "youTubeUrl": "https://youtube.com/@zadana",
  "linkedInUrl": "https://linkedin.com/company/zadana",
  "updatedAtUtc": "2026-07-24T15:00:00Z"
}
```

### الحقول

| الحقل | النوع | ملاحظات للواجهة |
|---|---|---|
| `supportEmail` | `string \| null` | افتح `mailto:` لو موجود |
| `supportPhone` | `string \| null` | افتح `tel:` لو موجود |
| `whatsAppUrl` | `string \| null` | افتح برابط مطلق (عادة `https://wa.me/...`) |
| `instagramUrl` | `string \| null` | افتح خارجيًا / in-app browser |
| `twitterUrl` | `string \| null` | إكس / تويتر |
| `tikTokUrl` | `string \| null` | |
| `snapchatUrl` | `string \| null` | |
| `facebookUrl` | `string \| null` | |
| `youTubeUrl` | `string \| null` | camelCase: `youTubeUrl` |
| `linkedInUrl` | `string \| null` | camelCase: `linkedInUrl` |
| `updatedAtUtc` | `string \| null` | ISO-8601 UTC — للـ cache فقط |

> أي حقل قد يكون `null` أو فارغًا. **لا تعرض زر/أيقونة لمنصة قيمتها null.**

## سلوك مقترح في Flutter

1. عند فتح شاشة الدعم / الإعدادات / About: استدعِ الـ endpoint مرة.
2. خزّن النتيجة في ذاكرة قصيرة (مثلاً 15–60 دقيقة) أو حتى `updatedAtUtc` يتغيّر.
3. عند الضغط:
   - إيميل → `mailto:{supportEmail}`
   - هاتف → `tel:{supportPhone}`
   - سوشيال / واتساب → `launchUrl(uri, mode: externalApplication)` مع التحقق أن الرابط `http/https`.
4. لو الطلب فشل (شبكة): اعرض رسالة عامة ولا تكسر الشاشة؛ اختياريًا ارجع لآخر cache ناجح.
5. لو كل الحقول `null`: اعرض «بيانات التواصل غير متاحة حاليًا».

### مثال نموذج

```dart
class PlatformContact {
  final String? supportEmail;
  final String? supportPhone;
  final String? whatsAppUrl;
  final String? instagramUrl;
  final String? twitterUrl;
  final String? tikTokUrl;
  final String? snapchatUrl;
  final String? facebookUrl;
  final String? youTubeUrl;
  final String? linkedInUrl;
  final DateTime? updatedAtUtc;

  PlatformContact.fromJson(Map<String, dynamic> json)
      : supportEmail = json['supportEmail'] as String?,
        supportPhone = json['supportPhone'] as String?,
        whatsAppUrl = json['whatsAppUrl'] as String?,
        instagramUrl = json['instagramUrl'] as String?,
        twitterUrl = json['twitterUrl'] as String?,
        tikTokUrl = json['tikTokUrl'] as String?,
        snapchatUrl = json['snapchatUrl'] as String?,
        facebookUrl = json['facebookUrl'] as String?,
        youTubeUrl = json['youTubeUrl'] as String?,
        linkedInUrl = json['linkedInUrl'] as String?,
        updatedAtUtc = json['updatedAtUtc'] == null
            ? null
            : DateTime.tryParse(json['updatedAtUtc'] as String);
}
```

### مثال استدعاء

```dart
final res = await dio.get('$apiBaseUrl/public/platform-contact');
final contact = PlatformContact.fromJson(res.data as Map<String, dynamic>);
```

## أخطاء متوقعة

| الحالة | المعنى | سلوك الواجهة |
|---|---|---|
| `200` مع حقول null | الأدمن لم يعبّئ بعد / fallback جزئي | أخفِ الأزرار الفارغة |
| شبكة / timeout | السيرفر غير متاح | رسالة لطيفة + cache قديم إن وُجد |
| `5xx` | خطأ سيرفر | نفس سلوك فشل الشبكة |

لا يوجد `401/403` على هذا الـ endpoint.

## مصدر الحقيقة في الأدمن

| المكان | المسار |
|---|---|
| لوحة المشرف | `/marketing/contact-social` |
| صلاحية القراءة | `marketing.view` |
| صلاحية الحفظ | `marketing.manage_settings` |

بعد ما الأدمن يحفظ، الموبايل يشوف التحديث في الطلب التالي (لا يوجد push إجباري).

## ملاحظة عن الشروط والخصوصية (مرتبط)

لو تحتاجون شاشات قانونية من الأدمن أيضًا:

```http
GET /api/public/legal/{documentType}
```

`documentType` أمثلة:

- `CustomerTerms` / `CustomerPrivacy`
- `DriverTerms` / `DriverPrivacy`
- `VendorTerms` / `VendorPrivacy`

الرد يشمل: `contentAr`, `contentEn`, `version`, `effectiveAtUtc`.

عقد منفصل يمكن إضافته لاحقًا؛ هذا الملف يركز على **الدعم والسوشيال**.

## اختبار سريع

```bash
curl -s https://api.zadna0.com/api/public/platform-contact
```

تأكد أن JSON يرجع camelCase كما فوق، وأن الروابط المطلقة فقط هي التي تُعرض للأيقونات.
