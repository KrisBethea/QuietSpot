using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuietSpot.App.Services;

namespace QuietSpot.App.ViewModels;

/// <summary>
/// Two-step onboarding: one promise, then the location soft ask. We show our
/// own explanation BEFORE triggering the OS dialog — on iOS you effectively get
/// one shot at it. "Browse instead" keeps hesitant users in the app.
/// </summary>
public partial class OnboardingViewModel(Session session, ApiClient api) : ObservableObject
{
    [ObservableProperty]
    private int step; // 0 = welcome, 1 = location soft ask

    [RelayCommand]
    private void Next() => Step = 1;

    [RelayCommand]
    private async Task UseMyLocationAsync()
    {
        // The soft ask happened on-screen; now trigger the real OS dialog.
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        // Granted or not, proceed — the feed falls back to the default area
        // and we can re-ask later once the app has earned some trust.
        await CompleteAsync();
    }

    [RelayCommand]
    private async Task BrowseInsteadAsync() => await CompleteAsync();

    private async Task CompleteAsync()
    {
        session.Onboarded = true;
        try { await api.EnsureDeviceRegisteredAsync(); } catch { /* retried on first report */ }
        await Shell.Current.Navigation.PopModalAsync();
    }
}
