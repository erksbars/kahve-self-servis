using System.Collections.Concurrent;
using System.Security.Claims;
using Kahve.Api.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Kahve.Api.Modules.Realtime;
public sealed record StaffSubscription(Guid TenantId, Guid StaffId, Guid BranchId, string Role);
public sealed class StaffSubscriptions
{
    public ConcurrentDictionary<string, StaffSubscription> Connections { get; } = new();
}
public sealed class OrderHub(AppDbContext db, StaffSubscriptions subscriptions) : Hub
{
    public static string StaffGroup(Guid branch) => $"branch:{branch}:staff";
    public static string TrackingGroup(Guid order) => $"order:{order}:tracking";
    public async Task JoinStaff(Guid branchId)
    {
        if (Context.User?.Identity?.IsAuthenticated != true || !Guid.TryParse(Context.User.FindFirstValue("tenant_id"), out var tenant) || !Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var staff)) throw new HubException("Personel oturumu gerekli.");
        var role = Context.User.FindFirstValue(ClaimTypes.Role) ?? "";
        if (!await db.StaffUsers.IgnoreQueryFilters().AnyAsync(x => x.Id == staff && x.TenantId == tenant && x.Active && x.Role == role) || !await db.StaffBranchAccess.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenant && x.StaffId == staff && x.BranchId == branchId)) throw new HubException("Şube yetkisi bulunamadı.");
        if (subscriptions.Connections.TryRemove(Context.ConnectionId, out var old)) await Groups.RemoveFromGroupAsync(Context.ConnectionId, StaffGroup(old.BranchId));
        subscriptions.Connections[Context.ConnectionId] = new(tenant, staff, branchId, role);
        await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup(branchId));
    }
    public async Task JoinTracking(string token)
    {
        if (!CapabilityTokens.Valid(token)) throw new HubException("Takip bağlantısı geçersiz.");
        var hash = CapabilityTokens.Hash(token);
        var order = await db.Orders.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.TrackingTokenHash == hash);
        if (order is null) throw new HubException("Takip bağlantısı geçersiz.");
        await Groups.AddToGroupAsync(Context.ConnectionId, TrackingGroup(order.Id));
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.Connections.TryRemove(Context.ConnectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }
}
