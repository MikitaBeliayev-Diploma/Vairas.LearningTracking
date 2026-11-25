using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public static class Wire
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Save<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Json) ?? throw LearningErrors.Dependency("INVALID_STORED_OUTCOME");
}
