using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Brands.CreateBrand;
using Zadana.Domain.Modules.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateBrandCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private CreateBrandCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WithValidData_ShouldAddBrandAndReturnDto()
    {
        // Arrange
        var mockBrandSet = new Mock<DbSet<Brand>>();
        _dbContextMock.Setup(c => c.Brands).Returns(mockBrandSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateBrandCommand("علامة تجارية", "Test Brand", "https://logo.png", Guid.NewGuid());
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.NameAr.Should().Be("علامة تجارية");
        result.NameEn.Should().Be("Test Brand");
        result.LogoUrl.Should().Be("https://logo.png");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCallSaveChanges()
    {
        // Arrange
        var mockBrandSet = new Mock<DbSet<Brand>>();
        _dbContextMock.Setup(c => c.Brands).Returns(mockBrandSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateBrandCommand("ماركة", "Brand", null, Guid.NewGuid());
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockBrandSet.Verify(d => d.Add(It.IsAny<Brand>()), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
