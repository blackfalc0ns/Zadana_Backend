using MediatR;

namespace Zadana.Application.Common.Interfaces;

/// <summary>
/// Marker interface for domain events dispatched via MediatR after SaveChangesAsync.
/// Implement this on records/classes that represent something that happened in the domain.
/// </summary>
public interface IDomainEvent : INotification { }
