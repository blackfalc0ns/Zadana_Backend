using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Catalog.Events;

/// <summary>Raised when a MasterProduct is soft-deleted.</summary>
public record MasterProductDeletedEvent(
    Guid ProductId,
    string NameAr,
    string NameEn,
    Guid? BrandId,
    Guid CategoryId,
    DateTime DeletedAtUtc) : IDomainEvent;

/// <summary>Raised when a MasterProduct's status changes.</summary>
public record MasterProductStatusChangedEvent(
    Guid ProductId,
    string NameAr,
    string NameEn,
    string OldStatus,
    string NewStatus) : IDomainEvent;

/// <summary>Raised when a Brand is soft-deleted.</summary>
public record BrandDeletedEvent(
    Guid BrandId,
    string NameAr,
    string NameEn,
    DateTime DeletedAtUtc) : IDomainEvent;

/// <summary>Raised when a Category is soft-deleted.</summary>
public record CategoryDeletedEvent(
    Guid CategoryId,
    string NameAr,
    string NameEn,
    DateTime DeletedAtUtc) : IDomainEvent;
