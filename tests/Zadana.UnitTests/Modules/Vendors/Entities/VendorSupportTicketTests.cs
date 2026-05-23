using FluentAssertions;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.Domain.Modules.Vendors.Enums;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.UnitTests.Modules.Vendors.Entities;

public class VendorSupportTicketTests
{
    [Fact]
    public void Constructor_CreatesGeneralVendorTicketWithInitialMessage()
    {
        var vendorId = Guid.NewGuid();
        var vendorUserId = Guid.NewGuid();

        var ticket = new VendorSupportTicket(
            vendorId,
            vendorUserId,
            "SUP-20260523-ABC123",
            "Need help with catalog",
            "products",
            VendorSupportTicketPriority.Medium,
            "Product upload is failing.");

        ticket.VendorId.Should().Be(vendorId);
        ticket.OrderId.Should().BeNull();
        ticket.Status.Should().Be(VendorSupportTicketStatus.Open);
        ticket.Messages.Should().ContainSingle();
        ticket.Messages.Single().AuthorRole.Should().Be("vendor");
        ticket.LastMessagePreview.Should().Be("Product upload is failing.");
    }

    [Fact]
    public void Constructor_AllowsOptionalLinkedOrder()
    {
        var orderId = Guid.NewGuid();

        var ticket = new VendorSupportTicket(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SUP-20260523-DEF456",
            "Order payout question",
            "finance",
            VendorSupportTicketPriority.High,
            "Please check this order payout.",
            orderId);

        ticket.OrderId.Should().Be(orderId);
    }

    [Fact]
    public void AddAdminMessage_MarksTicketWaitingForVendorAndRecordsFirstResponse()
    {
        var ticket = CreateTicket();

        ticket.AddAdminMessage(Guid.NewGuid(), "Please send the invoice number.");

        ticket.Status.Should().Be(VendorSupportTicketStatus.WaitingVendor);
        ticket.FirstResponseAtUtc.Should().NotBeNull();
        ticket.Messages.Should().HaveCount(2);
        ticket.Messages.Last().AuthorRole.Should().Be("admin");
    }

    [Fact]
    public void AddVendorMessage_ReopensWaitingTicketToInProgress()
    {
        var ticket = CreateTicket();
        ticket.AddAdminMessage(Guid.NewGuid(), "Please send the invoice number.");

        ticket.AddVendorMessage(Guid.NewGuid(), "Invoice number is INV-10.");

        ticket.Status.Should().Be(VendorSupportTicketStatus.InProgress);
        ticket.Messages.Should().HaveCount(3);
        ticket.Messages.Last().AuthorRole.Should().Be("vendor");
    }

    [Fact]
    public void AddVendorMessage_WhenResolved_ThrowsBusinessRuleException()
    {
        var ticket = CreateTicket();
        ticket.SetStatus(VendorSupportTicketStatus.Resolved);

        var act = () => ticket.AddVendorMessage(Guid.NewGuid(), "Can I add more?");

        act.Should().Throw<BusinessRuleException>()
            .Where(ex => ex.ErrorCode == "VENDOR_SUPPORT_TICKET_CLOSED");
    }

    private static VendorSupportTicket CreateTicket() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SUP-20260523-ABC123",
            "Need help",
            "general",
            VendorSupportTicketPriority.Medium,
            "Initial message.");
}
