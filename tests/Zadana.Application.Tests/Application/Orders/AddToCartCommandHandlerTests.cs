using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Commands.AddToCart;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

/// <summary>
/// Unit tests for AddToCartCommandHandler.
/// </summary>
public class AddToCartCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private AddToCartCommandHandler CreateHandler() => new(_dbContextMock.Object);

    // ─── Product Not Found ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var vendorProducts = Array.Empty<VendorProduct>().AsQueryable();
        var mockVpSet = new Mock<DbSet<VendorProduct>>();
        var mockQueryable = new TestAsyncEnumerable<VendorProduct>(vendorProducts);
        mockVpSet.As<IAsyncEnumerable<VendorProduct>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockQueryable.GetAsyncEnumerator());
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.Provider).Returns(((IQueryable<VendorProduct>)mockQueryable).Provider);
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.Expression).Returns(((IQueryable<VendorProduct>)mockQueryable).Expression);
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.ElementType).Returns(((IQueryable<VendorProduct>)mockQueryable).ElementType);
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.GetEnumerator()).Returns(((IQueryable<VendorProduct>)mockQueryable).GetEnumerator());

        _dbContextMock.Setup(c => c.VendorProducts).Returns(mockVpSet.Object);

        var command = new AddToCartCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Insufficient Stock ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenInsufficientStock_ShouldThrowBusinessRuleException()
    {
        // Arrange
        var vendorProduct = new VendorProduct(Guid.NewGuid(), Guid.NewGuid(), 50m, 3, null, null);
        var vpList = new List<VendorProduct> { vendorProduct }.AsQueryable();
        var mockVpSet = new Mock<DbSet<VendorProduct>>();
        var mockQueryable = new TestAsyncEnumerable<VendorProduct>(vpList);
        mockVpSet.As<IAsyncEnumerable<VendorProduct>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockQueryable.GetAsyncEnumerator());
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.Provider).Returns(((IQueryable<VendorProduct>)mockQueryable).Provider);
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.Expression).Returns(((IQueryable<VendorProduct>)mockQueryable).Expression);
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.ElementType).Returns(((IQueryable<VendorProduct>)mockQueryable).ElementType);
        mockVpSet.As<IQueryable<VendorProduct>>().Setup(m => m.GetEnumerator()).Returns(((IQueryable<VendorProduct>)mockQueryable).GetEnumerator());

        _dbContextMock.Setup(c => c.VendorProducts).Returns(mockVpSet.Object);

        // Request 100 items, but only 3 in stock
        var command = new AddToCartCommand(Guid.NewGuid(), Guid.NewGuid(), vendorProduct.Id, 100);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "INSUFFICIENT_STOCK");
    }
}

// Re-use the async enumerator helper
internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;

    internal TestAsyncQueryProvider(IQueryProvider inner)
    {
        _inner = inner;
    }

    public IQueryable CreateQuery(System.Linq.Expressions.Expression expression)
    {
        return new TestAsyncEnumerable<TEntity>(expression);
    }

    public IQueryable<TElement> CreateQuery<TElement>(System.Linq.Expressions.Expression expression)
    {
        return new TestAsyncEnumerable<TElement>(expression);
    }

    public object Execute(System.Linq.Expressions.Expression expression)
    {
        return _inner.Execute(expression)!;
    }

    public TResult Execute<TResult>(System.Linq.Expressions.Expression expression)
    {
        return _inner.Execute<TResult>(expression);
    }

    public TResult ExecuteAsync<TResult>(System.Linq.Expressions.Expression expression, CancellationToken cancellationToken = default)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: new[] { typeof(System.Linq.Expressions.Expression) })!
            .MakeGenericMethod(expectedResultType)
            .Invoke(this, new[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable)
        : base(enumerable)
    { }

    public TestAsyncEnumerable(System.Linq.Expressions.Expression expression)
        : base(expression)
    { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    IQueryProvider IQueryable.Provider
    {
        get { return new TestAsyncQueryProvider<T>(this); }
    }
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        return default;
    }
}
