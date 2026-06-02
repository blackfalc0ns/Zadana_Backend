using System.Globalization;
using System.Resources;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Zadana.Api.Modules.Identity.Controllers;
using Zadana.Api.Modules.Identity.Requests;
using Zadana.Api.Modules.Orders.Controllers;
using Zadana.Api.Modules.Orders.Requests;
using Zadana.Api.Modules.Social.Controllers;
using Zadana.Application.Common.Localization;
using Zadana.Application.Modules.Checkout.DTOs;
using Zadana.Application.Modules.Checkout.Queries.GetCheckoutSummary;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandById;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandFilters;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetBrandProducts;
using Zadana.Application.Modules.Catalog.Queries.Brands.GetCustomerBrands;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryFilters;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategoryProducts;
using Zadana.Application.Modules.Catalog.Queries.Categories.GetCategorySubcategories;
using Zadana.Application.Modules.Catalog.Queries.Products.GetProductDetails;
using Zadana.Application.Modules.Catalog.Queries.Products.SearchProducts;
using Zadana.Application.Modules.Delivery.Interfaces;
using Zadana.Application.Modules.Favorites.Commands;
using Zadana.Application.Modules.Favorites.Queries;
using Zadana.Application.Modules.Identity.Commands.ForgotPassword;
using Zadana.Application.Modules.Identity.Commands.ResetPassword;
using Zadana.Application.Modules.Orders.Commands.AddCartItem;
using Zadana.Application.Modules.Orders.Commands.CancelCustomerOrder;
using Zadana.Application.Modules.Orders.Commands.ClearCart;
using Zadana.Application.Modules.Orders.Commands.DeleteCustomerOrder;
using Zadana.Application.Modules.Orders.Commands.RemoveCartItem;
using Zadana.Application.Modules.Orders.Commands.UpdateCartItemQuantity;
using Zadana.Application.Modules.Orders.Interfaces;
using Zadana.Application.Modules.Orders.Queries.GetCart;
using Zadana.Application.Modules.Orders.Queries.GetCartVendors;
using Zadana.Application.Modules.Orders.Support;
using Zadana.Application.Modules.Payments.Interfaces;
using Zadana.Application.Modules.Social.Commands;
using Zadana.Application.Modules.Social.Queries;
using Zadana.Domain.Modules.Catalog.Entities;
using Zadana.Domain.Modules.Identity.Entities;
using Zadana.Domain.Modules.Identity.Enums;
using Zadana.Domain.Modules.Orders.Entities;
using Zadana.Domain.Modules.Orders.Enums;
using Zadana.Domain.Modules.Payments.Enums;
using Zadana.Domain.Modules.Vendors.Entities;
using Zadana.SharedKernel.Exceptions;
using Zadana.UnitTests.Common;
using Zadana.UnitTests.TestHelpers;

namespace Zadana.UnitTests.Modules.Localization;

public class CustomerEndpointLocalizationTests
{
    private static readonly ResourceManager SharedResourceManager =
        new("Zadana.Application.Common.Localization.SharedResource", typeof(SharedResource).Assembly);

    private const string CategoryAr = "\u0623\u0644\u0628\u0627\u0646";
    private const string CategoryEn = "Dairy";
    private const string ProductAr = "\u062D\u0644\u064A\u0628 \u0637\u0627\u0632\u062C";
    private const string ProductEn = "Fresh Milk";
    private const string ProductDescriptionAr = "\u0648\u0635\u0641 \u0639\u0631\u0628\u064A";
    private const string ProductDescriptionEn = "English description";
    private const string BrandAr = "\u0627\u0644\u0645\u0631\u0627\u0639\u064A";
    private const string BrandEn = "Almarai";
    private const string UnitAr = "\u0644\u062A\u0631";
    private const string UnitEn = "Liter";
    private const string StoreAr = "\u0645\u062A\u062C\u0631 \u0627\u0644\u062D\u0644\u064A\u0628";
    private const string StoreEn = "Milk Store";

