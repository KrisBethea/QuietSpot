using QuietSpot.App.Services;
using Shiny.Notifications;

namespace QuietSpot.App.Delegates;

/// <summary>
/// Receives the user's tap on a notification action button — Quiet / Some /
/// Packed — and submits the report in the background without opening the app.
/// The channel with these actions is registered in App startup (see FeedPage).
/// </summary>
public class ReportNotificationDelegate(ReportQueue reportQueue) : INotificationDelegate
{
    public const string ActionQuiet = "report_quiet";
    public const string ActionSome = "report_some";
    public const string ActionPacked = "report_packed";

    public async Task OnEntry(NotificationResponse response)
    {
        var payload = response.Notification.Payload;
        var venueIdRaw = payload is not null && payload.TryGetValue("venueId", out var value)
            ? value
            : null;
        if (!Guid.TryParse(venueIdRaw, out var venueId)) return;

        double? level = response.ActionIdentifier switch
        {
            ActionQuiet => 0.0,
            ActionSome => 0.5,
            ActionPacked => 1.0,
            _ => null // body tap: app opens; user can report in the venue screen
        };
        if (level is null) return;

        // Best-effort current position for geo-verification; fine if unavailable.
        double? lat = null, lng = null;
        try
        {
            var loc = await Geolocation.GetLastKnownLocationAsync();
            lat = loc?.Latitude;
            lng = loc?.Longitude;
        }
        catch { /* location off or denied — report still counts, just unverified */ }

        await reportQueue.SubmitAsync(venueId, level.Value, "notification", lat, lng);
    }

    public static ChannelAction[] BuildActions() =>
    [
        new() { Identifier = ActionQuiet,  Title = "Quiet",  ActionType = ChannelActionType.None },
        new() { Identifier = ActionSome,   Title = "Some",   ActionType = ChannelActionType.None },
        new() { Identifier = ActionPacked, Title = "Packed", ActionType = ChannelActionType.None }
    ];
}
