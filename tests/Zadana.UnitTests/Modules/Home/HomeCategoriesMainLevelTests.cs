using FluentAssertions;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Infrastructure.Modules.Home.Services;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Home;

public class HomeCategoriesMainLevelTests
{
    [Fact]
    public async Task GetCategoriesAsync_ReturnsOnlyCategoryLevelItems()
    {
        await using var context = TestDbContextFactory.Create();

        var activity = new Category("نشاط", "Activity", "activity.jpg", null, 1);
        context.Categories.Add(activity);
        await context.SaveChangesAsync();

        var subActivity = new Category("نشاط فرعي", "Sub Activity", "sub-activity.jpg", activity.Id, 1);
        context.Categories.Add(subActivity);
        await context.SaveChangesAsync();

        var categoryOne = new Category("ألبان", "Dairy", "dairy.jpg", subActivity.Id, 1);
        var categoryTwo = new Category("هواتف", "Phones", "phones.jpg", subActivity.Id, 2);
        context.Categories.AddRange(categoryOne, categoryTwo);
        await context.SaveChangesAsync();

        var subcategoryOne = new Category("حليب", "Milk", "milk.jpg", categoryOne.Id, 1);
        var subcategoryTwo = new Category("سامسونج", "Samsung", "samsung.jpg", categoryTwo.Id, 1);
        context.Categories.AddRange(subcategoryOne, subcategoryTwo);
        await context.SaveChangesAsync();

        var service = new HomeReadService(context, new FakeCurrentUserService());

        var result = await service.GetCategoriesAsync(10);

        result.Items.Should().HaveCount(2);
        result.Items.Select(x => x.Id).Should().Equal(categoryOne.Id, categoryTwo.Id);
        result.Items.Select(x => x.Name).Should().Equal("Dairy", "Phones");
    }

    private sealed class FakeCurrentUserService : Zadana.Application.Common.Interfaces.ICurrentUserService
    {
        public Guid? UserId => null;
        public string? GuestDeviceId => null;
        public string? Role => null;
        public bool IsAuthenticated => false;
        public string? GetDeviceInfo() => "test";
    }
}
