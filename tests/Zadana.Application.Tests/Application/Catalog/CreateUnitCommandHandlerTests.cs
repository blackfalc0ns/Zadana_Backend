using FluentAssertions;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.Units.CreateUnit;
using Zadana.Domain.Modules.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateUnitCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private CreateUnitCommandHandler CreateHandler() => new(_dbContextMock.Object);

    [Fact]
    public async Task Handle_WithValidData_ShouldAddUnitAndReturnDto()
    {
        // Arrange
        var mockUnitSet = new Mock<DbSet<UnitOfMeasure>>();
        _dbContextMock.Setup(c => c.UnitsOfMeasure).Returns(mockUnitSet.Object);
        _dbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateUnitCommand("كيلوغرام", "Kilogram", "kg");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.NameAr.Should().Be("كيلوغرام");
        result.NameEn.Should().Be("Kilogram");
        result.Symbol.Should().Be("kg");
        result.IsActive.Should().BeTrue();
        mockUnitSet.Verify(d => d.Add(It.IsAny<UnitOfMeasure>()), Times.Once);
        _dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
