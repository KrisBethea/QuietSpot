namespace QuietSpot.Shared.Dtos;

public record VenueSummaryDto(
    Guid Id,
    string Name,
    string Category,
    double Lat,
    double Lng,
    double DistanceMeters,
    double Score,
    double Confidence,
    string StatusLabel,
    bool IsLive,
    int RecentReportCount,
    int? LastReportMinutesAgo);

public record VenueDetailDto(
    Guid Id,
    string Name,
    string Category,
    double Lat,
    double Lng,
    int GeofenceMeters,
    double Score,
    double Confidence,
    string StatusLabel,
    bool IsLive,
    int RecentReportCount,
    int? LastReportMinutesAgo,
    IReadOnlyList<string> Attributes,
    IReadOnlyList<double> TodayBaseline,   // 24 values, 0..1, venue-local hours 0-23
    bool IsFavorite);

public record CreateReportRequest(
    Guid VenueId,
    Guid DeviceId,
    double Level,           // 0, 0.5, 1
    string Source,          // "in_app" | "notification"
    double? Lat,
    double? Lng);

public record CreateReportResponse(
    Guid ReportId,
    bool GeoVerified,
    int Streak,
    string NewStatusLabel);

public record RegisterDeviceRequest(string Platform);
public record RegisterDeviceResponse(Guid DeviceId);

public record FavoriteRequest(Guid DeviceId, Guid VenueId, double AlertThreshold = 0.35);
