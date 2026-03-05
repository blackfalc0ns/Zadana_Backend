using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Catalog.Commands.CreateVendorProduct;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Catalog;

public class CreateVendorProductCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContextMock = new();

    private CreateVendorProductCommandHandler CreateHandler() => new(_dbContextMock.Object);

    private static Vendor CreateVendor(VendorStatus status)
    {
        var vendor = new Vendor(Guid.NewGuid(), "متجر", "Store", "desc", "123", "vendor@test.com", "01011111111");
        // We need to set vendor status — by default it should be Pending
        // For Active status tests, we need to approve the vendor
        if (status == VendorStatus.Active)
        {
            vendor.Approve(5m, Guid.NewGuid()); // 5% commission, mock admin ID
        }
        return vendor;
    }

    // ─── Vendor Not Found ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenVendorNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var vendors = Array.Empty<Vendor>().AsQueryable();
        var mockVendorSet = new Mock<DbSet<Vendor>>();
        var mockQueryable = new TestAsyncEnumerable<Vendor>(vendors);
        mockVendorSet.As<IAsyncEnumerable<Vendor>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockQueryable.GetAsyncEnumerator());
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.Provider).Returns(((IQueryable<Vendor>)mockQueryable).Provider);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.Expression).Returns(((IQueryable<Vendor>)mockQueryable).Expression);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.ElementType).Returns(((IQueryable<Vendor>)mockQueryable).ElementType);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.GetEnumerator()).Returns(((IQueryable<Vendor>)mockQueryable).GetEnumerator());

        _dbContextMock.Setup(c => c.Vendors).Returns(mockVendorSet.Object);

        var command = new CreateVendorProductCommand(Guid.NewGuid(), Guid.NewGuid(), 100m, null, null, 10, 1, null, null, null);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ─── Vendor Not Active ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenVendorNotActive_ShouldThrowBusinessRuleException()
    {
        // Arrange — vendor exists but status is Pending (not Active)
        var vendor = new Vendor(Guid.NewGuid(), "متجر", "Store", "desc", "123", "vendor@test.com", "01011111111");
        var vendorList = new List<Vendor> { vendor }.AsQueryable();
        var mockVendorSet = new Mock<DbSet<Vendor>>();
        var mockQueryable = new TestAsyncEnumerable<Vendor>(vendorList);
        mockVendorSet.As<IAsyncEnumerable<Vendor>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockQueryable.GetAsyncEnumerator());
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.Provider).Returns(((IQueryable<Vendor>)mockQueryable).Provider);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.Expression).Returns(((IQueryable<Vendor>)mockQueryable).Expression);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.ElementType).Returns(((IQueryable<Vendor>)mockQueryable).ElementType);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.GetEnumerator()).Returns(((IQueryable<Vendor>)mockQueryable).GetEnumerator());

        _dbContextMock.Setup(c => c.Vendors).Returns(mockVendorSet.Object);

        var command = new CreateVendorProductCommand(vendor.Id, Guid.NewGuid(), 100m, null, null, 10, 1, null, null, null);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(e => e.ErrorCode == "VENDOR_NOT_VERIFIED");
    }

    // ─── Master Product Not Found ──────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMasterProductNotFound_ShouldThrowNotFoundException()
    {
        // Arrange — vendor is active, but master product not found
        var vendor = CreateVendor(VendorStatus.Active);
        var vendorList = new List<Vendor> { vendor }.AsQueryable();
        var mockVendorSet = new Mock<DbSet<Vendor>>();
        var mockQueryable = new TestAsyncEnumerable<Vendor>(vendorList);
        mockVendorSet.As<IAsyncEnumerable<Vendor>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(mockQueryable.GetAsyncEnumerator());
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.Provider).Returns(((IQueryable<Vendor>)mockQueryable).Provider);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.Expression).Returns(((IQueryable<Vendor>)mockQueryable).Expression);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.ElementType).Returns(((IQueryable<Vendor>)mockQueryable).ElementType);
        mockVendorSet.As<IQueryable<Vendor>>().Setup(m => m.GetEnumerator()).Returns(((IQueryable<Vendor>)mockQueryable).GetEnumerator());

        _dbContextMock.Setup(c => c.Vendors).Returns(mockVendorSet.Object);

        var masterProducts = Array.Empty<MasterProduct>().AsQueryable();
        var mockMasterProductSet = new Mock<DbSet<MasterProduct>>();
        mockMasterProductSet.As<IQueryable<MasterProduct>>().Setup(m => m.Provider).Returns(masterProducts.Provider);
        mockMasterProductSet.As<IQueryable<MasterProduct>>().Setup(m => m.Expression).Returns(masterProducts.Expression);
        mockMasterProductSet.As<IQueryable<MasterProduct>>().Setup(m => m.ElementType).Returns(masterProducts.ElementType);
        mockMasterProductSet.As<IQueryable<MasterProduct>>().Setup(m => m.GetEnumerator()).Returns(masterProducts.GetEnumerator());
        _dbContextMock.Setup(c => c.MasterProducts).Returns(mockMasterProductSet.Object);

        var command = new CreateVendorProductCommand(vendor.Id, Guid.NewGuid(), 100m, null, null, 10, 1, null, null, null);
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}

// ─── Helpers for async EF Core queries ─────────────────────────────────────

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
