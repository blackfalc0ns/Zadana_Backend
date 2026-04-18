const fs = require('fs');
const path = require('path');

const inputArg = process.argv[2] || 'swagger.live2.json';
const sourcePath = path.isAbsolute(inputArg) ? inputArg : path.join(__dirname, inputArg);

if (!fs.existsSync(sourcePath)) {
  console.error(`Swagger source not found: ${sourcePath}`);
  process.exit(1);
}

const supportedMethods = new Set(['get', 'post', 'put', 'patch', 'delete', 'options', 'head']);
const source = JSON.parse(fs.readFileSync(sourcePath, 'utf8'));

function deepClone(value) {
  return JSON.parse(JSON.stringify(value));
}

function startsWithAny(value, prefixes) {
  return prefixes.some((prefix) => value.startsWith(prefix));
}

function getOriginalTag(operation) {
  return Array.isArray(operation.tags) && operation.tags.length > 0 ? operation.tags[0] : '';
}

function mapDetailedTag(routePath, operation) {
  const tag = getOriginalTag(operation);

  if (tag === 'Admin Dashboard API') {
    if (routePath.startsWith('/api/admin/auth')) return 'Admin/Auth';
    if (routePath.startsWith('/api/admin/orders')) return 'Admin/Orders';
    if (routePath.startsWith('/api/admin/customers')) return 'Admin/Customers';
    if (routePath.startsWith('/api/admin/vendors')) return 'Admin/Vendors';
    if (routePath.startsWith('/api/admin/marketing/banners')) return 'Admin/Marketing/Banners';
    if (routePath.startsWith('/api/admin/marketing/featured-products')) return 'Admin/Marketing/Featured Products';
    if (routePath.startsWith('/api/admin/marketing/home-content-sections')) return 'Admin/Marketing/Home Content Sections';
    if (routePath.startsWith('/api/admin/marketing/home-sections')) return 'Admin/Marketing/Home Sections';
    return 'Admin';
  }

  if (tag === 'Catalog (Admins)') {
    if (routePath.includes('/brand-requests/')) return 'Admin/Catalog/Brand Requests';
    if (startsWithAny(routePath, ['/api/admin/catalog/brands', '/api/admin/catalog/brands/'])) return 'Admin/Catalog/Brands';
    if (startsWithAny(routePath, ['/api/admin/catalog/categories', '/api/admin/catalog/categories/'])) return 'Admin/Catalog/Categories';
    if (routePath.includes('/category-requests/')) return 'Admin/Catalog/Category Requests';
    if (startsWithAny(routePath, ['/api/admin/catalog/request-center', '/api/admin/catalog/request-center/'])) return 'Admin/Catalog/Request Center';
    if (startsWithAny(routePath, ['/api/admin/catalog/units', '/api/admin/catalog/units/'])) return 'Admin/Catalog/Units';
    if (routePath.includes('/product-requests')) return 'Admin/Catalog/Product Requests';
    if (startsWithAny(routePath, ['/api/admin/catalog/products', '/api/admin/catalog/products/'])) return 'Admin/Catalog/Master Products';
    return 'Admin/Catalog';
  }

  if (tag === 'Catalog (Vendors)') {
    if (startsWithAny(routePath, ['/api/vendor/catalog/brand-requests', '/api/vendor/catalog/brand-requests/'])) return 'Vendor/Catalog/Brand Requests';
    if (startsWithAny(routePath, ['/api/vendor/catalog/brands', '/api/vendor/catalog/brands/'])) return 'Vendor/Catalog/Brands';
    if (startsWithAny(routePath, ['/api/vendor/catalog/categories', '/api/vendor/catalog/categories/'])) return 'Vendor/Catalog/Categories';
    if (startsWithAny(routePath, ['/api/vendor/catalog/category-requests', '/api/vendor/catalog/category-requests/'])) return 'Vendor/Catalog/Category Requests';
    if (startsWithAny(routePath, ['/api/vendor/catalog/master-products', '/api/vendor/catalog/master-products/'])) return 'Vendor/Catalog/Master Products';
    if (startsWithAny(routePath, ['/api/vendor/catalog/notifications', '/api/vendor/catalog/notifications/'])) return 'Vendor/Catalog/Notifications';
    if (startsWithAny(routePath, ['/api/vendor/catalog/request-center', '/api/vendor/catalog/request-center/'])) return 'Vendor/Catalog/Request Center';
    if (startsWithAny(routePath, ['/api/vendor/catalog/units', '/api/vendor/catalog/units/'])) return 'Vendor/Catalog/Units';
    if (routePath.includes('/product-requests')) return 'Vendor/Catalog/Product Requests';
    if (startsWithAny(routePath, ['/api/vendor/products', '/api/vendor/products/'])) return 'Vendor/Catalog/Vendor Products';
    return 'Vendor/Catalog';
  }

  if (tag === 'Customer App API') {
    if (startsWithAny(routePath, ['/api/customers/auth', '/api/customers/auth/'])) return 'Customer/Auth';
    if (startsWithAny(routePath, ['/api/customers/addresses', '/api/customers/addresses/'])) return 'Customer/Addresses';
    if (startsWithAny(routePath, ['/api/home', '/api/home/'])) return 'Customer/Home';
    if (startsWithAny(routePath, ['/api/checkout', '/api/checkout/'])) return 'Customer/Checkout';
    if (startsWithAny(routePath, ['/api/orders', '/api/orders/', '/api/cart', '/api/cart/'])) return 'Customer/Orders';
    if (startsWithAny(routePath, ['/api/brands', '/api/brands/'])) return 'Customer/Brands';
    if (startsWithAny(routePath, ['/api/categories', '/api/categories/'])) return 'Customer/Categories';
    if (startsWithAny(routePath, ['/api/products', '/api/products/'])) return 'Customer/Products';
    if (startsWithAny(routePath, ['/api/favorites', '/api/favorites/'])) return 'Customer/Favorites';
    if (startsWithAny(routePath, ['/api/notifications', '/api/notifications/'])) return 'Customer/Notifications';
    return 'Customer';
  }

  if (tag === 'Driver App API') {
    if (startsWithAny(routePath, ['/api/drivers/auth', '/api/drivers/auth/'])) return 'Driver/Auth';
    if (startsWithAny(routePath, ['/api/drivers/orders', '/api/drivers/orders/'])) return 'Driver/Orders';
    if (startsWithAny(routePath, ['/api/drivers/register', '/api/drivers/register/'])) return 'Driver/Registration';
    return 'Driver';
  }

  if (tag === 'Vendor App API') {
    if (startsWithAny(routePath, ['/api/vendors/auth', '/api/vendors/auth/'])) return 'Vendor/Auth';
    if (startsWithAny(routePath, ['/api/vendors/profile', '/api/vendors/profile/'])) return 'Vendor/Profile';
    if (startsWithAny(routePath, ['/api/vendor/notifications', '/api/vendor/notifications/'])) return 'Vendor/Notifications';
    if (startsWithAny(routePath, ['/api/vendor/orders', '/api/vendor/orders/'])) return 'Vendor/Orders';
    if (startsWithAny(routePath, ['/api/vendors/register', '/api/vendors/register/'])) return 'Vendor/Registration';
    return 'Vendor';
  }

  if (tag === 'Common Systems (Files)') return 'System/Files';
  if (tag === 'Payments') return 'System/Payments';
  if (tag === 'Zadana.Api') return 'System/Health';
  if (tag === 'Development') return 'System/Development';
  if (tag === 'Operations') return 'System/Operations';

  return tag || 'System';
}

