using QuietSpot.App.Delegates;
using QuietSpot.App.ViewModels;
using Shiny.Notifications;

namespace QuietSpot.App.Views;

public partial class FeedPage : ContentPage
{
    private readonly FeedViewModel _vm;
    private readonly INotificationManager _notifications;
    private bool _channelRegistered;

    public FeedPage(FeedViewModel vm, INotificationManager notifications)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _notifications = notifications;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_channelRegistered)
        {
            _channelRegistered = true;
            // Channel with the three inline actions used by the dwell prompt.
            _notifications.AddChannel(new Channel
            {
                Identifier = VenueGeofenceDelegate.ReportChannelId,
                Description = "Report prompts",
                Importance = ChannelImportance.Normal,
                Actions = ReportNotificationDelegate.BuildActions().ToList()
            });
        }

        await _vm.LoadAsync();
    }
}
