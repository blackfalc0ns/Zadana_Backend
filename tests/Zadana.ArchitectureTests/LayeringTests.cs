using System.Reflection;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;
using Zadana.Api.Controllers;

namespace Zadana.ArchitectureTests;

public class LayeringTests
{
    [Fact]
    public void Domain_Should_Not_Depend_On_Api_Or_Infrastructure()
    {
        var result = Types.InAssembly(typeof(Zadana.Domain.Modules.Catalog.Entities.MasterProduct).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Zadana.Api", "Zadana.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Zadana.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Application_Should_Not_Depend_On_AspNetIdentity_Framework()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Vendors_Domain_Should_Not_Depend_On_Identity_Entities()
    {
        var result = Types.InAssembly(typeof(Zadana.Domain.Modules.Vendors.Entities.Vendor).Assembly)
            .That()
            .ResideInNamespace("Zadana.Domain.Modules.Vendors")
            .ShouldNot()
            .HaveDependencyOn("Zadana.Domain.Modules.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Vendors_Application_Should_Not_Depend_On_EntityFrameworkCore()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespace("Zadana.Application.Modules.Vendors")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Vendors_Application_Should_Not_Depend_On_ApplicationDbContext_Interface()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespace("Zadana.Application.Modules.Vendors")
            .ShouldNot()
            .HaveDependencyOn("Zadana.Application.Common.Interfaces.IApplicationDbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Catalog_ProductRequests_Application_Should_Not_Depend_On_EntityFrameworkCore()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespace("Zadana.Application.Modules.Catalog")
            .And()
            .HaveNameMatching(".*ProductRequest.*")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Catalog_ProductRequests_Application_Should_Not_Depend_On_ApplicationDbContext_Interface()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespace("Zadana.Application.Modules.Catalog")
            .And()
            .HaveNameMatching(".*ProductRequest.*")
            .ShouldNot()
            .HaveDependencyOn("Zadana.Application.Common.Interfaces.IApplicationDbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Orders_Application_Should_Not_Depend_On_EntityFrameworkCore()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespace("Zadana.Application.Modules.Orders")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Orders_Application_Should_Not_Depend_On_ApplicationDbContext_Interface()
    {
        var result = Types.InAssembly(typeof(Zadana.Application.DependencyInjection).Assembly)
            .That()
            .ResideInNamespace("Zadana.Application.Modules.Orders")
            .ShouldNot()
            .HaveDependencyOn("Zadana.Application.Common.Interfaces.IApplicationDbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.GetOffendingTypes());
    }

    [Fact]
    public void Application_DbContext_Interface_Should_Not_Expose_Identity_DbSets()
    {
        var dbSetPropertyNames = typeof(Zadana.Application.Common.Interfaces.IApplicationDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        dbSetPropertyNames.Should().NotContain("Users");
        dbSetPropertyNames.Should().NotContain("RefreshTokens");
    }

    [Fact]
    public void Controllers_Should_Not_Use_MediatR_Requests_As_Action_Parameters()
    {
        var controllerTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));

        var violations = controllerTypes
            .SelectMany(controller => controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetParameters()
                .Where(parameter => IsMediatRRequest(parameter.ParameterType) || IsApplicationType(parameter.ParameterType))
                .Select(parameter => $"{method.DeclaringType!.Name}.{method.Name}({parameter.ParameterType.FullName})"))
            .ToList();

        violations.Should().BeEmpty();
    }

    [Fact]
    public void Vendor_Entity_Should_Not_Expose_User_Navigation()
    {
        var userProperty = typeof(Zadana.Domain.Modules.Vendors.Entities.Vendor).GetProperty("User");

        userProperty.Should().BeNull();
    }

    [Fact]
    public void Modular_Controllers_Should_Inherit_From_ApiControllerBase()
    {
        var controllerTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(type => type.Namespace != null
                && type.Namespace.StartsWith("Zadana.Api.Modules", StringComparison.Ordinal)
                && !type.IsAbstract
                && typeof(ControllerBase).IsAssignableFrom(type))
            .ToList();

        var violations = controllerTypes
            .Where(type => !typeof(ApiControllerBase).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .ToList();

        violations.Should().BeEmpty();
    }

    private static bool IsMediatRRequest(Type parameterType)
    {
        if (typeof(IRequest).IsAssignableFrom(parameterType))
        {
            return true;
        }

        return parameterType.GetInterfaces()
            .Any(interfaceType => interfaceType.IsGenericType
                && interfaceType.GetGenericTypeDefinition() == typeof(IRequest<>));
    }

    private static bool IsApplicationType(Type parameterType) =>
        parameterType.Namespace != null
        && parameterType.Namespace.StartsWith("Zadana.Application.", StringComparison.Ordinal);
}

internal static class TestResultExtensions
{
    public static string GetOffendingTypes(this TestResult result) =>
        string.Join(", ", result.FailingTypeNames ?? []);
}
