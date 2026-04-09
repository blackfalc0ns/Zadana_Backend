using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Vendors.Entities;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public ApplicationDbContextInitialiser(
        ApplicationDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task InitialiseAsync()
    {
        try
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.MigrateAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        await SeedRolesAsync();
        await SeedSuperAdminAsync();
        await SeedUnitsAsync();
        await SeedCategoriesAsync();
        await SeedBrandsAsync();
        await SeedProductTypesAndPartsAsync();
        await SeedMasterProductsAsync();
        await SeedSampleVendorsAsync();
        await SeedHomeBannersAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in Enum.GetValues<UserRole>())
        {
            if (!await _roleManager.RoleExistsAsync(role.ToString()))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role.ToString()));
            }
        }
    }

    private async Task SeedSuperAdminAsync()
    {
        if (await _userManager.FindByEmailAsync("admin@system.com") != null)
        {
            return;
        }

        var admin = new User(
            "Super Admin",
            "admin@system.com",
            "01000000000",
            UserRole.SuperAdmin);

        await _userManager.CreateAsync(admin, "Admin@123");
        await _userManager.AddToRoleAsync(admin, UserRole.SuperAdmin.ToString());
    }

    private async Task SeedUnitsAsync()
    {
        if (await _context.UnitsOfMeasure.AnyAsync())
        {
            return;
        }

        var units = new List<UnitOfMeasure>
        {
            new("Kilogram", "Kilogram", "kg"),
            new("Gram", "Gram", "g"),
            new("Liter", "Liter", "L"),
            new("Milliliter", "Milliliter", "mL"),
            new("Piece", "Piece", "pcs"),
            new("Carton", "Carton", "ctn"),
            new("Pack", "Pack", "pk"),
            new("Box", "Box", "box"),
            new("Dozen", "Dozen", "dz")
        };

        await _context.UnitsOfMeasure.AddRangeAsync(units);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCategoriesAsync()
    {
        if (await _context.Categories.AnyAsync())
        {
            return;
        }

        var food = new Category("Food", "Food", null, null, 1);
        var electronics = new Category("Electronics", "Electronics", null, null, 2);
        var home = new Category("Home", "Home Appliances", null, null, 3);

        await _context.Categories.AddRangeAsync(food, electronics, home);
        await _context.SaveChangesAsync();

        var dairy = new Category("Dairy", "Dairy", null, food.Id, 1);
        var meat = new Category("Meat", "Meat", null, food.Id, 2);
        var phones = new Category("Phones", "Phones", null, electronics.Id, 1);

        await _context.Categories.AddRangeAsync(dairy, meat, phones);
        await _context.SaveChangesAsync();
    }

    private async Task SeedBrandsAsync()
    {
        if (await _context.Brands.AnyAsync())
        {
            return;
        }

        var brands = new List<Brand>
        {
            new("Almarai", "Almarai"),
            new("Nadec", "Nadec"),
            new("Samsung", "Samsung"),
            new("Apple", "Apple"),
            new("LG", "LG")
        };

        await _context.Brands.AddRangeAsync(brands);
        await _context.SaveChangesAsync();
    }

    private async Task SeedMasterProductsAsync()
    {
        var category = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Dairy");
        var brand = await _context.Brands.FirstOrDefaultAsync(item => item.NameEn == "Almarai");
        var unit = await _context.UnitsOfMeasure.FirstOrDefaultAsync(item => item.Symbol == "L");

        if (category == null || brand == null || unit == null)
        {
            return;
        }

        var milkType = await _context.ProductTypes.FirstOrDefaultAsync(item => item.NameEn == "Milk" && item.CategoryId == category.Id);
        var yogurtType = await _context.ProductTypes.FirstOrDefaultAsync(item => item.NameEn == "Yogurt" && item.CategoryId == category.Id);
        var fullCreamPart = milkType == null
            ? null
            : await _context.Parts.FirstOrDefaultAsync(item => item.NameEn == "Full Cream" && item.ProductTypeId == milkType.Id);
        var freshPart = yogurtType == null
            ? null
            : await _context.Parts.FirstOrDefaultAsync(item => item.NameEn == "Fresh" && item.ProductTypeId == yogurtType.Id);

        var products = new List<(string Slug, MasterProduct Product)>
        {
            (
                "full-cream-milk-1l",
                new MasterProduct(
                    "Full Cream Milk 1L",
                    "Full Cream Milk 1L",
                    "full-cream-milk-1l",
                    category.Id,
                    brand.Id,
                    unit.Id,
                    "Fresh local milk",
                    "Fresh local milk",
                    null,
                    milkType?.Id,
                    fullCreamPart?.Id)
            ),
            (
                "fresh-yoghurt",
                new MasterProduct(
                    "Fresh Yoghurt",
                    "Fresh Yoghurt",
                    "fresh-yoghurt",
                    category.Id,
                    brand.Id,
                    unit.Id,
                    "Natural yoghurt",
                    "Natural yoghurt",
                    null,
                    yogurtType?.Id,
                    freshPart?.Id)
            )
        };

        foreach (var (_, product) in products)
        {
            product.Publish();
        }

        foreach (var (slug, product) in products)
        {
            var existing = await _context.MasterProducts.FirstOrDefaultAsync(item => item.Slug == slug);
            if (existing == null)
            {
                await _context.MasterProducts.AddAsync(product);
                continue;
            }

            existing.ChangeProductType(product.ProductTypeId);
            existing.ChangePart(product.PartId);
        }

        await BackfillDairyProductTypesAndPartsAsync(category.Id, milkType?.Id, yogurtType?.Id, fullCreamPart?.Id, freshPart?.Id);
        await _context.SaveChangesAsync();
    }

    private async Task SeedProductTypesAndPartsAsync()
    {
        var dairyCategory = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Dairy");
        if (dairyCategory == null)
        {
            return;
        }

        var milkType = await EnsureProductTypeAsync("حليب", "Milk", dairyCategory.Id);
        var yogurtType = await EnsureProductTypeAsync("زبادي", "Yogurt", dairyCategory.Id);

        await EnsurePartAsync("كامل الدسم", "Full Cream", milkType.Id);
        await EnsurePartAsync("خالي الدسم", "Skimmed", milkType.Id);
        await EnsurePartAsync("طازج", "Fresh", yogurtType.Id);
        await EnsurePartAsync("يوناني", "Greek", yogurtType.Id);

        await _context.SaveChangesAsync();
    }

    private async Task<ProductType> EnsureProductTypeAsync(string nameAr, string nameEn, Guid categoryId)
    {
        var existing = await _context.ProductTypes
            .FirstOrDefaultAsync(item => item.CategoryId == categoryId && item.NameEn == nameEn);

        if (existing != null)
        {
            existing.Update(nameAr, nameEn, categoryId);
            return existing;
        }

        var entity = new ProductType(nameAr, nameEn, categoryId);
        await _context.ProductTypes.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    private async Task<Part> EnsurePartAsync(string nameAr, string nameEn, Guid productTypeId)
    {
        var existing = await _context.Parts
            .FirstOrDefaultAsync(item => item.ProductTypeId == productTypeId && item.NameEn == nameEn);

        if (existing != null)
        {
            existing.Update(nameAr, nameEn, productTypeId);
            return existing;
        }

        var entity = new Part(nameAr, nameEn, productTypeId);
        await _context.Parts.AddAsync(entity);
        return entity;
    }

    private async Task BackfillDairyProductTypesAndPartsAsync(
        Guid categoryId,
        Guid? milkTypeId,
        Guid? yogurtTypeId,
        Guid? fullCreamPartId,
        Guid? freshPartId)
    {
        var products = await _context.MasterProducts
            .Where(item => item.CategoryId == categoryId && (!item.ProductTypeId.HasValue || !item.PartId.HasValue))
            .ToListAsync();

        foreach (var product in products)
        {
            var normalized = $"{product.NameEn} {product.NameAr} {product.Slug}".ToLowerInvariant();

            if (normalized.Contains("milk") || normalized.Contains("حليب"))
            {
                if (!product.ProductTypeId.HasValue)
                {
                    product.ChangeProductType(milkTypeId);
                }

                if (!product.PartId.HasValue)
                {
                    product.ChangePart(fullCreamPartId);
                }

                continue;
            }

            if (normalized.Contains("yogurt") || normalized.Contains("yoghurt") || normalized.Contains("زباد"))
            {
                if (!product.ProductTypeId.HasValue)
                {
                    product.ChangeProductType(yogurtTypeId);
                }

                if (!product.PartId.HasValue)
                {
                    product.ChangePart(freshPartId);
                }
            }
        }
    }

    private async Task SeedSampleVendorsAsync()
    {
        var adminUser = await _userManager.FindByEmailAsync("admin@system.com");
        if (adminUser == null)
        {
            return;
        }

        var seeds = new[]
        {
            new VendorSeedDefinition(
                FullName: "Abdullah Khaled",
                Email: "vendor.test1@zadana.local",
                Phone: "+966501234567",
                Password: "Vendor@12345",
                BusinessNameAr: "Modern Tech Mart",
                BusinessNameEn: "Modern Tech Mart",
                BusinessType: "Electronics",
                CommercialRegistrationNumber: "1010123456",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddYears(2),
                ContactEmail: "vendor.test1@zadana.local",
                ContactPhone: "+966501234567",
                DescriptionAr: "Electronics and smart accessories store.",
                DescriptionEn: "Electronics and smart accessories store.",
                OwnerName: "Abdullah Khaled",
                OwnerEmail: "vendor.test1@zadana.local",
                OwnerPhone: "+966501234567",
                IdNumber: "1012344321",
                Nationality: "Saudi",
                Region: "Central",
                City: "Riyadh",
                NationalAddress: "7293 King Fahd Rd, Al Malqa, Riyadh 13524",
                TaxId: "300123456789012",
                LicenseNumber: "LIC-987654",
                BankName: "Alrajhi",
                AccountHolderName: "Abdullah Khaled",
                Iban: "SA1280000000608012345678",
                SwiftCode: "RJHISARI",
                PayoutCycle: "Biweekly",
                BranchName: "Modern Tech Mart - HQ",
                BranchAddressLine: "King Fahd Rd, Riyadh",
                BranchLatitude: 24.774265m,
                BranchLongitude: 46.738586m,
                BranchContactPhone: "+966501234567",
                BranchDeliveryRadiusKm: 18m,
                CommissionRate: 12.5m,
                StatusKind: SeedVendorStatus.Active,
                AcceptOrders: true,
                MinimumOrderAmount: 75m,
                PreparationTimeMinutes: 35,
                EmailNotificationsEnabled: true,
                SmsNotificationsEnabled: true,
                NewOrdersNotificationsEnabled: true,
                Hours: BuildStandardHours("09:00", "23:00")),

            new VendorSeedDefinition(
                FullName: "Sara Mahmoud",
                Email: "vendor.test2@zadana.local",
                Phone: "+966553334455",
                Password: "Vendor@12345",
                BusinessNameAr: "Fresh Basket",
                BusinessNameEn: "Fresh Basket",
                BusinessType: "Grocery",
                CommercialRegistrationNumber: "4032211455",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddYears(3),
                ContactEmail: "vendor.test2@zadana.local",
                ContactPhone: "+966553334455",
                DescriptionAr: "Groceries and daily essentials.",
                DescriptionEn: "Groceries and daily essentials.",
                OwnerName: "Sara Mahmoud",
                OwnerEmail: "vendor.test2@zadana.local",
                OwnerPhone: "+966553334455",
                IdNumber: "1023456789",
                Nationality: "Egyptian",
                Region: "Western",
                City: "Jeddah",
                NationalAddress: "Prince Sultan Rd, Jeddah 23435",
                TaxId: "300987654321098",
                LicenseNumber: "LIC-443322",
                BankName: "Alahli",
                AccountHolderName: "Sara Mahmoud",
                Iban: "SA0380000000608012345671",
                SwiftCode: "NCBKSAJE",
                PayoutCycle: "Monthly",
                BranchName: "Fresh Basket - Jeddah",
                BranchAddressLine: "Prince Sultan Rd, Jeddah",
                BranchLatitude: 21.543333m,
                BranchLongitude: 39.172779m,
                BranchContactPhone: "+966553334455",
                BranchDeliveryRadiusKm: 14m,
                CommissionRate: null,
                StatusKind: SeedVendorStatus.Pending,
                AcceptOrders: false,
                MinimumOrderAmount: 50m,
                PreparationTimeMinutes: 20,
                EmailNotificationsEnabled: true,
                SmsNotificationsEnabled: false,
                NewOrdersNotificationsEnabled: true,
                Hours: BuildStandardHours("08:00", "22:00")),

            new VendorSeedDefinition(
                FullName: "Omar Hassan",
                Email: "vendor.new@zadana.local",
                Phone: "+966512345890",
                Password: "Vendor@12345",
                BusinessNameAr: "Green Valley Market",
                BusinessNameEn: "Green Valley Market",
                BusinessType: "Grocery",
                CommercialRegistrationNumber: "1015566778",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddYears(2),
                ContactEmail: "vendor.new@zadana.local",
                ContactPhone: "+966512345890",
                DescriptionAr: "متجر بقالة ومنتجات طازجة للتجربة.",
                DescriptionEn: "Grocery and fresh products demo store.",
                OwnerName: "Omar Hassan",
                OwnerEmail: "vendor.new@zadana.local",
                OwnerPhone: "+966512345890",
                IdNumber: "1102233445",
                Nationality: "Egyptian",
                Region: "Central",
                City: "Riyadh",
                NationalAddress: "King Abdullah Rd, Riyadh",
                TaxId: "300666777888999",
                LicenseNumber: "LIC-554433",
                BankName: "Alrajhi",
                AccountHolderName: "Omar Hassan",
                Iban: "SA9980000000608012345683",
                SwiftCode: "RJHISARI",
                PayoutCycle: "Weekly",
                BranchName: "Green Valley Market - Riyadh",
                BranchAddressLine: "King Abdullah Rd, Riyadh",
                BranchLatitude: 24.713800m,
                BranchLongitude: 46.675300m,
                BranchContactPhone: "+966512345890",
                BranchDeliveryRadiusKm: 16m,
                CommissionRate: 12m,
                StatusKind: SeedVendorStatus.Active,
                AcceptOrders: true,
                MinimumOrderAmount: 55m,
                PreparationTimeMinutes: 25,
                EmailNotificationsEnabled: true,
                SmsNotificationsEnabled: true,
                NewOrdersNotificationsEnabled: true,
                Hours: BuildStandardHours("08:30", "23:30")),

            new VendorSeedDefinition(
                FullName: "Faisal Nasser",
                Email: "vendor.suspended@zadana.local",
                Phone: "+966544445566",
                Password: "Vendor@12345",
                BusinessNameAr: "Riyadh Kitchens",
                BusinessNameEn: "Riyadh Kitchens",
                BusinessType: "Restaurant",
                CommercialRegistrationNumber: "1019988776",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddYears(1),
                ContactEmail: "vendor.suspended@zadana.local",
                ContactPhone: "+966544445566",
                DescriptionAr: "Cloud kitchen for quick meals.",
                DescriptionEn: "Cloud kitchen for quick meals.",
                OwnerName: "Faisal Nasser",
                OwnerEmail: "vendor.suspended@zadana.local",
                OwnerPhone: "+966544445566",
                IdNumber: "1045678901",
                Nationality: "Saudi",
                Region: "Central",
                City: "Riyadh",
                NationalAddress: "Olaya St, Riyadh",
                TaxId: "300222222222222",
                LicenseNumber: "LIC-778899",
                BankName: "Alinma",
                AccountHolderName: "Faisal Nasser",
                Iban: "SA5080000000608012345679",
                SwiftCode: "INMASARI",
                PayoutCycle: "Weekly",
                BranchName: "Riyadh Kitchens - Main",
                BranchAddressLine: "Olaya St, Riyadh",
                BranchLatitude: 24.713552m,
                BranchLongitude: 46.675297m,
                BranchContactPhone: "+966544445566",
                BranchDeliveryRadiusKm: 10m,
                CommissionRate: 15m,
                StatusKind: SeedVendorStatus.Suspended,
                AcceptOrders: false,
                MinimumOrderAmount: 45m,
                PreparationTimeMinutes: 30,
                EmailNotificationsEnabled: true,
                SmsNotificationsEnabled: true,
                NewOrdersNotificationsEnabled: false,
                Hours: BuildStandardHours("10:00", "01:00")),

            new VendorSeedDefinition(
                FullName: "Mona Adel",
                Email: "vendor.rejected@zadana.local",
                Phone: "+966566778899",
                Password: "Vendor@12345",
                BusinessNameAr: "Beauty Corner",
                BusinessNameEn: "Beauty Corner",
                BusinessType: "Beauty",
                CommercialRegistrationNumber: "2055512344",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddYears(1),
                ContactEmail: "vendor.rejected@zadana.local",
                ContactPhone: "+966566778899",
                DescriptionAr: "Beauty and care products store.",
                DescriptionEn: "Beauty and care products store.",
                OwnerName: "Mona Adel",
                OwnerEmail: "vendor.rejected@zadana.local",
                OwnerPhone: "+966566778899",
                IdNumber: "1067890123",
                Nationality: "Egyptian",
                Region: "Eastern",
                City: "Dammam",
                NationalAddress: "King Saud St, Dammam",
                TaxId: "300333333333333",
                LicenseNumber: "LIC-112233",
                BankName: "Alahli",
                AccountHolderName: "Mona Adel",
                Iban: "SA0480000000608012345680",
                SwiftCode: "NCBKSAJE",
                PayoutCycle: "Monthly",
                BranchName: "Beauty Corner - Dammam",
                BranchAddressLine: "King Saud St, Dammam",
                BranchLatitude: 26.420683m,
                BranchLongitude: 50.088795m,
                BranchContactPhone: "+966566778899",
                BranchDeliveryRadiusKm: 12m,
                CommissionRate: null,
                StatusKind: SeedVendorStatus.Rejected,
                AcceptOrders: false,
                MinimumOrderAmount: 60m,
                PreparationTimeMinutes: 25,
                EmailNotificationsEnabled: true,
                SmsNotificationsEnabled: false,
                NewOrdersNotificationsEnabled: true,
                Hours: BuildStandardHours("11:00", "22:00")),

            new VendorSeedDefinition(
                FullName: "Yousef Ibrahim",
                Email: "vendor.locked@zadana.local",
                Phone: "+966577889900",
                Password: "Vendor@12345",
                BusinessNameAr: "Home Furniture Hub",
                BusinessNameEn: "Home Furniture Hub",
                BusinessType: "Home",
                CommercialRegistrationNumber: "3022244668",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddYears(2),
                ContactEmail: "vendor.locked@zadana.local",
                ContactPhone: "+966577889900",
                DescriptionAr: "Furniture and home setup store.",
                DescriptionEn: "Furniture and home setup store.",
                OwnerName: "Yousef Ibrahim",
                OwnerEmail: "vendor.locked@zadana.local",
                OwnerPhone: "+966577889900",
                IdNumber: "1089012345",
                Nationality: "Saudi",
                Region: "Eastern",
                City: "Khobar",
                NationalAddress: "Corniche Rd, Khobar",
                TaxId: "300444444444444",
                LicenseNumber: "LIC-665544",
                BankName: "Alrajhi",
                AccountHolderName: "Yousef Ibrahim",
                Iban: "SA1380000000608012345681",
                SwiftCode: "RJHISARI",
                PayoutCycle: "Monthly",
                BranchName: "Home Furniture Hub - Khobar",
                BranchAddressLine: "Corniche Rd, Khobar",
                BranchLatitude: 26.279445m,
                BranchLongitude: 50.208332m,
                BranchContactPhone: "+966577889900",
                BranchDeliveryRadiusKm: 20m,
                CommissionRate: 11m,
                StatusKind: SeedVendorStatus.Locked,
                AcceptOrders: false,
                MinimumOrderAmount: 120m,
                PreparationTimeMinutes: 90,
                EmailNotificationsEnabled: true,
                SmsNotificationsEnabled: true,
                NewOrdersNotificationsEnabled: false,
                Hours: BuildStandardHours("10:00", "23:30")),

            new VendorSeedDefinition(
                FullName: "Rania Hassan",
                Email: "vendor.archived@zadana.local",
                Phone: "+966588990011",
                Password: "Vendor@12345",
                BusinessNameAr: "Orient Perfumes",
                BusinessNameEn: "Orient Perfumes",
                BusinessType: "Retail",
                CommercialRegistrationNumber: "4033399887",
                CommercialRegistrationExpiryDate: DateTime.UtcNow.Date.AddMonths(9),
                ContactEmail: "vendor.archived@zadana.local",
                ContactPhone: "+966588990011",
                DescriptionAr: "Perfumes and gifts store.",
                DescriptionEn: "Perfumes and gifts store.",
                OwnerName: "Rania Hassan",
                OwnerEmail: "vendor.archived@zadana.local",
                OwnerPhone: "+966588990011",
                IdNumber: "1099123456",
                Nationality: "Jordanian",
                Region: "Western",
                City: "Makkah",
                NationalAddress: "Ibrahim Al Khalil Rd, Makkah",
                TaxId: "300555555555555",
                LicenseNumber: "LIC-998877",
                BankName: "Alinma",
                AccountHolderName: "Rania Hassan",
                Iban: "SA6080000000608012345682",
                SwiftCode: "INMASARI",
                PayoutCycle: "Biweekly",
                BranchName: "Orient Perfumes - Makkah",
                BranchAddressLine: "Ibrahim Al Khalil Rd, Makkah",
                BranchLatitude: 21.389082m,
                BranchLongitude: 39.857910m,
                BranchContactPhone: "+966588990011",
                BranchDeliveryRadiusKm: 9m,
                CommissionRate: 13m,
                StatusKind: SeedVendorStatus.Archived,
                AcceptOrders: false,
                MinimumOrderAmount: 80m,
                PreparationTimeMinutes: 15,
                EmailNotificationsEnabled: false,
                SmsNotificationsEnabled: false,
                NewOrdersNotificationsEnabled: false,
                Hours: BuildStandardHours("12:00", "21:00"))
        };

        foreach (var seed in seeds)
        {
            await EnsureVendorSeedAsync(seed, adminUser.Id);
        }
    }

    private async Task SeedHomeBannersAsync()
    {
        if (await _context.HomeBanners.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;
        var banners = new List<HomeBanner>
        {
            new(
                tagAr: "عروض اليوم",
                tagEn: "Today's deals",
                titleAr: "خصومات قوية على منتجاتك اليومية",
                titleEn: "Strong discounts on your daily essentials",
                imageUrl: "/images/home/banners/daily-deals.jpg",
                subtitleAr: "توصيل سريع وأسعار أفضل من المعتاد",
                subtitleEn: "Fast delivery and better-than-usual prices",
                actionLabelAr: "تسوق الآن",
                actionLabelEn: "Shop now",
                displayOrder: 1,
                startsAtUtc: now.AddDays(-7),
                endsAtUtc: now.AddMonths(2)),
            new(
                tagAr: "منتجات مميزة",
                tagEn: "Featured picks",
                titleAr: "اختيارات موصى بها من أفضل المتاجر",
                titleEn: "Recommended picks from top stores",
                imageUrl: "/images/home/banners/featured-picks.jpg",
                subtitleAr: "تشكيلة منتقاة بعناية لتسهيل قرار الشراء",
                subtitleEn: "A curated selection to make buying easier",
                actionLabelAr: "اكتشف المزيد",
                actionLabelEn: "Explore more",
                displayOrder: 2,
                startsAtUtc: now.AddDays(-3),
                endsAtUtc: now.AddMonths(1)),
            new(
                tagAr: "الأكثر مبيعاً",
                tagEn: "Best sellers",
                titleAr: "الأصناف الأكثر طلباً هذا الأسبوع",
                titleEn: "The most ordered items this week",
                imageUrl: "/images/home/banners/best-sellers.jpg",
                subtitleAr: "منتجات يحبها العملاء ويكررون طلبها",
                subtitleEn: "Products customers love and reorder",
                actionLabelAr: "شاهد القائمة",
                actionLabelEn: "See list",
                displayOrder: 3,
                startsAtUtc: now.AddDays(-1),
                endsAtUtc: now.AddMonths(1))
        };

        await _context.HomeBanners.AddRangeAsync(banners);
        await _context.SaveChangesAsync();
    }

    private async Task EnsureVendorSeedAsync(VendorSeedDefinition seed, Guid adminUserId)
    {
        var user = await _userManager.FindByEmailAsync(seed.Email);
        if (user == null)
        {
            user = new User(seed.FullName, seed.Email, seed.Phone, UserRole.Vendor);
            await _userManager.CreateAsync(user, seed.Password);
        }

        if (!await _userManager.IsInRoleAsync(user, UserRole.Vendor.ToString()))
        {
            await _userManager.AddToRoleAsync(user, UserRole.Vendor.ToString());
        }

        var existingVendor = await _context.Vendors.AnyAsync(item => item.UserId == user.Id);
        if (existingVendor)
        {
            return;
        }

        var vendor = new Vendor(
            user.Id,
            seed.BusinessNameAr,
            seed.BusinessNameEn,
            seed.BusinessType,
            seed.CommercialRegistrationNumber,
            seed.ContactEmail,
            seed.ContactPhone,
            seed.TaxId,
            seed.DescriptionAr,
            seed.DescriptionEn,
            seed.OwnerName,
            seed.OwnerEmail,
            seed.OwnerPhone,
            seed.IdNumber,
            seed.Nationality,
            seed.Region,
            seed.City,
            seed.NationalAddress,
            seed.CommercialRegistrationExpiryDate,
            seed.LicenseNumber,
            seed.PayoutCycle);

        vendor.UpdateOperationsSettings(seed.AcceptOrders, seed.MinimumOrderAmount, seed.PreparationTimeMinutes);
        vendor.UpdateNotificationSettings(
            seed.EmailNotificationsEnabled,
            seed.SmsNotificationsEnabled,
            seed.NewOrdersNotificationsEnabled);

        ApplyVendorSeedStatus(vendor, user, adminUserId, seed);

        var branch = new VendorBranch(
            vendor.Id,
            seed.BranchName,
            seed.BranchAddressLine,
            seed.BranchLatitude,
            seed.BranchLongitude,
            seed.BranchContactPhone,
            seed.BranchDeliveryRadiusKm);

        foreach (var hour in seed.Hours)
        {
            branch.OperatingHours.Add(new BranchOperatingHour(
                branch.Id,
                hour.DayOfWeek,
                TimeSpan.Parse(hour.OpenTime),
                TimeSpan.Parse(hour.CloseTime),
                !hour.IsOpen));
        }

        var bankAccount = new VendorBankAccount(
            vendor.Id,
            seed.BankName,
            seed.AccountHolderName,
            seed.Iban,
            seed.SwiftCode);

        bankAccount.Verify(adminUserId);
        bankAccount.SetAsPrimary();

        vendor.Branches.Add(branch);
        vendor.BankAccounts.Add(bankAccount);

        _context.Users.Update(user);
        await _context.Vendors.AddAsync(vendor);
        await _context.SaveChangesAsync();
    }

    private static void ApplyVendorSeedStatus(
        Vendor vendor,
        User user,
        Guid adminUserId,
        VendorSeedDefinition seed)
    {
        switch (seed.StatusKind)
        {
            case SeedVendorStatus.Pending:
                return;
            case SeedVendorStatus.Active:
                vendor.Approve(seed.CommissionRate ?? 12m, adminUserId);
                return;
            case SeedVendorStatus.Suspended:
                vendor.Approve(seed.CommissionRate ?? 12m, adminUserId);
                vendor.Suspend("Seeded suspension for manual testing.");
                user.Suspend();
                return;
            case SeedVendorStatus.Rejected:
                vendor.Reject("Seeded rejection for manual testing.");
                return;
            case SeedVendorStatus.Locked:
                vendor.Approve(seed.CommissionRate ?? 12m, adminUserId);
                vendor.Lock("Seeded lock for manual testing.");
                user.LockLogin("Seeded lock for manual testing.");
                return;
            case SeedVendorStatus.Archived:
                vendor.Approve(seed.CommissionRate ?? 12m, adminUserId);
                vendor.Archive("Seeded archive for manual testing.");
                user.Archive("Seeded archive for manual testing.");
                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static List<SeedOperatingHour> BuildStandardHours(string openTime, string closeTime) =>
        Enumerable.Range(0, 7)
            .Select(day => new SeedOperatingHour(day, openTime, closeTime, day != 5))
            .ToList();
}

internal enum SeedVendorStatus
{
    Pending,
    Active,
    Suspended,
    Rejected,
    Locked,
    Archived
}

internal sealed record VendorSeedDefinition(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string BusinessNameAr,
    string BusinessNameEn,
    string BusinessType,
    string CommercialRegistrationNumber,
    DateTime? CommercialRegistrationExpiryDate,
    string ContactEmail,
    string ContactPhone,
    string? DescriptionAr,
    string? DescriptionEn,
    string OwnerName,
    string OwnerEmail,
    string OwnerPhone,
    string? IdNumber,
    string? Nationality,
    string Region,
    string City,
    string NationalAddress,
    string? TaxId,
    string? LicenseNumber,
    string BankName,
    string AccountHolderName,
    string Iban,
    string? SwiftCode,
    string? PayoutCycle,
    string BranchName,
    string BranchAddressLine,
    decimal BranchLatitude,
    decimal BranchLongitude,
    string BranchContactPhone,
    decimal BranchDeliveryRadiusKm,
    decimal? CommissionRate,
    SeedVendorStatus StatusKind,
    bool AcceptOrders,
    decimal? MinimumOrderAmount,
    int? PreparationTimeMinutes,
    bool EmailNotificationsEnabled,
    bool SmsNotificationsEnabled,
    bool NewOrdersNotificationsEnabled,
    IReadOnlyCollection<SeedOperatingHour> Hours);

internal sealed record SeedOperatingHour(
    int DayOfWeek,
    string OpenTime,
    string CloseTime,
    bool IsOpen);
