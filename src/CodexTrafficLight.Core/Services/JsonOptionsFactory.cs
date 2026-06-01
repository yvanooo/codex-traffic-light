using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexTrafficLight.Core.Services;

internal static class JsonOptionsFactory
{
    public static JsonSerializerOptions Create(bool includeEnumConverter = false)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (includeEnumConverter)
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        return options;
    }
}
