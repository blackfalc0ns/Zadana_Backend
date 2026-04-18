using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Checkout.Commands.PlaceCheckoutOrder;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Tests.Helpers;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Infrastructure.Modules.Orders.Repositories;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Checkout;

public class PlaceCheckoutOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCashOrderPlaced_ShouldPersistPendingVendorAcceptanceHistoryWithoutConcurrencyFailure()
    {
        using var database = new SqliteCheckoutDatabase();
        await using var dbContext = database.CreateContext();

        var customer = new User("Checkout Customer", "checkout.customer@test.com", "01000000030", UserRole.Customer);
        var vendorUser = new User("Checkout Vendor", "checkout.vendor@test.com", "01000000031", UserRole.Vendor);
        var category = new Category("إلكترونيات", "Electronics");
        var product = new MasterProduct("جراب آيفون", "Transparent iPhone Case", "transparent-iphone-case-test", category.Id);
        product.Publish();

        var vendor = new Zadana.Domain.Modules.Vendors.Entities.Vendor(
            vendorUser.Id,
            "متجر الاختبار",
            "Checkout Test Store",
            "Electronics",
            "1234567890",
            "checkout.vendor@test.com",
            "01000000031");
        vendor.Approve(10m, Guid.NewGuid());
        vendor.UpdateOperationsSettings(true, null, 30);
        vendor.UpdateNotificationSettings(true, false, true);

        var vendorProduct = new VendorProduct(vendor.Id, product.Id, 49m, 10);
        var address = new CustomerAddress(customer.Id, "Checkout Customer", "01000000030", "Nasr City 12", AddressLabel.Home, city: "Cairo");
        address.SetAsDefault();

        var cart = new Cart(customer.Id);
        cart.Items.Add(new CartItem(cart.Id, product.Id, product.NameEn, 1));
        cart.UpdateTotals(49m, 0m);

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Categories.Add(category);
        dbContext.MasterProducts.Add(product);
        dbContext.Vendors.Add(vendor);
        dbContext.VendorProducts.Add(vendorProduct);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var orderRepository = new OrderRepository(dbContext);
        var placeOrderHandler = new PlaceOrderCommandHandler(orderRepository, TestLocalizer.Create<SharedResource>(), dbContext);
        var sender = new SenderProxy(type =>
        {
            if (type == typeof(IRequestHandler<PlaceOrderCommand, Guid>))
            {
                return placeOrderHandler;
            }

            throw new InvalidOperationException($"Unsupported handler: {type.FullName}");
        });

        var publisherMock = new Mock<IPublisher>();
        publisherMock
            .Setup(publisher => publisher.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisherMock
            .Setup(publisher => publisher.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new PlaceCheckoutOrderCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            sender,
            dbContext,
            publisherMock.Object);

        var result = await handler.Handle(
            new PlaceCheckoutOrderCommand(
                customer.Id,
                vendor.Id,
                address.Id,
                null,
                "cash",
                null,
                "checkout regression"),
            CancellationToken.None);

        result.Order.Status.Should().Be("processing");

        var savedOrder = await dbContext.Orders
            .Include(order => order.StatusHistory)
            .SingleAsync();

        savedOrder.Status.Should().Be(OrderStatus.PendingVendorAcceptance);
        savedOrder.StatusHistory
            .Select(history => history.NewStatus)
            .Should()
            .ContainInOrder(OrderStatus.Placed, OrderStatus.PendingVendorAcceptance);
    }

    private sealed class SenderProxy : ISender
    {
        private readonly Func<Type, object> _resolver;

        public SenderProxy(Func<Type, object> resolver)
        {
            _resolver = resolver;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
            dynamic handler = _resolver(handlerType);
            return handler.Handle((dynamic)request, cancellationToken);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            var requestInterface = request.GetType()
                .GetInterfaces()
                .First(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IRequest<>));
            var responseType = requestInterface.GetGenericArguments()[0];
            var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), responseType);
            dynamic handler = _resolver(handlerType);
            return handler.Handle((dynamic)request, cancellationToken);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return (Task)Send((object)request, cancellationToken);
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SqliteCheckoutDatabase : IDisposable
    {
        private readonly SqliteConnection _rootConnection;
        private readonly string _connectionString;

        public SqliteCheckoutDatabase()
        {
            _connectionString = $"Data Source=zadana-checkout-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _rootConnection = new SqliteConnection(_connectionString);
            _rootConnection.Open();

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public ApplicationDbContext CreateContext()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            return new ApplicationDbContext(options, new AuditableEntityInterceptor());
        }

        public void Dispose()
        {
            _rootConnection.Dispose();
        }
    }
}
