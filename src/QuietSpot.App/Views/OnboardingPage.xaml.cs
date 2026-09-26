using System.Globalization;
using QuietSpot.App.ViewModels;

namespace QuietSpot.App.Views;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage(OnboardingViewModel vm)
    {
        Resources.Add("IsStepZero", new StepEqualsConverter(0));
        Resources.Add("IsStepOne", new StepEqualsConverter(1));
        InitializeComponent();
        BindingContext = vm;
    }

    private class StepEqualsConverter(int step) : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is int i && i == step;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
