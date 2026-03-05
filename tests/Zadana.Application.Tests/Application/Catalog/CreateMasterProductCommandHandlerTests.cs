using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateMasterProductCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private CreateMasterProductCommandHandler CreateHandler() => new(_dbContextMock.Object);

    // ─── Category Not Found ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCategoryNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var categories = Array.Empty<Category>().AsQueryable();
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Provider).Returns(categories.Provider);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(categories.Expression);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(categories.ElementType);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(categories.GetEnumerator());
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var command = new CreateMasterProductCommand(Guid.NewGuid(), "Product", "product-slug", null, null, null, null);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Brand Not Found ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenBrandNotFound_ShouldThrowNotFoundException()
    {
        // Arrange — category exists, brand does not
        var category = new Category("فئة", "Cat", null, 1);
        var categoryList = new List<Category> { category }.AsQueryable();
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Provider).Returns(categoryList.Provider);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(categoryList.Expression);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(categoryList.ElementType);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(categoryList.GetEnumerator());
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var brands = Array.Empty<Brand>().AsQueryable();
        var mockBrandSet = new Mock<DbSet<Brand>>();
        mockBrandSet.As<IQueryable<Brand>>().Setup(m => m.Provider).Returns(brands.Provider);
        mockBrandSet.As<IQueryable<Brand>>().Setup(m => m.Expression).Returns(brands.Expression);
        mockBrandSet.As<IQueryable<Brand>>().Setup(m => m.ElementType).Returns(brands.ElementType);
        mockBrandSet.As<IQueryable<Brand>>().Setup(m => m.GetEnumerator()).Returns(brands.GetEnumerator());
        _dbContextMock.Setup(c => c.Brands).Returns(mockBrandSet.Object);

        var fakeBrandId = Guid.NewGuid();
        var command = new CreateMasterProductCommand(category.Id, "Product", "slug", null, null, fakeBrandId, null);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Success ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithValidData_ShouldAddProductAndReturnGuid()
    {
        // Arrange — category exists
        var category = new Category("فئة", "Cat", null, 1);
        var categoryList = new List<Category> { category }.AsQueryable();
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Provider).Returns(categoryList.Provider);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(categoryList.Expression);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(categoryList.ElementType);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(categoryList.GetEnumerator());
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var mockMasterProductSet = new Mock<DbSet<MasterProduct>>();
        _dbContextMock.Setup(c => c.MasterProducts).Returns(mockMasterProductSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateMasterProductCommand(category.Id, "Test Product", "test-product", null, "Description", null, null);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        mockMasterProductSet.Verify(d => d.Add(It.IsAny<MasterProduct>()), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
