using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Wallets.Entities;
using Zadana.Domain.Modules.Wallets.Enums;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private const string DefaultAdminPassword = "Admin@123";
    private const string DefaultUserPassword = "Zadana@12345";

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
        await TrySeedAsync();
    }

    public async Task<DevelopmentSeedSummary> ResetAndSeedAsync()
    {
        await ResetDevelopmentDataAsync();
        await TrySeedAsync();
        return await BuildSummaryAsync();
    }

    private async Task TrySeedAsync()
    {
        await SeedRolesAsync();
        await SeedSuperAdminAsync();
        await SeedSupportUsersAsync();
        await SeedUnitsAsync();
        await SeedCategoriesAsync();
        await SeedBrandsAsync();
        await SeedProductTypesAndPartsAsync();
        await SeedMasterProductsAsync();
        await SeedSampleVendorsAsync();
        await SeedVendorProductsAsync();
        await SeedHomeBannersAsync();
        await SeedHomeSectionsAsync();
        await SeedFeaturedPlacementsAsync();
        await SeedCouponsAsync();
        await SeedCustomersAsync();
        await SeedDriversAsync();
        await SeedCustomerExperienceAsync();
        await SeedDriverAssignmentsAsync();
        await SeedWalletsAndSettlementsAsync();
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

        await _userManager.CreateAsync(admin, DefaultAdminPassword);
        await _userManager.AddToRoleAsync(admin, UserRole.SuperAdmin.ToString());
    }

    private async Task SeedSupportUsersAsync()
    {
        await EnsureUserAsync(
            "ops.admin@zadana.local",
            "Operations Admin",
            "01000000001",
            UserRole.Admin,
            DefaultAdminPassword,
            user =>
            {
                user.VerifyEmail();
                user.VerifyPhone();
                user.RecordLogin();
                user.MarkPresenceOffline(DateTime.UtcNow.AddHours(-2));
            });
    }

    private async Task SeedUnitsAsync()
    {
        var unitSeeds = new (string NameAr, string NameEn, string Symbol)[]
        {
            ("كيلوغرام", "Kilogram", "kg"),
            ("غرام", "Gram", "g"),
            ("ملليغرام", "Milligram", "mg"),
            ("طن", "Ton", "t"),
            ("لتر", "Liter", "L"),
            ("ملليلتر", "Milliliter", "mL"),
            ("سنتيلتر", "Centiliter", "cL"),
            ("قطعة", "Piece", "pcs"),
            ("حبة", "Unit", "unit"),
            ("رول", "Roll", "roll"),
            ("عبوة", "Pack", "pk"),
            ("كرتون", "Carton", "ctn"),
            ("صندوق", "Box", "box"),
            ("زجاجة", "Bottle", "btl"),
            ("علبة", "Can", "can"),
            ("برطمان", "Jar", "jar"),
            ("كيس", "Bag", "bag"),
            ("صينية", "Tray", "tray"),
            ("رابطة", "Bundle", "bdl"),
            ("ربطة", "Bunch", "bnch"),
            ("شريحة", "Slice", "slc"),
            ("رغيف", "Loaf", "loaf"),
            ("ظرف", "Sachet", "scht"),
            ("عود", "Stick", "stk"),
            ("شريط", "Strip", "strip"),
            ("لوح", "Bar", "bar"),
            ("طقم", "Set", "set"),
            ("زوج", "Pair", "pair"),
            ("دزينة", "Dozen", "dz"),
            ("كبسولة", "Capsule", "cap"),
            ("قرص", "Tablet", "tab"),
            ("أنبوب", "Tube", "tube")
        };

        var existingUnits = await _context.UnitsOfMeasure.ToListAsync();
        var existingBySymbol = existingUnits
            .Where(x => !string.IsNullOrWhiteSpace(x.Symbol))
            .ToDictionary(x => x.Symbol!, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in unitSeeds)
        {
            if (existingBySymbol.TryGetValue(seed.Symbol, out var existing))
            {
                existing.Update(seed.NameAr, seed.NameEn, seed.Symbol);
                existing.Activate();
                continue;
            }

            await _context.UnitsOfMeasure.AddAsync(new UnitOfMeasure(seed.NameAr, seed.NameEn, seed.Symbol));
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedCategoriesAsync()
    {
        if (await _context.Categories.AnyAsync())
        {
            return;
        }

        var food = new Category("البقالة", "Groceries", ImageCatalog.CategoryGroceries, null, 1);
        var electronics = new Category("الإلكترونيات", "Electronics", ImageCatalog.CategoryElectronics, null, 2);
        var home = new Category("المنزل", "Home Essentials", ImageCatalog.CategoryHome, null, 3);

        await _context.Categories.AddRangeAsync(food, electronics, home);
        await _context.SaveChangesAsync();

        var categories = new[]
        {
            new Category("الأغذية والمشروبات", "Food & Drinks", ImageCatalog.CategoryGroceries, food.Id, 1),
            new Category("الخضار والفاكهة", "Fresh Market", ImageCatalog.CategoryProduce, food.Id, 2),
            new Category("الأجهزة الذكية", "Smart Devices", ImageCatalog.CategoryElectronics, electronics.Id, 1),
            new Category("ملحقات الأجهزة", "Device Accessories", ImageCatalog.CategoryAccessories, electronics.Id, 2),
            new Category("العناية المنزلية", "Home Care", ImageCatalog.CategoryHomeCare, home.Id, 1),
            new Category("المطبخ", "Kitchen", ImageCatalog.CategoryKitchen, home.Id, 2)
        };

        await _context.Categories.AddRangeAsync(categories);
        await _context.SaveChangesAsync();

        var foodAndDrinks = categories.Single(x => x.NameEn == "Food & Drinks");
        var freshMarket = categories.Single(x => x.NameEn == "Fresh Market");
        var smartDevices = categories.Single(x => x.NameEn == "Smart Devices");
        var deviceAccessories = categories.Single(x => x.NameEn == "Device Accessories");
        var homeCare = categories.Single(x => x.NameEn == "Home Care");
        var kitchen = categories.Single(x => x.NameEn == "Kitchen");

        var subCategories = new[]
        {
            new Category("الألبان", "Dairy", ImageCatalog.CategoryDairy, foodAndDrinks.Id, 1),
            new Category("المشروبات", "Beverages", ImageCatalog.CategoryBeverages, foodAndDrinks.Id, 2),
            new Category("المخبوزات", "Bakery", ImageCatalog.CategoryBakery, foodAndDrinks.Id, 3),
            new Category("الوجبات الخفيفة", "Snacks", ImageCatalog.CategorySnacks, foodAndDrinks.Id, 4),
            new Category("الفاكهة", "Fruits", ImageCatalog.CategoryProduce, freshMarket.Id, 1),
            new Category("الخضار", "Vegetables", ImageCatalog.CategoryProduce, freshMarket.Id, 2),
            new Category("الهواتف", "Phones", ImageCatalog.CategoryPhones, smartDevices.Id, 1),
            new Category("الإكسسوارات", "Accessories", ImageCatalog.CategoryAccessories, deviceAccessories.Id, 1),
            new Category("منظفات المنزل", "Household Care", ImageCatalog.CategoryHomeCare, homeCare.Id, 1),
            new Category("مستلزمات المطبخ", "Kitchen Supplies", ImageCatalog.CategoryKitchen, kitchen.Id, 1)
        };

        await _context.Categories.AddRangeAsync(subCategories);
        await _context.SaveChangesAsync();

        var dairy = subCategories.Single(x => x.NameEn == "Dairy");
        var beverages = subCategories.Single(x => x.NameEn == "Beverages");
        var bakery = subCategories.Single(x => x.NameEn == "Bakery");
        var snacks = subCategories.Single(x => x.NameEn == "Snacks");
        var fruits = subCategories.Single(x => x.NameEn == "Fruits");
        var vegetables = subCategories.Single(x => x.NameEn == "Vegetables");
        var phones = subCategories.Single(x => x.NameEn == "Phones");
        var accessories = subCategories.Single(x => x.NameEn == "Accessories");
        var householdCare = subCategories.Single(x => x.NameEn == "Household Care");

        var leafCategories = new[]
        {
            new Category("الحليب", "Milk", ImageCatalog.CategoryDairy, dairy.Id, 1),
            new Category("الزبادي", "Yogurt", ImageCatalog.CategoryDairy, dairy.Id, 2),
            new Category("العصائر", "Juices", ImageCatalog.CategoryBeverages, beverages.Id, 1),
            new Category("المياه", "Water", ImageCatalog.CategoryBeverages, beverages.Id, 2),
            new Category("خبز التوست", "Toast Bread", ImageCatalog.CategoryBakery, bakery.Id, 1),
            new Category("الشيبس", "Chips", ImageCatalog.CategorySnacks, snacks.Id, 1),
            new Category("الموز", "Bananas", ImageCatalog.CategoryProduce, fruits.Id, 1),
            new Category("الطماطم", "Tomatoes", ImageCatalog.CategoryProduce, vegetables.Id, 1),
            new Category("هواتف سامسونج", "Samsung Phones", ImageCatalog.CategoryPhones, phones.Id, 1),
            new Category("هواتف آيفون", "iPhone Phones", ImageCatalog.CategoryPhones, phones.Id, 2),
            new Category("الشواحن", "Chargers", ImageCatalog.CategoryAccessories, accessories.Id, 1),
            new Category("أغطية الجوال", "Phone Cases", ImageCatalog.CategoryAccessories, accessories.Id, 2),
            new Category("منظفات الأطباق", "Dishwashing", ImageCatalog.CategoryHomeCare, householdCare.Id, 1),
            new Category("المناديل", "Tissues", ImageCatalog.CategoryHomeCare, householdCare.Id, 2)
        };

        await _context.Categories.AddRangeAsync(leafCategories);
        await _context.SaveChangesAsync();
    }

    private async Task SeedBrandsAsync()
    {
        if (await _context.Brands.AnyAsync())
        {
            return;
        }

        // Only fetch subcategories (categories that have a parent)
        var dairy = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Dairy" && item.ParentCategoryId != null);
        var beverages = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Beverages" && item.ParentCategoryId != null);
        var snacks = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Snacks" && item.ParentCategoryId != null);
        var phones = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Phones" && item.ParentCategoryId != null);
        var accessories = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Accessories" && item.ParentCategoryId != null);
        var householdCare = await _context.Categories.FirstOrDefaultAsync(item => item.NameEn == "Household Care" && item.ParentCategoryId != null);

        var brands = new List<Brand>
        {
            new("المراعي", "Almarai", "https://cdn.simpleicons.org/tesco/00539f", dairy?.Id),
            new("نادك", "Nadec", "https://cdn.simpleicons.org/carrefour/004f9f", dairy?.Id),
            new("نادا", "Nada", "https://cdn.simpleicons.org/walmart/0071ce", beverages?.Id),
            new("بيبسي", "Pepsi", "https://cdn.simpleicons.org/pepsi/2151a1", beverages?.Id),
            new("ليز", "Lay's", "https://cdn.simpleicons.org/fritolay/ffcc00", snacks?.Id),
            new("سامسونج", "Samsung", "https://cdn.simpleicons.org/samsung/1428a0", phones?.Id),
            new("آبل", "Apple", "https://cdn.simpleicons.org/apple/000000", phones?.Id),
            new("أنكر", "Anker", "https://cdn.simpleicons.org/anker/00a7e1", accessories?.Id),
            new("برايل", "Pril", "https://cdn.simpleicons.org/homeassistant/41bdf5", householdCare?.Id),
            new("فاين", "Fine", "https://cdn.simpleicons.org/cloudflare/ff6633", householdCare?.Id)
        };

        await _context.Brands.AddRangeAsync(brands);
        await _context.SaveChangesAsync();
    }

    private async Task SeedMasterProductsAsync()
    {
        var categories = await _context.Categories.ToDictionaryAsync(x => x.NameEn);
        var brands = await _context.Brands.ToDictionaryAsync(x => x.NameEn);
        var units = await _context.UnitsOfMeasure.ToDictionaryAsync(x => x.Symbol!);
        var productTypes = await _context.ProductTypes.ToDictionaryAsync(x => x.NameEn);
        var parts = await _context.Parts.ToDictionaryAsync(x => x.NameEn);
        var existingSlugs = await _context.MasterProducts
            .Select(x => x.Slug)
            .ToListAsync();
        var existingSlugSet = existingSlugs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var products = new List<MasterProduct>
        {
            CreateProduct("حليب كامل الدسم 1 لتر", "Full Cream Milk 1L", "full-cream-milk-1l", "حليب طازج يومي غني بالكالسيوم.", "Fresh full cream milk for daily essentials.", categories["Milk"].Id, brands["Almarai"].Id, units["L"].Id, productTypes.GetValueOrDefault("Milk")?.Id, parts.GetValueOrDefault("Full Cream")?.Id, ImageCatalog.Milk1, ImageCatalog.Milk2),
            CreateProduct("زبادي يوناني", "Greek Yogurt", "greek-yogurt", "زبادي كثيف مناسب للفطور والوجبات الخفيفة.", "Rich Greek yogurt for breakfast and snacks.", categories["Yogurt"].Id, brands["Almarai"].Id, units["pk"].Id, productTypes.GetValueOrDefault("Yogurt")?.Id, parts.GetValueOrDefault("Greek")?.Id, ImageCatalog.Yogurt1, ImageCatalog.Yogurt2),
            CreateProduct("عصير برتقال طازج 1 لتر", "Orange Juice 1L", "orange-juice-1l", "عصير منعش بطعم طبيعي.", "Refreshing orange juice with a natural taste.", categories["Juices"].Id, brands["Nada"].Id, units["L"].Id, null, null, ImageCatalog.Juice1, ImageCatalog.Juice2),
            CreateProduct("مياه شرب عبوة 6", "Water Pack 6x330ml", "water-pack-6", "عبوة مياه للشرب اليومي.", "Convenient water pack for daily hydration.", categories["Water"].Id, brands["Pepsi"].Id, units["ctn"].Id, null, null, ImageCatalog.Water1, ImageCatalog.Water2),
            CreateProduct("خبز توست أبيض", "White Toast Bread", "white-toast-bread", "خبز طازج للسندويتشات اليومية.", "Fresh toast bread for everyday sandwiches.", categories["Toast Bread"].Id, null, units["pk"].Id, null, null, ImageCatalog.Bread1, ImageCatalog.Bread2),
            CreateProduct("بطاطس شيبس كلاسيك", "Classic Potato Chips", "classic-potato-chips", "وجبة خفيفة مقرمشة.", "Crunchy classic potato chips.", categories["Chips"].Id, brands["Lay's"].Id, units["pk"].Id, null, null, ImageCatalog.Chips1, ImageCatalog.Chips2),
            CreateProduct("موز طازج", "Fresh Bananas", "fresh-bananas", "موز طازج صالح للوجبات الخفيفة والعصائر.", "Fresh bananas for snacks and smoothies.", categories["Bananas"].Id, null, units["kg"].Id, null, null, ImageCatalog.Banana1, ImageCatalog.Banana2),
            CreateProduct("طماطم حمراء", "Red Tomatoes", "red-tomatoes", "طماطم يومية للطبخ والسلطات.", "Everyday tomatoes for cooking and salads.", categories["Tomatoes"].Id, null, units["kg"].Id, null, null, ImageCatalog.Tomato1, ImageCatalog.Tomato2),
            CreateProduct("سائل تنظيف أطباق", "Dishwashing Liquid", "dishwashing-liquid", "منظف أطباق بفعالية عالية.", "High-performance dishwashing liquid.", categories["Dishwashing"].Id, brands["Pril"].Id, units["L"].Id, null, null, ImageCatalog.DishSoap1, ImageCatalog.DishSoap2),
            CreateProduct("مناديل مطبخ رولين", "Kitchen Towels 2 Rolls", "kitchen-towels-2-rolls", "مناديل مطبخ بامتصاص ممتاز.", "Kitchen towels with strong absorption.", categories["Tissues"].Id, brands["Fine"].Id, units["roll"].Id, null, null, ImageCatalog.Towel1, ImageCatalog.Towel2),
            CreateProduct("شاحن سريع USB-C", "USB-C Fast Charger", "usb-c-fast-charger", "شاحن سريع متوافق مع أغلب الهواتف الحديثة.", "Fast charger compatible with most modern phones.", categories["Chargers"].Id, brands["Anker"].Id, units["pcs"].Id, null, null, ImageCatalog.Charger1, ImageCatalog.Charger2),
            CreateProduct("غطاء آيفون شفاف", "Transparent iPhone Case", "transparent-iphone-case", "غطاء شفاف خفيف يحمي الهاتف من الخدوش.", "Slim transparent case for scratch protection.", categories["Phone Cases"].Id, brands["Apple"].Id, units["pcs"].Id, null, null, ImageCatalog.Case1, ImageCatalog.Case2),
            CreateProduct("هاتف سامسونج جالاكسي A55", "Samsung Galaxy A55", "samsung-galaxy-a55", "هاتف ذكي للأداء اليومي.", "Smartphone with balanced daily performance.", categories["Samsung Phones"].Id, brands["Samsung"].Id, units["pcs"].Id, null, null, ImageCatalog.Phone1, ImageCatalog.Phone2),
            CreateProduct("آيفون 15", "iPhone 15", "iphone-15", "هاتف آيفون حديث بتجربة سلسة.", "Modern iPhone with a smooth experience.", categories["iPhone Phones"].Id, brands["Apple"].Id, units["pcs"].Id, null, null, ImageCatalog.Iphone1, ImageCatalog.Iphone2)
        };

        var missingProducts = products
            .Where(product => !existingSlugSet.Contains(product.Slug))
            .ToList();

        if (missingProducts.Count == 0)
        {
            return;
        }

        await _context.MasterProducts.AddRangeAsync(missingProducts);
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

    private static MasterProduct CreateProduct(
        string nameAr,
        string nameEn,
        string slug,
        string descriptionAr,
        string descriptionEn,
        Guid categoryId,
        Guid? brandId,
        Guid? unitId,
        Guid? productTypeId,
        Guid? partId,
        string primaryImage,
        string secondaryImage)
    {
        var product = new MasterProduct(
            nameAr,
            nameEn,
            slug,
            categoryId,
            brandId,
            unitId,
            descriptionAr,
            descriptionEn,
            null,
            productTypeId,
            partId);

        product.Publish();
        product.AddImage(primaryImage, nameEn, 0, true);
        product.AddImage(secondaryImage, nameEn, 1, false);
        return product;
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
                imageUrl: ImageCatalog.BannerDeals,
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
                imageUrl: ImageCatalog.BannerStores,
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
                imageUrl: ImageCatalog.BannerBestSelling,
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

    private async Task SeedVendorProductsAsync()
    {
        var vendors = await _context.Vendors
            .Include(x => x.Branches)
            .ToDictionaryAsync(x => x.BusinessNameEn);
        var products = await _context.MasterProducts.ToDictionaryAsync(x => x.Slug);
        var existingPairs = await _context.VendorProducts
            .Select(x => new { x.VendorId, x.MasterProductId })
            .ToListAsync();
        var existingLookup = existingPairs
            .Select(x => (x.VendorId, x.MasterProductId))
            .ToHashSet();

        var offers = new[]
        {
            new VendorProductSeed("Green Valley Market", "full-cream-milk-1l", 16.95m, 120, 19.95m),
            new VendorProductSeed("Green Valley Market", "greek-yogurt", 8.50m, 90, 10.00m),
            new VendorProductSeed("Green Valley Market", "orange-juice-1l", 11.75m, 70, 13.50m),
            new VendorProductSeed("Green Valley Market", "water-pack-6", 9.95m, 150, null),
            new VendorProductSeed("Green Valley Market", "white-toast-bread", 6.25m, 55, null),
            new VendorProductSeed("Green Valley Market", "fresh-bananas", 12.50m, 80, null),
            new VendorProductSeed("Green Valley Market", "red-tomatoes", 8.95m, 65, null),
            new VendorProductSeed("Green Valley Market", "dishwashing-liquid", 14.95m, 45, 17.95m),
            new VendorProductSeed("Green Valley Market", "kitchen-towels-2-rolls", 13.50m, 40, null),

            new VendorProductSeed("Fresh Basket", "full-cream-milk-1l", 17.50m, 35, 19.95m),
            new VendorProductSeed("Fresh Basket", "orange-juice-1l", 12.10m, 30, null),
            new VendorProductSeed("Fresh Basket", "classic-potato-chips", 7.25m, 25, null),
            new VendorProductSeed("Fresh Basket", "fresh-bananas", 13.20m, 22, null),

            new VendorProductSeed("Modern Tech Mart", "usb-c-fast-charger", 79.00m, 28, 99.00m),
            new VendorProductSeed("Modern Tech Mart", "transparent-iphone-case", 49.00m, 40, 59.00m),
            new VendorProductSeed("Modern Tech Mart", "samsung-galaxy-a55", 1499.00m, 12, 1599.00m),
            new VendorProductSeed("Modern Tech Mart", "iphone-15", 3199.00m, 8, 3399.00m),

            new VendorProductSeed("Riyadh Kitchens", "water-pack-6", 8.95m, 0, null),
            new VendorProductSeed("Riyadh Kitchens", "dishwashing-liquid", 16.95m, 10, null)
        };

        foreach (var offer in offers)
        {
            if (!vendors.TryGetValue(offer.VendorName, out var vendor))
            {
                continue;
            }

            if (!products.TryGetValue(offer.ProductSlug, out var product))
            {
                continue;
            }

            var branch = vendor.Branches.FirstOrDefault();
            if (branch == null)
            {
                continue;
            }

            if (existingLookup.Contains((vendor.Id, product.Id)))
            {
                continue;
            }

            var entity = new VendorProduct(vendor.Id, product.Id, offer.SellingPrice, offer.Quantity, offer.CompareAtPrice, branch.Id);
            if (offer.Quantity == 0)
            {
                entity.UpdateStock(0);
            }

            await _context.VendorProducts.AddAsync(entity);
            existingLookup.Add((vendor.Id, product.Id));
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedHomeSectionsAsync()
    {
        if (await _context.HomeContentSectionSettings.AnyAsync())
        {
            return;
        }

        var settings = Enum.GetValues<HomeContentSectionType>()
            .Select(section => new HomeContentSectionSetting(section, true))
            .ToArray();
        await _context.HomeContentSectionSettings.AddRangeAsync(settings);

        var dairy = await _context.Categories.FirstAsync(x => x.NameEn == "Dairy");
        var beverages = await _context.Categories.FirstAsync(x => x.NameEn == "Beverages");
        var accessories = await _context.Categories.FirstAsync(x => x.NameEn == "Accessories");

        await _context.HomeSections.AddRangeAsync(
            new HomeSection(dairy.Id, "soft-blue", 1, 8, DateTime.UtcNow.AddDays(-15), DateTime.UtcNow.AddMonths(2)),
            new HomeSection(beverages.Id, "fresh-orange", 2, 8, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddMonths(2)),
            new HomeSection(accessories.Id, "bold-dark", 3, 6, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddMonths(2)));

        await _context.SaveChangesAsync();
    }

    private async Task SeedFeaturedPlacementsAsync()
    {
        var vendorProducts = await _context.VendorProducts
            .Include(x => x.MasterProduct)
            .ToListAsync();
        var existingOrders = await _context.FeaturedProductPlacements
            .Select(x => x.DisplayOrder)
            .ToListAsync();
        var now = DateTime.UtcNow;
        var placements = new List<FeaturedProductPlacement>();

        var milkOffer = vendorProducts.FirstOrDefault(x => x.MasterProduct.Slug == "full-cream-milk-1l");
        if (milkOffer != null && !existingOrders.Contains(1))
        {
            placements.Add(new FeaturedProductPlacement(
                FeaturedPlacementType.VendorProduct,
                1,
                milkOffer.Id,
                null,
                now.AddDays(-7),
                now.AddMonths(1),
                "Daily essentials spotlight"));
        }

        var juice = await _context.MasterProducts.FirstOrDefaultAsync(x => x.Slug == "orange-juice-1l");
        if (juice != null && !existingOrders.Contains(2))
        {
            placements.Add(new FeaturedProductPlacement(
                FeaturedPlacementType.MasterProduct,
                2,
                null,
                juice.Id,
                now.AddDays(-7),
                now.AddMonths(1),
                "Fresh beverages"));
        }

        var phoneOffer = vendorProducts.FirstOrDefault(x => x.MasterProduct.Slug == "samsung-galaxy-a55");
        if (phoneOffer != null && !existingOrders.Contains(3))
        {
            placements.Add(new FeaturedProductPlacement(
                FeaturedPlacementType.VendorProduct,
                3,
                phoneOffer.Id,
                null,
                now.AddDays(-7),
                now.AddMonths(1),
                "Trending tech"));
        }

        if (placements.Count == 0)
        {
            return;
        }

        await _context.FeaturedProductPlacements.AddRangeAsync(placements);

        await _context.SaveChangesAsync();
    }

    private async Task SeedCouponsAsync()
    {
        if (await _context.Coupons.AnyAsync())
        {
            return;
        }

        var greenValley = await _context.Vendors.FirstAsync(x => x.BusinessNameEn == "Green Valley Market");
        var modernTech = await _context.Vendors.FirstAsync(x => x.BusinessNameEn == "Modern Tech Mart");
        var now = DateTime.UtcNow;

        var groceryCoupon = new Coupon("FRESH10", "Fresh Basket Savings", CouponDiscountType.Percentage, 10m, 60m, 25m, now.AddDays(-5), now.AddMonths(1), 500, 2);
        var techCoupon = new Coupon("TECH50", "Tech Accessories Deal", CouponDiscountType.Fixed, 50m, 300m, null, now.AddDays(-2), now.AddMonths(1), 100, 1);

        groceryCoupon.ApplicableVendors.Add(new CouponVendor(groceryCoupon.Id, greenValley.Id));
        techCoupon.ApplicableVendors.Add(new CouponVendor(techCoupon.Id, modernTech.Id));

        await _context.Coupons.AddRangeAsync(groceryCoupon, techCoupon);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCustomersAsync()
    {
        await EnsureUserAsync("ahmed.customer@zadana.local", "Ahmed Mostafa", "01000000010", UserRole.Customer, DefaultUserPassword, user =>
        {
            user.VerifyEmail();
            user.VerifyPhone();
            user.RecordLogin();
            user.MarkPresenceOnline(DateTime.UtcNow.AddMinutes(-2));
        });

        await EnsureUserAsync("layla.customer@zadana.local", "Layla Adel", "01000000011", UserRole.Customer, DefaultUserPassword, user =>
        {
            user.VerifyEmail();
            user.VerifyPhone();
            user.RecordLogin();
            user.MarkPresenceOffline(DateTime.UtcNow.AddHours(-1));
        });

        await EnsureUserAsync("noor.customer@zadana.local", "Noor Hossam", "01000000012", UserRole.Customer, DefaultUserPassword, user =>
        {
            user.VerifyEmail();
            user.VerifyPhone();
            user.RecordLogin();
            user.MarkPresenceOffline(DateTime.UtcNow.AddDays(-1));
        });
    }

    private async Task SeedDriversAsync()
    {
        if (await _context.Drivers.AnyAsync())
        {
            return;
        }

        var activeDriverUser = await EnsureUserAsync("driver.active@zadana.local", "Mahmoud Driver", "01000000020", UserRole.Driver, DefaultUserPassword, user =>
        {
            user.VerifyEmail();
            user.VerifyPhone();
            user.RecordLogin();
            user.MarkPresenceOnline(DateTime.UtcNow.AddMinutes(-12));
        });

        var pendingDriverUser = await EnsureUserAsync("driver.pending@zadana.local", "Yara Driver", "01000000021", UserRole.Driver, DefaultUserPassword, user =>
        {
            user.VerifyEmail();
            user.VerifyPhone();
            user.RecordLogin();
            user.MarkPresenceOffline(DateTime.UtcNow.AddHours(-5));
        });

        var activeDriver = new Driver(activeDriverUser.Id, "Motorbike", "29801011234567", "DRV-1001", "Riyadh", ImageCatalog.DriverNationalId, ImageCatalog.DriverLicense, ImageCatalog.DriverVehicle, ImageCatalog.DriverProfile);
        activeDriver.Approve();
        activeDriver.ToggleAvailability(true);

        var pendingDriver = new Driver(pendingDriverUser.Id, "Car", "29801011234568", "DRV-1002", "Jeddah", ImageCatalog.DriverNationalId, ImageCatalog.DriverLicense, ImageCatalog.DriverVehicle, ImageCatalog.DriverProfile);

        await _context.Drivers.AddRangeAsync(activeDriver, pendingDriver);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCustomerExperienceAsync()
    {
        if (await _context.Orders.AnyAsync())
        {
            return;
        }

        var ahmed = await _userManager.FindByEmailAsync("ahmed.customer@zadana.local");
        var layla = await _userManager.FindByEmailAsync("layla.customer@zadana.local");
        var noor = await _userManager.FindByEmailAsync("noor.customer@zadana.local");
        if (ahmed is null || layla is null || noor is null)
        {
            return;
        }

        var ahmedHome = new CustomerAddress(ahmed.Id, "Ahmed Mostafa", ahmed.PhoneNumber!, "King Fahd Road, Building 18", AddressLabel.Home, "18", "3", "12", "Riyadh", "Al Olaya", 24.7136m, 46.6753m);
        ahmedHome.SetAsDefault();
        var laylaWork = new CustomerAddress(layla.Id, "Layla Adel", layla.PhoneNumber!, "Prince Sultan Street, Tower 5", AddressLabel.Work, "5", "6", "22", "Jeddah", "Al Zahraa", 21.5433m, 39.1728m);
        laylaWork.SetAsDefault();
        var noorHome = new CustomerAddress(noor.Id, "Noor Hossam", noor.PhoneNumber!, "Corniche Road, Villa 7", AddressLabel.Home, "7", null, null, "Khobar", "Corniche", 26.2794m, 50.2083m);
        noorHome.SetAsDefault();

        await _context.CustomerAddresses.AddRangeAsync(ahmedHome, laylaWork, noorHome);
        await _context.SaveChangesAsync();

        await SeedFavoritesAsync(ahmed.Id, layla.Id, noor.Id);
        await SeedCartsAsync(ahmed.Id, layla.Id);
        await SeedOrdersAsync(ahmed, ahmedHome, layla, laylaWork, noor, noorHome);
        await SeedReviewsAsync(ahmed.Id, layla.Id);
        await SeedNotificationsAsync(ahmed.Id, layla.Id, noor.Id);
    }

    private async Task SeedFavoritesAsync(Guid ahmedId, Guid laylaId, Guid noorId)
    {
        var products = await _context.MasterProducts.ToDictionaryAsync(x => x.Slug);
        var favorites = new List<CustomerFavorite>();

        if (products.TryGetValue("full-cream-milk-1l", out var milk))
        {
            favorites.Add(new CustomerFavorite(ahmedId, null, milk.Id));
        }

        if (products.TryGetValue("usb-c-fast-charger", out var charger))
        {
            favorites.Add(new CustomerFavorite(ahmedId, null, charger.Id));
        }

        if (products.TryGetValue("orange-juice-1l", out var juice))
        {
            favorites.Add(new CustomerFavorite(laylaId, null, juice.Id));
        }

        if (products.TryGetValue("iphone-15", out var iphone))
        {
            favorites.Add(new CustomerFavorite(noorId, null, iphone.Id));
        }

        if (favorites.Count == 0)
        {
            return;
        }

        await _context.CustomerFavorites.AddRangeAsync(favorites);
        await _context.SaveChangesAsync();
    }

    private async Task SeedCartsAsync(Guid ahmedId, Guid laylaId)
    {
        var products = await _context.MasterProducts.ToDictionaryAsync(x => x.Slug);

        var ahmedCart = new Cart(ahmedId);
        if (products.TryGetValue("full-cream-milk-1l", out var milk))
        {
            ahmedCart.Items.Add(new CartItem(ahmedCart.Id, milk.Id, milk.NameEn, 2));
        }

        if (products.TryGetValue("dishwashing-liquid", out var soap))
        {
            ahmedCart.Items.Add(new CartItem(ahmedCart.Id, soap.Id, soap.NameEn, 1));
        }

        var laylaCart = new Cart(laylaId);
        if (products.TryGetValue("usb-c-fast-charger", out var charger))
        {
            laylaCart.Items.Add(new CartItem(laylaCart.Id, charger.Id, charger.NameEn, 1));
        }

        var carts = new List<Cart>();
        if (ahmedCart.Items.Count > 0)
        {
            carts.Add(ahmedCart);
        }

        if (laylaCart.Items.Count > 0)
        {
            carts.Add(laylaCart);
        }

        if (carts.Count == 0)
        {
            return;
        }

        await _context.Carts.AddRangeAsync(carts);
        await _context.SaveChangesAsync();
    }

    private async Task SeedOrdersAsync(User ahmed, CustomerAddress ahmedHome, User layla, CustomerAddress laylaWork, User noor, CustomerAddress noorHome)
    {
        var vendors = await _context.Vendors.Include(x => x.Branches).ToDictionaryAsync(x => x.BusinessNameEn);
        var vendorProducts = await _context.VendorProducts.Include(x => x.MasterProduct).ToListAsync();
        var coupons = await _context.Coupons.ToDictionaryAsync(x => x.Code);

        if (!vendors.TryGetValue("Green Valley Market", out var groceryVendor) ||
            !vendors.TryGetValue("Modern Tech Mart", out var techVendor))
        {
            return;
        }

        var groceryBranch = groceryVendor.Branches.Single();
        var techBranch = techVendor.Branches.Single();

        var milk = vendorProducts.FirstOrDefault(x => x.MasterProduct.Slug == "full-cream-milk-1l" && x.VendorId == groceryVendor.Id);
        var soap = vendorProducts.FirstOrDefault(x => x.MasterProduct.Slug == "dishwashing-liquid" && x.VendorId == groceryVendor.Id);
        var charger = vendorProducts.FirstOrDefault(x => x.MasterProduct.Slug == "usb-c-fast-charger" && x.VendorId == techVendor.Id);
        var phone = vendorProducts.FirstOrDefault(x => x.MasterProduct.Slug == "samsung-galaxy-a55" && x.VendorId == techVendor.Id);

        if (milk is null || soap is null || charger is null || phone is null)
        {
            return;
        }

        coupons.TryGetValue("FRESH10", out var freshCoupon);
        coupons.TryGetValue("TECH50", out var techCoupon);

        var deliveredOrder = new Order("ORD-DEV-1001", ahmed.Id, groceryVendor.Id, ahmedHome.Id, PaymentMethodType.Card, 48.85m, 4.00m, 12m, 5.50m, "Leave at the door", groceryBranch.Id, freshCoupon?.Id);
        deliveredOrder.Items.Add(new OrderItem(deliveredOrder.Id, milk.Id, milk.MasterProductId, milk.MasterProduct.NameEn, 2, 16.95m, 2.00m, "Liter"));
        deliveredOrder.Items.Add(new OrderItem(deliveredOrder.Id, soap.Id, soap.MasterProductId, soap.MasterProduct.NameEn, 1, 14.95m, 2.00m, "Liter"));
        var deliveredPayment = new Payment(deliveredOrder.Id, PaymentMethodType.Card, deliveredOrder.TotalAmount);
        deliveredPayment.MarkAsPending("MockGateway", "PAY-DEV-1001");
        deliveredPayment.MarkAsPaid();
        deliveredOrder.ChangeStatus(OrderStatus.Accepted, null, "Seed vendor accepted");
        deliveredOrder.ChangeStatus(OrderStatus.Preparing, null, "Seed preparing");
        deliveredOrder.ChangeStatus(OrderStatus.ReadyForPickup, null, "Seed ready");
        deliveredOrder.ChangeStatus(OrderStatus.DriverAssignmentInProgress, null, "Looking for driver");
        deliveredOrder.ChangeStatus(OrderStatus.DriverAssigned, null, "Driver assigned");
        deliveredOrder.ChangeStatus(OrderStatus.PickedUp, null, "Picked up");
        deliveredOrder.ChangeStatus(OrderStatus.OnTheWay, null, "On the way");
        deliveredOrder.ChangeStatus(OrderStatus.Delivered, null, "Delivered successfully");

        var refundedOrder = new Order("ORD-DEV-1002", layla.Id, techVendor.Id, laylaWork.Id, PaymentMethodType.ApplePay, 1578.00m, 79.00m, 0m, 90m, "Office reception", techBranch.Id, techCoupon?.Id);
        refundedOrder.Items.Add(new OrderItem(refundedOrder.Id, charger.Id, charger.MasterProductId, charger.MasterProduct.NameEn, 1, 79m, 0m, "Piece"));
        refundedOrder.Items.Add(new OrderItem(refundedOrder.Id, phone.Id, phone.MasterProductId, phone.MasterProduct.NameEn, 1, 1499m, 79m, "Piece"));
        var refundedPayment = new Payment(refundedOrder.Id, PaymentMethodType.ApplePay, refundedOrder.TotalAmount);
        refundedPayment.MarkAsPending("MockGateway", "PAY-DEV-1002");
        refundedPayment.MarkAsPaid();
        refundedOrder.ChangeStatus(OrderStatus.Accepted, null, "Accepted");
        refundedOrder.ChangeStatus(OrderStatus.Cancelled, null, "Customer cancellation");
        refundedOrder.ChangeStatus(OrderStatus.Refunded, null, "Refund completed");
        var refund = new Refund(refundedPayment.Id, refundedOrder.TotalAmount, "Customer cancellation after payment");
        refund.Process();
        refundedOrder.UpdatePaymentStatus(PaymentStatus.Refunded);

        var codOrder = new Order("ORD-DEV-1003", noor.Id, groceryVendor.Id, noorHome.Id, PaymentMethodType.CashOnDelivery, 29.70m, 0m, 10m, 3m, "Call on arrival", groceryBranch.Id);
        codOrder.Items.Add(new OrderItem(codOrder.Id, milk.Id, milk.MasterProductId, milk.MasterProduct.NameEn, 1, 16.95m, 0m, "Liter"));
        codOrder.Items.Add(new OrderItem(codOrder.Id, soap.Id, soap.MasterProductId, soap.MasterProduct.NameEn, 1, 14.95m, 2.20m, "Liter"));
        var codPayment = new Payment(codOrder.Id, PaymentMethodType.CashOnDelivery, codOrder.TotalAmount);
        codPayment.MarkAsPending("CashOnDelivery", "PAY-DEV-1003");
        codOrder.ChangeStatus(OrderStatus.Placed, null, "Order placed");
        codOrder.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Awaiting vendor response");

        await _context.Orders.AddRangeAsync(deliveredOrder, refundedOrder, codOrder);
        await _context.Payments.AddRangeAsync(deliveredPayment, refundedPayment, codPayment);
        await _context.Refunds.AddAsync(refund);
        await _context.SaveChangesAsync();
    }

    private async Task SeedReviewsAsync(Guid ahmedId, Guid laylaId)
    {
        if (await _context.Reviews.AnyAsync())
        {
            return;
        }

        var delivered = await _context.Orders.FirstOrDefaultAsync(x => x.Status == OrderStatus.Delivered);
        if (delivered == null)
        {
            return;
        }

        await _context.Reviews.AddRangeAsync(
            new Review(delivered.Id, ahmedId, delivered.VendorId, 5, "طلب ممتاز، التغليف جيد والتوصيل سريع."),
            new Review(delivered.Id, laylaId, delivered.VendorId, 4, "Quality products and fast support."));
        await _context.SaveChangesAsync();
    }

    private async Task SeedNotificationsAsync(Guid ahmedId, Guid laylaId, Guid noorId)
    {
        if (await _context.Notifications.AnyAsync())
        {
            return;
        }

        await _context.Notifications.AddRangeAsync(
            new Notification(ahmedId, "تم توصيل طلبك", "طلبك الأخير وصل بنجاح وتم تقييمه كأحد أفضل الطلبات هذا الأسبوع.", "order"),
            new Notification(ahmedId, "عرض جديد", "خصم 10% على منتجات البقالة من Green Valley Market.", "marketing"),
            new Notification(laylaId, "Refund completed", "Your refund for order ORD-DEV-1002 has been completed.", "payment"),
            new Notification(noorId, "طلبك قيد المراجعة", "المتجر يراجع طلب الدفع عند الاستلام الخاص بك الآن.", "order"));
        await _context.SaveChangesAsync();
    }

    private async Task SeedDriverAssignmentsAsync()
    {
        if (await _context.DeliveryAssignments.AnyAsync())
        {
            return;
        }

        var activeDriver = await _context.Drivers.FirstAsync(x => x.IsAvailable);
        var deliveredOrder = await _context.Orders.FirstAsync(x => x.OrderNumber == "ORD-DEV-1001");
        var pendingOrder = await _context.Orders.FirstAsync(x => x.OrderNumber == "ORD-DEV-1003");

        var deliveredAssignment = new DeliveryAssignment(deliveredOrder.Id, 0m);
        deliveredAssignment.OfferTo(activeDriver.Id);
        deliveredAssignment.Accept();
        deliveredAssignment.MarkPickedUp();
        deliveredAssignment.MarkDelivered();

        var searchingAssignment = new DeliveryAssignment(pendingOrder.Id, pendingOrder.TotalAmount);
        searchingAssignment.OfferTo(activeDriver.Id);

        await _context.DeliveryAssignments.AddRangeAsync(deliveredAssignment, searchingAssignment);
        await _context.SaveChangesAsync();
    }

    private async Task SeedWalletsAndSettlementsAsync()
    {
        if (await _context.Wallets.AnyAsync())
        {
            return;
        }

        var deliveredOrder = await _context.Orders.FirstOrDefaultAsync(x => x.OrderNumber == "ORD-DEV-1001");
        if (deliveredOrder == null)
        {
            return;
        }

        var deliveredPayment = await _context.Payments.FirstOrDefaultAsync(x => x.OrderId == deliveredOrder.Id);
        if (deliveredPayment == null)
        {
            return;
        }

        var vendor = await _context.Vendors
            .Include(x => x.BankAccounts)
            .FirstOrDefaultAsync(x => x.Id == deliveredOrder.VendorId);
        if (vendor == null)
        {
            return;
        }

        var driver = await _context.Drivers.FirstOrDefaultAsync(x => x.IsAvailable);
        if (driver == null)
        {
            return;
        }

        var vendorWallet = new Wallet(WalletOwnerType.Vendor, vendor.Id);
        vendorWallet.Credit(5200m);
        var driverWallet = new Wallet(WalletOwnerType.Driver, driver.Id);
        driverWallet.Credit(850m);

        await _context.Wallets.AddRangeAsync(vendorWallet, driverWallet);
        await _context.SaveChangesAsync();

        var settlement = new Settlement(vendor.Id, null);
        settlement.UpdateTotals(deliveredOrder.TotalAmount, deliveredOrder.CommissionAmount);
        settlement.MarkAsProcessing();
        settlement.Items.Add(new SettlementItem(
            settlement.Id,
            deliveredOrder.Id,
            settlement.NetAmount,
            18m,
            deliveredOrder.CommissionAmount,
            0m));
        settlement.MarkAsSettled();

        var payout = new Payout(settlement.Id, settlement.NetAmount, vendor.BankAccounts.FirstOrDefault(x => x.IsPrimary)?.Id);
        payout.MarkAsProcessing();
        payout.MarkAsPaid("TRX-SETTLEMENT-1001");
        settlement.Payouts.Add(payout);

        await _context.Settlements.AddAsync(settlement);
        await _context.WalletTransactions.AddRangeAsync(
            new WalletTransaction(vendorWallet.Id, WalletTxnType.Credit, settlement.NetAmount, "IN", deliveredOrder.Id, deliveredPayment.Id, settlement.Id, "SETTLEMENT", settlement.Id, "Seeded vendor settlement payout"),
            new WalletTransaction(driverWallet.Id, WalletTxnType.Credit, 18m, "IN", deliveredOrder.Id, null, null, "DELIVERY_FEE", deliveredOrder.Id, "Seeded driver earning"));
        await _context.SaveChangesAsync();
    }

    private async Task ResetDevelopmentDataAsync()
    {
        await DisableAllTableConstraintsAsync();
        try
        {
            await DeleteRangeAsync(_context.DeliveryProofs);
            await DeleteRangeAsync(_context.DriverLocations);
            await DeleteRangeAsync(_context.DeliveryAssignments);
            await DeleteRangeAsync(_context.Refunds);
            await DeleteRangeAsync(_context.Reviews);
            await DeleteRangeAsync(_context.Notifications);
            await DeleteRangeAsync(_context.Payouts);
            await DeleteRangeAsync(_context.WalletTransactions);
            await DeleteRangeAsync(_context.Wallets);
            await DeleteRangeAsync(_context.SettlementItems);
            await DeleteRangeAsync(_context.Settlements);
            await DeleteRangeAsync(_context.Payments);
            await DeleteRangeAsync(_context.OrderStatusHistories);
            await DeleteRangeAsync(_context.OrderItems);
            await DeleteRangeAsync(_context.Orders);
            await DeleteRangeAsync(_context.CartItems);
            await DeleteRangeAsync(_context.Carts);
            await DeleteRangeAsync(_context.CustomerFavorites);
            await DeleteRangeAsync(_context.CustomerAddresses);
            await DeleteRangeAsync(_context.CouponVendors);
            await DeleteRangeAsync(_context.Coupons);
            await DeleteRangeAsync(_context.FeaturedProductPlacements);
            await DeleteRangeAsync(_context.HomeSections);
            await DeleteRangeAsync(_context.HomeContentSectionSettings);
            await DeleteRangeAsync(_context.HomeBanners);
            await DeleteRangeAsync(_context.VendorProducts);
            await DeleteRangeAsync(_context.ProductRequests);
            await DeleteRangeAsync(_context.BrandRequests);
            await DeleteRangeAsync(_context.CategoryRequests);
            await DeleteRangeAsync(_context.Drivers);
            await DeleteRangeAsync(_context.VendorBankAccounts);
            await DeleteRangeAsync(_context.BranchOperatingHours);
            await DeleteRangeAsync(_context.VendorBranches);
            await DeleteRangeAsync(_context.Vendors);
            await DeleteRangeAsync(_context.RefreshTokens);

            var products = await _context.MasterProducts.ToListAsync();
            if (products.Count > 0)
            {
                _context.MasterProducts.RemoveRange(products);
                await _context.SaveChangesAsync();
            }

            await DeleteRangeAsync(_context.Parts);
            await DeleteRangeAsync(_context.ProductTypes);
            await DeleteRangeAsync(_context.Brands);

            var categories = await _context.Categories
                .OrderByDescending(x => x.ParentCategoryId.HasValue)
                .ToListAsync();
            if (categories.Count > 0)
            {
                _context.Categories.RemoveRange(categories);
                await _context.SaveChangesAsync();
            }

            await DeleteRangeAsync(_context.UnitsOfMeasure);

            var users = await _userManager.Users.ToListAsync();
            foreach (var user in users)
            {
                await _userManager.DeleteAsync(user);
            }

            var roles = await _roleManager.Roles.ToListAsync();
            foreach (var role in roles)
            {
                await _roleManager.DeleteAsync(role);
            }

            _context.ChangeTracker.Clear();
        }
        finally
        {
            await EnableAllTableConstraintsAsync();
        }
    }

    private async Task DeleteRangeAsync<TEntity>(DbSet<TEntity> dbSet) where TEntity : class
    {
        if (await dbSet.AnyAsync())
        {
            await dbSet.ExecuteDeleteAsync();
        }
    }

    private async Task DisableAllTableConstraintsAsync()
    {
        if (!_context.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += N'ALTER TABLE [' + SCHEMA_NAME(schema_id) + N'].[' + name + N'] NOCHECK CONSTRAINT ALL;'
            FROM sys.tables;
            EXEC sp_executesql @sql;
            """;

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task EnableAllTableConstraintsAsync()
    {
        if (!_context.Database.IsSqlServer())
        {
            return;
        }

        const string sql = """
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += N'ALTER TABLE [' + SCHEMA_NAME(schema_id) + N'].[' + name + N'] WITH CHECK CHECK CONSTRAINT ALL;'
            FROM sys.tables;
            EXEC sp_executesql @sql;
            """;

        await _context.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<User> EnsureUserAsync(
        string email,
        string fullName,
        string phone,
        UserRole role,
        string password,
        Action<User>? configure = null)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new User(fullName, email, phone, role);
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to create seeded user {email}: {string.Join(", ", result.Errors.Select(x => x.Description))}");
            }
        }

        if (!await _userManager.IsInRoleAsync(user, role.ToString()))
        {
            await _userManager.AddToRoleAsync(user, role.ToString());
        }

        configure?.Invoke(user);
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<DevelopmentSeedSummary> BuildSummaryAsync()
    {
        return new DevelopmentSeedSummary(
            await _context.Categories.CountAsync(),
            await _context.Brands.CountAsync(),
            await _context.MasterProducts.CountAsync(),
            await _context.Vendors.CountAsync(),
            await _context.VendorProducts.CountAsync(),
            await _userManager.Users.CountAsync(x => x.Role == UserRole.Customer),
            await _context.Drivers.CountAsync(),
            await _context.Orders.CountAsync(),
            await _context.HomeBanners.CountAsync(),
            await _context.Coupons.CountAsync(),
            await _context.Reviews.CountAsync(),
            await _context.Notifications.CountAsync());
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

public sealed record DevelopmentSeedSummary(
    int Categories,
    int Brands,
    int MasterProducts,
    int Vendors,
    int VendorProducts,
    int Customers,
    int Drivers,
    int Orders,
    int Banners,
    int Coupons,
    int Reviews,
    int Notifications);

internal sealed record VendorProductSeed(
    string VendorName,
    string ProductSlug,
    decimal SellingPrice,
    int Quantity,
    decimal? CompareAtPrice);

internal static class ImageCatalog
{
    public const string CategoryGroceries = "https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryElectronics = "https://images.unsplash.com/photo-1519389950473-47ba0277781c?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryHome = "https://images.unsplash.com/photo-1484154218962-a197022b5858?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryDairy = "https://images.unsplash.com/photo-1550583724-b2692b85b150?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryBeverages = "https://images.unsplash.com/photo-1544145945-f90425340c7e?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryProduce = "https://images.unsplash.com/photo-1610832958506-aa56368176cf?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryBakery = "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=1200&q=80";
    public const string CategorySnacks = "https://images.unsplash.com/photo-1585238342024-78d387f4a707?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryPhones = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryAccessories = "https://images.unsplash.com/photo-1585386959984-a41552231658?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryHomeCare = "https://images.unsplash.com/photo-1581578731548-c64695cc6952?auto=format&fit=crop&w=1200&q=80";
    public const string CategoryKitchen = "https://images.unsplash.com/photo-1513694203232-719a280e022f?auto=format&fit=crop&w=1200&q=80";
    public const string BannerDeals = "https://images.unsplash.com/photo-1607082350899-7e105aa886ae?auto=format&fit=crop&w=1600&q=80";
    public const string BannerStores = "https://images.unsplash.com/photo-1520607162513-77705c0f0d4a?auto=format&fit=crop&w=1600&q=80";
    public const string BannerBestSelling = "https://images.unsplash.com/photo-1515169067868-5387ec356754?auto=format&fit=crop&w=1600&q=80";
    public const string Milk1 = "https://images.unsplash.com/photo-1550583724-b2692b85b150?auto=format&fit=crop&w=1200&q=80";
    public const string Milk2 = "https://images.unsplash.com/photo-1563636619-e9143da7973b?auto=format&fit=crop&w=1200&q=80";
    public const string Yogurt1 = "https://images.unsplash.com/photo-1571212515416-fca88f7c75bb?auto=format&fit=crop&w=1200&q=80";
    public const string Yogurt2 = "https://images.unsplash.com/photo-1488477181946-6428a0291777?auto=format&fit=crop&w=1200&q=80";
    public const string Juice1 = "https://images.unsplash.com/photo-1600271886742-f049cd451bba?auto=format&fit=crop&w=1200&q=80";
    public const string Juice2 = "https://images.unsplash.com/photo-1621506289937-a8e4df240d0b?auto=format&fit=crop&w=1200&q=80";
    public const string Water1 = "https://images.unsplash.com/photo-1564419320461-6870880221ad?auto=format&fit=crop&w=1200&q=80";
    public const string Water2 = "https://images.unsplash.com/photo-1616118132534-381148898bb4?auto=format&fit=crop&w=1200&q=80";
    public const string Bread1 = "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=1200&q=80";
    public const string Bread2 = "https://images.unsplash.com/photo-1608198093002-ad4e005484ec?auto=format&fit=crop&w=1200&q=80";
    public const string Chips1 = "https://images.unsplash.com/photo-1585238342024-78d387f4a707?auto=format&fit=crop&w=1200&q=80";
    public const string Chips2 = "https://images.unsplash.com/photo-1621939514649-280e2ee25f60?auto=format&fit=crop&w=1200&q=80";
    public const string Banana1 = "https://images.unsplash.com/photo-1571771894821-ce9b6c11b08e?auto=format&fit=crop&w=1200&q=80";
    public const string Banana2 = "https://images.unsplash.com/photo-1603833665858-e61d17a86224?auto=format&fit=crop&w=1200&q=80";
    public const string Tomato1 = "https://images.unsplash.com/photo-1592924357228-91a4daadcfea?auto=format&fit=crop&w=1200&q=80";
    public const string Tomato2 = "https://images.unsplash.com/photo-1546094096-0df4bcaaa337?auto=format&fit=crop&w=1200&q=80";
    public const string DishSoap1 = "https://images.unsplash.com/photo-1581578731548-c64695cc6952?auto=format&fit=crop&w=1200&q=80";
    public const string DishSoap2 = "https://images.unsplash.com/photo-1583947582886-f40ec95dd752?auto=format&fit=crop&w=1200&q=80";
    public const string Towel1 = "https://images.unsplash.com/photo-1527515637462-cff94eecc1ac?auto=format&fit=crop&w=1200&q=80";
    public const string Towel2 = "https://images.unsplash.com/photo-1616627456094-7d0f04f0353f?auto=format&fit=crop&w=1200&q=80";
    public const string Charger1 = "https://images.unsplash.com/photo-1585386959984-a41552231658?auto=format&fit=crop&w=1200&q=80";
    public const string Charger2 = "https://images.unsplash.com/photo-1615526675159-e248c3021d3f?auto=format&fit=crop&w=1200&q=80";
    public const string Case1 = "https://images.unsplash.com/photo-1601593346740-925612772716?auto=format&fit=crop&w=1200&q=80";
    public const string Case2 = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80";
    public const string Phone1 = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=80";
    public const string Phone2 = "https://images.unsplash.com/photo-1598327105666-5b89351aff97?auto=format&fit=crop&w=1200&q=80";
    public const string Iphone1 = "https://images.unsplash.com/photo-1695048133142-1a20484d2569?auto=format&fit=crop&w=1200&q=80";
    public const string Iphone2 = "https://images.unsplash.com/photo-1592750475338-74b7b21085ab?auto=format&fit=crop&w=1200&q=80";
    public const string DriverNationalId = "https://images.unsplash.com/photo-1517841905240-472988babdf9?auto=format&fit=crop&w=800&q=80";
    public const string DriverLicense = "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=800&q=80";
    public const string DriverVehicle = "https://images.unsplash.com/photo-1558981806-ec527fa84c39?auto=format&fit=crop&w=800&q=80";
    public const string DriverProfile = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=800&q=80";
}
