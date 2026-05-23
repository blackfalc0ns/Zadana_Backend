# دليل ربط Real-Time Order Tracking للموبايل

> **الهدف:** ربط تطبيق المندوب (Driver) وتطبيق العميل (Customer) بنظام تتبع الطلب اللحظي عبر SignalR على الـ Hub المخصص `/hubs/order-tracking`، بحيث يبث المندوب موقعه فيظهر فوراً عند العميل والتاجر والأدمن، وتنتقل تغييرات حالة الطلب بنفس الآلية.

- **الإصدار:** 1.0.0
- **آخر تحديث:** 2026-05-21
- **الجمهور المستهدف:** فريق Flutter/Native لتطبيقي السائق والعميل
- **المطلوب من تطبيق التاجر/الأدمن:** لا شيء (Web يستخدم نفس العقد عبر `OrderTrackingRealtimeService`)

---

## 1) معمارية الميزة باختصار

```
+-----------+    POST /api/drivers/location     +-------------+
|  Driver   | --------------------------------> |   Backend   |
|  Mobile   |                                   |  (.NET 9)   |
+-----------+                                   +------+------+
                                                       |
                                                       | broadcast إلى
                                                       v
                                       +----------------------------------+
                                       |  SignalR Hub: /hubs/order-tracking |
                                       |  Group: order-{orderIdN}          |
                                       +----------------------------------+
                                                       |
            +------------------------+----------------+----------------+
            |                        |                |                |
            v                        v                v                v
       Customer App           Vendor Web        Admin Web         Driver App
       (يشترك بطلبه)        (يشترك بطلبه)      (يشترك بأي طلب)   (يشترك بطلبه)
```

- المندوب يبعث موقعه عبر REST كل بضع ثوان (موجود مسبقًا).
- الـ Backend يخزن الموقع ثم يبث `ReceiveDriverLocation` لكل Group `order-{orderIdN}` المرتبط بالطلب الحالي للسائق.
- أي طرف (عميل/تاجر/مندوب/أدمن) يشترك في `order-{orderId}` يستلم البث فوراً.
- نفس الـ Hub يبث أيضاً `ReceiveOrderTrackingStatusChanged` و `ReceiveOrderTrackingArrivalState`.

---

## 2) Endpoints الأساسية

### 2.1 Base URLs

| البيئة     | Base API URL                         | SignalR Base                       |
|------------|--------------------------------------|------------------------------------|
| Development| `http://localhost:5298/api`          | `http://localhost:5298`            |
| Production | `https://zadana.runasp.net/api`      | `https://zadana.runasp.net`        |

> قاعدة بناء URL الـ Hub: انزع `/api` من نهاية الـ Base API URL ثم أضف `/hubs/order-tracking`.

### 2.2 Driver App: تسجيل الدخول والحصول على Token

```
POST {API}/drivers/auth/login
Content-Type: application/json

{
  "identifier": "+201xxxxxxxxx",
  "password": "********"
}
```

استخدم الـ `accessToken` الراجع لكل الطلبات التالية في Header `Authorization: Bearer <token>`.

### 2.3 Driver App: بث الموقع (موجود ويعمل)

```
POST {API}/drivers/location
Authorization: Bearer <driver_token>
Content-Type: application/json

{
  "latitude": 24.7136,
  "longitude": 46.6753,
  "accuracyMeters": 12.5
}
```

- **التكرار المقترح:** كل 4-6 ثواني أثناء تنفيذ الطلب، وكل 15-30 ثانية في وضع الانتظار.
- لا يحتاج المندوب لاستدعاء أي SignalR `invoke` لإرسال الموقع. البث يتم تلقائيًا من الـ Backend بعد كل `POST /location`.

### 2.4 Customer App: الحصول على بيانات تتبع الطلب الأولية

```
GET {API}/orders/{orderId}/tracking
Authorization: Bearer <customer_token>
```

