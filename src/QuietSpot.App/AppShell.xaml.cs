using QuietSpot.App.Views;

namespace QuietSpot.App;

public partial class AppShell : Shell
{
    private bool _onboardingShown;

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("venue", typeof(VenueDetailPage));
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // First run: present onboarding modally over the feed.
        if (!_onboardingShown && !Preferences.Get("onboarded", false))
        {
            _onboardingShown = true;
            Dispatcher.Dispatch(async () =>
            {
                var page = Handler!.MauiContext!.Services.GetRequiredService<OnboardingPage>();
                await Navigation.PushModalAsync(page, animated: false);
            });
        }
    }
}
