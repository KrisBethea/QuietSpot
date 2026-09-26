using QuietSpot.Shared.Dtos;
using Shiny;
using Shiny.Locations;

namespace QuietSpot.App.Services;

/// <summary>
/// The OS only allows a small number of geofences (iOS ~20, Android ~60/100),
/// so we "fence-shuffle": after each feed load, register fences for the nearest
/// venues only, and re-register as the user moves. Gated behind the user's
/// opt-in (Session.BackgroundReportingEnabled) and the background permission.
/// </summary>
public class GeofenceSyncService(IGeofenceManager geofenceManager, Session session)
{
    private const int MaxFences = 15; // stay under the iOS 20-region cap with headroom

    public async Task SyncAsync(IReadOnlyList<VenueSummaryDto> nearbyVenues)
    {
        if (!session.BackgroundReportingEnabled) return;

        var access = await geofenceManager.RequestAccess();
        if (access != Shiny.AccessState.Available) return;

        await geofenceManager.StopAllMonitoring();

        foreach (var venue in nearbyVenues.OrderBy(v => v.DistanceMeters).Take(MaxFences))
        {
            await geofenceManager.StartMonitoring(new GeofenceRegion(
                venue.Id.ToString(),
                new Position(venue.Lat, venue.Lng),
                Distance.FromMeters(100))
            {
                NotifyOnEntry = true,
                NotifyOnExit = true,
                SingleUse = false
            });
        }
    }
}
