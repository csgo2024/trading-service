using System.Globalization;

namespace Trading.Common.Services;

public class LanguageService : ILanguageService
{
    private readonly AsyncLocal<CultureInfo> _currentCulture = new();

    public CultureInfo CurrentCulture => _currentCulture.Value ?? CultureInfo.CurrentCulture;

    public void SetCurrentCulture(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            _currentCulture.Value = CultureInfo.CurrentCulture;
            return;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            _currentCulture.Value = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            _currentCulture.Value = CultureInfo.CurrentCulture;
        }
    }
}
