using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Categories.CreateCategory;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateCategoryCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private CreateCategoryCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WithValidData_ShouldAddCategoryAndReturnDto()
    {
        // Arrange
        var mockCategorySet = new Mock<DbSet<Category>>();
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateCategoryCommand("فئة", "Category", null, 1);
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.NameAr.Should().Be("فئة");
        result.NameEn.Should().Be("Category");
        result.DisplayOrder.Should().Be(1);
        result.IsActive.Should().BeTrue();
        mockCategorySet.Verify(d => d.Add(It.IsAny<Category>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenParentCategoryNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var fakeParentId = Guid.NewGuid();
        var command = new CreateCategoryCommand("فرعي", "Sub", fakeParentId, 2);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
