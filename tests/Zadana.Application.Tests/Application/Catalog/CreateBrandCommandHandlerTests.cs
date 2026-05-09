using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateBrandCommandHandlerTests
{
    private readonly Mock<ICacheInvalidator> _cacheInvalidatorMock = new();

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldAddBrandAndReturnDto()
    {
        await using var context = CreateContext();
        var category = new Category("فئة رئيسية", "Parent Category", null, Guid.NewGuid(), 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var command = new CreateBrandCommand("علامة تجارية", "Test Brand", "https://logo.png", "https://cover.png", category.Id);
        var handler = new CreateBrandCommandHandler(context, _cacheInvalidatorMock.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.NameAr.Should().Be("علامة تجارية");
        result.NameEn.Should().Be("Test Brand");
        result.LogoUrl.Should().Be("https://logo.png");
        result.CoverImageUrl.Should().Be("https://cover.png");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCallSaveChanges()
    {
        await using var context = CreateContext();
        var category = new Category("فئة رئيسية", "Parent Category", null, Guid.NewGuid(), 1);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var command = new CreateBrandCommand("ماركة", "Brand", null, null, category.Id);
        var handler = new CreateBrandCommandHandler(context, _cacheInvalidatorMock.Object);

        await handler.Handle(command, CancellationToken.None);

        context.Brands.Should().ContainSingle(brand => brand.NameEn == "Brand");
        _cacheInvalidatorMock.Verify(
            c => c.RemoveByTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
