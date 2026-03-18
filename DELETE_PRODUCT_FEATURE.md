# ميزة حذف المنتج - Product Delete Feature

## التغييرات المضافة / Changes Added

### Backend Changes

#### 1. Command & Handler
تم إنشاء Command و Handler جديد للحذف:
- `DeleteMasterProductCommand.cs` - الأمر الخاص بحذف المنتج
- `DeleteMasterProductCommandHandler.cs` - معالج الأمر

#### 2. API Endpoint
تم إضافة endpoint جديد في `AdminMasterProductsController.cs`:
```csharp
[HttpDelete("{id}")]
public async Task<ActionResult> DeleteProduct(Guid id)
{
    await _sender.Send(new DeleteMasterProductCommand(id));
    return NoContent();
}
```

**Endpoint Details:**
- Method: `DELETE`
- URL: `/api/admin/catalog/products/{id}`
- Authorization: Required (Admin, SuperAdmin)
- Response: 204 No Content on success

### Frontend Changes

#### 1. Service Method
تم إضافة method جديد في `catalog.service.ts`:
```typescript
deleteProduct(id: string): Observable<void> {
  return this.http.delete<void>(`${this.apiUrl}/products/${id}`, 
    { headers: this.getHeaders() });
}
```

#### 2. Component Method
تم إضافة method في `master-products.component.ts`:
```typescript
deleteProduct(productId: string, event: Event) {
  event.stopPropagation();
  
  if (!confirm(this.translate.currentLang === 'ar' 
    ? 'هل أنت متأكد من حذف هذا المنتج؟' 
    : 'Are you sure you want to delete this product?')) {
    return;
  }

  this.catalogService.deleteProduct(productId).subscribe({
    next: () => {
      this.loadProducts();
    },
    error: (err) => {
      console.error('Failed to delete product', err);
      alert(this.translate.currentLang === 'ar' 
        ? 'فشل حذف المنتج' 
        : 'Failed to delete product');
    }
  });
}
```

#### 3. UI Changes
تم إضافة زر حذف في:
- **Desktop Table View**: أيقونة سلة المهملات باللون الأحمر
- **Mobile Cards View**: زر حذف مع أيقونة

**Button Features:**
- ✅ أيقونة سلة المهملات (Trash icon)
- ✅ تأثير hover باللون الأحمر
- ✅ رسالة تأكيد قبل الحذف
- ✅ تحديث القائمة تلقائياً بعد الحذف
- ✅ رسالة خطأ في حالة الفشل

## كيفية الاستخدام / How to Use

### من واجهة المستخدم / From UI
1. افتح صفحة المنتجات: `http://localhost:4200/catalog/products`
2. ابحث عن المنتج المراد حذفه
3. اضغط على أيقونة سلة المهملات (الزر الأحمر)
4. أكد عملية الحذف في النافذة المنبثقة
5. سيتم حذف المنتج وتحديث القائمة تلقائياً

### من API مباشرة / From API Directly
```bash
DELETE https://zadana.runasp.net/api/admin/catalog/products/{product-id}
Authorization: Bearer {your-token}
```

## الأمان / Security

- ✅ يتطلب تسجيل دخول (Authentication required)
- ✅ يتطلب صلاحيات Admin أو SuperAdmin
- ✅ رسالة تأكيد قبل الحذف
- ✅ التحقق من وجود المنتج قبل الحذف

## الملفات المعدلة / Modified Files

### Backend
- `Zadana.Application/Modules/Catalog/Commands/DeleteMasterProduct/DeleteMasterProductCommand.cs` (جديد)
- `Zadana.Application/Modules/Catalog/Commands/DeleteMasterProduct/DeleteMasterProductCommandHandler.cs` (جديد)
- `Zadana.Api/Modules/Catalog/Controllers/AdminMasterProductsController.cs` (معدل)

### Frontend
- `catalog.service.ts` (معدل)
- `master-products.component.ts` (معدل)
- `master-products.component.html` (معدل)

## النشر / Deployment

### Backend
تم بناء ونشر الـ backend في:
```
Zadana-Backend/publish/
```

**لنشر على runasp.net:**
1. ارفع محتويات مجلد `publish/` إلى السيرفر
2. أعد تشغيل التطبيق
3. اختبر الـ endpoint: `DELETE /api/admin/catalog/products/{id}`

### Frontend
لا يتطلب نشر إضافي - التغييرات جاهزة للاستخدام

## الاختبار / Testing

### Test Cases
1. ✅ حذف منتج موجود - يجب أن ينجح
2. ✅ حذف منتج غير موجود - يجب أن يرجع 404
3. ✅ حذف بدون صلاحيات - يجب أن يرجع 401/403
4. ✅ إلغاء عملية الحذف - لا يجب أن يحذف المنتج

## ملاحظات / Notes

- الحذف نهائي (Hard Delete) - لا يمكن استرجاع المنتج بعد الحذف
- يتم تحديث قائمة المنتجات تلقائياً بعد الحذف
- رسالة التأكيد تظهر باللغة المناسبة (عربي/إنجليزي)

## التحسينات المستقبلية / Future Improvements

- [ ] إضافة Soft Delete (حذف منطقي بدلاً من فعلي)
- [ ] إضافة سجل للمنتجات المحذوفة
- [ ] إضافة إمكانية استرجاع المنتجات المحذوفة
- [ ] إضافة تأكيد بإدخال اسم المنتج للحذف (للمنتجات المهمة)
- [ ] إضافة إشعار نجاح بعد الحذف (Toast notification)
