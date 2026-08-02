# تسليم تعديل رقم جوال العميل — التسجيل والـ Checkout

## الملخص

تم تعديل الباك إند بحيث:

1. رقم الجوال اختياري عند إنشاء حساب العميل.
2. رقم الجوال يصبح إلزاميًا عند تنفيذ الطلب النهائي (Checkout).
3. إذا لم يوجد رقم، لا يتم إنشاء الطلب ويرجع الباك إند خطأ واضحًا يوجه العميل لإضافة الرقم إلى ملفه.

## 1) تسجيل العميل

Endpoint:

```http
POST /api/customers/auth/register
```

حقل `phone` لم يعد إلزاميًا. يمكن حذفه من body أو إرساله بقيمة `null` أو نص فارغ.

مثال:

```json
{
  "fullName": "أحمد علي",
  "email": "ahmed@example.com",
  "password": "P@ssword1234",
  "addressLine": "العنوان"
}
```

إذا أرسل العميل رقمًا، يستمر التحقق الحالي لطول الرقم وعدم تكراره.

## 2) قراءة حالة رقم الجوال

بعد تسجيل الدخول استخدم:

```http
GET /api/customers/auth/me
Authorization: Bearer <access-token>
```

الحقل `user.phone` قد يكون `null` أو فارغًا. يجب اعتبار الحالتين «لا يوجد رقم».

## 3) إضافة الرقم من التطبيق

لتحديث الملف الشخصي:

```http
PUT /api/customers/auth/me
Authorization: Bearer <access-token>
Content-Type: application/json
```

Body:

```json
{
  "fullName": "أحمد علي",
  "email": "ahmed@example.com",
  "phone": "+9665XXXXXXX"
}
```

يجب إرسال `fullName` و`email` و`phone` في الطلب. عند نجاح التحديث، خزّن قيمة `user.phone` الجديدة في حالة المستخدم.

## 4) تنفيذ الطلب النهائي

Endpoint:

```http
POST /api/orders
Authorization: Bearer <access-token>
```

إذا كان رقم الجوال مفقودًا، يرجع الباك إند:

```http
409 Conflict
```

```json
{
  "status": 409,
  "errorCode": "CUSTOMER_PHONE_REQUIRED",
  "message": "لازم تضيف رقم جوال إلى ملفك الشخصي قبل إتمام الطلب.",
  "detail": "لازم تضيف رقم جوال إلى ملفك الشخصي قبل إتمام الطلب."
}
```

في اللغة الإنجليزية تكون الرسالة:

```text
Please add a phone number to your profile before placing an order.
```

## سلوك التطبيق المطلوب

- لا تجعل حقل الجوال إجباريًا في شاشة التسجيل.
- عند فتح شاشة الـ Checkout، إذا كانت قيمة `user.phone` غير موجودة، اعرض تنبيهًا أو اترك الباك إند يرفض الطلب عند الضغط على «إتمام الطلب».
- عند استقبال `409` مع `errorCode = CUSTOMER_PHONE_REQUIRED`:
  - لا تكرر إرسال الطلب تلقائيًا.
  - اعرض رسالة إضافة رقم الجوال.
  - افتح شاشة الملف الشخصي أو شاشة إدخال رقم الجوال.
  - بعد نجاح `PUT /api/customers/auth/me`، حدّث بيانات المستخدم ثم أعد المستخدم إلى Checkout.
- لا تعتمد على فحص التطبيق وحده؛ تحقق الباك إند هو الحماية النهائية.

## ملاحظات التوافق

- الحسابات القديمة التي لا تحتوي على رقم ستستمر في تسجيل الدخول بشكل طبيعي.
- الحساب القديم الذي لا يملك رقمًا لن يستطيع تنفيذ طلب جديد حتى يضيف الرقم.
- لا يوجد Migration مطلوب؛ عمود `PhoneNumber` في قاعدة البيانات nullable بالفعل.

## 6) رقم التواصل في العنوان

في إنشاء العنوان وتعديله، حقل `contactPhone` أصبح اختياريًا:

```http
POST /api/customers/addresses
PUT /api/customers/addresses/{addressId}
```

- يمكن حذف `contactPhone` من الـbody أو إرساله بقيمة `null` أو نص فارغ.
- لا تعرضه كحقل إلزامي في شاشة إضافة أو تعديل العنوان.
- لا تغيّر شرط رقم الحساب في Checkout: الشرط يبقى على `user.phone` في الملف الشخصي، وليس على `contactPhone` في العنوان.
