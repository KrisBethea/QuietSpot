namespace QuietSpot.Api.Data;

public class Venue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Category { get; set; }   // cafe | library | gym | grocery | ...
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int GeofenceMeters { get; set; } = 75;
    public string TimeZoneId { get; set; } = "America/Chicago";

    // Precomputed score cache — the feed reads these, never computes.
    public double CachedScore { get; set; } = 0.5;
    public double CachedConfidence { get; set; }
    public string CachedStatusLabel { get; set; } = "Usually moderate";
    public bool CachedIsLive { get; set; }
    public DateTime CachedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Report> Reports { get; set; } = [];
    public List<Baseline> Baselines { get; set; } = [];
    public List<VenueAttribute> Attributes { get; set; } = [];
}

public class Report
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VenueId { get; set; }
    public Guid DeviceId { get; set; }
    public double Level { get; set; }               // 0, 0.5, 1
    public string Source { get; set; } = "in_app";  // in_app | notification
    public bool GeoVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Venue? Venue { get; set; }
    public Device? Device { get; set; }
}

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Platform { get; set; } = "unknown";
    public double Credibility { get; set; } = 0.5;  // 0..1, moves with consensus agreement
    public int Streak { get; set; }
    public DateTime? LastReportAtUtc { get; set; }
    public DateTime? LastPromptedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class Baseline
{
    public Guid VenueId { get; set; }
    public int HourOfWeek { get; set; }             // 0..167, Monday 00:00 = 0
    public double Busyness { get; set; } = 0.5;     // 0..1
    public int SampleCount { get; set; }

    public Venue? Venue { get; set; }
}

public class VenueAttribute
{
    public Guid VenueId { get; set; }
    public required string Attribute { get; set; }  // "solo_seating", "low_music", ...
    public int Confirms { get; set; } = 1;

    public Venue? Venue { get; set; }
}

public class Favorite
{
    public Guid DeviceId { get; set; }
    public Guid VenueId { get; set; }
    public double AlertThreshold { get; set; } = 0.35;
    public DateTime? LastAlertedAtUtc { get; set; }
}
