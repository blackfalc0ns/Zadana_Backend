# تعديلات تطبيق المندوب — Real-Time Order Tracking

> **ملاحظة مهمة قبل البدء:**  
> تطبيق العميل **لا يحتاج أي تعديل**. تطبيق التاجر/الأدمن (Web) لا يحتاج أي تعديل من جانبك. التعديلات هنا **حصرية لتطبيق المندوب** فقط.

- **الإصدار:** 1.0.0
- **آخر تحديث:** 2026-05-21
- **الجمهور:** مبرمج تطبيق المندوب (Flutter/Native)

---

## 1) ملخص التعديلات في 30 ثانية

| التعديل | إلزامي؟ | الجهد التقريبي |
|---------|---------|----------------|
| 1. زيادة معدل بث الموقع أثناء تنفيذ الطلب | ✅ نعم | 5 دقائق |
| 2. التأكد من إرسال `accuracyMeters` في كل request | ✅ نعم | 5 دقائق |
| 3. الاشتراك في `/hubs/order-tracking` للطلب الحالي | ⚠️ موصى به | 30 دقيقة |
| 4. إعادة جلب assignment عند `ReceiveOrderTrackingStatusChanged` | ⚠️ موصى به | 15 دقيقة |
| باقي إشعارات السائق (offers, arrivals, wallet) | ❌ بدون تغيير | — |

**الباقي يعمل تلقائياً من الـ Backend.** بمجرد ما المندوب يستدعي `POST /api/drivers/location`، الـ Backend يبث الموقع لكل المتابعين (عميل/تاجر/أدمن) من غير أي شغل إضافي على المندوب.

---

## 2) ما هو الموجود فعلاً عند المندوب (لا تغيره)

تطبيق المندوب الحالي عنده:

- ✅ تسجيل دخول عبر `POST /api/drivers/auth/login` ويحفظ JWT.
- ✅ اتصال SignalR على `/hubs/notifications` لاستلام الـ offers والـ assignments والـ wallet updates.
- ✅ استدعاء دوري لـ `POST /api/drivers/location` كل 30 ثانية.
- ✅ Endpoints حالة الطلب: `picked-up`, `arrived-at-vendor`, `on-the-way`, `arrived-at-customer`, `delivered`, `delivery-failed`.

كل ده يكمل شغله بدون تغيير.

---

## 3) التعديلات المطلوبة (إلزامية)

### 3.1 زيادة معدل بث الموقع أثناء تنفيذ طلب فعّال

**السبب:** الخريطة عند العميل والتاجر والأدمن تتحدث مباشرة من بث المندوب. لو السائق يبعث كل 30 ثانية فقط، تجربة المتابعة هتبان متقطعة.

**التغيير المطلوب:** اجعل معدل البث ديناميكي حسب حالة المندوب:

| الحالة | معدل البث |
|--------|-----------|
| السائق متاح بدون طلب نشط (idle) | كل 30 ثانية (كما هو) |
| السائق عنده DeliveryAssignment فعّال (Accepted / ArrivedAtVendor / PickedUp / ArrivedAtCustomer) | **كل 5 ثوانٍ** |
| السائق في الخلفية (background) أثناء طلب نشط | كل 10-15 ثانية حسب صلاحيات المنصة |

**Endpoint بدون تغيير:**
```http
POST /api/drivers/location
Authorization: Bearer <driver_token>
Content-Type: application/json

{
  "latitude": 24.7136,
  "longitude": 46.6753,
  "accuracyMeters": 12.5
}
```

**كود مرجعي (Flutter):**

