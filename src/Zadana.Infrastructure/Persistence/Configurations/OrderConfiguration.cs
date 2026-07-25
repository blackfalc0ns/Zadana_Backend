using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;

namespace Zadana.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.PlacedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Orders_UserId_PlacedAt_Desc");
        builder.HasIndex(x => new { x.UserId, x.Status, x.PlacedAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Orders_UserId_Status_PlacedAt_Desc");
        builder.HasIndex(x => new { x.VendorId, x.PlacedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Orders_VendorId_PlacedAt_Desc");
        builder.HasIndex(x => new { x.VendorId, x.Status, x.PlacedAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Orders_VendorId_Status_PlacedAt_Desc");
        builder.HasIndex(x => new { x.VendorId, x.VendorBranchId, x.PlacedAtUtc })
            .IsDescending(false, false, true)
            .HasDatabaseName("IX_Orders_VendorId_BranchId_PlacedAt_Desc");
        builder.HasIndex(x => new { x.Status, x.PlacedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Orders_Status_PlacedAt_Desc");
        builder.HasIndex(x => new { x.PaymentStatus, x.PlacedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Orders_PaymentStatus_PlacedAt_Desc");

        builder.Property(x => x.Fulfillment).HasConversion<string>().HasMaxLength(20).IsRequired().HasDefaultValue(FulfillmentType.Delivery);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(50).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.CustomerAddressId).IsRequired(false);

        builder.Property(x => x.Subtotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DiscountTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DeliveryFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BaseDeliveryFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DistanceDeliveryFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.SurgeDeliveryFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.QuotedDistanceKm).HasPrecision(18, 2);
        builder.Property(x => x.DeliveryPricingMode).HasMaxLength(50);
        builder.Property(x => x.DeliveryPricingRuleLabel).HasMaxLength(150);
        builder.Property(x => x.DriverToVendorDistanceKm).HasPrecision(18, 2);
        builder.Property(x => x.VendorToCustomerDistanceKm).HasPrecision(18, 2);
        builder.Property(x => x.DriverToVendorFee).HasPrecision(18, 2);
        builder.Property(x => x.VendorToCustomerFee).HasPrecision(18, 2);
        builder.Property(x => x.DriverToVendorPricingSource).HasMaxLength(50);
        builder.Property(x => x.VendorToCustomerPricingSource).HasMaxLength(50);
        builder.Property(x => x.PricingOriginType).HasMaxLength(50);
        builder.Property(x => x.DeliveryQuoteStatus).HasMaxLength(50);
        builder.Property(x => x.ActualAssignedDriverPickupDistanceKm).HasPrecision(18, 2);
        builder.Property(x => x.ActualDispatchDeviationPercent).HasPrecision(18, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.VatAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.CodFee).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2).IsRequired();

        // Revised SAR-only financial snapshot.
        builder.Property(x => x.ProductGross).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.ProductNet).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.VendorCommissionAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.DriverCommissionAmount).HasPrecision(18, 2).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired().HasDefaultValue("SAR");
        builder.Property(x => x.PricingMode).HasMaxLength(16).IsRequired().HasDefaultValue("live");
        builder.Property(x => x.TaxPolicySnapshot);
        builder.Property(x => x.CommissionPolicySnapshot);

        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.PickupOtpCode).HasMaxLength(10);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => new { x.Fulfillment, x.Status, x.ReadyForPickupAtUtc })
            .HasDatabaseName("IX_Orders_Fulfillment_Status_ReadyForPickup");
        builder.HasIndex(x => new { x.Fulfillment, x.Status, x.PickupNoShowDeadlineUtc })
            .HasDatabaseName("IX_Orders_Fulfillment_Status_NoShowDeadline");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Vendor)
            .WithMany()
            .HasForeignKey(x => x.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VendorBranch)
            .WithMany()
            .HasForeignKey(x => x.VendorBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistory)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Complaints)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.SupportCases)
            .WithOne(x => x.Order)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}