    [Fact]
    public async Task CatalogCustomerEndpoints_ReturnLocalizedNames_ForEnglishAndArabicCultures()
    {
        await using var englishContext = TestDbContextFactory.Create();
        var english = await SeedCatalogAsync(englishContext);

        using (new CultureScope("en"))
        {
            var result = await ReadCatalogAsync(englishContext, english);

            result.CustomerBrand.Should().Be(BrandEn);
            result.BrandDetails.Should().Be(BrandEn);
            result.BrandFilter.Should().Be(BrandEn);
            result.CategoryFilter.Should().Be(CategoryEn);
            result.CategoryProduct.Should().Be(ProductEn);
            result.BrandProduct.Should().Be(ProductEn);
            result.SearchProduct.Should().Be(ProductEn);
            result.ProductDetailsName.Should().Be(ProductEn);
            result.ProductDetailsStore.Should().Be(StoreEn);
            result.ProductDetailsUnit.Should().Be(UnitEn);
            result.ProductDetailsDescription.Should().Be(ProductDescriptionEn);
            result.SubcategoryName.Should().Be(ProductEn);
        }

        await using var arabicContext = TestDbContextFactory.Create();
        var arabic = await SeedCatalogAsync(arabicContext);

        using (new CultureScope("ar"))
        {
            var result = await ReadCatalogAsync(arabicContext, arabic);

            result.CustomerBrand.Should().Be(BrandAr);
            result.BrandDetails.Should().Be(BrandAr);
            result.BrandFilter.Should().Be(BrandAr);
            result.CategoryFilter.Should().Be(CategoryAr);
            result.CategoryProduct.Should().Be(ProductAr);
            result.BrandProduct.Should().Be(ProductAr);
            result.SearchProduct.Should().Be(ProductAr);
            result.ProductDetailsName.Should().Be(ProductAr);
            result.ProductDetailsStore.Should().Be(StoreAr);
            result.ProductDetailsUnit.Should().Be(UnitAr);
            result.ProductDetailsDescription.Should().Be(ProductDescriptionAr);
            result.SubcategoryName.Should().Be(ProductAr);
        }
    }

