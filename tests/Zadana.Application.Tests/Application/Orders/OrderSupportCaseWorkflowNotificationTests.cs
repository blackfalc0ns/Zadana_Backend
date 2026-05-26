using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Modules.Orders.Services;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.Domain.Modules.Delivery.Enums;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Social.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Infrastructure.Persistence;
using Zadana.Infrastructure.Persistence.Interceptors;

namespace Zadana.Application.Tests.Application.Orders;

public class OrderSupportCaseWorkflowNotificationTests
{
    [Fact]
    public async Task CreateCustomerCaseAsync_WhenComplaint_ShouldNotifyAdminSupportAndVendorSupportView()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.support.case@test.com", "01000000201", UserRole.Customer);
        var vendorUser = new User("Vendor User", "vendor.support.case@test.com", "01000000202", UserRole.Vendor);
        var vendor = CreateVendor(vendorUser.Id);
        var order = CreateOrder(customer.Id, vendor.Id, OrderStatus.Delivered, "ORD-SUPPORT-001");

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = CreateNotificationServiceMock();
        var adminAlertServiceMock = CreateAdminAlertServiceMock();
        var workflowService = CreateWorkflowService(
            dbContext,
            notificationServiceMock,
            adminAlertServiceMock);

        var supportCase = await workflowService.CreateCustomerCaseAsync(
            order.Id,
            customer.Id,
            "complaint",
            null,
            "Order item arrived damaged.",
            null,
            CancellationToken.None);

