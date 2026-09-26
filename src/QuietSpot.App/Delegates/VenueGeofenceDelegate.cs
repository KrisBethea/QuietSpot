using QuietSpot.App.Services;
using Shiny.Locations;
using Shiny.Notifications;

namespace QuietSpot.App.Delegates;

/// <summary>
/// Dwell detection without timers: on geofence ENTER, schedule a local
/// notification 12 minutes out; on EXIT, cancel it. If the user leaves before
/// the dwell window elapses, the prompt simply never fires. Shiny wakes this
/// delegate even when the app process is dead.
/// </summary>
public class VenueGeofenceDelegate(INotificationManager notifications, Session session) : IGeofenceDelegate
{
    public const string ReportChannelId = "report_prompt";
    private const int DwellMinutes = 12;
    private const int MaxPromptsPerDay = 3;

    public async Task OnStatusChanged(GeofenceState newStatus, GeofenceRegion region)
    {
        var notificationId = StableId(region.Identifier);

        if (newStatus == GeofenceState.Exited)
        {
            await notifications.Cancel(notificationId);
            return;
        }

        if (newStatus != GeofenceState.Entered) return;
        if (!ShouldPromptToday()) return;

        await notifications.Send(new Notification
        {
            Id = notificationId,
            Title = "Here for a bit?",
            Message = "One tap helps someone nearby find a quiet seat.",
            Channel = ReportChannelId,
            ScheduleDate = DateTimeOffset.UtcNow.AddMinutes(DwellMinutes),
            Payload = new Dictionary<string, string> { ["venueId"] = region.Identifier }
        });
    }

    private bool ShouldPromptToday()
    {
        // Respect the daily cap and the "swiped away = leave me alone" signal.
        var today = DateTime.UtcNow.Date;
        var count = Preferences.Get("prompt_count_date", "") == today.ToString("O")
            ? Preferences.Get("prompt_count", 0)
            : 0;

        if (count >= MaxPromptsPerDay) return false;

        Preferences.Set("prompt_count_date", today.ToString("O"));
        Preferences.Set("prompt_count", count + 1);
        return true;
    }

    /// <summary>Deterministic int id per venue so exit-cancel finds the scheduled prompt.</summary>
    private static int StableId(string venueId)
        => Math.Abs(venueId.GetHashCode() % 100_000) + 1000;
}
