using System.Net.Http.Json;
using System.Text.Json;
using AuthManager.Core.Options;
using AuthManagerSample.AdminApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AuthManager.Tests.ApiTests;

/// <summary>
/// Spins up the real <c>AuthManagerSample.AdminApi</c> app in-memory (via <c>TestServer</c> —
/// no real socket, no Kestrel) for true HTTP-pipeline tests: routing, model binding,
/// JWT bearer auth, JSON serialization. Each instance gets its own throwaway SQLite files
/// so tests never share state with a developer's local run of the sample or with each other.
/// </summary>
public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"amapitest-{Guid.NewGuid():N}.db");
    private readonly string _authDbFile = Path.Combine(Path.GetTempPath(), $"amapitest-auth-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbFile}");
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<AuthManagerOptions>(o => o.InternalDatabaseConnectionString = $"Data Source={_authDbFile}");
        });
    }

    /// <summary>Logs in as the seeded SuperAdmin and returns the JWT access token.</summary>
    public async Task<string> LoginAsSuperAdminAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/login", new { email = "superadmin@example.com", password = "SuperAdmin@123456!" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString()!;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var token = await LoginAsSuperAdminAsync();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        foreach (var file in new[] { _dbFile, _authDbFile })
        foreach (var candidate in new[] { file, file + "-journal", file + "-wal", file + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { /* best-effort cleanup */ }
        }
    }
}
