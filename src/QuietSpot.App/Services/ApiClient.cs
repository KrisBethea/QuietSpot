using System.Net.Http.Json;
using QuietSpot.Shared.Dtos;

namespace QuietSpot.App.Services;

public class ApiClient(Session session)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private string Base => session.ApiBaseUrl.TrimEnd('/');

    public async Task<Guid> EnsureDeviceRegisteredAsync(CancellationToken ct = default)
    {
        if (session.DeviceId is { } existing) return existing;

        var resp = await _http.PostAsJsonAsync($"{Base}/devices",
            new RegisterDeviceRequest(DeviceInfo.Platform.ToString().ToLowerInvariant()), ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<RegisterDeviceResponse>(ct)
                   ?? throw new InvalidOperationException("Empty device registration response");
        session.DeviceId = body.DeviceId;
        return body.DeviceId;
    }

    public async Task<List<VenueSummaryDto>> GetNearbyAsync(double lat, double lng, double radiusMeters = 3000, CancellationToken ct = default)
        => await _http.GetFromJsonAsync<List<VenueSummaryDto>>(
               $"{Base}/venues/nearby?lat={lat}&lng={lng}&radiusMeters={radiusMeters}", ct) ?? [];

    public async Task<VenueDetailDto?> GetVenueAsync(Guid id, CancellationToken ct = default)
    {
        var deviceQuery = session.DeviceId is { } d ? $"?deviceId={d}" : "";
        return await _http.GetFromJsonAsync<VenueDetailDto>($"{Base}/venues/{id}{deviceQuery}", ct);
    }

    public async Task<CreateReportResponse?> PostReportAsync(CreateReportRequest req, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync($"{Base}/reports", req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Conflict) return null; // rate-limited: already reported
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<CreateReportResponse>(ct);
    }

    public async Task SetFavoriteAsync(Guid venueId, bool favorite, CancellationToken ct = default)
    {
        var deviceId = await EnsureDeviceRegisteredAsync(ct);
        if (favorite)
        {
            var resp = await _http.PostAsJsonAsync($"{Base}/favorites", new FavoriteRequest(deviceId, venueId), ct);
            resp.EnsureSuccessStatusCode();
        }
        else
        {
            var resp = await _http.DeleteAsync($"{Base}/favorites?deviceId={deviceId}&venueId={venueId}", ct);
            resp.EnsureSuccessStatusCode();
        }
    }
}
