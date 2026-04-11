using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Brands.UpdateBrand;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class UpdateBrandCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private UpdateBrandCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WhenBrandNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var mockBrandSet = new Mock<DbSet<Brand>>();
        mockBrandSet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Brand?)null);
        _dbContextMock.Setup(c => c.Brands).Returns(mockBrandSet.Object);

        var command = new UpdateBrandCommand(Guid.NewGuid(), "Updated", "Updated", null, Guid.NewGuid(), true);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldUpdateAndSave()
    {
        // Arrange
        var brand = new Brand("قديم", "Old", null);
        var mockBrandSet = new Mock<DbSet<Brand>>();
        mockBrandSet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(brand);
        _dbContextMock.Setup(c => c.Brands).Returns(mockBrandSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateBrandCommand(brand.Id, "جديد", "New", "https://new.png", Guid.NewGuid(), true);
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        brand.NameAr.Should().Be("جديد");
        brand.NameEn.Should().Be("New");
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
