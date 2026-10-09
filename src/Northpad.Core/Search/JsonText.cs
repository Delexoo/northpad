using System.Text;
using System.Text.Json;

namespace Northpad.Core.Search;

public static class JsonText
{
    public static string Collect(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var builder = new StringBuilder();
            Walk(document.RootElement, builder);
            return builder.ToString();
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    public static string First(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return FirstString(document.RootElement) ?? string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static void Walk(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(element.GetString());
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, builder);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("secret"))
                    {
                        continue;
                    }

                    Walk(property.Value, builder);
                }

                break;
        }
    }

    private static string? FirstString(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var found = FirstString(item);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var found = FirstString(property.Value);
                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        return found;
                    }
                }

                break;
        }

        return null;
    }
}
