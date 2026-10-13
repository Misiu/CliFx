using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using CliFx.Demo.Fleet.Domain;

namespace CliFx.Demo.Fleet.Services;

public class FleetApiException(string message, bool isUnauthorized = false, bool isNotFound = false)
    : Exception(message)
{
    public bool IsUnauthorized { get; } = isUnauthorized;

    public bool IsNotFound { get; } = isNotFound;
}

// Client for the dummyjson.com API (https://dummyjson.com/docs/auth), where "devices" are products
// from the smartphone, laptop and tablet categories. The endpoints under /auth require a bearer token.
public class FleetApiClient(HttpClient http)
{
    private const string Fields = "id,title,brand,category,price,stock";

    private record LoginResponse(int Id, string Username, string AccessToken);

    private record ProductsResponse(Device[] Products);

    private static async Task<T> SendAsync<T>(
        HttpClient http,
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        using var response = await http.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new FleetApiException("The session is invalid or has expired.", true);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new FleetApiException("The requested resource does not exist.", isNotFound: true);

        if (!response.IsSuccessStatusCode)
            throw new FleetApiException($"The API responded with {(int)response.StatusCode}.");

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new FleetApiException("The API responded with an empty body.");
    }

    private static HttpRequestMessage CreateRequest(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    public async Task<Session> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken
    )
    {
        const int expiresInMinutes = 60;

        using var response = await http.PostAsJsonAsync(
            "auth/login",
            new
            {
                username,
                password,
                expiresInMins = expiresInMinutes,
            },
            cancellationToken
        );

        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new FleetApiException("Invalid username or password.", true);

        response.EnsureSuccessStatusCode();

        var login =
            await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
            ?? throw new FleetApiException("The API responded with an empty body.");

        return new Session(
            login.Id,
            login.Username,
            login.AccessToken,
            DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes)
        );
    }

    // Returns devices whose details match the query, preferring the ones that start with it
    public async Task<IReadOnlyList<Device>> SearchDevicesAsync(
        string accessToken,
        string? query,
        CancellationToken cancellationToken
    )
    {
        var url = string.IsNullOrWhiteSpace(query)
            ? $"auth/products?limit=0&select={Fields}"
            : $"auth/products/search?q={Uri.EscapeDataString(query)}&limit=0&select={Fields}";

        var response = await SendAsync<ProductsResponse>(
            http,
            CreateRequest(url, accessToken),
            cancellationToken
        );

        return response
            .Products.Where(p => Device.CategorySlugs.Contains(p.Category))
            .OrderBy(p =>
                !string.IsNullOrWhiteSpace(query)
                && p.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1
            )
            .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<Device?> TryGetDeviceAsync(
        string accessToken,
        int id,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await SendAsync<Device>(
                http,
                CreateRequest($"auth/products/{id}?select={Fields}", accessToken),
                cancellationToken
            );
        }
        catch (FleetApiException ex) when (ex.IsNotFound)
        {
            return null;
        }
    }
}
