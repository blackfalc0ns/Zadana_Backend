using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Zadana.Api.Modules.Orders.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.DTOs;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Payments.Entities;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Orders;

public class OrdersControllerRefundStatusTests
{
    [Fact]
    public async Task GetRefundStatus_ReturnsLatestRefund_WhenOrderHasRefundWithoutSupportCase()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var customer = new User("Customer User", "refund.customer@test.com", "01000000000", UserRole.Customer);
        var order = CreateOrder(customer.Id);
        var payment = new Payment(order.Id, PaymentMethodType.Card, order.TotalAmount);
        payment.MarkAsPaid();

        var refund = new Refund(payment.Id, 42m, "Manual refund", costBearer: "Platform");
        refund.Process();

        dbContext.Users.Add(customer);
        dbContext.Orders.Add(order);
        dbContext.Payments.Add(payment);
        dbContext.Refunds.Add(refund);
        await dbContext.SaveChangesAsync();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.UserId).Returns(customer.Id);

        var orderReadService = new Mock<IOrderReadService>();
        orderReadService
            .Setup(x => x.GetCustomerOrderSupportCasesAsync(order.Id, customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var controller = new OrdersController(
            currentUserService.Object,
            orderReadService.Object,
            Mock.Of<IOrderSupportCaseWorkflowService>(),
            dbContext);

        var result = await controller.GetRefundStatus(order.Id, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CustomerRefundStatusResponse>().Subject;
        response.HasActiveCase.Should().BeFalse();
        response.RequestedAmount.Should().Be(42m);
        response.ApprovedAmount.Should().Be(42m);
        response.RefundMethod.Should().Be("same_method");
        response.CompensationType.Should().Be("cash_refund");
        response.SettlementStatus.Should().Be("cash_refunded");
        response.RefundStatus.Should().Be("approved");
        response.RefundLifecycleStatus.Should().Be("succeeded");
    }

    [Fact]
    public async Task GetRefundStatus_ReturnsLatestCase_WhenOrderHasNonReturnCaseWithoutRefund()
    {
        await using var dbContext = TestDbContextFactory.Create();
        var customer = new User("Customer User", "case.customer@test.com", "01000000001", UserRole.Customer);
        var order = CreateOrder(customer.Id);

        dbContext.Users.Add(customer);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var currentUserService = new Mock<ICurrentUserService>();
        currentUserService.SetupGet(x => x.UserId).Returns(customer.Id);

        var supportCase = CreateSupportCaseDto(
            order.Id,
            "driver_dispute",
            "resolved",
            "Driver dispute resolved.");

        var orderReadService = new Mock<IOrderReadService>();
        orderReadService
            .Setup(x => x.GetCustomerOrderSupportCasesAsync(order.Id, customer.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([supportCase]);

        var controller = new OrdersController(
            currentUserService.Object,
            orderReadService.Object,
            Mock.Of<IOrderSupportCaseWorkflowService>(),
            dbContext);

        var result = await controller.GetRefundStatus(order.Id, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<CustomerRefundStatusResponse>().Subject;
        response.HasActiveCase.Should().BeFalse();
        response.CaseStatus.Should().Be("resolved");
        response.CaseType.Should().Be("driver_dispute");
        response.RefundStatus.Should().Be("resolved");
        response.CustomerNote.Should().Be("Driver dispute resolved.");
        response.RequestedAmount.Should().BeNull();
        response.RefundLifecycleStatus.Should().BeNull();
    }

    private static Order CreateOrder(Guid userId) =>
        new(
            "ORD-REFUND",
            userId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            PaymentMethodType.Card,
            100m,
            0m,
            10m,
            10m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            5m);

    private static OrderSupportCaseDto CreateSupportCaseDto(
        Guid orderId,
        string type,
        string status,
        string message) =>
        new(
            Guid.NewGuid(),
            orderId,
            type,
            "Financial dispute",
            status,
            "Resolved",
            "finance",
            "Finance",
            "medium",
            "Medium",
            "payout_dispute",
            "Financial dispute",
            message,
            null,
            null,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            "driver",
            "Driver",
            null,
            null,
            [],
            [],
            [],
            [],
            []);
}
