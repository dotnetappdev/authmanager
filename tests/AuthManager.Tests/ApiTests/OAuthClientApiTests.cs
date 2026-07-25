using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AuthManager.Tests.ApiTests;

/// <summary>
/// End-to-end HTTP test of the OAuth2 client-credentials grant: register a client through
/// the admin API, then obtain a token through the actual anonymous token endpoint exactly
/// as an external service would (form-encoded POST per RFC 6749), and confirm the resulting
/// token is honoured by this same app's own JWT bearer authentication.
/// </summary>
public sealed class OAuthClientApiTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public OAuthClientApiTests(AdminApiFactory factory) => _factory = factory;

    private async Task<(string ClientId, string Secret)> RegisterClientAsync(HttpClient adminClient, string clientId)
    {
        var response = await adminClient.PostAsJsonAsync("/authmanager/api/clients", new
        {
            clientId,
            name = "Test Client",
            allowedScopes = new[] { "read:invoices" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (clientId, body.GetProperty("clientSecret").GetString()!);
    }

    [Fact]
    public async Task Token_endpoint_is_reachable_without_authentication()
    {
        var admin = await _factory.CreateAuthenticatedClientAsync();
        var (clientId, secret) = await RegisterClientAsync(admin, "http-oauth-anon-check");

        var anonymousClient = _factory.CreateClient(); // deliberately no Authorization header
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        });

        var response = await anonymousClient.PostAsync("/authmanager/api/oauth/token", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_valid_client_receives_a_bearer_token_with_its_scopes()
    {
        var admin = await _factory.CreateAuthenticatedClientAsync();
        var (clientId, secret) = await RegisterClientAsync(admin, "http-oauth-scopes-check");
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/authmanager/api/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
        Assert.True(body.GetProperty("expires_in").GetInt32() > 0);
        Assert.Equal("read:invoices", body.GetProperty("scope").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task A_wrong_secret_is_rejected()
    {
        var admin = await _factory.CreateAuthenticatedClientAsync();
        var (clientId, _) = await RegisterClientAsync(admin, "http-oauth-wrongsecret-check");
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/authmanager/api/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = "wrong",
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unsupported_grant_type_is_rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/authmanager/api/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "whatever",
            ["client_secret"] = "whatever",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_issued_token_is_honoured_by_this_apps_own_JWT_bearer_auth()
    {
        var admin = await _factory.CreateAuthenticatedClientAsync();
        var (clientId, secret) = await RegisterClientAsync(admin, "http-oauth-crossvalidate-check");
        var client = _factory.CreateClient();

        var tokenResponse = await client.PostAsync("/authmanager/api/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }));
        var body = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("access_token").GetString()!;

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync("/authmanager/api/users");

        // The signature/issuer/audience validate correctly (proving the shared signing key
        // works) but the client has no SuperAdmin role claim, so access is correctly denied —
        // 403, not 401 (unauthenticated) and definitely not a 500.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Regenerating_a_secret_invalidates_the_old_one_immediately()
    {
        var admin = await _factory.CreateAuthenticatedClientAsync();
        var (clientId, oldSecret) = await RegisterClientAsync(admin, "http-oauth-regen-check");

        var clientsResponse = await admin.GetAsync("/authmanager/api/clients");
        var clients = await clientsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var internalId = clients.EnumerateArray()
            .First(c => c.GetProperty("clientId").GetString() == clientId)
            .GetProperty("id").GetString();

        var regenResponse = await admin.PostAsync($"/authmanager/api/clients/{internalId}/regenerate-secret", content: null);
        var regenBody = await regenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newSecret = regenBody.GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        var oldSecretResponse = await client.PostAsync("/authmanager/api/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = oldSecret,
        }));
        var newSecretResponse = await client.PostAsync("/authmanager/api/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = newSecret,
        }));

        Assert.Equal(HttpStatusCode.Unauthorized, oldSecretResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newSecretResponse.StatusCode);
    }
}