        adminAlertServiceMock.Verify(
            service => service.SendAsync(
                It.Is<AdminAlertRequest>(request =>
                    request.Type == AdminAlertTypes.SupportCreated &&
                    request.Category == AdminAlertCategories.Support &&
                    request.TargetUrl == "/notifications?category=support"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                vendorUser.Id,
                It.IsAny<string>(),
                It.Is<string>(title => title.Contains("support", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("support", StringComparison.OrdinalIgnoreCase)),
                NotificationTypes.OrderSupportCaseChanged,
                supportCase.Id,
                It.Is<string>(data =>
                    data.Contains("\"type\":\"complaint\"", StringComparison.Ordinal) &&
                    data.Contains($"/support?view=support&legacyCaseId={supportCase.Id}", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddAdminPublicMessageAsync_WhenAudienceIncludesDriver_ShouldKeepDriverSupportNotificationContract()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.driver.case@test.com", "01000000203", UserRole.Customer);
        var driverUser = new User("Driver User", "driver.case@test.com", "01000000204", UserRole.Driver);
        var driver = new Driver(driverUser.Id, DriverVehicleType.Car, "3234567890", "LIC-2001");
        var vendor = CreateVendor(Guid.NewGuid());
        var order = CreateOrder(customer.Id, vendor.Id, OrderStatus.DriverAssigned, "ORD-DRIVER-CASE-001");
        var supportCase = new OrderSupportCase(
            order.Id,
            driverUser.Id,
            OrderSupportCaseType.DriverDispute,
            OrderSupportCasePriority.High,
            OrderSupportCaseQueue.DriverOps,
            "pickup_issue",
            "Driver reported a pickup issue.",
            initiatorRole: "driver");
        var assignment = new DeliveryAssignment(order.Id, 0m);
        assignment.OfferTo(driver.Id, 1, DateTime.UtcNow.AddMinutes(5));
        assignment.Accept();

        dbContext.Users.AddRange(customer, driverUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Drivers.Add(driver);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        dbContext.DeliveryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = CreateNotificationServiceMock();
        var workflowService = CreateWorkflowService(dbContext, notificationServiceMock);

        await workflowService.AddAdminPublicMessageAsync(
            supportCase.Id,
            Guid.NewGuid(),
            "Please share your pickup update.",
            "driver",
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                driverUser.Id,
                It.Is<NotificationDispatchRequest>(request =>
                    request.Type == NotificationTypes.OrderSupportCaseChanged &&
                    request.Category == NotificationCategories.Support &&
                    request.Data != null &&
                    request.Data.Contains("\"screen\":\"support_case_detail\"", StringComparison.Ordinal) &&
                    request.Data.Contains("\"event\":\"support.admin_message\"", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notificationServiceMock.Verify(
            service => service.SendDriverSupportCaseChangedToUserAsync(
                driverUser.Id,
                supportCase.Id,
                null,
                order.Id,
                order.OrderNumber,
                "driver_dispute",
                "in_review",
                "admin_message",
                $"/orders/{order.Id}/cases/{supportCase.Id}",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_WhenCustomerSupportCaseStatusChanges_ShouldMarkNotificationAsPopup()
    {
        await using var dbContext = CreateDbContext();
        var customer = new User("Customer User", "customer.popup.case@test.com", "01000000206", UserRole.Customer);
        var vendorUser = new User("Vendor User", "vendor.popup.case@test.com", "01000000207", UserRole.Vendor);
        var vendor = CreateVendor(vendorUser.Id);
        var order = CreateOrder(customer.Id, vendor.Id, OrderStatus.Delivered, "ORD-POPUP-001");
        var supportCase = new OrderSupportCase(
            order.Id,
            customer.Id,
            OrderSupportCaseType.Complaint,
            OrderSupportCasePriority.Medium,
            OrderSupportCaseQueue.Support,
            "damaged_item",
            "Order item arrived damaged.",
            initiatorRole: "customer");

        dbContext.Users.AddRange(customer, vendorUser);
        dbContext.Vendors.Add(vendor);
        dbContext.Orders.Add(order);
        dbContext.OrderSupportCases.Add(supportCase);
        await dbContext.SaveChangesAsync();

        var notificationServiceMock = CreateNotificationServiceMock();
        var workflowService = CreateWorkflowService(dbContext, notificationServiceMock);

        await workflowService.ResolveAsync(
            supportCase.Id,
            Guid.NewGuid(),
            "Resolved by support.",
            CancellationToken.None);

        notificationServiceMock.Verify(
            service => service.SendToUserAsync(
                customer.Id,
                It.IsAny<string>(),
                It.Is<string>(title => title.Contains("resolved", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                NotificationTypes.OrderSupportCaseChanged,
                supportCase.Id,
                It.Is<string>(data =>
                    data.Contains("\"presentation\":\"popup\"", StringComparison.Ordinal) &&
                    data.Contains("\"popupType\":\"support_case_status_update\"", StringComparison.Ordinal) &&
                    data.Contains("\"showPopup\":true", StringComparison.Ordinal) &&
                    data.Contains("\"screen\":\"support_case_detail\"", StringComparison.Ordinal) &&
                    data.Contains("\"eventName\":\"support.resolved\"", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static OrderSupportCaseWorkflowService CreateWorkflowService(
        ApplicationDbContext dbContext,
        Mock<INotificationService> notificationServiceMock,
        Mock<IAdminAlertService>? adminAlertServiceMock = null)
    {
        var pushServiceMock = new Mock<IOneSignalPushService>();
        pushServiceMock
            .Setup(service => service.SendMobileNotificationAsync(
                It.IsAny<OneSignalMobilePushRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OneSignalPushDispatchResult(true, true, false, 200, "push-id", null));

        return new OrderSupportCaseWorkflowService(
            dbContext,
            dbContext,
            notificationServiceMock.Object,
            pushServiceMock.Object,
            adminAlertServiceMock?.Object);
    }

    private static Mock<INotificationService> CreateNotificationServiceMock()
    {
        var notificationServiceMock = new Mock<INotificationService>();
        notificationServiceMock
            .Setup(service => service.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationDispatchRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notificationServiceMock
            .Setup(service => service.SendToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notificationServiceMock
            .Setup(service => service.SendOrderSupportCaseChangedToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notificationServiceMock
            .Setup(service => service.SendDriverSupportCaseChangedToUserAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        notificationServiceMock
            .Setup(service => service.SendDriverHomeUpdatedAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return notificationServiceMock;
    }

    private static Mock<IAdminAlertService> CreateAdminAlertServiceMock()
    {
        var adminAlertServiceMock = new Mock<IAdminAlertService>();
        adminAlertServiceMock
            .Setup(service => service.SendAsync(
                It.IsAny<AdminAlertRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminAlertDispatchResult(
                RecipientCount: 1,
                SignalRSuccessCount: 1,
                PushResult: new OneSignalPushDispatchResult(true, true, false, 200, "push-id", null)));
        return adminAlertServiceMock;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new AuditableEntityInterceptor());
    }

    private static Vendor CreateVendor(Guid userId) =>
        new(
            userId,
            "Test Vendor Arabic",
            "Test Vendor",
            "grocery",
            $"CR-{Guid.NewGuid():N}"[..12],
            "vendor.support@test.com",
            "01000000205");

    private static Order CreateOrder(Guid userId, Guid vendorId, OrderStatus status, string orderNumber)
    {
        var order = new Order(orderNumber, userId, vendorId, Guid.NewGuid(), PaymentMethodType.Card, 120m, 0m, 15m, 15m, 0m, 0m, null, null, null, 0m, 0m, 0m, 0m, null, null, false, null, null, null, null, 1, false, 5m);
        order.Items.Add(new OrderItem(order.Id, Guid.NewGuid(), Guid.NewGuid(), "Support Item", 1, 120m));

        if (status != OrderStatus.PendingPayment)
        {
            order.ChangeStatus(status);
        }

        return order;
    }
}