function mapFeatureTag(routePath, operation) {
  const detailedTag = mapDetailedTag(routePath, operation);

  if (detailedTag.startsWith('Admin/Auth') || detailedTag.startsWith('Customer/Auth') || detailedTag.startsWith('Vendor/Auth')) return 'Auth';
  if (detailedTag.startsWith('Customer/Addresses')) return 'Addresses';
  if (detailedTag.startsWith('Customer/Home')) return 'Home';
  if (detailedTag.startsWith('Customer/Checkout')) return 'Checkout';
  if (detailedTag.startsWith('Customer/Orders') || detailedTag.startsWith('Vendor/Orders') || detailedTag.startsWith('Admin/Orders')) return 'Orders';
  if (detailedTag.startsWith('Customer/Favorites')) return 'Favorites';
  if (detailedTag.startsWith('Customer/Notifications') || detailedTag.startsWith('Vendor/Notifications') || detailedTag.startsWith('Vendor/Catalog/Notifications')) return 'Notifications';
  if (detailedTag.startsWith('Admin/Marketing')) return 'Marketing';
  if (detailedTag.startsWith('Admin/Vendors') || detailedTag.startsWith('Vendor/Profile') || detailedTag.startsWith('Vendor/Registration')) return 'Vendors';
  if (detailedTag.startsWith('Admin/Customers')) return 'Customers';
  if (detailedTag.startsWith('Driver/')) return 'Drivers';
  if (detailedTag.startsWith('Admin/Catalog') || detailedTag.startsWith('Vendor/Catalog') || detailedTag.startsWith('Customer/Brands') || detailedTag.startsWith('Customer/Categories') || detailedTag.startsWith('Customer/Products')) return 'Catalog';
  if (detailedTag.startsWith('System/Files')) return 'Files';
  if (detailedTag.startsWith('System/Payments')) return 'Payments';
  if (detailedTag.startsWith('System/Health')) return 'Health';
  if (detailedTag.startsWith('System/Development')) return 'Development';
  if (detailedTag.startsWith('System/Operations')) return 'Operations';

  return detailedTag.split('/').pop() || detailedTag;
}