استخدم هذا الـ endpoint مرة واحدة عند فتح شاشة التتبع لتحميل: مكان المتجر، مكان العميل، آخر موقع للسائق المعروف، حالة الطلب الحالية. ثم اعتمد على SignalR لكل التحديثات اللاحقة.

---

## 3) الاتصال بـ SignalR Hub

### 3.1 Hub URL

```
{SIGNALR_BASE}/hubs/order-tracking
```

### 3.2 Authentication

- مرّر الـ `accessToken` عبر `accessTokenFactory` (الطريقة الموصى بها)، أو عبر query string `?access_token=...` لو الـ SDK لا يدعم factory.
- الـ Hub يدعم WebSockets وLong Polling تلقائياً.

### 3.3 الأدوار المسموح لها

| الدور          | يقدر يشترك في طلب لو          |
|----------------|--------------------------------|
| Customer       | الطلب يخصه (`Order.UserId == userId`) |
| Vendor / VendorStaff | الطلب من تجارته (`Order.Vendor.UserId == userId`) |
| Driver         | عنده DeliveryAssignment فعّال على الطلب |
| Admin / SuperAdmin | كل الطلبات بدون قيود |

أي محاولة اشتراك بطلب غير مصرح بها ترجع خطأ `HubException("FORBIDDEN_ORDER_TRACKING")`.

---

## 4) عقود الـ Events

### 4.1 الاشتراك في طلب (Client → Server)

```dart
// SignalR invoke
await connection.invoke('SubscribeToOrder', args: [orderId]);
```

- `orderId`: `String` (Guid).
- يجب الاستدعاء بعد `connection.start()` وقبل توقع أي event للطلب.
- آمن للاستدعاء عدة مرات لنفس الـ orderId (الـ Backend يتجاهل التكرار).

### 4.2 إلغاء الاشتراك (Client → Server)

```dart
await connection.invoke('UnsubscribeFromOrder', args: [orderId]);
```

استدعها عند مغادرة شاشة تتبع الطلب لمنع استقبال events غير ضرورية.

### 4.3 موقع المندوب اللحظي (Server → Client)

**Event name:** `ReceiveDriverLocation`

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "driverId": "44444444-4444-4444-4444-444444444444",
  "latitude": 24.7136,
  "longitude": 46.6753,
  "accuracyMeters": 12.5,
  "recordedAtUtc": "2026-05-21T14:32:18.512Z"
}
```

- يصل بمعدل اقل من 1 hz عادة (يساوي معدل بث المندوب).
- الـ `recordedAtUtc` بـ UTC ISO 8601، استخدمها لرسم مؤشر "الموقع قديم" لو فرق الوقت > 60 ثانية.

### 4.4 تغيير حالة الطلب (Server → Client)

**Event name:** `ReceiveOrderTrackingStatusChanged`

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "ReadyForPickup",
  "newStatus": "PickedUp",
  "actorRole": "driver",
  "action": "status_changed",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-05-21T14:33:01.000Z"
}
```

- القيم الممكنة لـ `newStatus`: انظر [قسم 7](#7-قاموس-حالات-الطلب).
- عند استلام هذا الـ event، الموصى به تحديث UI من الـ payload مباشرة، أو إعادة جلب `GET /orders/{orderId}/tracking` لو المنطق المعتمد على التفاصيل معقد.

### 4.5 وصول المندوب (Server → Client)

**Event name:** `ReceiveOrderTrackingArrivalState`

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "arrivalState": "arrived_at_vendor",
  "driverName": "أحمد علي",
  "actorRole": "driver",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-05-21T14:31:00.000Z"
}
```

- `arrivalState` تأخذ إحدى القيمتين: `"arrived_at_vendor"` أو `"arrived_at_customer"`.

---

## 5) أمثلة كود

### 5.1 Flutter (signalr_netcore)

أضف الـ dependency في `pubspec.yaml`:

```yaml
dependencies:
  signalr_netcore: ^1.3.7
  logging: ^1.2.0
