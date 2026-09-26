using QuietSpot.App.ViewModels;

namespace QuietSpot.App.Views;

public partial class VenueDetailPage : ContentPage
{
    public VenueDetailPage(VenueDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
