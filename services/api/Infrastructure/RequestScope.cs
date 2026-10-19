using System.Security.Claims;

namespace Kahve.Api.Infrastructure;

public sealed class RequestScope(IHttpContextAccessor accessor)
{
    public Guid? TenantId => Parse("tenant_id");
    public Guid? StaffId => Parse(ClaimTypes.NameIdentifier);
    private Guid? Parse(string claim) => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(claim), out var id) ? id : null;
}
