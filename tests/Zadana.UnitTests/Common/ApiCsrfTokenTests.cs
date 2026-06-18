using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Zadana.Api.Security;

namespace Zadana.UnitTests.Common;

public sealed class ApiCsrfTokenTests
{
    [Fact]
    public void Issue_ThenMatchingCookieAndHeader_IsValid()
    {
        var environment = CreateProductionEnvironment();
        var issueContext = new DefaultHttpContext();

        var token = ApiCsrfToken.Issue(issueContext.Response, environment);

        token.Should().HaveLength(64);
        issueContext.Response.Headers.SetCookie.ToString()
            .Should().Contain("__Host-XSRF-TOKEN=")
            .And.Contain("httponly")
            .And.Contain("samesite=none");

        var validationContext = new DefaultHttpContext();
        validationContext.Request.Headers.Cookie = $"__Host-XSRF-TOKEN={token}";
        validationContext.Request.Headers[ApiCsrfToken.HeaderName] = token;

        ApiCsrfToken.IsValid(validationContext.Request, environment).Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenHeaderDoesNotMatchCookie_ReturnsFalse()
    {
        var environment = CreateProductionEnvironment();
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"__Host-XSRF-TOKEN={new string('A', 64)}";
        context.Request.Headers[ApiCsrfToken.HeaderName] = new string('B', 64);

        ApiCsrfToken.IsValid(context.Request, environment).Should().BeFalse();
    }

    private static IWebHostEnvironment CreateProductionEnvironment()
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Production);
        return environment.Object;
    }
}
