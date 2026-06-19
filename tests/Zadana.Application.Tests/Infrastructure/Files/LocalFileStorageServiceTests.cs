using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Zadana.Application.Common.Interfaces;
using Zadana.Infrastructure.Modules.Files.Services;
using Zadana.Infrastructure.Settings;
using Zadana.SharedKernel.Exceptions;

namespace Zadana.Application.Tests.Infrastructure.Files;

public sealed class LocalFileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "zadana-local-media-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task UploadAsync_Png_ConvertsToResizedWebpAndReturnsPublicUrl()
    {
        var service = CreateService(maxWidth: 320, maxHeight: 320);
        await using var source = CreatePng(width: 640, height: 480);

        var url = await service.UploadAsync(
            new FileUploadDto("product.png", "image/png", source),
            "uploads/catalog/products");

        url.Should().StartWith("https://media.zadna0.com/uploads/catalog/products/");
        url.Should().EndWith(".webp");

        var outputPath = Path.Combine(
            _root,
            "uploads",
            "catalog",
            "products",
            Path.GetFileName(new Uri(url).LocalPath));
        File.Exists(outputPath).Should().BeTrue();

        using var converted = SKBitmap.Decode(outputPath);
        converted.Should().NotBeNull();
        converted.Width.Should().Be(320);
        converted.Height.Should().Be(240);
    }

    [Fact]
    public async Task UploadAsync_Pdf_PreservesFileAndDeleteAsyncRemovesIt()
    {
        var service = CreateService();
        await using var source = new MemoryStream("%PDF-1.7 test"u8.ToArray());

        var url = await service.UploadAsync(
            new FileUploadDto("document.pdf", "application/pdf", source),
            "uploads/vendors/licenses");

        url.Should().EndWith(".pdf");
        var outputPath = Path.Combine(
            _root,
            "uploads",
            "vendors",
            "licenses",
            Path.GetFileName(new Uri(url).LocalPath));
        var savedContent = await File.ReadAllTextAsync(outputPath);
        savedContent.Should().Be("%PDF-1.7 test");

        await service.DeleteAsync(url);

        File.Exists(outputPath).Should().BeFalse();
    }

    [Fact]
    public async Task UploadAsync_OptimizedWebpWithinLimits_PreservesOriginalBytes()
    {
        var service = CreateService();
        await using var source = CreateWebp(800, 600);
        var expectedBytes = source.ToArray();

        var url = await service.UploadAsync(
            new FileUploadDto("product.webp", "image/webp", source),
            "uploads/catalog/products");

        var outputPath = Path.Combine(
            _root,
            "uploads",
            "catalog",
            "products",
            Path.GetFileName(new Uri(url).LocalPath));

        (await File.ReadAllBytesAsync(outputPath)).Should().Equal(expectedBytes);
    }

    [Fact]
    public async Task UploadAsync_PathTraversal_IsRejected()
    {
        var service = CreateService();
        await using var source = CreatePng(10, 10);

        var action = () => service.UploadAsync(
            new FileUploadDto("image.png", "image/png", source),
            "../outside");

        await action.Should().ThrowAsync<BadRequestException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private LocalFileStorageService CreateService(int maxWidth = 2000, int maxHeight = 2000)
    {
        var settings = Options.Create(new FileStorageSettings
        {
            Provider = "Local",
            Local = new LocalMediaStorageSettings
            {
                RootPath = _root,
                PublicBaseUrl = "https://media.zadna0.com",
                ConvertImagesToWebp = true,
                WebpQuality = 82,
                MaxWidth = maxWidth,
                MaxHeight = maxHeight,
                MaxPixelCount = 40_000_000,
                MaxConcurrentImageProcessors = 2
            }
        });

        return new LocalFileStorageService(
            settings,
            new TestHostEnvironment(_root),
            NullLogger<LocalFileStorageService>.Instance);
    }

    private static MemoryStream CreatePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(encoded.ToArray());
    }

    private static MemoryStream CreateWebp(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 82);
        return new MemoryStream(encoded.ToArray());
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Zadana.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