    [Fact]
    public async Task CartCustomerEndpoints_ReturnLocalizedProductVendorAndMessages()
    {
        await using var englishContext = TestDbContextFactory.Create();
        var english = await SeedCatalogAsync(englishContext);

        using (new CultureScope("en"))
        {
            var cart = new Cart(english.UserId);
            cart.Items.Add(new CartItem(cart.Id, english.Product.Id, english.Product.NameEn, 1));
            englishContext.Carts.Add(cart);
            await englishContext.SaveChangesAsync();

            var cartDto = await new GetCartQueryHandler(englishContext)
                .Handle(new GetCartQuery(CartActor.Create(english.UserId, null), null), CancellationToken.None);
            var vendors = await new GetCartVendorsQueryHandler(englishContext)
                .Handle(new GetCartVendorsQuery(CartActor.Create(english.UserId, null)), CancellationToken.None);
            var clear = await new ClearCartCommandHandler(englishContext, NullLogger<ClearCartCommandHandler>.Instance)
                .Handle(new ClearCartCommand(CartActor.Create(english.UserId, null)), CancellationToken.None);

            cartDto.Items[0].Name.Should().Be(ProductEn);
            cartDto.Items[0].Unit.Should().Be(UnitEn);
            cartDto.Items[0].VendorPrices[0].Name.Should().Be(StoreEn);
            vendors.Vendors[0].Name.Should().Be(StoreEn);
            clear.Message.Should().Be(LocalizedMessages.GetEn(LocalizedMessages.CartCleared));
        }

        await using var arabicContext = TestDbContextFactory.Create();
        var arabic = await SeedCatalogAsync(arabicContext);

        using (new CultureScope("ar"))
        {
            var add = await new AddCartItemCommandHandler(arabicContext, NullLogger<AddCartItemCommandHandler>.Instance)
                .Handle(new AddCartItemCommand(CartActor.Create(arabic.UserId, null), arabic.Product.Id, 1), CancellationToken.None);
            var cartDto = await new GetCartQueryHandler(arabicContext)
                .Handle(new GetCartQuery(CartActor.Create(arabic.UserId, null), null), CancellationToken.None);
            var vendors = await new GetCartVendorsQueryHandler(arabicContext)
                .Handle(new GetCartVendorsQuery(CartActor.Create(arabic.UserId, null)), CancellationToken.None);
            var update = await new UpdateCartItemQuantityCommandHandler(arabicContext, NullLogger<UpdateCartItemQuantityCommandHandler>.Instance)
                .Handle(new UpdateCartItemQuantityCommand(CartActor.Create(arabic.UserId, null), cartDto.Items[0].Id, 2), CancellationToken.None);
            var remove = await new RemoveCartItemCommandHandler(arabicContext, NullLogger<RemoveCartItemCommandHandler>.Instance)
                .Handle(new RemoveCartItemCommand(CartActor.Create(arabic.UserId, null), update.Item.Id), CancellationToken.None);

            add.Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.CartItemAdded));
            update.Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.CartItemUpdated));
            remove.Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.CartItemRemoved));
            cartDto.Items[0].Name.Should().Be(ProductAr);
            cartDto.Items[0].Unit.Should().Be(UnitAr);
            cartDto.Items[0].VendorPrices[0].Name.Should().Be(StoreAr);
            vendors.Vendors[0].Name.Should().Be(StoreAr);
        }
    }

    [Fact]
    public async Task FavoritesCustomerEndpoints_ReturnLocalizedProductVendorAndMessages()
    {
        await using var englishContext = TestDbContextFactory.Create();
        var english = await SeedCatalogAsync(englishContext);
        var localizer = new CultureAwareLocalizer();

        using (new CultureScope("en"))
        {
            var add = await new AddFavoriteCommandHandler(englishContext, TestServiceFactory.CreateCacheInvalidator(), localizer)
                .Handle(new AddFavoriteCommand(english.UserId, null, english.Product.Id), CancellationToken.None);
            var list = await new GetFavoritesQueryHandler(englishContext, localizer)
                .Handle(new GetFavoritesQuery(english.UserId, null), CancellationToken.None);
            var remove = await new RemoveFavoriteCommandHandler(englishContext, TestServiceFactory.CreateCacheInvalidator(), localizer)
                .Handle(new RemoveFavoriteCommand(english.UserId, null, english.Product.Id), CancellationToken.None);

            add.Message.Should().Be(GetSharedResource("FavoriteAddedSuccessfully"));
            remove.Message.Should().Be(GetSharedResource("FavoriteRemovedSuccessfully"));
            add.Item!.Name.Should().Be(ProductEn);
            add.Item.Store.Should().Be(StoreEn);
            add.Item.Unit.Should().Be(UnitEn);
            list.Items[0].Name.Should().Be(ProductEn);
        }

        await using var arabicContext = TestDbContextFactory.Create();
        var arabic = await SeedCatalogAsync(arabicContext);

        using (new CultureScope("ar"))
        {
            var add = await new AddFavoriteCommandHandler(arabicContext, TestServiceFactory.CreateCacheInvalidator(), localizer)
                .Handle(new AddFavoriteCommand(arabic.UserId, null, arabic.Product.Id), CancellationToken.None);
            var list = await new GetFavoritesQueryHandler(arabicContext, localizer)
                .Handle(new GetFavoritesQuery(arabic.UserId, null), CancellationToken.None);
            var clear = await new ClearFavoritesCommandHandler(arabicContext, TestServiceFactory.CreateCacheInvalidator(), localizer)
                .Handle(new ClearFavoritesCommand(arabic.UserId, null), CancellationToken.None);

            add.Message.Should().Be(GetSharedResource("FavoriteAddedSuccessfully"));
            clear.Message.Should().Be(GetSharedResource("FavoritesClearedSuccessfully"));
            add.Item!.Name.Should().Be(ProductAr);
            add.Item.Store.Should().Be(StoreAr);
            add.Item.Unit.Should().Be(UnitAr);
            list.Items[0].Name.Should().Be(ProductAr);
        }
    }

    [Fact]
    public async Task CheckoutCustomerEndpoints_ReturnLocalizedSummaryAndMessages()
    {
        await using var englishContext = TestDbContextFactory.Create();
        var english = await SeedCatalogAsync(englishContext);
        await SeedCheckoutAsync(englishContext, english);

        using (new CultureScope("en"))
        {
            var summary = await CreateCheckoutSummaryHandler(englishContext)
                .Handle(new GetCheckoutSummaryQuery(english.UserId, null, null, null, "cash"), CancellationToken.None);

            summary.Cart.Items[0].Name.Should().Be(ProductEn);
            summary.Cart.Items[0].Unit.Should().Be(UnitEn);
            summary.DeliverySlots[0].Label.Should().Be("30-45 minutes");
            summary.PaymentMethods.First(x => x.Code == "cash").Label.Should().Be("Cash on Delivery");
            summary.ShippingBreakdown.First(x => x.Code == "base_delivery").Label.Should().Be("Base delivery");
            CreateApplyPromoCodeResult().Message.Should().Be(LocalizedMessages.GetEn(LocalizedMessages.PromoCodeApplied));
            CreateRemovePromoCodeResult().Message.Should().Be(LocalizedMessages.GetEn(LocalizedMessages.PromoCodeRemoved));
            CreatePlaceOrderResult().Message.Should().Be(LocalizedMessages.GetEn(LocalizedMessages.OrderPlacedSuccess));
        }

        await using var arabicContext = TestDbContextFactory.Create();
        var arabic = await SeedCatalogAsync(arabicContext);
        await SeedCheckoutAsync(arabicContext, arabic);

        using (new CultureScope("ar"))
        {
            var summary = await CreateCheckoutSummaryHandler(arabicContext)
                .Handle(new GetCheckoutSummaryQuery(arabic.UserId, null, null, null, "cash"), CancellationToken.None);

            summary.Cart.Items[0].Name.Should().Be(ProductAr);
            summary.Cart.Items[0].Unit.Should().Be(UnitAr);
            ContainsArabic(summary.DeliverySlots[0].Label).Should().BeTrue();
            ContainsArabic(summary.PaymentMethods.First(x => x.Code == "cash").Label).Should().BeTrue();
            ContainsArabic(summary.ShippingBreakdown.First(x => x.Code == "base_delivery").Label).Should().BeTrue();
            CreateApplyPromoCodeResult().Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.PromoCodeApplied));
            CreateRemovePromoCodeResult().Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.PromoCodeRemoved));
            CreatePlaceOrderResult().Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.OrderPlacedSuccess));
        }
    }

    [Fact]
    public async Task OrderMutationCustomerEndpoints_ReturnLocalizedMessages()
    {
        await using var englishContext = TestDbContextFactory.Create();
        var english = await SeedCatalogAsync(englishContext);

        using (new CultureScope("en"))
        {
            var cancelOrder = CreateOrder(english.UserId, english.VendorId);
            cancelOrder.ChangeStatus(OrderStatus.PendingVendorAcceptance);
            var deleteOrder = CreateOrder(english.UserId, english.VendorId);
            englishContext.Orders.AddRange(cancelOrder, deleteOrder);
            await englishContext.SaveChangesAsync();

            var cancel = await new CancelCustomerOrderCommandHandler(englishContext, englishContext, new NoOpPublisher())
                .Handle(new CancelCustomerOrderCommand(cancelOrder.Id, english.UserId, "changed_my_mind", null, null), CancellationToken.None);
            var delete = await new DeleteCustomerOrderCommandHandler(englishContext, englishContext)
                .Handle(new DeleteCustomerOrderCommand(deleteOrder.Id, english.UserId), CancellationToken.None);

            cancel.Message.Should().Be(LocalizedMessages.GetEn(LocalizedMessages.OrderCancelledSuccess));
            delete.Message.Should().Be(LocalizedMessages.GetEn(LocalizedMessages.OrderDeletedSuccess));
        }

        await using var arabicContext = TestDbContextFactory.Create();
        var arabic = await SeedCatalogAsync(arabicContext);

        using (new CultureScope("ar"))
        {
            var cancelOrder = CreateOrder(arabic.UserId, arabic.VendorId);
            cancelOrder.ChangeStatus(OrderStatus.PendingVendorAcceptance);
            var deleteOrder = CreateOrder(arabic.UserId, arabic.VendorId);
            arabicContext.Orders.AddRange(cancelOrder, deleteOrder);
            await arabicContext.SaveChangesAsync();

            var cancel = await new CancelCustomerOrderCommandHandler(arabicContext, arabicContext, new NoOpPublisher())
                .Handle(new CancelCustomerOrderCommand(cancelOrder.Id, arabic.UserId, "changed_my_mind", null, null), CancellationToken.None);
            var delete = await new DeleteCustomerOrderCommandHandler(arabicContext, arabicContext)
                .Handle(new DeleteCustomerOrderCommand(deleteOrder.Id, arabic.UserId), CancellationToken.None);

            cancel.Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.OrderCancelledSuccess));
            delete.Message.Should().Be(LocalizedMessages.GetAr(LocalizedMessages.OrderDeletedSuccess));
        }
    }

    [Fact]
    public async Task AuthAndAddressesCustomerEndpoints_ReturnLocalizedMessages()
    {
        using (new CultureScope("en"))
        {
            var controller = CreateCustomerAuthController();

            var forgotPassword = await controller.ForgotPassword(new ForgotPasswordRequest("customer@test.com"));
            var resetPassword = await controller.ResetPassword(new ResetPasswordRequest("customer@test.com", "1234", "Password123!"));

            GetOkMessage(forgotPassword).Should().Be(GetSharedResource("PasswordResetOtpSent"));
            GetOkMessage(resetPassword).Should().Be(GetSharedResource("PasswordResetSuccess"));

            var addresses = CreateCustomerAddressesController(null);
            var act = () => addresses.GetAddresses();
            await act.Should().ThrowAsync<UnauthorizedException>()
                .WithMessage(GetSharedResource("UserNotAuthenticated"));
        }

        using (new CultureScope("ar"))
        {
            var controller = CreateCustomerAuthController();

            var forgotPassword = await controller.ForgotPassword(new ForgotPasswordRequest("customer@test.com"));
            var resetPassword = await controller.ResetPassword(new ResetPasswordRequest("customer@test.com", "1234", "Password123!"));

            ContainsArabic(GetOkMessage(forgotPassword)).Should().BeTrue();
            ContainsArabic(GetOkMessage(resetPassword)).Should().BeTrue();

            var addresses = CreateCustomerAddressesController(null);
            var act = () => addresses.GetAddresses();
            var exception = await act.Should().ThrowAsync<UnauthorizedException>();
            ContainsArabic(exception.Which.Message).Should().BeTrue();
        }
    }

    [Fact]
    public async Task NotificationsCustomerEndpoints_ReturnLocalizedMessagePayloads()
    {
        var controller = CreateNotificationsController(Guid.NewGuid(), out var sender);

        sender.Setup(x => x.Send(It.IsAny<MarkNotificationReadCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        sender.Setup(x => x.Send(It.IsAny<MarkAllNotificationsReadCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        sender.Setup(x => x.Send(It.IsAny<DeleteAllNotificationsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var read = await controller.MarkAsRead(Guid.NewGuid(), CancellationToken.None);
        var readAll = await controller.MarkAllAsRead(CancellationToken.None);
        var deleteAll = await controller.DeleteAllNotifications(CancellationToken.None);

        var readPayload = GetOkAnonymous(read);
        var readAllPayload = GetOkAnonymous(readAll);
        var deleteAllPayload = GetOkAnonymous(deleteAll);

        ReadProperty<string>(readPayload, "message_ar").Should().Be(LocalizedMessages.GetAr(LocalizedMessages.NotificationMarkedRead));
        ReadProperty<string>(readPayload, "message_en").Should().Be(LocalizedMessages.GetEn(LocalizedMessages.NotificationMarkedRead));
        ReadProperty<string>(readAllPayload, "message_ar").Should().Be(LocalizedMessages.GetAr(LocalizedMessages.AllNotificationsMarkedRead));
        ReadProperty<string>(readAllPayload, "message_en").Should().Be(LocalizedMessages.GetEn(LocalizedMessages.AllNotificationsMarkedRead));
        ReadProperty<int>(readAllPayload, "count").Should().Be(2);
        ReadProperty<string>(deleteAllPayload, "message_ar").Should().Be("\u062a\u0645 \u062d\u0630\u0641 \u062c\u0645\u064a\u0639 \u0627\u0644\u0625\u0634\u0639\u0627\u0631\u0627\u062a");
        ReadProperty<string>(deleteAllPayload, "message_en").Should().Be("All notifications deleted");
        ReadProperty<int>(deleteAllPayload, "count").Should().Be(3);
    }

    [Fact]
    public void OrdersReasonEndpoints_ReturnLocalizedLabels()
    {
        var controller = new OrdersController(
            Mock.Of<Zadana.Application.Common.Interfaces.ICurrentUserService>(),
            Mock.Of<IOrderReadService>(),
            Mock.Of<IOrderSupportCaseWorkflowService>(),
            Mock.Of<Zadana.Application.Common.Interfaces.IApplicationDbContext>());

        using (new CultureScope("en"))
        {
            var cancellationReasons = GetOkValue<IReadOnlyList<CustomerOrderCancellationReasonResponse>>(
                controller.GetCancellationReasons());
            var supportReasons = GetOkValue<IReadOnlyList<CustomerOrderSupportReasonResponse>>(
                controller.GetSupportReasons("return_request"));

            cancellationReasons.Should().NotBeEmpty();
            supportReasons.Should().NotBeEmpty();
            cancellationReasons[0].Label.Should().NotContainAny("\u0627", "\u0644");
            supportReasons[0].Label.Should().NotContainAny("\u0627", "\u0644");
        }

        using (new CultureScope("ar"))
        {
            var cancellationReasons = GetOkValue<IReadOnlyList<CustomerOrderCancellationReasonResponse>>(
                controller.GetCancellationReasons());
            var supportReasons = GetOkValue<IReadOnlyList<CustomerOrderSupportReasonResponse>>(
                controller.GetSupportReasons("return_request"));

            cancellationReasons.Should().NotBeEmpty();
            supportReasons.Should().NotBeEmpty();
            ContainsArabic(cancellationReasons[0].Label).Should().BeTrue();
            ContainsArabic(supportReasons[0].Label).Should().BeTrue();
        }
    }

    private static async Task<CatalogEndpointResult> ReadCatalogAsync(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        CatalogSeed seed)
    {
        var cache = TestServiceFactory.CreateAppCache();
        var options = TestServiceFactory.CreateCachingOptions();
        var catalogCache = TestServiceFactory.CreateCatalogReadCacheService(context);

        var customerBrands = await new GetCustomerBrandsQueryHandler(context, cache, options)
            .Handle(new GetCustomerBrandsQuery(), CancellationToken.None);
        var brandDetails = await new GetBrandByIdQueryHandler(context)
            .Handle(new GetBrandByIdQuery(seed.Brand.Id), CancellationToken.None);
        var brandFilters = await new GetBrandFiltersQueryHandler(context, cache, options)
            .Handle(new GetBrandFiltersQuery(seed.Brand.Id), CancellationToken.None);
        var subcategories = await new GetCategorySubcategoriesQueryHandler(context, cache, options)
            .Handle(new GetCategorySubcategoriesQuery(seed.Category.Id), CancellationToken.None);
        var categoryFilters = await new GetCategoryFiltersQueryHandler(context, cache, options)
            .Handle(new GetCategoryFiltersQuery(seed.Category.Id), CancellationToken.None);
        var categoryProducts = await new GetCategoryProductsQueryHandler(context, cache, catalogCache, options)
            .Handle(new GetCategoryProductsQuery(seed.Subcategory.Id, null, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None);
        var brandProducts = await new GetBrandProductsQueryHandler(context, cache, catalogCache, options)
            .Handle(new GetBrandProductsQuery(seed.Brand.Id, null, null, null, null, null, null, null, null, 1, 20), CancellationToken.None);
        var search = await new SearchProductsQueryHandler(context, cache, catalogCache, options)
            .Handle(new SearchProductsQuery(ProductEn, null, null, null, null, null, 1, 20), CancellationToken.None);
        var productDetails = await new GetProductDetailsQueryHandler(context, cache, catalogCache, options)
            .Handle(new GetProductDetailsQuery(seed.Product.Id), CancellationToken.None);

        return new CatalogEndpointResult(
            customerBrands[0].Name,
            brandDetails.Name,
            brandFilters.Brand.Name,
            categoryFilters.Category.Name,
            categoryProducts.Items[0].Name,
            brandProducts.Items[0].Name,
            search.Items[0].Name,
            productDetails.Name,
            productDetails.Store,
            productDetails.Unit,
            productDetails.Description,
            subcategories[0].Name);
    }

    private static GetCheckoutSummaryQueryHandler CreateCheckoutSummaryHandler(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context)
    {
        var gatewayResolver = TestPaymentGatewayResolver.Enabled();

        var deliveryPricing = new Mock<IDeliveryPricingService>();
        deliveryPricing
            .Setup(x => x.QuoteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>(), It.IsAny<decimal?>()))
            .ReturnsAsync(new DeliveryPriceQuote(5m, 2m, 0m, 7m, 3m, "zone", "Zone rule", 1m, 2m, 3m, 4m, "driver", "vendor", false, "manual", null, "locked", DateTime.UtcNow, 1, false));

        return new GetCheckoutSummaryQueryHandler(context, gatewayResolver, deliveryPricing.Object);
    }

    private static ApplyCheckoutPromoCodeResultDto CreateApplyPromoCodeResult() =>
        new(
            LocalizedMessages.GetAr(LocalizedMessages.PromoCodeApplied),
            LocalizedMessages.GetEn(LocalizedMessages.PromoCodeApplied),
            new CheckoutPromoCodeDto("SAVE10", "fixed", 10m, 10m),
            new CheckoutTotalsDto(100m, 7m, 10m, 0m, 0m, 97m, "EGP"));

    private static RemoveCheckoutPromoCodeResultDto CreateRemovePromoCodeResult() =>
        new(
            LocalizedMessages.GetAr(LocalizedMessages.PromoCodeRemoved),
            LocalizedMessages.GetEn(LocalizedMessages.PromoCodeRemoved),
            new CheckoutTotalsDto(100m, 7m, 0m, 0m, 0m, 107m, "EGP"));

    private static PlaceCheckoutOrderResultDto CreatePlaceOrderResult() =>
        new(
            LocalizedMessages.GetAr(LocalizedMessages.OrderPlacedSuccess),
            LocalizedMessages.GetEn(LocalizedMessages.OrderPlacedSuccess),
            new CheckoutPlacedOrderDto(Guid.NewGuid(), DateTime.UtcNow, "placed", "cash", "pending", 107m),
            null);

    private static CustomerAuthController CreateCustomerAuthController()
    {
        var sender = new Mock<ISender>();
        sender.Setup(x => x.Send(It.IsAny<ForgotPasswordCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        sender.Setup(x => x.Send(It.IsAny<ResetPasswordCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new CustomerAuthController(new CultureAwareLocalizer());
        AttachSender(controller, sender.Object);
        return controller;
    }

    private static CustomerAddressesController CreateCustomerAddressesController(Guid? userId)
    {
        var currentUser = new Mock<Zadana.Application.Common.Interfaces.ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(userId.HasValue);

        var controller = new CustomerAddressesController(currentUser.Object, new CultureAwareLocalizer());
        AttachSender(controller, Mock.Of<ISender>());
        return controller;
    }

    private static NotificationsController CreateNotificationsController(Guid userId, out Mock<ISender> sender)
    {
        sender = new Mock<ISender>();
        var currentUser = new Mock<Zadana.Application.Common.Interfaces.ICurrentUserService>();
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);

        var controller = new NotificationsController(currentUser.Object);
        AttachSender(controller, sender.Object);
        return controller;
    }

    private static void AttachSender(ControllerBase controller, ISender sender)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sender);
        var serviceProvider = services.BuildServiceProvider();

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider
            }
        };
    }

    private static async Task<CatalogSeed> SeedCatalogAsync(Zadana.Infrastructure.Persistence.ApplicationDbContext context)
    {
        var customer = new User("Customer", $"customer-{Guid.NewGuid():N}@test.com", "01000000001", Zadana.Domain.Modules.Identity.Enums.UserRole.Customer);
        var category = new Category(CategoryAr, CategoryEn, null, null, 1);
        context.Users.Add(customer);
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        var subcategory = new Category(ProductAr, ProductEn, null, category.Id, 1);
        var brand = new Brand(BrandAr, BrandEn, "brand.png");
        var unit = new UnitOfMeasure(UnitAr, UnitEn, "L");
        context.Categories.Add(subcategory);
        context.Brands.Add(brand);
        context.UnitsOfMeasure.Add(unit);
        await context.SaveChangesAsync();

        var product = new MasterProduct(
            ProductAr,
            ProductEn,
            $"fresh-milk-{Guid.NewGuid():N}",
            subcategory.Id,
            brand.Id,
            unit.Id,
            ProductDescriptionAr,
            ProductDescriptionEn);
        product.Publish();
        product.AddImage("https://cdn.test/milk.jpg", displayOrder: 0, isPrimary: true);
        context.MasterProducts.Add(product);
        await context.SaveChangesAsync();

        var vendor = new Vendor(
            Guid.NewGuid(),
            StoreAr,
            StoreEn,
            "groceries",
            $"CR-{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}@example.com",
            "01000000002");
        vendor.Approve(10m, Guid.NewGuid());
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var branch = new VendorBranch(vendor.Id, "Main branch", "Branch address", 30m, 31m, "01000000003", 10m);
        context.VendorBranches.Add(branch);
        await context.SaveChangesAsync();

        context.VendorProducts.Add(new VendorProduct(vendor.Id, product.Id, 50m, 10, 60m, vendorBranchId: branch.Id));
        await context.SaveChangesAsync();

        return new CatalogSeed(customer.Id, category, subcategory, brand, product, vendor.Id);
    }

    private static async Task SeedCheckoutAsync(
        Zadana.Infrastructure.Persistence.ApplicationDbContext context,
        CatalogSeed seed)
    {
        var address = new CustomerAddress(
            seed.UserId,
            "Customer",
            "01000000004",
            "Checkout address",
            AddressLabel.Home,
            city: "Cairo",
            area: "Maadi",
            latitude: 30m,
            longitude: 31m);
        address.SetAsDefault();

        var cart = new Cart(seed.UserId);
        cart.Items.Add(new CartItem(cart.Id, seed.Product.Id, seed.Product.NameEn, 2));

        context.CustomerAddresses.Add(address);
        context.Carts.Add(cart);
        await context.SaveChangesAsync();
    }

    private static Order CreateOrder(Guid userId, Guid vendorId) =>
        new(
            $"ORD-{Guid.NewGuid():N}",
            userId,
            vendorId,
            Guid.NewGuid(),
            PaymentMethodType.CashOnDelivery,
            100m,
            0m,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            null,
            0m,
            0m,
            0m,
            0m,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            1,
            false,
            0m);

    private static T GetOkValue<T>(ActionResult<T> actionResult)
    {
        var ok = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        return ok.Value.Should().BeAssignableTo<T>().Subject;
    }

    private static string GetOkMessage(IActionResult actionResult)
    {
        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        return ReadProperty<string>(ok.Value!, "Message");
    }

    private static object GetOkAnonymous(ActionResult actionResult)
    {
        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
        return ok.Value!;
    }

    private static T ReadProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");

        return property.GetValue(instance).Should().BeAssignableTo<T>().Subject;
    }

    private static bool ContainsArabic(string value) =>
        value.Any(character => character is >= '\u0600' and <= '\u06FF');

    private static string GetSharedResource(string key) =>
        SharedResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

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

    private sealed class CultureAwareLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, GetSharedResource(name));

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(CultureInfo.CurrentCulture, GetSharedResource(name), arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            Task.CompletedTask;
    }

    private sealed record CatalogSeed(
        Guid UserId,
        Category Category,
        Category Subcategory,
        Brand Brand,
        MasterProduct Product,
        Guid VendorId);

    private sealed record CatalogEndpointResult(
        string CustomerBrand,
        string BrandDetails,
        string BrandFilter,
        string CategoryFilter,
        string CategoryProduct,
        string BrandProduct,
        string SearchProduct,
        string ProductDetailsName,
        string ProductDetailsStore,
        string? ProductDetailsUnit,
        string? ProductDetailsDescription,
        string SubcategoryName);
}
