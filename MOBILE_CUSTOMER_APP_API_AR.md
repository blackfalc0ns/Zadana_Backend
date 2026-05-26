# دليل API تطبيق العميل (Customer Mobile App)

> آخر تحديث: 2026-05-24  
> الجمهور: مبرمج تطبيق العميل (Flutter/Native)

---

## البيئات

| البيئة | Base URL | SignalR |
|--------|----------|--------|
| Development | `http://localhost:5298/api` | `http://localhost:5298` |
| Production | `https://zadana.runasp.net/api` | `https://zadana.runasp.net` |

## المصادقة (Authentication)

- JWT Bearer Token في Header: `Authorization: Bearer <access_token>`
- Access Token صلاحيته 60 دقيقة
- Refresh Token لتجديد الـ Access Token
- الضيف يستخدم `X-Device-Id` + `X-Device-Signature` للسلة

---

## 1. المصادقة (`/api/customers/auth`)

| Method | Path | Auth | الوصف |
|--------|------|------|--------|
| POST | `/register` | ❌ + CAPTCHA | تسجيل عميل جديد |
| POST | `/login` | ❌ | تسجيل دخول |
| POST | `/refresh-token` | ❌ | تجديد التوكن |
| POST | `/verify-otp` | ❌ | تأكيد OTP |
| POST | `/resend-otp` | ❌ | إعادة إرسال OTP |
| POST | `/forgot-password` | ❌ + CAPTCHA | نسيت كلمة المرور |
| POST | `/reset-password` | ❌ | إعادة تعيين كلمة المرور |
| POST | `/logout` | ✅ | تسجيل خروج |
| GET | `/me` | ✅ | بيانات المستخدم الحالي |
| PUT | `/me` | ✅ | تحديث الملف الشخصي |
| PUT | `/me/profile-photo` | ✅ | تحديث صورة الملف الشخصي |
| DELETE | `/me/profile-photo` | ✅ | حذف صورة الملف الشخصي |

### تسجيل دخول
```http
POST /api/customers/auth/login
Content-Type: application/json

{ "identifier": "+966501234567", "password": "MyPass123" }
```
**Response 200:**
```json
{ "accessToken": "eyJ...", "refreshToken": "abc...", "expiresAtUtc": "..." }
```

### تجديد التوكن
```http
POST /api/customers/auth/refresh-token
Content-Type: application/json

{ "refreshToken": "abc..." }
```

---

## 2. العناوين (`/api/customers/addresses`)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/` | قائمة العناوين |
| POST | `/` | إضافة عنوان |
| PUT | `/{addressId}` | تعديل عنوان |
| PATCH | `/{addressId}/default` | تعيين كافتراضي |
| DELETE | `/{addressId}` | حذف عنوان |

### إضافة عنوان
```json
{
  "contactName": "أحمد",
  "contactPhone": "+966501234567",
  "addressLine": "شارع الملك فهد",
  "label": "المنزل",
  "buildingNo": "12",
  "floorNo": "3",
  "apartmentNo": "5",
  "city": "الدمام",
  "area": "حي الشاطئ",
  "latitude": 26.4207,
  "longitude": 50.0888,
  "isDefault": true
}
```

---

## 3. الصفحة الرئيسية (`/api/home`)

| Method | Path | Auth | الوصف |
|--------|------|------|--------|
| GET | `/` | ❌ | Header الرئيسية |
| GET | `/content` | ❌ | المحتوى الكامل |
| GET | `/banners` | ❌ | البانرات |
| GET | `/categories` | ❌ | الأقسام |
| GET | `/special-offers` | ❌ | العروض |
| GET | `/recommended` | ❌ | المنتجات الموصى بها |
| GET | `/best-selling` | ❌ | الأكثر مبيعاً |
| GET | `/brands` | ❌ | العلامات التجارية |
| GET | `/featured-products` | ❌ | منتجات مميزة |
| GET | `/explore-more` | ❌ | اكتشف المزيد |
| GET | `/dynamic-sections` | ❌ | أقسام ديناميكية |

كل endpoint يقبل `?take=10` لتحديد العدد.

---

## 4. السلة (`/api/cart`)

| Method | Path | Auth | الوصف |
|--------|------|------|--------|
| GET | `/` | ❌/✅ | عرض السلة |
| GET | `/vendors` | ❌/✅ | البائعين في السلة |
| GET | `/delivery-check` | ✅ | فحص التوصيل |
| POST | `/items` | ❌* | إضافة منتج |
| PATCH | `/items/{itemId}` | ❌* | تعديل الكمية |
| DELETE | `/items/{itemId}` | ❌* | حذف منتج |
| DELETE | `/` | ❌* | تفريغ السلة |
| POST | `/guest-token` | ❌ | طلب توقيع الضيف |

> *الضيف يحتاج `X-Device-Id` + `X-Device-Signature` للعمليات الكتابية

### إضافة منتج للسلة
```http
POST /api/cart/items
Authorization: Bearer <token>
Content-Type: application/json

{ "productId": "guid-here", "quantity": 2 }
```

---

## 5. الدفع والطلب (`/api/checkout`)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/summary?vendor_id=&address_id=&payment_method=` | ملخص الطلب |
| POST | `/promo-code` | تطبيق كود خصم |
| DELETE | `/promo-code` | إزالة كود خصم |

### إنشاء طلب
```http
POST /api/orders
Authorization: Bearer <token>
Content-Type: application/json

{
  "effectiveVendorId": "guid",
  "effectiveAddressId": "guid",
  "effectivePaymentMethod": "CashOnDelivery",
  "effectiveNotes": "ملاحظة اختيارية"
}
```

---

## 6. الطلبات (`/api/orders`)

