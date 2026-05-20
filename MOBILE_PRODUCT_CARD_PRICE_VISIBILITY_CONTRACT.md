# Mobile Product Card Price Visibility Contract

آخر تحديث: 2026-05-20

## الحالة

تم تنفيذ التعديل في الباك إند والداشبورد.

الغرض من التعديل أن الأدمن يقدر يتحكم في ظهور سعر المنتج على كروت المنتجات في تطبيق العميل. صفحة تفاصيل المنتج نفسها لا تدخل في هذا التحكم، وتعرض الأسعار عادي.

## الفكرة العامة

كل كارت منتج راجع للموبايل يحتوي على field جديد:

```json
{
  "show_price_on_card": true
}
```

لو القيمة `true`:

- اعرض سعر الكارت من `price`.
- لو `is_discounted = true` اعرض `old_price` والخصم حسب تصميم الكارت.
- قيمة `price` في الكروت جاية من الباك إند كأقل سعر متاح للمنتج أو المجموعة المعروضة في هذا الكارت.

لو القيمة `false`:

- اخفي السعر من الكارت.
- اخفي `old_price`.
- اخفي `discount` أو أي badge مبني على السعر.
- لا تمنع فتح صفحة تفاصيل المنتج.
- لا تخفي اسم المنتج أو الصورة أو التقييم أو زر المفضلة.

مهم: حتى لو `show_price_on_card = false` قد يظل `price` موجودا في JSON. الموبايل لا يعرضه على الكارت في هذه الحالة.

## Default للموبايل

لو field `show_price_on_card` غير موجود لأي سبب، تعامل معه كأنه `true`.

مثال Flutter/Dart:

```dart
final showPriceOnCard = json['show_price_on_card'] as bool? ?? true;
```

## أين يظهر هذا field؟

### Home

Endpoints:

```http
GET /api/home/content
GET /api/home/special-offers
GET /api/home/recommended
GET /api/home/best-selling
GET /api/home/featured-products
GET /api/home/explore-more
GET /api/home/dynamic-sections
```

المكان:

```text
items[].show_price_on_card
special_offers_section.items[].show_price_on_card
recommended_section.items[].show_price_on_card
best_selling_section.items[].show_price_on_card
featured_products_section.items[].show_price_on_card
explore_more_section.items[].show_price_on_card
dynamic_sections[].items[].show_price_on_card
```

### Search

Endpoint:

```http
GET /api/products/search
```

المكان:

```text
items[].show_price_on_card
```

### Category Products

Endpoints:

```http
GET /api/categories/products
GET /api/categories/{categoryId}/products
```

المكان:

```text
items[].show_price_on_card
```

### Brand Products

Endpoint:

```http
GET /api/brands/{brandId}/products
```

المكان:

```text
items[].show_price_on_card
```

### Favorites

Endpoints:

```http
GET /api/favorites
POST /api/favorites
```

المكان:

```text
items[].show_price_on_card
item.show_price_on_card
```

### Product Details

Endpoint:

```http
GET /api/products/{productId}
```

المهم هنا:

- تفاصيل المنتج الرئيسية لا تتأثر بزر إظهار أو إخفاء سعر الكارت.
- اعرض `price`, `old_price`, `vendor_prices`, و `variant_options` في صفحة التفاصيل كالمعتاد.
- كروت المنتجات المتشابهة فقط تتأثر بالتحكم.

المكان الذي يجب تطبيق التحكم عليه:

```text
similar_products[].show_price_on_card
```

لا تعتمد على وجود `show_price_on_card` في root الخاص بـ `ProductDetailsDto`.

## مثال كارت السعر ظاهر

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Product name",
  "store": "Vendor name",
  "price": 120.0,
  "old_price": 150.0,
  "discount": "20%",
  "is_discounted": true,
  "show_price_on_card": true
}
```

الموبايل يعرض:

- السعر: `120.0`
- السعر القديم: `150.0`
- الخصم: `20%`

## مثال كارت السعر مخفي

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Product name",
  "store": "Vendor name",
  "price": 120.0,
  "old_price": 150.0,
  "discount": "20%",
  "is_discounted": true,
  "show_price_on_card": false
}
```

الموبايل يعرض بيانات الكارت العادية، لكنه يخفي:

- السعر
- السعر القديم
- الخصم
- أي label مثل "يبدأ من" أو "وفر"

## Product Card Widget Logic

يفضل أن يكون التحكم داخل widget أو component واحد مشترك لكل كروت المنتجات.

Pseudo code:

```dart
if (product.showPriceOnCard) {
  showPrice(product.price);

  if (product.isDiscounted && product.oldPrice != null) {
    showOldPrice(product.oldPrice);
    showDiscount(product.discount);
  }
} else {
  hidePriceBlock();
}
```

## ملاحظات مهمة

- لا تعمل API call إضافي لجلب أقل سعر للكارت. الباك إند يرسل أقل سعر في `price`.
- لا تغير سلوك صفحة تفاصيل المنتج الرئيسية. الأسعار هناك تظهر عادي.
- عند تغيير الزر من الداشبورد، الكاش يتم تفريغه من الباك إند. الموبايل يحتاج فقط يعمل refresh للـ list أو يفتح الشاشة من جديد.
- لا تستخدم admin endpoint من تطبيق العميل.

## Admin Endpoint للعلم فقط

هذا endpoint مستخدم من الداشبورد فقط داخل قسم التسويق للتحكم في كل كروت المنتجات مرة واحدة:

```http
GET /api/admin/marketing/product-card-price-visibility
Authorization: Bearer <admin-token>
```

Response:

```json
{
  "showPriceOnCard": true,
  "totalProducts": 120,
  "visibleProducts": 120,
  "hiddenProducts": 0,
  "isMixed": false
}
```

لتغيير الحالة لكل المنتجات:

```http
PATCH /api/admin/marketing/product-card-price-visibility
Authorization: Bearer <admin-token>
Content-Type: application/json
```

Body:

```json
{
  "showPriceOnCard": false
}
```

استخدم `true` لإظهار السعر مرة أخرى.

## Acceptance Checklist للموبايل

- عند `show_price_on_card = true` يظهر السعر على كل كروت المنتجات.
- عند `show_price_on_card = false` يختفي السعر والخصم من كل كروت المنتجات.
- صفحة تفاصيل المنتج الرئيسية تعرض السعر دائما.
- `similar_products` داخل صفحة التفاصيل تتبع نفس قاعدة الكروت.
- لو field غير موجود، السعر يظهر كالمعتاد.
