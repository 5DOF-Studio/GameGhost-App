namespace GaimerDesktop.Views;

[QueryProperty(nameof(ErrorCode), "ErrorCode")]
[QueryProperty(nameof(ErrorTitle), "ErrorTitle")]
[QueryProperty(nameof(ErrorMessage), "ErrorMessage")]
[QueryProperty(nameof(ErrorDetail), "ErrorDetail")]
public partial class ErrorPage : ContentPage
{
    public ErrorPage()
    {
        InitializeComponent();
    }

    public string? ErrorCode
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                ErrorCodeLabel.Text = $"ERROR {value}";
        }
    }

    public string? ErrorTitle
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                TitleLabel.Text = value;
        }
    }

    public string? ErrorMessage
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
                DescriptionLabel.Text = value;
        }
    }

    public string? ErrorDetail
    {
        set
        {
            if (!string.IsNullOrEmpty(value))
            {
                ErrorDetailLabel.Text = value;
                ErrorDetailBorder.IsVisible = true;
            }
        }
    }

    private async void OnReturnHomeTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            await Shell.Current.GoToAsync("//ProductionTab/Onboarding");
    }
}
