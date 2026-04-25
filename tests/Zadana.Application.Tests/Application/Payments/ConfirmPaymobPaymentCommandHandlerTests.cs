using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Events;
using Zadana.Application.Modules.Payments.Commands.ConfirmPaymobPayment;
using Zadana.Application.Modules.Payments.Commands.ProcessPaymobWebhook;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Payments;

public class ConfirmPaymobPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenFallbackConfirmsPayment_ShouldClearCart_MoveOrderToPendingVendorAcceptance_AndPublishNotification()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        var cart = CreateCart(user.Id);

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var gatewayMock = new Mock<IPaymobGateway>();
        var publisherMock = CreatePublisherMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ConfirmPaymobPaymentCommandHandler(dbContext, gatewayMock.Object, unitOfWorkMock.Object, publisherMock.Object);

        var result = await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, null, payment.ProviderTransactionId, "txn-paid-1", true, false, null),
            CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Paid);
        order.Status.Should().Be(OrderStatus.PendingVendorAcceptance);
        (await dbContext.Carts.AnyAsync(item => item.Id == cart.Id)).Should().BeFalse();
        result.PaymentStatus.Should().Be("paid");
        result.OrderStatus.Should().Be("pending_vendor_acceptance");
        result.AlreadyConfirmed.Should().BeFalse();
        unitOfWorkMock.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(
            publisher => publisher.Publish(
                It.Is<OrderStatusChangedNotification>(notification =>
                    notification.OrderId == order.Id &&
                    notification.OldStatus == OrderStatus.PendingPayment &&
                    notification.NewStatus == OrderStatus.PendingVendorAcceptance &&
                    notification.NotifyVendor),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDeviceIdProvided_ShouldClearGuestCartForSameDeviceToo()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        var userCart = CreateCart(user.Id);
        var guestCart = CreateGuestCart("device-100");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Carts.AddRange(userCart, guestCart);
        await dbContext.SaveChangesAsync();

        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            CreateUnitOfWorkMock().Object,
            CreatePublisherMock().Object);

        await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, null, payment.ProviderTransactionId, "txn-paid-2", true, false, "device-100"),
            CancellationToken.None);

        (await dbContext.Carts.AnyAsync(item => item.Id == userCart.Id)).Should().BeFalse();
        (await dbContext.Carts.AnyAsync(item => item.Id == guestCart.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenPaymentCarriesCheckoutDeviceId_WebhookConfirmationShouldClearGuestCartToo()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        payment.SetCheckoutDeviceId("device-200");
        var guestCart = CreateGuestCart("device-200");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Carts.Add(guestCart);
        await dbContext.SaveChangesAsync();

        var gatewayMock = new Mock<IPaymobGateway>();
        gatewayMock
            .Setup(gateway => gateway.ParseWebhookNotification("payload-success"))
            .Returns(new PaymobWebhookNotificationDto(
                payment.Id,
                payment.ProviderTransactionId,
                "txn-webhook-200",
                true,
                false,
                "TRANSACTION"));

        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            gatewayMock.Object,
            CreateUnitOfWorkMock().Object,
            CreatePublisherMock().Object);

        await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, "payload-success", null, null, null, null, null),
            CancellationToken.None);

        (await dbContext.Carts.AnyAsync(item => item.Id == guestCart.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenPaymentAlreadyPaidButOrderStillPlaced_ShouldCompleteOperationalTransition()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        payment.MarkAsPaid("txn-paid-legacy");
        order.ChangeStatus(OrderStatus.Placed, null, "legacy placed after payment");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var publisherMock = CreatePublisherMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            unitOfWorkMock.Object,
            publisherMock.Object);

        var result = await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, null, payment.ProviderTransactionId, payment.ProviderTransactionId, true, false, null),
            CancellationToken.None);

        order.Status.Should().Be(OrderStatus.PendingVendorAcceptance);
        result.AlreadyConfirmed.Should().BeFalse();
        unitOfWorkMock.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(
            publisher => publisher.Publish(
                It.Is<OrderStatusChangedNotification>(notification =>
                    notification.OldStatus == OrderStatus.Placed &&
                    notification.NewStatus == OrderStatus.PendingVendorAcceptance),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPaymentAlreadyConfirmed_ShouldBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        payment.MarkAsPaid("txn-paid-confirmed");
        order.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "already confirmed");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var publisherMock = CreatePublisherMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            unitOfWorkMock.Object,
            publisherMock.Object);

        var result = await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, null, payment.ProviderTransactionId, payment.ProviderTransactionId, true, false, null),
            CancellationToken.None);

        result.AlreadyConfirmed.Should().BeTrue();
        unitOfWorkMock.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPaymentFails_ShouldMarkPaymentAsFailedWithoutClearingCart()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        var cart = CreateCart(user.Id);

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var publisherMock = CreatePublisherMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            unitOfWorkMock.Object,
            publisherMock.Object);

        var result = await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, null, payment.ProviderTransactionId, "txn-failed", false, false, null),
            CancellationToken.None);

        payment.Status.Should().Be(PaymentStatus.Failed);
        order.Status.Should().Be(OrderStatus.PendingPayment);
        dbContext.Entry(cart).State.Should().Be(EntityState.Unchanged);
        result.PaymentStatus.Should().Be("failed");
        unitOfWorkMock.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WebhookHandler_ShouldResolvePaymentByWebhookPayload_AndDelegateToSharedConfirmationFlow()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        payment.SetProviderTransactionId("provider-order-100");

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var gatewayMock = new Mock<IPaymobGateway>();
        gatewayMock
            .Setup(gateway => gateway.ParseWebhookNotification("payload-success"))
            .Returns(new PaymobWebhookNotificationDto(
                null,
                "provider-order-100",
                "txn-webhook-100",
                true,
                false,
                "TRANSACTION"));

        var publisherMock = CreatePublisherMock();
        var unitOfWorkMock = CreateUnitOfWorkMock();
        var confirmHandler = new ConfirmPaymobPaymentCommandHandler(dbContext, gatewayMock.Object, unitOfWorkMock.Object, publisherMock.Object);
        var processHandler = new ProcessPaymobWebhookCommandHandler(
            new SenderProxy(type =>
            {
                if (type == typeof(IRequestHandler<ConfirmPaymobPaymentCommand, PaymobPaymentConfirmationResultDto>))
                {
                    return confirmHandler;
                }

                throw new InvalidOperationException($"Unsupported handler: {type.FullName}");
            }));

        var result = await processHandler.Handle(new ProcessPaymobWebhookCommand("payload-success"), CancellationToken.None);

        result.Status.Should().Be("paid");
        order.Status.Should().Be(OrderStatus.PendingVendorAcceptance);
        unitOfWorkMock.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenPaymentIdMissing_ShouldResolvePaymentByProviderTransactionId()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        var cart = CreateCart(user.Id);

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            CreateUnitOfWorkMock().Object,
            CreatePublisherMock().Object);

        var result = await handler.Handle(
            new ConfirmPaymobPaymentCommand(
                null,
                null,
                null,
                payment.ProviderTransactionId,
                true,
                false,
                null),
            CancellationToken.None);

        result.PaymentId.Should().Be(payment.Id);
        result.PaymentStatus.Should().Be("paid");
        result.OrderStatus.Should().Be("pending_vendor_acceptance");
        (await dbContext.Carts.AnyAsync(item => item.Id == cart.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenConcurrentRequestAlreadyCompletedVendorPlacement_ShouldStayIdempotentWithoutRepublishing()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var dbContext = CreateDbContext(databaseName, databaseRoot);
        var user = CreateUser();
        var order = CreateOrder(user.Id);
        var payment = CreatePendingCardPayment(order);
        var cart = CreateCart(user.Id);

        dbContext.Users.Add(user);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync();

        var saveAttempt = 0;
        var publisherMock = CreatePublisherMock();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock
            .Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async cancellationToken =>
            {
                saveAttempt++;
                if (saveAttempt == 1)
                {
                    await using var parallelContext = CreateDbContext(databaseName, databaseRoot);
                    var parallelPayment = await parallelContext.Payments
                        .Include(item => item.Order)
                        .FirstAsync(item => item.Id == payment.Id, cancellationToken);
                    var parallelOrder = parallelPayment.Order;

                    parallelPayment.MarkAsPaid("txn-paid-race-complete");
                    parallelOrder.ChangeStatus(OrderStatus.PendingVendorAcceptance, null, "Concurrent confirmation completed vendor placement");

                    var parallelCarts = await parallelContext.Carts
                        .Include(item => item.Items)
                        .Where(item => item.UserId == user.Id)
                        .ToListAsync(cancellationToken);

                    parallelContext.CartItems.RemoveRange(parallelCarts.SelectMany(item => item.Items));
                    parallelContext.Carts.RemoveRange(parallelCarts);
                    await parallelContext.SaveChangesAsync(cancellationToken);

                    throw new DbUpdateConcurrencyException("Simulated concurrent confirmation.");
                }

                return 0;
            });

        var handler = new ConfirmPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(),
            unitOfWorkMock.Object,
            publisherMock.Object);

        var result = await handler.Handle(
            new ConfirmPaymobPaymentCommand(payment.Id, null, payment.ProviderTransactionId, "txn-paid-race-complete", true, false, null),
            CancellationToken.None);

        result.AlreadyConfirmed.Should().BeTrue();
        result.PaymentStatus.Should().Be("paid");
        result.OrderStatus.Should().Be("pending_vendor_acceptance");
        saveAttempt.Should().Be(1);
        publisherMock.Verify(
            publisher => publisher.Publish(It.IsAny<OrderStatusChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IPublisher> CreatePublisherMock()
    {
        var publisherMock = new Mock<IPublisher>();
        publisherMock
            .Setup(publisher => publisher.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisherMock
            .Setup(publisher => publisher.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return publisherMock;
    }

    private static Mock<IUnitOfWork> CreateUnitOfWorkMock()
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock
            .Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return unitOfWorkMock;
    }

    private static ApplicationDbContext CreateDbContext(string? databaseName = null, InMemoryDatabaseRoot? databaseRoot = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), databaseRoot ?? new InMemoryDatabaseRoot())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateUser() =>
        new("Payment Customer", "payment.customer@test.com", "01000000010", UserRole.Customer);

    private static Order CreateOrder(Guid userId) =>
        new("ORD-PAY-001", userId, Guid.NewGuid(), Guid.NewGuid(), PaymentMethodType.Card, 100m, 0m, 10m, 10m, 0m, 0m, null, null, null, 5m);

    private static Payment CreatePendingCardPayment(Order order)
    {
        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        payment.MarkAsPending("Paymob", "provider-order-1");
        return payment;
    }

    private static Cart CreateCart(Guid userId)
    {
        var cart = new Cart(userId);
        cart.UpdateTotals(110m, 10m);
        return cart;
    }

    private static Cart CreateGuestCart(string deviceId)
    {
        var cart = new Cart(null, deviceId);
        cart.UpdateTotals(110m, 10m);
        return cart;
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

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