```

ثم أنشئ الخدمة:

```dart
import 'package:signalr_netcore/signalr_client.dart';
import 'package:logging/logging.dart';

class OrderTrackingDriverLocation {
  final String orderId;
  final String driverId;
  final double latitude;
  final double longitude;
  final double? accuracyMeters;
  final DateTime recordedAtUtc;

  OrderTrackingDriverLocation.fromJson(Map<String, dynamic> json)
      : orderId = json['orderId'] as String,
        driverId = json['driverId'] as String,
        latitude = (json['latitude'] as num).toDouble(),
        longitude = (json['longitude'] as num).toDouble(),
        accuracyMeters = (json['accuracyMeters'] as num?)?.toDouble(),
        recordedAtUtc = DateTime.parse(json['recordedAtUtc'] as String);
}

class OrderTrackingService {
  static const String _hubPath = '/hubs/order-tracking';

  final String signalRBaseUrl; // e.g. https://zadana.runasp.net
  final Future<String?> Function() tokenProvider;

  HubConnection? _connection;
  final Set<String> _subscribed = {};

  OrderTrackingService({
    required this.signalRBaseUrl,
    required this.tokenProvider,
  });

  Stream<OrderTrackingDriverLocation> get driverLocations =>
      _driverLocationsController.stream;
  final _driverLocationsController =
      StreamController<OrderTrackingDriverLocation>.broadcast();

  // مثال مماثل لـ status changes و arrival states
  // ...

  Future<void> connect() async {
    if (_connection != null) return;

    final token = await tokenProvider();
    if (token == null) return;

    _connection = HubConnectionBuilder()
        .withUrl(
          '$signalRBaseUrl$_hubPath',
          options: HttpConnectionOptions(
            accessTokenFactory: () async => (await tokenProvider()) ?? '',
            logging: (level, message) =>
                Logger('SignalR').log(Level.FINE, message),
          ),
        )
        .withAutomaticReconnect()
        .build();

    _connection!.on('ReceiveDriverLocation', (args) {
      if (args == null || args.isEmpty) return;
      final payload = args.first as Map<String, dynamic>;
      _driverLocationsController.add(
        OrderTrackingDriverLocation.fromJson(payload),
      );
    });

    _connection!.onreconnected(({connectionId}) async {
      // أعد الاشتراك في كل الطلبات بعد الاتصال
      for (final orderId in _subscribed) {
        await _connection!.invoke('SubscribeToOrder', args: [orderId]);
      }
    });

    await _connection!.start();
  }

  Future<void> subscribe(String orderId) async {
    await connect();
    if (_subscribed.contains(orderId)) return;
    await _connection!.invoke('SubscribeToOrder', args: [orderId]);
    _subscribed.add(orderId);
  }

  Future<void> unsubscribe(String orderId) async {
    if (!_subscribed.contains(orderId)) return;
    try {
      await _connection?.invoke('UnsubscribeFromOrder', args: [orderId]);
    } finally {
      _subscribed.remove(orderId);
    }
  }

  Future<void> disconnect() async {
    _subscribed.clear();
    await _connection?.stop();
    _connection = null;
  }
}
```

### 5.2 Android (Kotlin) — Microsoft.SignalR Java SDK

```kotlin
import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import com.microsoft.signalr.HttpHubConnectionBuilder
import io.reactivex.rxjava3.core.Single

