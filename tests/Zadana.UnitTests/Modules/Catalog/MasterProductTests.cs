using FluentAssertions;
using Zadana.Domain.Modules.Catalog.Entities;

namespace Zadana.UnitTests.Modules.Catalog;

public class MasterProductTests
{
    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var nameAr = "منتج تجريبي";
        var nameEn = "Test Product";

        // Act
        var product = new MasterProduct(nameAr, nameEn, categoryId);

        // Assert
        product.NameAr.Should().Be(nameAr);
        product.NameEn.Should().Be(nameEn);
        product.CategoryId.Should().Be(categoryId);
        product.Images.Should().BeEmpty();
    }

    [Fact]
    public void AddImage_ShouldAddImageToCollection()
    {
        // Arrange
        var product = new MasterProduct("ار", "en", Guid.NewGuid());
        var url = "https://example.com/image.png";
        var altText = "Alt Text";

        // Act
        product.AddImage(url, altText, 1, true);

        // Assert
        product.Images.Should().HaveCount(1);
        var image = product.Images.First();
        image.Url.Should().Be(url);
        image.AltText.Should().Be(altText);
        image.DisplayOrder.Should().Be(1);
        image.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void ClearImages_ShouldEmptyCollection()
    {
        // Arrange
        var product = new MasterProduct("ار", "en", Guid.NewGuid());
        product.AddImage("url1");
        product.AddImage("url2");

        // Act
        product.ClearImages();

        // Assert
        product.Images.Should().BeEmpty();
    }
}
