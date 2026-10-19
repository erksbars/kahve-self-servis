namespace Kahve.Api.Modules.Music;

public sealed class Song
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Title { get; set; } = "";
    public string Artist { get; set; } = "";
    public string SearchKey { get; set; } = "";
    public Guid? MusicBrainzRecordingId { get; set; }
}
public sealed class BranchMusic
{
    public Guid BranchId { get; set; }
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; }
    public int Version { get; set; } = 1;
}
public sealed class BranchSong
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SongId { get; set; }
    public bool Active { get; set; } = true;
}
public sealed class SongRequest
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? OrderId { get; set; }
    public string? GuestKeyHash { get; set; }
    public Guid? SongId { get; set; }
    public string? RequestedText { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTimeOffset CreatedAt { get; set; }
    public int Version { get; set; } = 1;
}