class OrderTrackingService(
    private val baseUrl: String,
    private val tokenProvider: () -> String?
) {
    private var connection: HubConnection? = null
    private val subscribedOrders = mutableSetOf<String>()

    fun connect(): Single<HubConnection> {
        connection?.let { return Single.just(it) }

        val builder: HttpHubConnectionBuilder = HubConnectionBuilder
            .create("$baseUrl/hubs/order-tracking")
            .withAccessTokenProvider(Single.defer {
                Single.just(tokenProvider() ?: "")
            })

        connection = builder.build().also { hub ->
            hub.on("ReceiveDriverLocation", { payload ->
                onDriverLocation(payload)
            }, OrderTrackingDriverLocation::class.java)

            hub.on("ReceiveOrderTrackingStatusChanged", { payload ->
                onStatusChanged(payload)
            }, OrderTrackingStatusChanged::class.java)

            hub.onClosed { /* تتولى الاتصال إعادة المحاولة في طبقة الخدمة */ }
        }

        return connection!!.start().toSingleDefault(connection!!)
    }

    suspend fun subscribe(orderId: String) {
        connect().blockingGet()
        if (subscribedOrders.contains(orderId)) return
        connection!!.invoke(Void::class.java, "SubscribeToOrder", orderId).blockingGet()
        subscribedOrders.add(orderId)
    }
}
```

### 5.3 iOS (Swift) — SignalRClient

```swift
import SignalRClient

final class OrderTrackingService {
    private let baseUrl: String
    private let tokenProvider: () -> String?
    private var connection: HubConnection?
    private var subscribedOrders = Set<String>()

    init(baseUrl: String, tokenProvider: @escaping () -> String?) {
        self.baseUrl = baseUrl
        self.tokenProvider = tokenProvider
    }

    func connect() {
        guard connection == nil else { return }

        let url = URL(string: "\(baseUrl)/hubs/order-tracking")!
        let options = HttpConnectionOptions()
        options.accessTokenProvider = { [weak self] in self?.tokenProvider() }

        connection = HubConnectionBuilder(url: url)
            .withHttpConnectionOptions(options: { _ in options })
            .withAutoReconnect()
            .build()

        connection?.on(method: "ReceiveDriverLocation") { (payload: OrderTrackingDriverLocation) in
            // يحدث الخريطة
        }

        connection?.on(method: "ReceiveOrderTrackingStatusChanged") { (payload: OrderTrackingStatusChanged) in
            // يحدث الـ UI
        }

        connection?.start()
    }

