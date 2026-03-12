using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Catalog.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;

namespace Zadana.Infrastructure.Persistence;

public class ApplicationDbContextInitialiser
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;

    public ApplicationDbContextInitialiser(
        ApplicationDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> _roleManager)
    {
        _context = context;
        _userManager = userManager;
        this._roleManager = _roleManager;
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
        catch (Exception ex)
        {
            // Log error
            throw;
        }
    }

    public async Task SeedAsync()
    {
        try
        {
            await TrySeedAsync();
        }
        catch (Exception ex)
        {
            // Log error
            throw;
        }
    }

    private async Task TrySeedAsync()
    {
        // 1. Seed Roles
        var roles = Enum.GetValues<UserRole>();
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role.ToString()))
            {
                await _roleManager.CreateAsync(new IdentityRole<Guid>(role.ToString()));
            }
        }

        // 2. Seed Super Admin
        if (await _userManager.FindByEmailAsync("admin@system.com") == null)
        {
            var admin = new User(
                "Super Admin",
                "admin@system.com",
                "01000000000",
                UserRole.SuperAdmin);

            await _userManager.CreateAsync(admin, "Admin@123");
            await _userManager.AddToRoleAsync(admin, UserRole.SuperAdmin.ToString());
        }

        // 3. Seed Units of Measure
        if (!await _context.UnitsOfMeasure.AnyAsync())
        {
            var units = new List<UnitOfMeasure>
            {
                new UnitOfMeasure("كيلوجرام", "Kilogram", "kg"),
                new UnitOfMeasure("جرام", "Gram", "g"),
                new UnitOfMeasure("لتر", "Liter", "L"),
                new UnitOfMeasure("ملليلتر", "Milliliter", "mL"),
                new UnitOfMeasure("قطعة", "Piece", "pcs"),
                new UnitOfMeasure("كرتونة", "Carton", "ctn"),
                new UnitOfMeasure("عبوة", "Pack", "pk"),
                new UnitOfMeasure("صندوق", "Box", "box"),
                new UnitOfMeasure("دستة", "Dozen", "dz")
            };
            await _context.UnitsOfMeasure.AddRangeAsync(units);
            await _context.SaveChangesAsync();
        }

        // 4. Seed Categories
        if (!await _context.Categories.AnyAsync())
        {
            var food = new Category("مواد غذائية", "Food", null, null, 1);
            var electronics = new Category("إلكترونيات", "Electronics", null, null, 2);
            var home = new Category("أدوات منزلية", "Home Appliances", null, null, 3);
            
            await _context.Categories.AddRangeAsync(food, electronics, home);
            await _context.SaveChangesAsync();

            // Subcategories
            var dairy = new Category("ألبان", "Dairy", null, food.Id, 1);
            var meat = new Category("لحوم", "Meat", null, food.Id, 2);
            var phones = new Category("هواتف", "Phones", null, electronics.Id, 1);
            
            await _context.Categories.AddRangeAsync(dairy, meat, phones);
            await _context.SaveChangesAsync();
        }

        // 5. Seed Brands
        if (!await _context.Brands.AnyAsync())
        {
            var brands = new List<Brand>
            {
                new Brand("المراعي", "Almarai"),
                new Brand("نادك", "Nadec"),
                new Brand("سامسونج", "Samsung"),
                new Brand("أبل", "Apple"),
                new Brand("إل جي", "LG")
            };
            await _context.Brands.AddRangeAsync(brands);
            await _context.SaveChangesAsync();
        }

        // 6. Seed Master Products
        if (!await _context.MasterProducts.AnyAsync())
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.NameEn == "Dairy");
            var brand = await _context.Brands.FirstOrDefaultAsync(b => b.NameEn == "Almarai");
            var unit = await _context.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Symbol == "L");

            if (category != null && brand != null && unit != null)
            {
                var products = new List<MasterProduct>
                {
                    new MasterProduct("حليب كامل الدسم 1 لتر", "Full Cream Milk 1L", "full-cream-milk-1l", category.Id, brand.Id, unit.Id, "حليب طازج", "Fresh local milk"),
                    new MasterProduct("زبادي طازج", "Fresh Yoghurt", "fresh-yoghurt", category.Id, brand.Id, unit.Id, "زبادي طبيعي", "Natural yoghurt")
                };
                
                foreach (var p in products)
                {
                    p.Publish();
                }
                
                await _context.MasterProducts.AddRangeAsync(products);
                await _context.SaveChangesAsync();
            }
        }
    }
}
