using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Categories.UpdateCategory;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class UpdateCategoryCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private UpdateCategoryCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WhenCategoryNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var command = new UpdateCategoryCommand(Guid.NewGuid(), "تحديث", "Update", null, null, 1, true);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenParentCategoryNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var existingCategory = new Category("قديم", "Old", null, null, 1);

        var mockCategorySet = new Mock<DbSet<Category>>();
        var callCount = 0;
        mockCategorySet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? existingCategory : null; // first call returns category, second returns null for parent
            });
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);

        var fakeParentId = Guid.NewGuid();
        var command = new UpdateCategoryCommand(existingCategory.Id, "تحديث", "Update", null, fakeParentId, 1, true);
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
        var existingCategory = new Category("قديم", "Old", null, null, 1);
        var mockCategorySet = new Mock<DbSet<Category>>();
        mockCategorySet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCategory);
        _dbContextMock.Setup(c => c.Categories).Returns(mockCategorySet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateCategoryCommand(existingCategory.Id, "جديد", "New", null, null, 5, true);
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        existingCategory.NameAr.Should().Be("جديد");
        existingCategory.NameEn.Should().Be("New");
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
