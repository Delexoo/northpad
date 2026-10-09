using System.Text.Json;

namespace Northpad.Core.Translate;

public static class LibreTranslate
{
    public static bool TryRead(string json, out string translated)
    {
        translated = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("translatedText", out var value))
            {
                return false;
            }

            translated = value.GetString() ?? string.Empty;
            return translated.Length > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
