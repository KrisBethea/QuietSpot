using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuietSpot.App.Services;
using QuietSpot.Shared.Dtos;

namespace QuietSpot.App.ViewModels;

public partial class FeedViewModel(ApiClient api, ReportQueue reportQueue, GeofenceSyncService geofenceSync) : ObservableObject
{
    // Uptown Minneapolis fallback when location is unavailable/denied —
    // matches the seeded venues so the demo always shows data.
    private const double FallbackLat = 44.9523;
    private const double FallbackLng = -93.2965;

    public ObservableCollection<VenueSummaryDto> Venues { get; } = [];

    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isRefreshing;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string areaLabel = "Near you";

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (lat, lng) = await GetPositionAsync();

            // Stale reports are worse than no reports — flush the offline queue
            // before fetching so our own taps are reflected in the feed.
            await reportQueue.FlushAsync();

            var venues = await api.GetNearbyAsync(lat, lng);
            Venues.Clear();
            foreach (var v in venues) Venues.Add(v);

            await geofenceSync.SyncAsync(venues); // no-op unless user opted in
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Couldn't reach the server. Pull to retry. ({ex.Message})";
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenVenueAsync(VenueSummaryDto venue)
        => await Shell.Current.GoToAsync("venue", new Dictionary<string, object>
        {
            ["venueId"] = venue.Id
        });

    private async Task<(double Lat, double Lng)> GetPositionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                AreaLabel = "Uptown, Minneapolis";
                return (FallbackLat, FallbackLng);
            }

            var location = await Geolocation.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)));
            if (location is null)
            {
                AreaLabel = "Uptown, Minneapolis";
                return (FallbackLat, FallbackLng);
            }

            AreaLabel = "Near you";
            return (location.Latitude, location.Longitude);
        }
        catch
        {
            AreaLabel = "Uptown, Minneapolis";
            return (FallbackLat, FallbackLng);
        }
    }
}