    func subscribe(orderId: String) {
        guard !subscribedOrders.contains(orderId) else { return }
        connection?.invoke(method: "SubscribeToOrder", arguments: [orderId]) { error in
            if error == nil { self.subscribedOrders.insert(orderId) }
        }
    }
}
```

---

## 6) دورة حياة موصى بها داخل تطبيق العميل

```
1. شاشة OrderDetail تفتح بـ orderId
2. اطلب GET /orders/{orderId}/tracking → ارسم الخريطة بالبيانات الأولية
3. orderTrackingService.subscribe(orderId)
4. اشترك في streams: driverLocations, statusChanges, arrivalStates
5. لكل ReceiveDriverLocation → حدّث marker السائق فقط
6. لكل ReceiveOrderTrackingStatusChanged → اعرض banner + إعادة جلب التفاصيل
7. لكل ReceiveOrderTrackingArrivalState → اعرض إشعار "وصل المندوب"
8. عند مغادرة الشاشة: orderTrackingService.unsubscribe(orderId)
9. عند logout أو إنهاء التطبيق: orderTrackingService.disconnect()
```

داخل تطبيق المندوب نفس التدفق، مع فرق إضافي: الموقع نفسه يُرسل عبر `POST /api/drivers/location` (مش عبر SignalR)، والمندوب فقط يستهلك `ReceiveOrderTrackingStatusChanged` ليتأكد من تزامن حالة الطلب لو غيّرها التاجر/الأدمن.

---

## 7) قاموس حالات الطلب

كل قيم `newStatus` المتوقعة في `ReceiveOrderTrackingStatusChanged`:

```
PendingPayment, PendingBankConfirmation, Placed, PendingVendorAcceptance,
VendorRejected, Accepted, Preparing, ReadyForPickup, DriverAssignmentInProgress,
DriverAssigned, PickedUp, OnTheWay, Delivered, DeliveryFailed, Cancelled, Refunded
```

التحويلات اللي تهم العميل عرضها:
- `Accepted` → "قبل التاجر طلبك"
- `Preparing` → "جاري التحضير"
- `ReadyForPickup` → "الطلب جاهز للاستلام"
- `DriverAssigned` → "تم تعيين مندوب"
- `PickedUp` → "المندوب استلم الطلب"
- `OnTheWay` → "المندوب في الطريق إليك"
- `Delivered` → "تم التسليم"
- `DeliveryFailed` → "تعذر التسليم"
- `Cancelled` → "تم الإلغاء"

---

## 8) Best Practices وأخطاء شائعة

- **استدعِ `subscribe` بعد `start` فقط.** لو الـ connection غير متصل، الـ invoke يفشل.
- **أعد الاشتراك في `onreconnected`.** الـ Backend يفقد الـ groups عند انقطاع الاتصال.
- **اعمل debouncing لتحديثات الخريطة.** لو وصلت 5 events في ثانية واحدة، حدث الـ marker مرة واحدة.
- **لا تعمل `disconnect` بين الشاشات.** اعمل `unsubscribe` فقط واحتفظ بالـ connection حية لتفادي إعادة handshake.
- **تحقق من `recordedAtUtc`.** لو الفرق > 60 ثانية، أعرض حالة "تحديث الموقع قديم" بدل تحريك مؤشر السائق بشكل قافز.
- **لا تستخدم polling لـ `/tracking` بعد ربط SignalR.** الـ events كافية. خليه fallback فقط لو الاتصال مقطوع لأكثر من 30 ثانية.
- **تعامل مع `HubException`.** الرسائل الممكنة: `UNAUTHENTICATED`, `INVALID_ORDER_ID`, `FORBIDDEN_ORDER_TRACKING`. لو وصلت الأخيرة، أوقف محاولة الاشتراك.

---

## 9) خرائط (Map SDK)

كلا المنصتين (Web و Mobile) تستخدم Leaflet/Google Maps حسب اختيارك. النقطة الأساسية: عند استلام `ReceiveDriverLocation` حدث `marker.position` بدون إعادة بناء الخريطة بالكامل، وارسم accuracy circle لو `accuracyMeters` موجود.

---

## 10) جدول مرجعي سريع

| العنصر                       | القيمة                                              |
|------------------------------|-----------------------------------------------------|
| Hub URL (Dev)                | `http://localhost:5298/hubs/order-tracking`         |
| Hub URL (Prod)               | `https://zadana.runasp.net/hubs/order-tracking`     |
| Auth                         | JWT Bearer (نفس توكن REST)                          |
| Subscribe method             | `SubscribeToOrder(orderId)`                         |
| Unsubscribe method           | `UnsubscribeFromOrder(orderId)`                     |
| Driver location event        | `ReceiveDriverLocation`                             |
| Status change event          | `ReceiveOrderTrackingStatusChanged`                 |
| Arrival state event          | `ReceiveOrderTrackingArrivalState`                  |
| REST: تحديث موقع السائق      | `POST /api/drivers/location`                        |
| REST: تتبع العميل الأولي     | `GET /api/orders/{orderId}/tracking`                |

---

## 11) جهة الاتصال للدعم

- **Backend Owner:** فريق Zadana Backend
- **Realtime infra files for reference:**
  - `Zadana.Api/Realtime/OrderTrackingHub.cs`
  - `Zadana.Api/Realtime/OrderTrackingRealtimeNotifier.cs`
  - `Zadana.Api/Realtime/Contracts/OrderTrackingDriverLocationPayload.cs`
- **Web reference implementations:**
  - `Zadana-Frontend/superadmin-panel/src/app/features/orders/services/order-tracking-realtime.service.ts`
  - `Zadana-Frontend/vendor-panel/src/app/features/orders/services/order-tracking-realtime.service.ts`

---

## ملحق: ملف JSON Manifest

نسخة قابلة للقراءة آليًا من نفس العقد متاحة في `MOBILE_REALTIME_ORDER_TRACKING_HANDOFF.json` بجوار هذا الملف، مفيدة لتوليد typed clients أو الاستهلاك من Postman/Apidog.
