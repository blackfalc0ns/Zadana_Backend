# الشروط والأحكام وسياسة الخصوصية — Handoff للموبايل

تاريخ التحديث: 2026-07-24  
الحالة: `implemented` في الباك إند  
الجمهور: تطبيق العميل + تطبيق المندوب (+ تاجر للمرجع)

## الخلاصة

الأدمن يحرّر النصوص من:

- **التسويق → الشروط والخصوصية**

التطبيق يجلب المحتوى من API عام **بدون توكن**، ويعرض اللغة حسب لغة الواجهة (`ar` / `en`).

```text
شاشة الشروط / الخصوصية / قبول عند التسجيل
        ↓
GET /api/public/legal/{documentType}
        ↓
اعرض contentAr أو contentEn + version + effectiveAtUtc
```

## Endpoint

```http
GET /api/public/legal/{documentType}
```

| البند | القيمة |
|---|---|
| Auth | **غير مطلوب** (`AllowAnonymous`) |
| Method | `GET` |
| Base | `API_BASE_URL` مثال إنتاج: `https://api.zadna0.com` |
| مثال كامل | `https://api.zadna0.com/api/public/legal/DriverPrivacy` |

لا ترسل `Authorization`. مناسب قبل وبعد تسجيل الدخول.

## قيم `documentType`

| التطبيق | الشروط والأحكام | سياسة الخصوصية |
|---|---|---|
| عميل (Customer) | `CustomerTerms` | `CustomerPrivacy` |
| مندوب (Driver) | `DriverTerms` | `DriverPrivacy` |
| تاجر (Vendor) | `VendorTerms` | `VendorPrivacy` |

الأسماء **case-insensitive** على السيرفر، لكن استخدم PascalCase كما في الجدول.

### أي مستند يستخدمه كل تطبيق؟

**تطبيق العميل**
- شاشة الشروط: `CustomerTerms`
- شاشة الخصوصية: `CustomerPrivacy`
- Checkbox القبول عند التسجيل: نفس المستندين (أو رابط لكل شاشة)

**تطبيق المندوب**
- شاشة الشروط: `DriverTerms`
- شاشة الخصوصية: `DriverPrivacy`
- Checkbox القبول عند التسجيل: نفس المستندين

## Response `200`

```json
{
  "documentType": "DriverPrivacy",
  "contentAr": "# سياسة خصوصية...\n\n## 1. من نحن\n...",
  "contentEn": "# Zadana Driver App Privacy Policy\n\n## 1. Who we are\n...",
  "version": "1.0",
  "effectiveAtUtc": "2026-07-24T00:00:00Z",
  "updatedAtUtc": "2026-07-24T16:20:00Z"
}
```

### الحقول

| الحقل | النوع | ملاحظات للواجهة |
|---|---|---|
| `documentType` | `string` | نفس المفتاح المطلوب |
| `contentAr` | `string` | Markdown عربي — قد يكون فارغًا `""` |
| `contentEn` | `string` | Markdown إنجليزي — قد يكون فارغًا `""` |
| `version` | `string` | اعرضه أسفل الصفحة (مثل `1.0`) |
| `effectiveAtUtc` | `string` (ISO UTC) | تاريخ السريان — اعرض التاريخ فقط للمستخدم |
| `updatedAtUtc` | `string` (ISO UTC) | للـ cache / إعادة الجلب عند التغيير |

> المحتوى **Markdown** (عناوين، قوائم، جداول، اقتباسات). حوّله إلى HTML أو Widget Markdown داخل التطبيق.

## سلوك مقترح في Flutter

1. حدّد `documentType` حسب التطبيق + نوع الشاشة.
2. استدعِ الـ endpoint عند فتح الشاشة (أو مع cache قصير).
3. اختر النص حسب اللغة:
   - لو اللغة عربية → `contentAr`
   - لو إنجليزي → `contentEn`
   - لو اللغة المطلوبة فارغة والثانية فيها نص → اعرض البديل مع تلميح اختياري.
4. اعرض شارات:
   - الإصدار: `version`
   - تاريخ السريان: من `effectiveAtUtc` بصيغة تاريخ محلية.
5. اتجاه العرض:
   - عربي: `rtl` / محاذاة يمين
   - إنجليزي: `ltr` / محاذاة شمال
