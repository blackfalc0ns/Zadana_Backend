using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Zadana.Api.Modules.Marketing.Controllers;
using Zadana.Api.Modules.Marketing.Requests;
using Zadana.Application.Modules.Marketing.Commands.FeaturedPlacements;
using Zadana.Application.Modules.Marketing.Commands.HomeBanners;
using Zadana.Application.Modules.Marketing.Commands.HomeContentSections;
using Zadana.Application.Modules.Marketing.Commands.HomeSections;
using Zadana.Application.Modules.Marketing.DTOs;

namespace Zadana.UnitTests.Modules.Marketing;

public class AdminMarketingControllersTests
{
    private readonly Mock<ISender> _senderMock = new();

    [Fact]
    public async Task CreateBanner_ReturnsOkResult()
    {
        var controller = CreateBannersController();
        var dto = new HomeBannerAdminDto(Guid.NewGuid(), "tagAr", "tagEn", "titleAr", "titleEn", null, null, null, null, "/banner.jpg", 1, true, null, null, DateTime.UtcNow, DateTime.UtcNow);
        _senderMock.Setup(x => x.Send(It.IsAny<CreateHomeBannerCommand>(), default)).ReturnsAsync(dto);

        var result = await controller.CreateBanner(new CreateHomeBannerRequest("tagAr", "tagEn", "titleAr", "titleEn", null, null, null, null, "/banner.jpg", 1, null, null));

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task CreateFeaturedPlacement_ReturnsOkResult()
    {
        var controller = CreateFeaturedController();
        var dto = new FeaturedProductPlacementDto(Guid.NewGuid(), "VendorProduct", Guid.NewGuid(), null, "Milk", "Milk", 1, true, null, null, null, DateTime.UtcNow, DateTime.UtcNow);
        _senderMock.Setup(x => x.Send(It.IsAny<CreateFeaturedProductPlacementCommand>(), default)).ReturnsAsync(dto);

        var result = await controller.CreatePlacement(new CreateFeaturedPlacementRequest("VendorProduct", dto.VendorProductId, null, 1, null, null, null));

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task CreateHomeSection_ReturnsOkResult()
    {
        var controller = CreateHomeSectionsController();
        var dto = new HomeSectionAdminDto(Guid.NewGuid(), Guid.NewGuid(), "فواكه", "Fruits", "theme1", 1, 8, true, null, null, DateTime.UtcNow, DateTime.UtcNow);
        _senderMock.Setup(x => x.Send(It.IsAny<CreateHomeSectionCommand>(), default)).ReturnsAsync(dto);

        var result = await controller.CreateSection(new CreateHomeSectionRequest(dto.CategoryId, dto.Theme, dto.DisplayOrder, dto.ProductsTake, null, null));

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(dto);
    }

    [Fact]
    public async Task UpdateHomeContentSectionSetting_ReturnsOkResult()
    {
        var controller = CreateHomeContentSectionsController();
        var dto = new HomeContentSectionSettingDto("Banners", false);
        _senderMock.Setup(x => x.Send(It.IsAny<UpdateHomeContentSectionSettingCommand>(), default)).ReturnsAsync(dto);

        var result = await controller.UpdateSetting("Banners", new UpdateHomeContentSectionSettingRequest(false));

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(dto);
    }

    private AdminMarketingBannersController CreateBannersController()
    {
        var controller = new AdminMarketingBannersController();
        AttachSender(controller);
        return controller;
    }

    private AdminMarketingFeaturedProductsController CreateFeaturedController()
    {
        var controller = new AdminMarketingFeaturedProductsController();
        AttachSender(controller);
        return controller;
    }

    private AdminMarketingHomeSectionsController CreateHomeSectionsController()
    {
        var controller = new AdminMarketingHomeSectionsController();
        AttachSender(controller);
        return controller;
    }

    private AdminMarketingHomeContentSectionsController CreateHomeContentSectionsController()
    {
        var controller = new AdminMarketingHomeContentSectionsController();
        AttachSender(controller);
        return controller;
    }

    private void AttachSender(ControllerBase controller)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_senderMock.Object);
        var provider = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
            {
                RequestServices = provider
            }
        };
    }
}
