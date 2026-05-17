using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Orders.Commands.AddToCart;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Orders;

public class AddToCartCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Fact]
    public async Task Handle_WhenProductNotFound_ShouldThrowNotFoundException()
    {
        await using var dbContext = CreateDbContext();
        _orderRepositoryMock
            .Setup(repository => repository.GetMasterProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MasterProduct?)null);

        var command = new AddToCartCommand(Guid.NewGuid(), Guid.NewGuid(), 2);
        var handler = CreateHandler(dbContext);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCartItemExists_ShouldIncreaseQuantity()
    {
        await using var dbContext = CreateDbContext();

        var userId = Guid.NewGuid();
        var vendor = new Vendor(
            Guid.NewGuid(),
            "متجر",
            "Store",
            "Groceries",
            "123456",
            "vendor@test.com",
            "0500000000");
        vendor.Approve(10m, Guid.NewGuid());

        var masterProduct = new MasterProduct("Name Ar", "Name En", "name-en", Guid.NewGuid());
        masterProduct.Publish();
        var vendorProduct = new VendorProduct(vendor.Id, masterProduct.Id, 10m, 10);

        dbContext.Vendors.Add(vendor);
        dbContext.MasterProducts.Add(masterProduct);
        dbContext.VendorProducts.Add(vendorProduct);
        await dbContext.SaveChangesAsync();

        var cart = new Zadana.Domain.Modules.Orders.Entities.Cart(userId);
        cart.Items.Add(new Zadana.Domain.Modules.Orders.Entities.CartItem(cart.Id, masterProduct.Id, masterProduct.NameEn, 1));

        _orderRepositoryMock
            .Setup(repository => repository.GetMasterProductAsync(masterProduct.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(masterProduct);
        _orderRepositoryMock
            .Setup(repository => repository.GetCartAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var command = new AddToCartCommand(userId, masterProduct.Id, 2);
        var handler = CreateHandler(dbContext);

        var cartId = await handler.Handle(command, CancellationToken.None);

        cartId.Should().Be(cart.Id);
        cart.Items.Should().ContainSingle();
        cart.Items.Single().Quantity.Should().Be(3);
        _unitOfWorkMock.Verify(unitOfWork => unitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private AddToCartCommandHandler CreateHandler(IApplicationDbContext context) =>
        new(context, _orderRepositoryMock.Object, TestLocalizer.Create<SharedResource>(), _unitOfWorkMock.Object);

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
