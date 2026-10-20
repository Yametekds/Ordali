using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ordali.Client.Http;

public static class ApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
