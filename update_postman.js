const fs = require('fs');
const path = require('path');

const filePath = path.join(__dirname, 'Zadana_Postman_Collection.json');
const data = fs.readFileSync(filePath, 'utf8');
const collection = JSON.parse(data);

// Find the Vendor App folder
const vendorAppFolder = collection.item.find(i => i.name.includes('2. Vendor App'));

if (!vendorAppFolder) {
    console.error('Vendor App folder not found');
    process.exit(1);
}

// 1. Add Vendor Product Requests Folder
const productRequestsFolder = {
    name: "Product Requests",
    item: [
        {
            name: "1. Submit Product Request",
            request: {
                method: "POST",
                header: [
                    { key: "Authorization", value: "Bearer {{vendorToken}}" },
                    { key: "Content-Type", value: "application/json" }
                ],
                body: {
                    mode: "raw",
                    raw: JSON.stringify({
                        suggestedNameAr: "كوكا كولا 330 مل",
                        suggestedNameEn: "Coca Cola 330ml",
                        suggestedCategoryId: "00000000-0000-0000-0000-000000000000",
                        suggestedDescriptionAr: "مشروب غازي",
                        suggestedDescriptionEn: "Soft Drink",
                        imageUrl: "https://example.com/coke.jpg"
                    }, null, 2)
                },
                url: { raw: "{{baseUrl}}/vendor/product-requests", host: ["{{baseUrl}}"], path: ["vendor", "product-requests"] }
            }
        },
        {
            name: "2. Get My Product Requests",
            request: {
                method: "GET",
                header: [{ key: "Authorization", value: "Bearer {{vendorToken}}" }],
                url: {
                    raw: "{{baseUrl}}/vendor/product-requests?pageNumber=1&pageSize=10",
                    host: ["{{baseUrl}}"],
                    path: ["vendor", "product-requests"],
                    query: [
                        { key: "pageNumber", value: "1" },
                        { key: "pageSize", value: "10" },
                        { key: "status", value: "Pending", disabled: true }
                    ]
                }
            }
        }
    ]
};

// 2. Add Vendor Products Folder
const vendorProductsFolder = {
    name: "Vendor Products",
    item: [
        {
            name: "1. Create Vendor Product",
            request: {
                method: "POST",
                header: [
                    { key: "Authorization", value: "Bearer {{vendorToken}}" },
                    { key: "Content-Type", value: "application/json" }
                ],
                body: {
                    mode: "raw",
                    raw: JSON.stringify({
                        masterProductId: "00000000-0000-0000-0000-000000000000",
                        sellingPrice: 15.50,
                        compareAtPrice: 17.00,
                        costPrice: 12.00,
                        stockQty: 100,
                        minOrderQty: 1,
                        maxOrderQty: 10,
                        sku: "COKE-330-01",
                        branchId: null
                    }, null, 2)
                },
                url: { raw: "{{baseUrl}}/vendor/products", host: ["{{baseUrl}}"], path: ["vendor", "products"] }
            }
        },
        {
            name: "2. Update Vendor Product",
            request: {
                method: "PUT",
                header: [
                    { key: "Authorization", value: "Bearer {{vendorToken}}" },
                    { key: "Content-Type", value: "application/json" }
                ],
                body: {
                    mode: "raw",
                    raw: JSON.stringify({
                        sellingPrice: 16.00,
                        compareAtPrice: 18.00,
                        stockQty: 150,
                        customNameAr: "كوكا كولا عرض خاص",
                        customNameEn: "Coca Cola Special Offer",
                        customDescriptionAr: "عرض كوكا كولا",
                        customDescriptionEn: "Coke offer"
                    }, null, 2)
                },
                url: { raw: "{{baseUrl}}/vendor/products/{{productId}}", host: ["{{baseUrl}}"], path: ["vendor", "products", "{{productId}}"] }
            }
        },
        {
            name: "3. Change Product Status",
            request: {
                method: "PATCH",
                header: [
                    { key: "Authorization", value: "Bearer {{vendorToken}}" },
                    { key: "Content-Type", value: "application/json" }
                ],
                body: {
                    mode: "raw",
                    raw: JSON.stringify({
                        isActive: false
                    }, null, 2)
                },
                url: { raw: "{{baseUrl}}/vendor/products/{{productId}}/status", host: ["{{baseUrl}}"], path: ["vendor", "products", "{{productId}}", "status"] }
            }
        },
        {
            name: "4. Get My Products",
            request: {
                method: "GET",
                header: [{ key: "Authorization", value: "Bearer {{vendorToken}}" }],
                url: {
                    raw: "{{baseUrl}}/vendor/products?pageNumber=1&pageSize=10",
                    host: ["{{baseUrl}}"],
                    path: ["vendor", "products"],
                    query: [
                        { key: "pageNumber", value: "1" },
                        { key: "pageSize", value: "10" },
                        { key: "categoryId", value: "00000000-0000-0000-0000-000000000000", disabled: true },
                        { key: "branchId", value: "00000000-0000-0000-0000-000000000000", disabled: true }
                    ]
                }
            }
        },
        {
            name: "5. Get Product By Id",
            request: {
                method: "GET",
                header: [{ key: "Authorization", value: "Bearer {{vendorToken}}" }],
                url: {
                    raw: "{{baseUrl}}/vendor/products/{{productId}}",
                    host: ["{{baseUrl}}"],
                    path: ["vendor", "products", "{{productId}}"]
                }
            }
        }
    ]
};

// Check if folders already exist to avoid duplication
const existingProductReqs = vendorAppFolder.item.find(i => i.name === 'Product Requests');
const existingVendorProds = vendorAppFolder.item.find(i => i.name === 'Vendor Products');

if (!existingProductReqs) vendorAppFolder.item.push(productRequestsFolder);
if (!existingVendorProds) vendorAppFolder.item.push(vendorProductsFolder);

// Save back
fs.writeFileSync(filePath, JSON.stringify(collection, null, 2));
console.log('Postman collection updated successfully!');
