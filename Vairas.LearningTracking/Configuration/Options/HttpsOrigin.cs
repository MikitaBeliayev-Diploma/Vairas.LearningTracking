namespace Vairas.LearningTracking.Configuration.Options;

internal static class HttpsOrigin
{
    public static bool IsValid(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
}
