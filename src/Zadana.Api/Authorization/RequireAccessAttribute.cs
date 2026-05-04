using Microsoft.AspNetCore.Mvc;

namespace Zadana.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireAccessAttribute : TypeFilterAttribute
{
    public RequireAccessAttribute(params string[] permissions)
        : this(false, permissions)
    {
    }

    public RequireAccessAttribute(bool requireAll, params string[] permissions)
        : base(typeof(RequireAccessFilter))
    {
        Arguments = [permissions, requireAll];
        Order = int.MinValue + 100;
    }
}
