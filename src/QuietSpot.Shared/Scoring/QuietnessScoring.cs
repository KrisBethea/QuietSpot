namespace QuietSpot.Shared.Scoring;

/// <summary>
/// A single report as seen by the scoring engine.
/// Level: 0.0 = quiet, 0.5 = some people, 1.0 = packed.
/// </summary>
public readonly record struct ScoredReport(
    double Level,
    double AgeMinutes,
    double ReporterCredibility,
    bool GeoVerified);

public enum BusynessStatus { Quiet, Moderate, Packed }

public readonly record struct ScoreResult(
    double Score,          // 0..1 blended busyness
    double Confidence,     // 0..1 confidence in live data
    BusynessStatus Status,
    bool IsLive)           // false => display as "usually quiet/moderate/packed"
{
    public string DisplayLabel => IsLive
        ? Status.ToString()
        : $"Usually {Status.ToString().ToLowerInvariant()}";
}

/// <summary>
/// displayed = confidence * liveEstimate + (1 - confidence) * baseline
///
/// - Each report's weight decays exponentially with age (half-life ~25 min)
///   and is scaled by reporter credibility; geo-verified reports get a boost.
/// - Confidence saturates with total weight: 1 - exp(-K * totalWeight).
/// - Below FullStatusConfidence the UI should show the "usually ..." framing.
/// </summary>
public static class QuietnessScoring
{
    public const double HalfLifeMinutes = 25.0;
    public const double ConfidenceK = 1.2;
    public const double GeoVerifiedBoost = 1.5;
    public const double FullStatusConfidence = 0.30;
    public const double MaxReportAgeMinutes = 90.0;

    /// <summary>Max share of total weight any single device may contribute (anti-spam).</summary>
    public const double MaxSingleDeviceShare = 0.40;

    public const double QuietThreshold = 0.35;
    public const double ModerateThreshold = 0.70;

    public static ScoreResult Compute(IReadOnlyList<ScoredReport> recentReports, double baselineBusyness)
    {
        var baseline = Math.Clamp(baselineBusyness, 0.0, 1.0);

        double totalWeight = 0.0;
        double weightedLevelSum = 0.0;

        foreach (var r in recentReports)
        {
            if (r.AgeMinutes < 0 || r.AgeMinutes > MaxReportAgeMinutes) continue;

            var decay = Math.Exp(-Math.Log(2) * r.AgeMinutes / HalfLifeMinutes);
            var credibility = Math.Clamp(r.ReporterCredibility, 0.05, 1.0);
            var weight = decay * credibility * (r.GeoVerified ? GeoVerifiedBoost : 1.0);

            totalWeight += weight;
            weightedLevelSum += weight * Math.Clamp(r.Level, 0.0, 1.0);
        }

        double confidence, score;
        if (totalWeight <= 0)
        {
            confidence = 0;
            score = baseline;
        }
        else
        {
            var liveEstimate = weightedLevelSum / totalWeight;
            confidence = 1.0 - Math.Exp(-ConfidenceK * totalWeight);
            score = confidence * liveEstimate + (1.0 - confidence) * baseline;
        }

        var status = score switch
        {
            < QuietThreshold => BusynessStatus.Quiet,
            < ModerateThreshold => BusynessStatus.Moderate,
            _ => BusynessStatus.Packed
        };

        return new ScoreResult(score, confidence, status, confidence >= FullStatusConfidence);
    }

    /// <summary>Slow EWMA update of an hour-of-week baseline from a new report.</summary>
    public static double UpdateBaseline(double currentBaseline, double reportLevel, double alpha = 0.05)
        => Math.Clamp((1 - alpha) * currentBaseline + alpha * Math.Clamp(reportLevel, 0, 1), 0, 1);

    /// <summary>0..167 slot: Monday 00:00 = 0 ... Sunday 23:00 = 167 (venue-local time).</summary>
    public static int HourOfWeek(DateTime localTime)
    {
        var day = ((int)localTime.DayOfWeek + 6) % 7; // Monday = 0
        return day * 24 + localTime.Hour;
    }
}