| Method | Path | الوصف |
|--------|------|--------|
| POST | `/` | إنشاء طلب جديد |
| GET | `/active` | الطلبات النشطة |
| GET | `/completed` | الطلبات المكتملة |
| GET | `/returns` | طلبات الاسترجاع |
| GET | `/{orderId}` | تفاصيل طلب |
| GET | `/{orderId}/tracking` | تتبع الطلب |
| POST | `/{orderId}/cancel` | إلغاء طلب |
| POST | `/{orderId}/retry-payment` | إعادة محاولة الدفع |
| DELETE | `/{orderId}` | حذف طلب |
| GET | `/cancellation-reasons` | أسباب الإلغاء |
| GET | `/support-reasons/{type}` | أسباب الدعم |
| POST | `/{orderId}/complaints` | تقديم شكوى |
| GET | `/{orderId}/complaints` | عرض الشكوى |
| POST | `/{orderId}/cases` | فتح قضية دعم |
| GET | `/{orderId}/cases` | قضايا الدعم |
| GET | `/{orderId}/cases/{caseId}` | تفاصيل قضية |
| POST | `/{orderId}/cases/{caseId}/reply` | الرد على قضية |
| GET | `/{orderId}/refund-status` | حالة الاسترجاع |

---

## 7. المفضلة (`/api/favorites`)

| Method | Path | Auth | الوصف |
|--------|------|------|--------|
| GET | `/` | ❌/✅ | قائمة المفضلة |
| POST | `/` | ❌/✅ | إضافة للمفضلة |
| DELETE | `/{productId}` | ❌/✅ | إزالة من المفضلة |
| DELETE | `/` | ❌/✅ | مسح الكل |

> الضيف يستخدم `X-Device-Id` header

---

## 8. الإشعارات (`/api/notifications`)

| Method | Path | الوصف |
|--------|------|--------|
| GET | `/` | قائمة الإشعارات |
| GET | `/unread-count` | عدد غير المقروءة |
| POST | `/{id}/read` | تعليم كمقروء |
| POST | `/read-all` | تعليم الكل كمقروء |
| DELETE | `/{id}` | حذف إشعار |
| DELETE | `/` | حذف الكل |
| GET | `/preferences` | إعدادات الإشعارات |
| PUT | `/preferences` | تحديث الإعدادات |

### تسجيل جهاز Push (`/api/notifications/devices`)
```http
POST /api/notifications/devices/register
Authorization: Bearer <token>
Content-Type: application/json

{
  "deviceToken": "fcm-token-here",
  "platform": "fcm",
  "deviceId": "unique-device-id",
  "deviceName": "Samsung Galaxy S24",
  "appVersion": "1.0.0",
  "locale": "ar"
}
```

---

## 9. الدفع الإلكتروني (`/api/payments/moyasar`)

| Method | Path | Auth | الوصف |
|--------|------|------|--------|
| GET | `/verify?id=<moyasar_id>` | ❌ | تأكيد الدفع (callback) |
| POST | `/confirm` | ❌ | تأكيد الدفع (programmatic) |

بعد فتح WebView للدفع، Moyasar يعيد التوجيه لـ `/verify`. استدعِ `/confirm` من التطبيق مع `provider_payment_id`.

---

## 10. SignalR — Real-Time

### Hub: `/hubs/notifications`
| Event | الوصف |
|-------|--------|
| `ReceiveNotification` | إشعار جديد |
| `ReceiveOrderStatusChanged` | تغيير حالة طلب |
| `ReceiveOrderSupportCaseChanged` | تحديث قضية دعم |

### Hub: `/hubs/order-tracking`
| Event | الوصف |
|-------|--------|
| `ReceiveDriverLocation` | موقع المندوب اللحظي |
| `ReceiveOrderTrackingStatusChanged` | تغيير حالة الطلب |
| `ReceiveOrderTrackingArrivalState` | وصول المندوب |

**الاشتراك:**
```dart
await connection.invoke('SubscribeToOrder', args: [orderId]);
```

### Hub: `/hubs/customer-presence`
| Method | الوصف |
|--------|--------|
| `AppForeground()` | التطبيق في المقدمة |
| `AppBackground()` | التطبيق في الخلفية |
| `Heartbeat()` | نبض الاتصال |

---

## 11. حالات الطلب

```
PendingPayment → Placed → PendingVendorAcceptance → Accepted → Preparing →
ReadyForPickup → DriverAssignmentInProgress → DriverAssigned → PickedUp →
OnTheWay → Delivered
```

حالات أخرى: `VendorRejected`, `Cancelled`, `DeliveryFailed`, `Refunded`

---

## 12. أكواد الخطأ الشائعة

| Code | Status | الوصف |
|------|--------|--------|
| `USER_NOT_AUTHENTICATED` | 401 | لا يوجد JWT |
| `TOKEN_REVOKED` | 401 | التوكن ملغي |
| `RATE_LIMIT_EXCEEDED` | 429 | طلبات كثيرة |
| `OTP_ACCOUNT_LOCKED` | 401 | الحساب مقفل |
| `GUEST_CART_SIGNATURE_REQUIRED` | 401 | توقيع الضيف مطلوب |
| `BOT_CHALLENGE_FAILED` | 400 | CAPTCHA فاشل |
| `DELIVERY_PRICING_UNAVAILABLE` | 400 | التوصيل غير متاح |

---

## مراجع إضافية

- `MOBILE_CUSTOMER_SECURITY_HANDOFF_AR.md` — تفاصيل تغييرات الأمان
- `MOBILE_REALTIME_ORDER_TRACKING_HANDOFF_AR.md` — تفاصيل التتبع اللحظي
