using Microsoft.EntityFrameworkCore;
using QuietSpot.Api.Data;
using QuietSpot.Api.Services;
using QuietSpot.Shared.Dtos;
using QuietSpot.Shared.Scoring;

var builder = WebApplication.CreateBuilder(args);

// Postgres in production, SQLite file for zero-setup local dev.
var pg = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    if (!string.IsNullOrWhiteSpace(pg)) opt.UseNpgsql(pg);
    else opt.UseSqlite("Data Source=quietspot-dev.db");
});

builder.Services.AddScoped<ScoringService>();
builder.Services.AddHostedService<ScoreSweepService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync(); // swap for migrations once schema settles
    await SeedData.EnsureSeededAsync(db);
}

app.MapGet("/health", () => Results.Ok(new { ok = true, at = DateTime.UtcNow }));

// ---------- Devices ----------

app.MapPost("/devices", async (RegisterDeviceRequest req, AppDbContext db) =>
{
    var device = new Device { Platform = req.Platform };
    db.Devices.Add(device);
    await db.SaveChangesAsync();
    return Results.Ok(new RegisterDeviceResponse(device.Id));
});

// ---------- Venues ----------

app.MapGet("/venues/nearby", async (double lat, double lng, AppDbContext db, double radiusMeters = 3000) =>
{
    // Bounding-box prefilter in SQL, exact haversine + sort in memory.
    // Fine for MVP scale; swap to PostGIS ST_DWithin when venue count grows.
    var dLat = radiusMeters / 111_320.0;
    var dLng = radiusMeters / (111_320.0 * Math.Cos(lat * Math.PI / 180.0));

    var candidates = await db.Venues
        .Where(v => v.Lat >= lat - dLat && v.Lat <= lat + dLat
                 && v.Lng >= lng - dLng && v.Lng <= lng + dLng)
        .ToListAsync();

    var now = DateTime.UtcNow;
    var windowStart = now.AddMinutes(-QuietnessScoring.MaxReportAgeMinutes);
    var ids = candidates.Select(v => v.Id).ToList();

    var reportMeta = await db.Reports
        .Where(r => ids.Contains(r.VenueId) && r.CreatedAtUtc >= windowStart)
        .GroupBy(r => r.VenueId)
        .Select(g => new { VenueId = g.Key, Count = g.Count(), Last = g.Max(r => r.CreatedAtUtc) })
        .ToDictionaryAsync(x => x.VenueId);

    var result = candidates
        .Select(v =>
        {
            reportMeta.TryGetValue(v.Id, out var meta);
            return new VenueSummaryDto(
                v.Id, v.Name, v.Category, v.Lat, v.Lng,
                Haversine(lat, lng, v.Lat, v.Lng),
                Math.Round(v.CachedScore, 3),
                Math.Round(v.CachedConfidence, 3),
                v.CachedStatusLabel,
                v.CachedIsLive,
                meta?.Count ?? 0,
                meta is null ? null : (int)Math.Round((now - meta.Last).TotalMinutes));
        })
        .Where(v => v.DistanceMeters <= radiusMeters)
        .OrderBy(v => v.Score)            // quietest first — the product's whole point
        .ThenBy(v => v.DistanceMeters)
        .ToList();

    return Results.Ok(result);
});

