using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AuthManager.Tests.ApiTests;

/// <summary>
/// HTTP-level coverage for the passkey endpoints — these need a real HttpContext (SignInManager
/// correlates the WebAuthn ceremony's challenge state to the current request), which only exists
/// in an actual HTTP pipeline, not a bare DI scope. See PasskeyServiceTests for what's covered
/// without one. The actual attestation/assertion round-trip needs a real browser authenticator,
/// so it isn't exercised here — this locks in that the endpoints are reachable, correctly gated,
/// and return well-formed WebAuthn options.
/// </summary>
public sealed class PasskeysApiTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public PasskeysApiTests(AdminApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Listing_passkeys_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/authmanager/api/passkeys");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_authenticated_user_gets_an_empty_passkey_list_by_default()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/authmanager/api/passkeys");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var passkeys = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, passkeys.GetArrayLength());
    }

    [Fact]
    public async Task Creation_options_are_well_formed_WebAuthn_JSON_scoped_to_the_caller()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/authmanager/api/passkeys/creation-options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(options.TryGetProperty("challenge", out _));
        Assert.True(options.TryGetProperty("user", out var user));
        Assert.Equal("superadmin", user.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Login_options_are_reachable_without_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/authmanager/api/passkeys/login/options");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(options.TryGetProperty("challenge", out _));
    }

    [Fact]
    public async Task Login_rejects_a_garbage_credential_response()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/authmanager/api/passkeys/login", new { credentialJson = "not a real credential" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