```dart
class DriverLocationStreamer {
  Timer? _timer;
  bool _hasActiveAssignment = false;

  void onAssignmentStateChanged(bool hasActive) {
    if (_hasActiveAssignment == hasActive) return;
    _hasActiveAssignment = hasActive;
    _restartTimer();
  }

  void _restartTimer() {
    _timer?.cancel();
    final intervalSeconds = _hasActiveAssignment ? 5 : 30;
    _timer = Timer.periodic(Duration(seconds: intervalSeconds), (_) {
      _pushCurrentLocation();
    });
  }

  Future<void> _pushCurrentLocation() async {
    final position = await Geolocator.getCurrentPosition(
      desiredAccuracy: LocationAccuracy.high,
    );
    await driverApi.postLocation(
      latitude: position.latitude,
      longitude: position.longitude,
      accuracyMeters: position.accuracy,
    );
  }
}
```

### 3.2 التأكد من إرسال `accuracyMeters` في كل request

الـ Backend يستخدم `accuracyMeters` لرسم accuracy circle حول السائق على الخريطة. لو القيمة `null` أو محذوفة، الويب/تطبيق العميل ما يقدرش يرسم الدقة.

**كل request لـ `/api/drivers/location` لازم يحتوي على `accuracyMeters`** (من `Geolocator` على Flutter أو `LocationManager` على Android أو `CLLocationManager.horizontalAccuracy` على iOS).

---

## 4) التعديلات الموصى بها (تحسين تجربة المندوب)

> هذا القسم اختياري ولا يكسر الميزة لو تخطيته. الهدف منه: السائق يشوف تغييرات حالة الطلب فوراً لو غيّرها التاجر أو الأدمن (مثلاً: إلغاء الطلب) بدون انتظار polling.

### 4.1 الاشتراك في `/hubs/order-tracking`

تطبيق المندوب حالياً متصل بـ `/hubs/notifications` فقط. الـ Hub الجديد `/hubs/order-tracking` يبث events مخصصة للطلب الحالي.

**Hub URL:**
```
{SIGNALR_BASE}/hubs/order-tracking
```

**Auth:** نفس JWT المستخدم لـ `/hubs/notifications`. مرّره عبر `accessTokenFactory`.

**ملاحظة عن الـ scope:** السائق يقدر يشترك فقط في الطلبات اللي عنده عليها DeliveryAssignment فعّال. أي محاولة subscribe على طلب آخر ترجع `HubException("FORBIDDEN_ORDER_TRACKING")`.

### 4.2 دورة الحياة على شاشة تفاصيل المهمة

```
1. السائق يقبل offer جديد → يطلع AssignmentDetail Screen
2. orderTrackingService.subscribe(orderId)   ← invoke('SubscribeToOrder', orderId)
3. يستمع لـ:
   - ReceiveOrderTrackingStatusChanged   (تغيير من التاجر/الأدمن)
   - ReceiveOrderTrackingArrivalState    (للتزامن مع backend بعد ما هو نفسه ضغط زرار الوصول)
4. عند مغادرة الشاشة:
   orderTrackingService.unsubscribe(orderId) ← invoke('UnsubscribeFromOrder', orderId)
5. عند انتهاء الطلب (Delivered / Failed / Cancelled):
   orderTrackingService.unsubscribe(orderId)
```

**ملاحظة:** السائق **لا يستهلك** `ReceiveDriverLocation` (لأنه هو المصدر). تجاهل هذا الـ event لو وصل.

### 4.3 الـ events اللي تهم تطبيق المندوب

