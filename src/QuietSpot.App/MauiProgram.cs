using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using QuietSpot.App.Delegates;
using QuietSpot.App.Services;
using QuietSpot.App.ViewModels;
using QuietSpot.App.Views;
using Shiny;

namespace QuietSpot.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiMaps()
            .UseShiny();

        // --- Shiny background services ---
        // Geofencing: wakes VenueGeofenceDelegate on region enter/exit, even app-killed.
        builder.Services.AddGeofencing<VenueGeofenceDelegate>();
        // Local notifications with action buttons (the Quiet/Some/Packed report prompt).
        builder.Services.AddNotifications<ReportNotificationDelegate>();

        // --- App services ---
        builder.Services.AddSingleton<Session>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<ReportQueue>();
        builder.Services.AddSingleton<GeofenceSyncService>();

        // --- ViewModels & pages ---
        builder.Services.AddTransient<OnboardingViewModel>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<FeedViewModel>();
        builder.Services.AddTransient<FeedPage>();
        builder.Services.AddTransient<VenueDetailViewModel>();
        builder.Services.AddTransient<VenueDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
