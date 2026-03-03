using MediatR;
using Zadana.Application.Common.Interfaces;

namespace Zadana.Application.Modules.Files.Commands.UploadFile;

public record UploadFileCommand(
    string Directory,
    FileUploadDto File) : IRequest<string>;
