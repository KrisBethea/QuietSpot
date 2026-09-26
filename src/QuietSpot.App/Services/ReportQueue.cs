using QuietSpot.App.Models;
using QuietSpot.Shared.Dtos;
using SQLite;

namespace QuietSpot.App.Services;

/// <summary>
/// Reports must never be lost to bad signal — a café basement is exactly where
/// our users are. Try the network first; on failure, persist to SQLite and
/// flush on next app start / feed refresh.
/// </summary>
public class ReportQueue(ApiClient api)
{
    private SQLiteAsyncConnection? _db;

    private async Task<SQLiteAsyncConnection> GetDbAsync()
    {
        if (_db is null)
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "quietspot.db3");
            _db = new SQLiteAsyncConnection(path);
            await _db.CreateTableAsync<PendingReport>();
        }
        return _db;
    }

    /// <summary>Returns the server response if sent live, or null if queued offline.</summary>
    public async Task<CreateReportResponse?> SubmitAsync(Guid venueId, double level, string source, double? lat, double? lng)
    {
        var deviceId = await api.EnsureDeviceRegisteredAsync();
        var request = new CreateReportRequest(venueId, deviceId, level, source, lat, lng);

        try
        {
            return await api.PostReportAsync(request);
        }
        catch (Exception)
        {
            var db = await GetDbAsync();
            await db.InsertAsync(new PendingReport
            {
                VenueId = venueId,
                Level = level,
                Source = source,
                Lat = lat,
                Lng = lng,
                CreatedAtUtc = DateTime.UtcNow
            });
            return null;
        }
    }

    public async Task FlushAsync()
    {
        var db = await GetDbAsync();
        var pending = await db.Table<PendingReport>().OrderBy(p => p.CreatedAtUtc).ToListAsync();
        if (pending.Count == 0) return;

        var deviceId = await api.EnsureDeviceRegisteredAsync();
        foreach (var p in pending)
        {
            // Drop reports too stale to be useful — they'd only pollute the score.
            if ((DateTime.UtcNow - p.CreatedAtUtc).TotalMinutes > 90)
            {
                await db.DeleteAsync(p);
                continue;
            }

            try
            {
                await api.PostReportAsync(new CreateReportRequest(p.VenueId, deviceId, p.Level, p.Source, p.Lat, p.Lng));
                await db.DeleteAsync(p);
            }
            catch (Exception)
            {
                break; // still offline; try again later
            }
        }
    }
}
