using Microsoft.Extensions.Localization;
using Moq;

namespace Zadana.Application.Tests.Helpers;

public static class TestLocalizer
{
    public static IStringLocalizer<T> Create<T>() where T : class
    {
        var mock = new Mock<IStringLocalizer<T>>();

        mock.Setup(localizer => localizer[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));

        mock.Setup(localizer => localizer[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string key, object[] _) => new LocalizedString(key, key));

        return mock.Object;
    }
}