#### `ReceiveOrderTrackingStatusChanged`

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "vendorId": "33333333-3333-3333-3333-333333333333",
  "oldStatus": "Preparing",
  "newStatus": "Cancelled",
  "actorRole": "customer",
  "action": "cancelled",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-05-21T14:33:01Z"
}
```

**ماذا يفعل التطبيق عند الاستلام:**

- لو `newStatus == "Cancelled"` أو `"Refunded"`: اعرض dialog فوري للسائق "تم إلغاء الطلب" واسحبه من شاشة التفاصيل.
- لو `actorRole != "driver"` (يعني التغيير ما جاش من المندوب نفسه): أعد جلب `GET /api/drivers/assignments/{assignmentId}` لتحديث الـ UI.
- لو `actorRole == "driver"`: تجاهل الـ event (السائق هو من غير الحالة).

#### `ReceiveOrderTrackingArrivalState`

```json
{
  "orderId": "22222222-2222-2222-2222-222222222222",
  "orderNumber": "12345",
  "arrivalState": "arrived_at_vendor",
  "driverName": "أحمد علي",
  "actorRole": "driver",
  "targetUrl": "/orders/22222222-2222-2222-2222-222222222222",
  "changedAtUtc": "2026-05-21T14:31:00Z"
}
```

السائق غالباً ما يحتاج هذا الـ event (هو نفسه أطلقه)، لكن من المفيد استخدامه كـ confirmation أن البث للأطراف الأخرى نجح. تجاهله بأمان.

### 4.4 كود مرجعي (Flutter — signalr_netcore)

```dart
class DriverOrderTrackingService {
  final String signalRBaseUrl;
  final Future<String?> Function() tokenProvider;
  HubConnection? _connection;
  String? _currentOrderId;

  final _statusChanges = StreamController<OrderTrackingStatusChanged>.broadcast();
  Stream<OrderTrackingStatusChanged> get statusChanges => _statusChanges.stream;

  DriverOrderTrackingService({
    required this.signalRBaseUrl,
    required this.tokenProvider,
  });

  Future<void> _ensureConnection() async {
    if (_connection != null) return;
    final token = await tokenProvider();
    if (token == null) return;

    _connection = HubConnectionBuilder()
        .withUrl(
          '$signalRBaseUrl/hubs/order-tracking',
          options: HttpConnectionOptions(
            accessTokenFactory: () async => (await tokenProvider()) ?? '',
          ),
        )
        .withAutomaticReconnect()
        .build();

    _connection!.on('ReceiveOrderTrackingStatusChanged', (args) {
      if (args == null || args.isEmpty) return;
      final payload = OrderTrackingStatusChanged.fromJson(
        args.first as Map<String, dynamic>,
      );
      _statusChanges.add(payload);
    });

    _connection!.onreconnected(({connectionId}) async {
      if (_currentOrderId != null) {
        await _connection!.invoke('SubscribeToOrder', args: [_currentOrderId!]);
      }
    });

    await _connection!.start();
  }

  Future<void> subscribeToOrder(String orderId) async {
    await _ensureConnection();
    if (_currentOrderId == orderId) return;

    if (_currentOrderId != null) {
      await _connection?.invoke('UnsubscribeFromOrder', args: [_currentOrderId!]);
    }

    _currentOrderId = orderId;
    await _connection!.invoke('SubscribeToOrder', args: [orderId]);
  }

  Future<void> unsubscribe() async {
    if (_currentOrderId == null) return;
    try {
      await _connection?.invoke('UnsubscribeFromOrder', args: [_currentOrderId!]);
    } finally {
      _currentOrderId = null;
    }
  }

