using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategorySubcategories;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetCategorySubcategoriesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOnlyDirectActiveChildren_ForExistingCategory()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("الألبان", "Dairy", "dairy.jpg", null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var childB = new Category("زبادي", "Yogurt", "yogurt.jpg", root.Id, 2);
        var childA = new Category("حليب", "Milk", "milk.jpg", root.Id, 1);
        var inactiveChild = new Category("جبنة", "Cheese", "cheese.jpg", root.Id, 3);
        inactiveChild.Deactivate();

        context.Categories.AddRange(childB, childA, inactiveChild);
        await context.SaveChangesAsync();

        var grandChild = new Category("كامل الدسم", "Full Fat", "full-fat.jpg", childA.Id, 1);
        context.Categories.Add(grandChild);
        await context.SaveChangesAsync();

        var handler = new GetCategorySubcategoriesQueryHandler(context);

        var result = await handler.Handle(new GetCategorySubcategoriesQuery(root.Id), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().Equal(childA.Id, childB.Id);
        result.Select(x => x.Name).Should().Equal("Milk", "Yogurt");
        result.Should().NotContain(x => x.Id == grandChild.Id);
        result.Should().NotContain(x => x.Id == inactiveChild.Id);
    }

    [Fact]
    public async Task Handle_ReturnsLocalizedNames_UsingCurrentUiCulture()
    {
        using var scope = new CultureScope("ar");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("الفواكه", "Fruits", "fruits.jpg", null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var child = new Category("تفاح", "Apple", "apple.jpg", root.Id, 1);
        context.Categories.Add(child);
        await context.SaveChangesAsync();

        var handler = new GetCategorySubcategoriesQueryHandler(context);

        var result = await handler.Handle(new GetCategorySubcategoriesQuery(root.Id), CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("تفاح");
    }

    [Fact]
    public async Task Handle_WhenCategoryHasNoChildren_ReturnsEmptyList()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var root = new Category("خضار", "Vegetables", "veg.jpg", null, 1);
        context.Categories.Add(root);
        await context.SaveChangesAsync();

        var handler = new GetCategorySubcategoriesQueryHandler(context);

        var result = await handler.Handle(new GetCategorySubcategoriesQuery(root.Id), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenCategoryDoesNotExist_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var handler = new GetCategorySubcategoriesQueryHandler(context);
        var missingId = Guid.NewGuid();

        var act = () => handler.Handle(new GetCategorySubcategoriesQuery(missingId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCategoryIdIsNotProvided_ReturnsAllLeafActiveSubcategories()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var rootOne = new Category("القسم الأول", "Root One", "root-one.jpg", null, 1);
        var rootTwo = new Category("القسم الثاني", "Root Two", "root-two.jpg", null, 2);
        context.Categories.AddRange(rootOne, rootTwo);
        await context.SaveChangesAsync();

        var childOne = new Category("طفل 1", "Child One", "child-one.jpg", rootOne.Id, 2);
        var childTwo = new Category("طفل 2", "Child Two", "child-two.jpg", rootTwo.Id, 1);
        var grandChild = new Category("حفيد", "Grand Child", "grand-child.jpg", childOne.Id, 1);
        var inactiveChild = new Category("طفل 3", "Child Three", "child-three.jpg", rootOne.Id, 3);
        inactiveChild.Deactivate();
        var rootOnly = new Category("جذر فقط", "Root Only", "root-only.jpg", null, 3);

        context.Categories.AddRange(childOne, childTwo, grandChild, inactiveChild, rootOnly);
        await context.SaveChangesAsync();

        var handler = new GetCategorySubcategoriesQueryHandler(context);

        var result = await handler.Handle(new GetCategorySubcategoriesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Id).Should().Equal(childTwo.Id, grandChild.Id);
        result.Select(x => x.Name).Should().Equal("Child Two", "Grand Child");
        result.Should().NotContain(x => x.Id == inactiveChild.Id || x.Id == rootOnly.Id || x.Id == childOne.Id);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
