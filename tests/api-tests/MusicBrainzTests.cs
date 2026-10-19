using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kahve.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kahve.Api.Tests;

public sealed class MusicBrainzTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private HttpClient Client() => fixture.Factory.CreateClient();
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object input)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/session/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(input) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
    private static async Task<JsonElement> Read(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private static object Row(Guid id, string title, string artist) => new Dictionary<string, object> { ["id"] = id, ["title"] = title, ["artist-credit"] = new[] { new { name = artist } }, ["tags"] = new[] { new { name = "must not import" } } };
    private async Task<(Guid Branch, int Count)> Setup(HttpClient client, string email = "owner-a@demo.local")
    {
        (await Send(client, "/api/session/login", new { email, password = ApiFixture.Password })).EnsureSuccessStatusCode();
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Branches.IgnoreQueryFilters().Where(x => x.Slug == "demo-moda").Select(x => x.Id).SingleAsync(), await db.Songs.IgnoreQueryFilters().CountAsync());
    }
    [Fact]
    public async Task Preview_is_read_only_import_is_idempotent_and_requires_branch_activation()
    {
        using var owner = Client(); var setup = await Setup(owner);
        var path = $"/api/admin/music/{setup.Branch}/musicbrainz/import";
        var mbid = Guid.NewGuid();
        var data = new { recordings = new[] { Row(mbid, "Dış Kaynak Testi", "Dış Sanatçı"), Row(mbid, "Dış Kaynak Testi", "Dış Sanatçı"), Row(Guid.NewGuid(), "dis kaynak testi", "dis sanatci") } };
        var preview = await Read(await Send(owner, path, new { data }));
        Assert.True(preview.GetProperty("preview").GetBoolean()); Assert.Equal(1, preview.GetProperty("added").GetInt32());
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(setup.Count, await db.Songs.IgnoreQueryFilters().CountAsync());
        await owner.GetAsync("/api/session/csrf");
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Send(owner, path, new { data, preview = false })));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(setup.Count + 1, await db.Songs.IgnoreQueryFilters().CountAsync());
        var song = await db.Songs.IgnoreQueryFilters().SingleAsync(x => x.MusicBrainzRecordingId == mbid);
        var link = await db.BranchSongs.IgnoreQueryFilters().SingleAsync(x => x.SongId == song.Id && x.BranchId == setup.Branch);
        Assert.False(link.Active);
        // Existing availability/metadata must survive a new snapshot and a repeat import.
        link.Active = true; song.Title = "İşletmenin Şarkı Adı"; await db.SaveChangesAsync();
        (await Send(owner, path, new { data, preview = false })).EnsureSuccessStatusCode();
        await db.Entry(song).ReloadAsync(); await db.Entry(link).ReloadAsync();
        Assert.True(link.Active); Assert.Equal("İşletmenin Şarkı Adı", song.Title);
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(x => x.EntityId == setup.Branch && x.Action == "music.musicbrainz.import"));
    }
    [Fact]
    public async Task Invalid_rows_reject_entire_batch_and_match_existing_titles_without_overwriting()
    {
        using var owner = Client(); var setup = await Setup(owner); var path = $"/api/admin/music/{setup.Branch}/musicbrainz/import";
        foreach (var data in new object[] { new { nope = true }, Array.Empty<object>(), new[] { Row(Guid.Empty, "A", "B") }, new[] { Row(Guid.NewGuid(), "Valid", "Artist"), Row(Guid.NewGuid(), "", "Artist") }, Enumerable.Repeat(Row(Guid.NewGuid(), "A", "B"), 501).ToArray(), new[] { Row(Guid.NewGuid(), new string('x', 161), "A") } })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(owner, path, new { data, preview = false })).StatusCode);
        using var scope = fixture.Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(setup.Count, await db.Songs.IgnoreQueryFilters().CountAsync());
        var mbid = Guid.NewGuid();
        var result = await Read(await Send(owner, path, new { data = new[] { Row(mbid, "gulumse", "sezen aksu") }, preview = false }));
        Assert.Equal(0, result.GetProperty("added").GetInt32());
        var song = await db.Songs.IgnoreQueryFilters().SingleAsync(x => x.MusicBrainzRecordingId == mbid);
        Assert.Equal("Gülümse", song.Title); Assert.Equal("Sezen Aksu", song.Artist);
        var credit = JsonSerializer.Deserialize<JsonElement>("""[{"id":"37de5bd5-74a4-4d03-8e1f-f25a12ab0dd0","title":"Duet","artist-credit":[{"artist":{"name":"A"},"joinphrase":" & "},{"name":"B"}]}]""");
        Assert.Equal("A & B", Kahve.Api.Modules.Music.MusicBrainzImport.Parse(credit)[0].Artist);
    }
    [Fact]
    public async Task Import_is_owner_only_csrf_protected_and_tenant_scoped()
    {
        using var anonymous = Client(); using var owner = Client(); using var foreign = Client(); using var barista = Client();
        var setup = await Setup(owner); await Setup(foreign, "owner-b@demo.local"); await Setup(barista, "barista-a@demo.local");
        var path = $"/api/admin/music/{setup.Branch}/musicbrainz/import";
        var body = new { data = new[] { Row(Guid.NewGuid(), "Test", "Artist") }, preview = false };
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(anonymous, path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(barista, path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(foreign, path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, body)).StatusCode);
    }
}
