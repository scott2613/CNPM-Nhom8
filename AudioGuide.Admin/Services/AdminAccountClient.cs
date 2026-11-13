using System.Net;
using System.Net.Http.Json;
using AudioGuide.Accounts;

namespace AudioGuide.Admin.Services;

public sealed class AdminAccountClient(IHttpClientFactory factory)
{
    private const string Route = "api/admin-account";

    public async Task<AdminAccountStatus?> GetStatusAsync(CancellationToken ct)
    {
        using var client = factory.CreateClient("AudioGuideApiClient");
        using var response = await client.GetAsync(Route, ct);
        response.EnsureSuccessStatusCode();
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        return await response.Content.ReadFromJsonAsync<AdminAccountStatus>(ct)
            ?? throw new HttpRequestException("Missing admin account status.");
    }

    public async Task<HttpStatusCode> InitializeAsync(AdminAccountSeed seed, CancellationToken ct)
    {
        using var client = factory.CreateClient("AudioGuideApiClient");
        using var response = await client.PostAsJsonAsync(Route + "/initialize", seed, ct);
        return response.StatusCode;
    }

    public async Task<AdminAccountStatus?> VerifyAsync(AdminLoginRequest request, CancellationToken ct)
    {
        using var client = factory.CreateClient("AudioGuideApiClient");
        using var response = await client.PostAsJsonAsync(Route + "/login", request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AdminAccountStatus>(ct)
            ?? throw new HttpRequestException("Missing admin account status.");
    }

    public async Task<HttpStatusCode> ChangeAsync(AdminAccountChange request, CancellationToken ct)
    {
        using var client = factory.CreateClient("AudioGuideApiClient");
        using var response = await client.PutAsJsonAsync(Route, request, ct);
        return response.StatusCode;
    }
}
