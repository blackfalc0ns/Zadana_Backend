using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Units.UpdateUnit;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class UpdateUnitCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private UpdateUnitCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WhenUnitNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var mockUnitSet = new Mock<DbSet<UnitOfMeasure>>();
        mockUnitSet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UnitOfMeasure?)null);
        _dbContextMock.Setup(c => c.UnitsOfMeasure).Returns(mockUnitSet.Object);

        var command = new UpdateUnitCommand(Guid.NewGuid(), "تحديث", "Update", "u", true);
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
        var unit = new UnitOfMeasure("قديم", "Old", "o");
        var mockUnitSet = new Mock<DbSet<UnitOfMeasure>>();
        mockUnitSet.Setup(s => s.FindAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(unit);
        _dbContextMock.Setup(c => c.UnitsOfMeasure).Returns(mockUnitSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new UpdateUnitCommand(unit.Id, "جديد", "New", "n", true);
        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        unit.NameAr.Should().Be("جديد");
        unit.NameEn.Should().Be("New");
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
