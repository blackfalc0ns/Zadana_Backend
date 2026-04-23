using MediatR;
using Zadana.Application.Common.Interfaces;
using Zadana.Domain.Modules.Delivery.Entities;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Modules.Delivery.Commands.AddDriverNote;

public record AddDriverNoteCommand(Guid DriverId, Guid AuthorUserId, string Message) : IRequest<Guid>;

public class AddDriverNoteCommandHandler : IRequestHandler<AddDriverNoteCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AddDriverNoteCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(AddDriverNoteCommand request, CancellationToken cancellationToken)
    {
        var driverExists = await _context.Drivers.FindAsync([request.DriverId], cancellationToken)
            ?? throw new NotFoundException("Driver", request.DriverId);

        var note = new DriverNote(request.DriverId, request.AuthorUserId, request.Message);
        _context.DriverNotes.Add(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return note.Id;
    }
}
