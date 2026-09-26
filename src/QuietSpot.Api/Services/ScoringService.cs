using Microsoft.EntityFrameworkCore;
using QuietSpot.Api.Data;
using QuietSpot.Shared.Scoring;

namespace QuietSpot.Api.Services;

public class ScoringService(AppDbContext db)
{
    /// <summary>
    /// Recompute one venue's cached score from recent reports + its baseline,
    /// with the single-device weight cap applied by keeping only each device's
    /// most recent report in the window.
    /// </summary>
    public async Task<ScoreResult> RecomputeVenueAsync(Venue venue, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-QuietnessScoring.MaxReportAgeMinutes);

        var recent = await db.Reports
            .Where(r => r.VenueId == venue.Id && r.CreatedAtUtc >= windowStart)
            .Include(r => r.Device)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);

        // One report per device in the window (most recent wins) — cheap anti-spam
        // alongside the formal MaxSingleDeviceShare cap.
        var perDevice = recent
            .GroupBy(r => r.DeviceId)
            .Select(g => g.First())
            .ToList();

        var scored = perDevice
            .Select(r => new ScoredReport(
                r.Level,
                (now - r.CreatedAtUtc).TotalMinutes,
                r.Device?.Credibility ?? 0.5,
                r.GeoVerified))
            .ToList();

        var baseline = await GetBaselineAsync(venue, now, ct);
        var result = QuietnessScoring.Compute(scored, baseline);

        venue.CachedScore = result.Score;
        venue.CachedConfidence = result.Confidence;
        venue.CachedStatusLabel = result.DisplayLabel;
        venue.CachedIsLive = result.IsLive;
        venue.CachedAtUtc = now;
        await db.SaveChangesAsync(ct);

        return result;
    }

    public async Task<double> GetBaselineAsync(Venue venue, DateTime utcNow, CancellationToken ct = default)
    {
        var local = ToVenueLocal(venue, utcNow);
        var hour = QuietnessScoring.HourOfWeek(local);
        var row = await db.Baselines
            .FirstOrDefaultAsync(x => x.VenueId == venue.Id && x.HourOfWeek == hour, ct);
        return row?.Busyness ?? CategoryDefault(venue.Category, local.Hour);
    }

    /// <summary>Feed a new report into the slow-moving hour-of-week baseline.</summary>
    public async Task UpdateBaselineAsync(Venue venue, Report report, CancellationToken ct = default)
    {
        var local = ToVenueLocal(venue, report.CreatedAtUtc);
        var hour = QuietnessScoring.HourOfWeek(local);
        var row = await db.Baselines
            .FirstOrDefaultAsync(x => x.VenueId == venue.Id && x.HourOfWeek == hour, ct);

        if (row is null)
        {
            row = new Baseline
            {
                VenueId = venue.Id,
                HourOfWeek = hour,
                Busyness = CategoryDefault(venue.Category, local.Hour),
                SampleCount = 0
            };
            db.Baselines.Add(row);
        }

        row.Busyness = QuietnessScoring.UpdateBaseline(row.Busyness, report.Level);
        row.SampleCount++;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Nudge reporter credibility toward/away based on agreement with other
    /// recent reports at the same venue. Deliberately simple for MVP.
    /// </summary>
    public async Task UpdateCredibilityAsync(Report report, CancellationToken ct = default)
    {
        var windowStart = report.CreatedAtUtc.AddMinutes(-45);
        var others = await db.Reports
            .Where(r => r.VenueId == report.VenueId
                        && r.DeviceId != report.DeviceId
                        && r.CreatedAtUtc >= windowStart)
            .Select(r => r.Level)
            .ToListAsync(ct);

        if (others.Count < 2) return; // not enough consensus to judge

        var consensus = others.Average();
        var agreement = 1.0 - Math.Abs(report.Level - consensus); // 1 = perfect agreement
        var device = await db.Devices.FindAsync([report.DeviceId], ct);
        if (device is null) return;

        var delta = (agreement - 0.5) * 0.04; // tiny steps; trust is earned slowly
        device.Credibility = Math.Clamp(device.Credibility + delta, 0.05, 1.0);
        await db.SaveChangesAsync(ct);
    }

    public static DateTime ToVenueLocal(Venue venue, DateTime utc)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(venue.TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        }
        catch (TimeZoneNotFoundException)
        {
            return utc;
        }
    }

    /// <summary>Cold-start fallback when a venue has no baseline row for this hour.</summary>
    public static double CategoryDefault(string category, int localHour) => category switch
    {
        "library" => localHour is >= 15 and <= 18 ? 0.5 : 0.3,
        "gym"     => localHour is (>= 6 and <= 8) or (>= 17 and <= 19) ? 0.8 : 0.4,
        "grocery" => localHour is >= 16 and <= 19 ? 0.75 : 0.45,
        "cafe"    => localHour is >= 8 and <= 11 ? 0.65 : 0.45,
        _         => 0.5
    };
}
