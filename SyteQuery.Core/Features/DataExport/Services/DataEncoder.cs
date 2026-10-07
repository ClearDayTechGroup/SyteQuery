namespace SyteQuery.Features.DataExport.Services;

/// <summary>
/// Utility for encoding/decoding data payloads.
/// </summary>
public static class DataEncoder
{
    /// <summary>
    /// Attempts to decode a Base64 string to UTF-8 text if it appears to be JSON.
    /// </summary>
    /// <param name="input">Potentially Base64-encoded string.</param>
    /// <returns>Decoded UTF-8 string if valid JSON, otherwise null.</returns>
    public static string? TryDecodeBase64ToUtf8(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length % 4 != 0)
            return null;

        try
        {
            var bytes = Convert.FromBase64String(input);
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            var trimmed = text.TrimStart();

            // Only return if it looks like JSON
            return (trimmed.StartsWith('{') || trimmed.StartsWith('[')) ? text : null;
        }
        catch
        {
            return null;
        }
    }
}