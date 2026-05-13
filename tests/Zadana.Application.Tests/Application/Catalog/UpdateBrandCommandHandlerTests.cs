using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Catalog;

public class UpdateBrandCommandHandlerTests
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
    public async Task Handle_WhenBrandNotFound_ShouldThrowNotFoundException()
    {
        await using var context = CreateContext();
        var command = new UpdateBrandCommand(Guid.NewGuid(), "Updated", "Updated", null, null, Guid.NewGuid(), null, true);
        var handler = new UpdateBrandCommandHandler(context, _cacheInvalidatorMock.Object);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldUpdateAndSave()
    {
        await using var context = CreateContext();
        var category = new Category("فرعي", "Sub", null, Guid.NewGuid(), 1);
        var brand = new Brand("قديم", "Old", null, null, category.Id);

        context.Categories.Add(category);
        context.Brands.Add(brand);
        await context.SaveChangesAsync();

        var command = new UpdateBrandCommand(brand.Id, "جديد", "New", "https://new.png", "https://cover-new.png", category.Id, null, true);
        var handler = new UpdateBrandCommandHandler(context, _cacheInvalidatorMock.Object);

        await handler.Handle(command, CancellationToken.None);

        var updated = await context.Brands.FirstAsync(item => item.Id == brand.Id);
        updated.NameAr.Should().Be("جديد");
        updated.NameEn.Should().Be("New");
        updated.LogoUrl.Should().Be("https://new.png");
        updated.CoverImageUrl.Should().Be("https://cover-new.png");
        _cacheInvalidatorMock.Verify(
            c => c.RemoveByTagsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
