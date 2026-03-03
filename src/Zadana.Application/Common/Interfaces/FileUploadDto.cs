namespace Zadana.Application.Common.Interfaces;

public record FileUploadDto(
    string FileName,
    string ContentType,
    Stream ContentStream);
