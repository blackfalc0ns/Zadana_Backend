using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class PlaceOrderCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private PlaceOrderCommandHandler CreateHandler() =>
        new(_dbContextMock.Object, TestLocalizer.Create<SharedResource>());

    [Fact]
    public async Task Handle_WhenCartEmpty_ShouldThrowBusinessRuleException()
    {
        var carts = Array.Empty<Cart>().AsQueryable();
        var mockCartSet = new Mock<DbSet<Cart>>();
        var mockQueryable = new TestAsyncEnumerable<Cart>(carts);
        mockCartSet.As<IAsyncEnumerable<Cart>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockQueryable.GetAsyncEnumerator());
        mockCartSet.As<IQueryable<Cart>>().Setup(m => m.Provider).Returns(((IQueryable<Cart>)mockQueryable).Provider);
        mockCartSet.As<IQueryable<Cart>>().Setup(m => m.Expression).Returns(((IQueryable<Cart>)mockQueryable).Expression);
        mockCartSet.As<IQueryable<Cart>>().Setup(m => m.ElementType).Returns(((IQueryable<Cart>)mockQueryable).ElementType);
        mockCartSet.As<IQueryable<Cart>>().Setup(m => m.GetEnumerator()).Returns(((IQueryable<Cart>)mockQueryable).GetEnumerator());

        _dbContextMock.Setup(c => c.Carts).Returns(mockCartSet.Object);

        var command = new PlaceOrderCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "CashOnDelivery", null, null, null);
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "EMPTY_CART");
    }
}