app.MapGet("/venues/{id:guid}", async (Guid id, AppDbContext db, Guid? deviceId) =>
{
    var venue = await db.Venues
        .Include(v => v.Attributes)
        .FirstOrDefaultAsync(v => v.Id == id);
    if (venue is null) return Results.NotFound();

    var now = DateTime.UtcNow;
    var windowStart = now.AddMinutes(-QuietnessScoring.MaxReportAgeMinutes);
    var recent = await db.Reports
        .Where(r => r.VenueId == id && r.CreatedAtUtc >= windowStart)
        .OrderByDescending(r => r.CreatedAtUtc)
        .Select(r => r.CreatedAtUtc)
        .ToListAsync();

    var local = ScoringService.ToVenueLocal(venue, now);
    var dayStartHourOfWeek = QuietnessScoring.HourOfWeek(local.Date);
    var baselineRows = await db.Baselines
        .Where(b => b.VenueId == id
                 && b.HourOfWeek >= dayStartHourOfWeek
                 && b.HourOfWeek < dayStartHourOfWeek + 24)
        .ToDictionaryAsync(b => b.HourOfWeek - dayStartHourOfWeek, b => b.Busyness);

    var today = Enumerable.Range(0, 24)
        .Select(h => baselineRows.TryGetValue(h, out var v) ? v : ScoringService.CategoryDefault(venue.Category, h))
        .ToList();

    var isFavorite = deviceId.HasValue &&
        await db.Favorites.AnyAsync(f => f.DeviceId == deviceId && f.VenueId == id);

    return Results.Ok(new VenueDetailDto(
        venue.Id, venue.Name, venue.Category, venue.Lat, venue.Lng, venue.GeofenceMeters,
        Math.Round(venue.CachedScore, 3), Math.Round(venue.CachedConfidence, 3),
        venue.CachedStatusLabel, venue.CachedIsLive,
        recent.Count,
        recent.Count == 0 ? null : (int)Math.Round((now - recent[0]).TotalMinutes),
        venue.Attributes.Select(a => a.Attribute).ToList(),
        today,
        isFavorite));
});

// ---------- Reports ----------

app.MapPost("/reports", async (CreateReportRequest req, AppDbContext db, ScoringService scoring) =>
{
    if (req.Level is not (0 or 0.5 or 1)) return Results.BadRequest("Level must be 0, 0.5 or 1.");

    var venue = await db.Venues.FindAsync(req.VenueId);
    if (venue is null) return Results.NotFound("Unknown venue.");
    var device = await db.Devices.FindAsync(req.DeviceId);
    if (device is null) return Results.NotFound("Unknown device. Register first via POST /devices.");

    // Rate limit: one report per device per venue per 30 minutes.
    var cutoff = DateTime.UtcNow.AddMinutes(-30);
    var alreadyReported = await db.Reports.AnyAsync(r =>
        r.DeviceId == req.DeviceId && r.VenueId == req.VenueId && r.CreatedAtUtc >= cutoff);
    if (alreadyReported) return Results.Conflict("Already reported here recently.");

    var geoVerified = req.Lat.HasValue && req.Lng.HasValue &&
        Haversine(req.Lat.Value, req.Lng.Value, venue.Lat, venue.Lng) <= venue.GeofenceMeters * 1.5;

    var report = new Report
    {
        VenueId = req.VenueId,
        DeviceId = req.DeviceId,
        Level = req.Level,
        Source = req.Source,
        GeoVerified = geoVerified
    };
    db.Reports.Add(report);

    // Streak: consecutive calendar days with at least one report.
    var todayUtc = DateTime.UtcNow.Date;
    device.Streak = device.LastReportAtUtc?.Date == todayUtc.AddDays(-1) ? device.Streak + 1
                  : device.LastReportAtUtc?.Date == todayUtc ? device.Streak
                  : 1;
    device.LastReportAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    await scoring.UpdateBaselineAsync(venue, report);
    await scoring.UpdateCredibilityAsync(report);
    var result = await scoring.RecomputeVenueAsync(venue);

    return Results.Ok(new CreateReportResponse(report.Id, geoVerified, device.Streak, result.DisplayLabel));
});

// ---------- Favorites ----------

app.MapPost("/favorites", async (FavoriteRequest req, AppDbContext db) =>
{
    var exists = await db.Favorites.FindAsync(req.DeviceId, req.VenueId);
    if (exists is null)
    {
        db.Favorites.Add(new Favorite { DeviceId = req.DeviceId, VenueId = req.VenueId, AlertThreshold = req.AlertThreshold });
        await db.SaveChangesAsync();
    }
    return Results.Ok();
});

app.MapDelete("/favorites", async (Guid deviceId, Guid venueId, AppDbContext db) =>
{
    var fav = await db.Favorites.FindAsync(deviceId, venueId);
    if (fav is not null)
    {
        db.Favorites.Remove(fav);
        await db.SaveChangesAsync();
    }
    return Results.Ok();
});

app.Run();

static double Haversine(double lat1, double lng1, double lat2, double lng2)
{
    const double R = 6_371_000;
    var dLat = (lat2 - lat1) * Math.PI / 180;
    var dLng = (lng2 - lng1) * Math.PI / 180;
    var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
    return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
}
