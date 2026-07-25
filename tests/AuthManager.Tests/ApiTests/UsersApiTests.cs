using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AuthManager.Tests.ApiTests;

/// <summary>
/// HTTP-level tests against the real admin REST API (MapAuthManagerApi()) — these exercise
/// the actual ASP.NET Core routing/model-binding pipeline, which is what caught two real
/// bugs during development: DELETE endpoints need an explicit [FromBody] (Minimal API
/// disallows inferred bodies on DELETE/GET), and GET /users 400'd when paging query params
/// were omitted because they lacked C# default values. Both are covered below.
/// </summary>
public sealed class UsersApiTests : IClassFixture<AdminApiFactory>
{
    private readonly AdminApiFactory _factory;

    public UsersApiTests(AdminApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/authmanager/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_users_works_with_no_query_parameters_at_all()
    {
        // Regression: this 400'd before `page`/`pageSize`/etc had C# default values, because
        // Minimal API treats parameters with no default as required when bound from the
        // query string.
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/authmanager/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1); // at least the seeded SuperAdmin
    }

    [Fact]
    public async Task Full_user_lifecycle_via_HTTP()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/authmanager/api/users", new
        {
            userName = "http-alice",
            email = "http-alice@example.com",
            password = "Passw0rd!123",
            emailConfirmed = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("id").GetString()!;

        var getResponse = await client.GetAsync($"/authmanager/api/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync($"/authmanager/api/users/{userId}", new
        {
            userName = "http-alice",
            email = "http-alice@example.com",
            emailConfirmed = true,
            twoFactorEnabled = false,
            lockoutEnabled = true
        });
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync($"/authmanager/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var afterDelete = await client.GetAsync($"/authmanager/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task DELETE_user_claim_accepts_a_JSON_body()
    {
        // Regression: this 500'd at startup with "Body was inferred but the method does not
        // allow inferred body parameters" until the `claim` parameter got [FromBody].
        var client = await _factory.CreateAuthenticatedClientAsync();

        var createResponse = await client.PostAsJsonAsync("/authmanager/api/users", new
        {
            userName = "http-bob",
            email = "http-bob@example.com",
            password = "Passw0rd!123"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("id").GetString()!;

        await client.PostAsJsonAsync($"/authmanager/api/users/{userId}/claims", new { type = "department", value = "Engineering" });

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/authmanager/api/users/{userId}/claims")
        {
            Content = JsonContent.Create(new { type = "department", value = "Engineering" })
        };
        var deleteClaimResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, deleteClaimResponse.StatusCode);
    }

    [Fact]
    public async Task Temporary_role_assignment_round_trips_through_the_API()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        await client.PostAsync("/authmanager/api/roles", JsonContent.Create(new { name = "http-manager" }));

        var createResponse = await client.PostAsJsonAsync("/authmanager/api/users", new
        {
            userName = "http-carol",
            email = "http-carol@example.com",
            password = "Passw0rd!123"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("id").GetString()!;

        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);
        var grantResponse = await client.PostAsync(
            $"/authmanager/api/users/{userId}/roles/http-manager/temporary?expiresAt={Uri.EscapeDataString(expiresAt.ToString("O"))}",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, grantResponse.StatusCode);

        var expiriesResponse = await client.GetAsync($"/authmanager/api/users/{userId}/roles/expiries");
        var expiries = await expiriesResponse.Content.ReadFromJsonAsync<Dictionary<string, DateTimeOffset>>();
        Assert.True(expiries!.ContainsKey("http-manager"));
    }
}
