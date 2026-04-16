using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Modules.Orders.Commands.PlaceOrder;
using Zadana.Application.Modules.Payments.Commands.StartPaymobCheckout;
using Zadana.Application.Modules.Payments.DTOs;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Marketing.Entities;
using Zadana.Domain.Modules.Marketing.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Application.Payments;

public class StartPaymobCheckoutCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCardCheckoutSucceeds_ShouldCreatePendingPaymentAndReturnIframeUrl()
    {
        await using var dbContext = CreateDbContext();
        var user = new User("Paymob User", "paymob@test.com", "01000000000", UserRole.Customer);
        var address = new CustomerAddress(user.Id, "Paymob User", "01000000000", "Nasr City 10", AddressLabel.Home, city: "Cairo");
        var order = new Order("ORD-TEST-001", user.Id, Guid.NewGuid(), address.Id, PaymentMethodType.Card, 100m, 0m, 10m, 5m);

        dbContext.Users.Add(user);
        dbContext.CustomerAddresses.Add(address);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(x => x.Send(It.IsAny<PlaceOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(order.Id);

        var gatewayMock = new Mock<IPaymobGateway>();
        gatewayMock.SetupGet(x => x.IsEnabled).Returns(true);
        gatewayMock
            .Setup(x => x.CreateCheckoutSessionAsync(It.IsAny<PaymobCheckoutSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymobCheckoutSessionDto("12345", "token-123", "https://accept.paymob.com/api/acceptance/iframes/1?payment_token=token-123"));

        var handler = new StartPaymobCheckoutCommandHandler(dbContext, gatewayMock.Object, senderMock.Object, dbContext);

        var result = await handler.Handle(
            new StartPaymobCheckoutCommand(user.Id, order.VendorId, address.Id, "card", null, null, null),
            CancellationToken.None);

        result.Payment.IframeUrl.Should().Contain("payment_token=token-123");
        var payment = await dbContext.Payments.SingleAsync();
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.ProviderName.Should().Be("Paymob");
        payment.ProviderTransactionId.Should().Be("12345");
    }

    [Fact]
    public async Task Handle_WhenGatewayDisabled_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var handler = new StartPaymobCheckoutCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(x => x.IsEnabled == false),
            Mock.Of<ISender>(),
            dbContext);

        var act = () => handler.Handle(
            new StartPaymobCheckoutCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "card", null, null, null),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "PAYMENT_UNAVAILABLE");
    }

    [Fact]
    public async Task Handle_WhenPromoCodeDoesNotApplyToVendor_ShouldThrowBusinessRuleException()
    {
        await using var dbContext = CreateDbContext();
        var coupon = new Coupon("SAVE10", "Save 10", CouponDiscountType.Fixed, 10m);
        dbContext.Coupons.Add(coupon);
        dbContext.CouponVendors.Add(new Zadana.Domain.Modules.Marketing.Entities.CouponVendor(coupon.Id, Guid.NewGuid()));
        await dbContext.SaveChangesAsync();

        var handler = new StartPaymobCheckoutCommandHandler(
            dbContext,
            Mock.Of<IPaymobGateway>(x => x.IsEnabled == true),
            Mock.Of<ISender>(),
            dbContext);

        var act = () => handler.Handle(
            new StartPaymobCheckoutCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "card", null, null, "SAVE10"),
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<BusinessRuleException>()
            .Where(x => x.ErrorCode == "PROMO_CODE_NOT_APPLICABLE");
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }
}
