using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.CreateMasterProduct;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateMasterProductCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private CreateMasterProductCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WhenCategoryNotFound_ShouldThrowNotFoundException()
    {
        var categories = Array.Empty<Category>().AsQueryable();
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Provider).Returns(categories.Provider);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.Expression).Returns(categories.Expression);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.ElementType).Returns(categories.ElementType);
        mockCategorySet.As<IQueryable<Category>>().Setup(m => m.GetEnumerator()).Returns(categories.GetEnumerator());
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var command = new CreateMasterProductCommand(Guid.NewGuid(), "منتج", "Product", "product-slug", null, null, null, null, null);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenBrandNotFound_ShouldThrowNotFoundException()
    {
        var category = new Category("فئة", "Category", null, null, 1);
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
        var command = new CreateMasterProductCommand(category.Id, "منتج", "Product", "slug", null, null, null, fakeBrandId, null);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldAddProductAndReturnGuid()
    {
        var category = new Category("فئة", "Category", null, null, 1);
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

        var command = new CreateMasterProductCommand(category.Id, "منتج تجريبي", "Test Product", "test-product", null, "وصف", "Description", null, null);
        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        mockMasterProductSet.Verify(d => d.Add(It.IsAny<MasterProduct>()), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithImages_ShouldAddProductAndImages()
    {
        var category = new Category("فئة", "Category", null, null, 1);
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

        var images = new List<CreateProductImageInfo>
        {
            new("https://example.com/img1.png", "Alt 1", 0, true),
            new("https://example.com/img2.png", "Alt 2", 1, false)
        };

        var command = new CreateMasterProductCommand(category.Id, "منتج بالصور", "Product with Images", "slug", null, "وصف", "Description", null, null, images);
        var handler = CreateHandler();

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeEmpty();
        mockMasterProductSet.Verify(d => d.Add(It.Is<MasterProduct>(p => p.Images.Count == 2)), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