6. عند فشل الشبكة: رسالة عامة + آخر cache ناجح إن وُجد.
7. عند قبول الشروط في التسجيل: اربط الـ checkbox بروابط تفتح نفس الشاشات (لا تعتمد على ملفات assets ثابتة إلا كـ fallback مؤقت).

### اختيار المحتوى حسب اللغة

```dart
String pickLegalContent({
  required String langCode,
  required String contentAr,
  required String contentEn,
}) {
  final isAr = langCode.toLowerCase().startsWith('ar');
  final primary = (isAr ? contentAr : contentEn).trim();
  if (primary.isNotEmpty) return primary;
  final fallback = (isAr ? contentEn : contentAr).trim();
  return fallback;
}
```

### نموذج بيانات

```dart
class LegalDocument {
  final String documentType;
  final String contentAr;
  final String contentEn;
  final String version;
  final DateTime? effectiveAtUtc;
  final DateTime? updatedAtUtc;

  LegalDocument.fromJson(Map<String, dynamic> json)
      : documentType = json['documentType'] as String? ?? '',
        contentAr = json['contentAr'] as String? ?? '',
        contentEn = json['contentEn'] as String? ?? '',
        version = json['version'] as String? ?? '1.0',
        effectiveAtUtc = json['effectiveAtUtc'] == null
            ? null
            : DateTime.tryParse(json['effectiveAtUtc'] as String),
        updatedAtUtc = json['updatedAtUtc'] == null
            ? null
            : DateTime.tryParse(json['updatedAtUtc'] as String);
}
```

### مثال استدعاء

```dart
Future<LegalDocument> fetchLegal(String documentType) async {
  final res = await dio.get('$apiBaseUrl/public/legal/$documentType');
  return LegalDocument.fromJson(res.data as Map<String, dynamic>);
}

// مندوب — خصوصية
final privacy = await fetchLegal('DriverPrivacy');

// مندوب — شروط
final terms = await fetchLegal('DriverTerms');

// عميل — شروط
final customerTerms = await fetchLegal('CustomerTerms');
```

## أخطاء متوقعة

| الحالة | المعنى | سلوك الواجهة |
|---|---|---|
| `200` مع محتوى فارغ | الأدمن لم يعبّئ بعد | «المحتوى غير متاح حاليًا» |
| `400` | `documentType` غير معروف | راجع أسماء الجدول أعلاه |
| شبكة / `5xx` | فشل الاتصال | رسالة لطيفة + cache قديم |

لا يوجد `401/403` على هذا الـ endpoint.

## شاشات مقترحة

| الشاشة | Customer | Driver |
|---|---|---|
| الشروط والأحكام | `CustomerTerms` | `DriverTerms` |
| سياسة الخصوصية | `CustomerPrivacy` | `DriverPrivacy` |
| قبول عند التسجيل | روابط للشاشتين + `requiredTrue` | نفس الفكرة |

اعرض دائمًا `version` و `effectiveAtUtc` أسفل المقدمة أو أعلى المستند.

## مصدر الحقيقة في الأدمن

| المكان | المسار |
|---|---|
| لوحة المشرف | `/marketing/legal-documents` |
| صلاحية القراءة | `marketing.view` |
| صلاحية الحفظ | `marketing.manage_settings` |

بعد حفظ الأدمن، الموبايل يرى التحديث في الطلب التالي (لا يوجد push إجباري). استخدم `updatedAtUtc` لإبطال الـ cache.

## اختبار سريع

```bash
# مندوب — خصوصية
curl -s https://api.zadna0.com/api/public/legal/DriverPrivacy

# مندوب — شروط
curl -s https://api.zadna0.com/api/public/legal/DriverTerms

# عميل — شروط
curl -s https://api.zadna0.com/api/public/legal/CustomerTerms

# عميل — خصوصية
curl -s https://api.zadna0.com/api/public/legal/CustomerPrivacy
```

تأكد أن `contentAr` / `contentEn` فيهما Markdown، وأن اختيار اللغة في التطبيق صحيح.

## ملاحظة مرتبطة: الدعم والسوشيال

بيانات التواصل والدعم منفصلة:

```http
GET /api/public/platform-contact
```

انظر أيضًا: `PLATFORM_CONTACT_SUPPORT_SOCIAL_MOBILE_HANDOFF_AR.md`
