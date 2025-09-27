using System.Globalization;

namespace Trading.Common.Services;

public interface ILanguageService
{
    CultureInfo CurrentCulture { get; }
    void SetCurrentCulture(string languageCode);
}
