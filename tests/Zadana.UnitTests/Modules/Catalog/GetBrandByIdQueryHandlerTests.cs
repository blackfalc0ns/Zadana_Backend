using System.Globalization;
using FluentAssertions;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandById;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;

namespace Zadana.UnitTests.Modules.Catalog;

public class GetBrandByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsLocalizedBrandDetails()
    {
        using var scope = new CultureScope("ar");
        await using var context = TestDbContextFactory.Create();

        var brand = new Brand("\u0627\u0644\u0645\u0631\u0627\u0639\u064A", "Almarai", "almarai.png");
        context.Brands.Add(brand);
        await context.SaveChangesAsync();

        var handler = new GetBrandByIdQueryHandler(context);

        var result = await handler.Handle(new GetBrandByIdQuery(brand.Id), CancellationToken.None);

        result.Id.Should().Be(brand.Id);
        result.Name.Should().Be("\u0627\u0644\u0645\u0631\u0627\u0639\u064A");
        result.Logo.Should().Be("almarai.png");
        result.ProductCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenBrandMissingOrInactive_ThrowsNotFound()
    {
        using var scope = new CultureScope("en");
        await using var context = TestDbContextFactory.Create();

        var inactiveBrand = new Brand("inactive-ar", "Inactive", null);
        inactiveBrand.Deactivate();
        context.Brands.Add(inactiveBrand);
        await context.SaveChangesAsync();

        var handler = new GetBrandByIdQueryHandler(context);

        var missing = () => handler.Handle(new GetBrandByIdQuery(Guid.NewGuid()), CancellationToken.None);
        var inactive = () => handler.Handle(new GetBrandByIdQuery(inactiveBrand.Id), CancellationToken.None);

        await missing.Should().ThrowAsync<NotFoundException>();
        await inactive.Should().ThrowAsync<NotFoundException>();
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture;
        private readonly CultureInfo _originalUiCulture;

        public CultureScope(string cultureName)
        {
            _originalCulture = CultureInfo.CurrentCulture;
            _originalUiCulture = CultureInfo.CurrentUICulture;

            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
