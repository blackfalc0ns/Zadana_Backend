using FluentAssertions;
using Zadana.Api.Modules.Files.Security;

namespace Zadana.UnitTests.Modules.Files;

public class FileUploadSecurityPolicyTests
{
    [Theory]
    [InlineData("drivers/national-id", true)]
    [InlineData("drivers/license", true)]
    [InlineData("drivers/vehicle", true)]
    [InlineData("drivers/profile", true)]
    [InlineData("drivers/proofs", false)]
    public void TryResolve_AllowsDocumentedDriverDirectories(string directory, bool allowAnonymous)
    {
        var resolved = FileUploadSecurityPolicy.TryResolve(directory, out var rule);

        resolved.Should().BeTrue();
        rule.Directory.Should().Be(directory);
        rule.AllowAnonymous.Should().Be(allowAnonymous);
    }

    [Fact]
    public void TryResolve_RequiresDriverRoleForProofUploads()
    {
        var resolved = FileUploadSecurityPolicy.TryResolve("drivers/proofs", out var rule);

        resolved.Should().BeTrue();
        rule.AllowAnonymous.Should().BeFalse();
        rule.AllowedRoles.Should().Contain("Driver");
    }
}