  Future<void> disconnect() async {
    _currentOrderId = null;
    await _connection?.stop();
    _connection = null;
  }
}
```

---

## 5) ما لا يتغير عند المندوب (ابقها كما هي)

- ❌ لا تغير منطق استقبال offers من `ReceiveDeliveryOffer`.
- ❌ لا تغير منطق `ReceiveAssignmentUpdated` على `/hubs/notifications`.
- ❌ لا تنشئ اتصال SignalR ثاني لـ NotificationHub. يبقى موجود كما هو.
- ❌ لا تستهلك `ReceiveDriverLocation` على `/hubs/order-tracking`. السائق هو المصدر مش المستلم.
- ❌ لا تتعامل مع منطق "أنت لست مصرحاً لتتبع هذا الطلب" بـ retry — لو الـ Backend رفض، التطبيق ما يعرفش الطلب أصلاً.

---

## 6) checklist الاختبار

### اختبار التعديل الإلزامي (1 و 2):

1. [ ] السائق idle → معدل البث 30 ثانية.
2. [ ] السائق قبل offer → معدل البث ينقلب فوراً لـ 5 ثواني.
3. [ ] كل request يحتوي على `accuracyMeters` بقيمة رقمية > 0.
4. [ ] افتح صفحة الطلب من الـ Web (لوحة التاجر أو الأدمن) — مؤشر السائق على الخريطة يتحرك بسلاسة كل ~5 ثواني.
5. [ ] السائق يضغط "تم التسليم" → بث الموقع يرجع 30 ثانية.

### اختبار التعديل الموصى به (3 و 4):

6. [ ] على الويب (لوحة الأدمن)، غير حالة الطلب (مثلاً: ألغ الطلب). تطبيق المندوب يستلم `ReceiveOrderTrackingStatusChanged` خلال أقل من 2 ثانية.
7. [ ] افصل الإنترنت عن الموبايل لـ 10 ثوان ثم أعد الاتصال. الـ subscribe يتم تلقائياً مع `onreconnected`.
8. [ ] السائق فتح طلب ليس له. محاولة `SubscribeToOrder` ترجع `HubException` ولا يحاول التطبيق إعادتها.

---

## 7) جدول مرجعي

| العنصر | القيمة |
|--------|--------|
| Hub URL (Dev) | `http://localhost:5298/hubs/order-tracking` |
| Hub URL (Prod) | `https://zadana.runasp.net/hubs/order-tracking` |
| Auth | نفس JWT الموجود |
| Subscribe | `invoke('SubscribeToOrder', orderId)` |
| Unsubscribe | `invoke('UnsubscribeFromOrder', orderId)` |
| Status event | `ReceiveOrderTrackingStatusChanged` |
| Arrival event | `ReceiveOrderTrackingArrivalState` |
| Driver location event | `ReceiveDriverLocation` ← **تجاهله، السائق مصدر مش مستلم** |
| Location push endpoint | `POST /api/drivers/location` (موجود) |
| الـ assignment endpoint | `GET /api/drivers/assignments/{assignmentId}` (موجود) |

---

## 8) أسئلة محتملة

**س: هل لازم أضيف اتصال SignalR ثاني، ولّا أستخدم نفس اتصال `/hubs/notifications`؟**  
ج: لازم اتصال ثاني لأن `/hubs/order-tracking` Hub منفصل بـ groups مختلفة. تكلفة الـ overhead قليلة جداً (ping/keepalive لكل اتصال).

**س: لو ما اشتغلتش على القسم الـ "موصى به"، هل الميزة هتشتغل؟**  
ج: نعم تماماً. تطبيق العميل/التاجر/الأدمن هيستلم الموقع والحالة بشكل طبيعي. القسم الموصى به فقط يحسن تجربة السائق.

**س: لو السائق فقد إذن GPS وسط الطلب، أتصرف إزاي؟**  
ج: أوقف بث الموقع، وأطلق إشعار محلي للسائق "أعد تشغيل GPS". الـ Backend يعتبر الموقع stale تلقائياً بعد 60 ثانية.

**س: ماذا لو معدل 5 ثواني يستهلك بطارية؟**  
ج: استخدم `LocationAccuracy.high` بدل `bestForNavigation`، وعطّل البث لو الجهاز ثابت لأكثر من 30 ثانية (نفس الإحداثيات).

---

## 9) للمراجع

- ملف العقد الكامل (شامل تطبيق العميل): `MOBILE_REALTIME_ORDER_TRACKING_HANDOFF_AR.md`
- نسخة JSON: `MOBILE_REALTIME_ORDER_TRACKING_HANDOFF.json`
- المرجع المعتمد لكل API السائق: `DRIVER_MOBILE_API_CONTRACT.md`
- Backend Hub source: `Zadana-Backend/src/Zadana.Api/Realtime/OrderTrackingHub.cs`
