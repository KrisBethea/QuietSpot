using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuietSpot.App.Services;
using QuietSpot.Shared.Dtos;

namespace QuietSpot.App.ViewModels;

public partial class VenueDetailViewModel(ApiClient api, ReportQueue reportQueue) : ObservableObject, IQueryAttributable
{
    private Guid _venueId;

    [ObservableProperty] private VenueDetailDto? venue;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private bool isFavorite;
    [ObservableProperty] private bool hasReported;
    [ObservableProperty] private string? confirmationText;

    public ObservableCollection<string> AttributeChips { get; } = [];
    public ObservableCollection<HourBar> TodayBars { get; } = [];

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("venueId", out var raw) && raw is Guid id)
        {
            _venueId = id;
            _ = LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Venue = await api.GetVenueAsync(_venueId);
            if (Venue is null) return;

            IsFavorite = Venue.IsFavorite;

            AttributeChips.Clear();
            foreach (var attr in Venue.Attributes)
                AttributeChips.Add(Humanize(attr));

            var nowHour = DateTime.Now.Hour;
            TodayBars.Clear();
            for (var h = 7; h <= 21; h++) // show waking hours only
                TodayBars.Add(new HourBar(h, Venue.TodayBaseline[h], h == nowHour));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task ReportQuietAsync() => SubmitReportAsync(0.0);

    [RelayCommand]
    private Task ReportSomeAsync() => SubmitReportAsync(0.5);

    [RelayCommand]
    private Task ReportPackedAsync() => SubmitReportAsync(1.0);

    private async Task SubmitReportAsync(double level)
    {
        if (HasReported) return;

        double? lat = null, lng = null;
        try
        {
            var loc = await Geolocation.GetLastKnownLocationAsync();
            lat = loc?.Latitude;
            lng = loc?.Longitude;
        }
        catch { /* unverified report still counts */ }

        var response = await reportQueue.SubmitAsync(_venueId, level, "in_app", lat, lng);

        HasReported = true;
        ConfirmationText = response is null
            ? "Saved — we'll send it when you're back online."
            : response.Streak > 1
                ? $"Logged. {response.Streak}-day streak — thanks for helping."
                : "Logged. Someone nearby will find a quieter seat because of you.";

        if (response is not null) await LoadAsync(); // reflect the new status immediately
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        IsFavorite = !IsFavorite;
        try
        {
            await api.SetFavoriteAsync(_venueId, IsFavorite);
        }
        catch
        {
            IsFavorite = !IsFavorite; // revert on failure
            return;
        }

        // Contextual notification soft-ask: only now does a ping have obvious value.
        if (IsFavorite && !Preferences.Get("asked_notifications", false))
        {
            Preferences.Set("asked_notifications", true);
            var want = await Shell.Current.DisplayAlert(
                "Quiet-now pings?",
                $"Want a heads-up when {Venue?.Name} gets quiet? You can turn this off anytime.",
                "Yes, notify me", "No thanks");
            if (want)
            {
                try { await Permissions.RequestAsync<Permissions.PostNotifications>(); }
                catch { /* iOS: notification permission is requested by Shiny channel setup */ }
            }
        }
    }

    private static string Humanize(string attr) => attr.Replace('_', ' ') switch
    {
        var s => char.ToUpper(s[0]) + s[1..]
    };
}

public record HourBar(int Hour, double Busyness, bool IsNow)
{
    public double BarHeight => 12 + Busyness * 48;
    public Color BarColor => IsNow ? Color.FromArgb("#0F6E56") : Color.FromArgb("#D3D1C7");
    public string HourLabel => Hour switch
    {
        0 => "12a", < 12 => $"{Hour}a", 12 => "12p", _ => $"{Hour - 12}p"
    };
    public bool ShowLabel => Hour % 3 == 0;
}
