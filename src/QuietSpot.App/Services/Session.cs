namespace QuietSpot.App.Services;

/// <summary>
/// Lightweight app state: device identity (anonymous, registered with the API
/// on first run) and user preferences. No accounts in MVP — device IS the user.
/// </summary>
public class Session
{
    // Android emulator reaches host via 10.0.2.2; iOS simulator via localhost.
#if ANDROID
    public string ApiBaseUrl => Preferences.Get("api_base_url", "http://10.0.2.2:5210");
#else
    public string ApiBaseUrl => Preferences.Get("api_base_url", "http://localhost:5210");
#endif

    public Guid? DeviceId
    {
        get
        {
            var raw = Preferences.Get("device_id", string.Empty);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
        set => Preferences.Set("device_id", value?.ToString() ?? string.Empty);
    }

    public bool Onboarded
    {
        get => Preferences.Get("onboarded", false);
        set => Preferences.Set("onboarded", value);
    }

    /// <summary>Opt-in for background dwell prompts (build step 5).</summary>
    public bool BackgroundReportingEnabled
    {
        get => Preferences.Get("bg_reporting", false);
        set => Preferences.Set("bg_reporting", value);
    }

    public DateTime? LastPromptDismissedAtUtc
    {
        get
        {
            var raw = Preferences.Get("last_prompt_dismissed", string.Empty);
            return DateTime.TryParse(raw, out var d) ? d : null;
        }
        set => Preferences.Set("last_prompt_dismissed", value?.ToString("O") ?? string.Empty);
    }
}