function setOperationTags(document, mapper) {
  const usedTags = new Set();

  Object.entries(document.paths || {}).forEach(([routePath, pathItem]) => {
    Object.entries(pathItem || {}).forEach(([method, operation]) => {
      if (!supportedMethods.has(method.toLowerCase()) || !operation || typeof operation !== 'object') {
        return;
      }

      const mappedTag = mapper(routePath, operation);
      operation.tags = mappedTag ? [mappedTag] : [];
      if (mappedTag) {
        usedTags.add(mappedTag);
      }
    });
  });

  document.tags = Array.from(usedTags)
    .sort((a, b) => a.localeCompare(b))
    .map((name) => ({
      name,
      description: `APIDog folder for ${name}`
    }));

  return document;
}

const liveSwagger = deepClone(source);
const openApiDoc = deepClone(source);
openApiDoc.info = {
  title: 'Zadana API - APIDog Import',
  version: source.info?.version || '1.0',
  description: 'OpenAPI document prepared for APIDog import.'
};
openApiDoc.tags = Array.from(new Set(
  Object.values(openApiDoc.paths || {}).flatMap((pathItem) =>
    Object.entries(pathItem || {})
      .filter(([method]) => supportedMethods.has(method.toLowerCase()))
      .flatMap(([, operation]) => operation.tags || []))
)).sort().map((name) => ({ name }));

const byFeatureDoc = setOperationTags(deepClone(source), mapFeatureTag);
byFeatureDoc.info = {
  title: 'Zadana API - APIDog By Feature',
  version: source.info?.version || '1.0',
  description: 'OpenAPI document grouped by feature folders like Home, Auth, Marketing, Catalog, and more.'
};

const foldersDoc = setOperationTags(deepClone(source), mapDetailedTag);
foldersDoc.info = {
  title: 'Zadana API - APIDog Detailed Folders',
  version: source.info?.version || '1.0',
  description: 'OpenAPI document with detailed folder-style tags for APIDog import.'
};

const outputs = [
  ['swagger.json', liveSwagger],
  ['swagger.runtime.json', liveSwagger],
  ['Zadana_APIDog_OpenAPI.json', openApiDoc],
  ['Zadana_APIDog_ByFeature.json', byFeatureDoc],
  ['Zadana_APIDog_Folders.json', foldersDoc]
];

for (const [filename, document] of outputs) {
  fs.writeFileSync(path.join(__dirname, filename), JSON.stringify(document, null, 4));
}

console.log(`APIDog documents generated from ${path.basename(sourcePath)}.`);
