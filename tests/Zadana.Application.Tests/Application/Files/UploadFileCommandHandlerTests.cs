using FluentAssertions;
using Microsoft.Extensions.Localization;
using Moq;
using Zadana.Application.Common.Interfaces;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Files.Commands.UploadFile;
using Zadana.Application.Tests.Helpers;

namespace Zadana.Application.Tests.Application.Files;

public class UploadFileCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidWebpImage_UploadsFile()
    {
        var storage = new Mock<IFileStorageService>();
        storage
            .Setup(x => x.UploadAsync(It.IsAny<FileUploadDto>(), "uploads/catalog/categories", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://ik.imagekit.io/test/category.webp");

        var handler = CreateHandler(storage.Object);
        var file = new FileUploadDto(
            "category.webp",
            "image/webp",
            new MemoryStream([0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20]));

        var result = await handler.Handle(new UploadFileCommand("uploads/catalog/categories", file), CancellationToken.None);

        result.Should().Be("https://ik.imagekit.io/test/category.webp");
        storage.Verify(x => x.UploadAsync(
            It.Is<FileUploadDto>(dto => dto.FileName == "category.webp" && dto.ContentType == "image/webp"),
            "uploads/catalog/categories",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static UploadFileCommandHandler CreateHandler(IFileStorageService storage)
    {
        return new UploadFileCommandHandler(
            storage,
            new Mock<IApplicationDbContext>().Object,
            new Mock<ICurrentUserService>().Object,
            TestLocalizer.Create<SharedResource>());
    }
}
