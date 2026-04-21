using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Modules.Payments.Commands.RetryPaymobPayment;
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
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Payments;

public class RetryPaymobPaymentCommandHandlerTests
{
    [Fact]
    public async Task Handle_ShouldReuseExistingOrderAndPayment_WhenRetryIsAllowed()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var address = CreateAddress(user.Id);
        var order = CreateOrder(user.Id, address.Id);
        order.UpdatePaymentStatus(PaymentStatus.Failed);
        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        payment.MarkAsFailed("first attempt failed", "provider-old");

        dbContext.Users.Add(user);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var gatewayMock = new Mock<IPaymobGateway>();
        gatewayMock.SetupGet(x => x.IsEnabled).Returns(true);
        gatewayMock
            .Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<PaymobCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymobCheckoutSessionDto("provider-new", "token-new", "https://paymob/retry"));

        var handler = new RetryPaymobPaymentCommandHandler(dbContext, gatewayMock.Object, dbContext);

        var result = await handler.Handle(new RetryPaymobPaymentCommand(order.Id, user.Id), CancellationToken.None);

        result.Payment.Id.Should().Be(payment.Id);
        result.Payment.Status.Should().Be("pending");
        result.Payment.IframeUrl.Should().Be("https://paymob/retry");
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.ProviderTransactionId.Should().Be("provider-new");
        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.PaymentStatus.Should().Be(PaymentStatus.Pending);
        (await dbContext.Payments.CountAsync(x => x.OrderId == order.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRejectRetry_WhenOrderAlreadyVisibleToVendor()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var address = CreateAddress(user.Id);
        var order = CreateOrder(user.Id, address.Id);
        order.UpdatePaymentStatus(PaymentStatus.Paid);
        order.ChangeStatus(OrderStatus.PendingVendorAcceptance);
        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        payment.MarkAsPaid("provider-paid");

        dbContext.Users.Add(user);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var handler = new RetryPaymobPaymentCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(gateway => gateway.IsEnabled == true),
            dbContext);

        var act = () => handler.Handle(new RetryPaymobPaymentCommand(order.Id, user.Id), CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "ORDER_PAYMENT_RETRY_NOT_ALLOWED");
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static User CreateUser() =>
        new("Retry Customer", "retry.customer@test.com", "01000000012", UserRole.Customer);

    private static CustomerAddress CreateAddress(Guid userId) =>
        new(userId, "Retry Customer", "01000000012", "Primary address", AddressLabel.Home, city: "Cairo", area: "Nasr City");

    private static Order CreateOrder(Guid userId, Guid addressId)
    {
        var order = new Order("ORD-RETRY-001", userId, Guid.NewGuid(), addressId, PaymentMethodType.Card, 100m, 0m, 10m, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Retry Item", 2, 50m));
        return order;
    }
}
