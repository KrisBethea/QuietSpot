using Microsoft.EntityFrameworkCore;
using QuietSpot.Api.Data;
using QuietSpot.Shared.Scoring;

namespace QuietSpot.Api.Services;

/// <summary>
/// Periodic sweep so cached scores decay toward the baseline even when no new
/// reports arrive, and so favorited venues crossing below a user's threshold
/// can trigger a quiet-now alert.
/// </summary>
public class ScoreSweepService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<ScoreSweepService> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMinutes(config.GetValue("Scoring:SweepMinutes", 10));
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await SweepAsync(ct); }
            catch (Exception ex) { log.LogError(ex, "Score sweep failed"); }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scoring = scope.ServiceProvider.GetRequiredService<ScoringService>();

        // Only venues whose cache could have changed: had any report in the last
        // window, or are still showing live confidence that should now decay.
        var cutoff = DateTime.UtcNow.AddMinutes(-QuietnessScoring.MaxReportAgeMinutes);
        var venueIds = await db.Reports
            .Where(r => r.CreatedAtUtc >= cutoff)
            .Select(r => r.VenueId)
            .Union(db.Venues.Where(v => v.CachedConfidence > 0.01).Select(v => v.Id))
            .Distinct()
            .ToListAsync(ct);

        foreach (var id in venueIds)
        {
            var venue = await db.Venues.FirstAsync(v => v.Id == id, ct);
            var before = venue.CachedScore;
            var result = await scoring.RecomputeVenueAsync(venue, ct);

            await MaybeQueueQuietAlertsAsync(db, venue, before, result.Score, ct);
        }

        log.LogInformation("Score sweep recomputed {Count} venues", venueIds.Count);
    }

    private async Task MaybeQueueQuietAlertsAsync(AppDbContext db, Venue venue, double before, double after, CancellationToken ct)
    {
        if (after >= QuietnessScoring.QuietThreshold || before < QuietnessScoring.QuietThreshold)
            return; // only fire on a downward crossing into "quiet"

        var today = DateTime.UtcNow.Date;
        var favorites = await db.Favorites
            .Where(f => f.VenueId == venue.Id
                        && after <= f.AlertThreshold
                        && (f.LastAlertedAtUtc == null || f.LastAlertedAtUtc < today))
            .ToListAsync(ct);

        foreach (var fav in favorites)
        {
            fav.LastAlertedAtUtc = DateTime.UtcNow;
            // TODO (build step 4): hand off to FCM here.
            // e.g. await _pushSender.SendQuietNowAsync(fav.DeviceId, venue);
        }
        if (favorites.Count > 0) await db.SaveChangesAsync(ct);
    }
}
