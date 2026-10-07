namespace MyDiary.Web.Features.Shared.Services;

/// <summary>
/// Shared singleton service for language state. Layout sets the language,
/// child components subscribe to changes and re-render instantly.
/// </summary>
public class LanguageState
{
    private string _currentLang = "en";

    public string CurrentLang => _currentLang;

    public event Action? OnChange;

    public void SetLanguage(string lang)
    {
        if (_currentLang == lang) return;
        _currentLang = lang;
        OnChange?.Invoke();
    }
}
