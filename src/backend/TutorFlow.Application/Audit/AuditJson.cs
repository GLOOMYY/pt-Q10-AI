using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using TutorFlow.Application.Ports.Audit;

namespace TutorFlow.Application.Audit;

/// <summary>
/// Serialization of an <see cref="AuditRecord"/> as one JSON line (R-LOG-07, R-LOG-08). Non-ASCII characters are written
/// as they are, not escaped, so the question and the evals' comparisons stay readable (README §5.3).
/// </summary>
public static class AuditJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
        Converters = { new UtcTimestampConverter() },
    };

    /// <summary>Serializes the record on a single line (newlines inside text are escaped by JSON).</summary>
    /// <param name="record">The record.</param>
    /// <returns>The JSON text, without a trailing newline.</returns>
    public static string Serialize(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonSerializer.Serialize(record, Options);
    }

    /// <summary>Writes the timestamp in UTC with a <c>Z</c> suffix and millisecond precision (R-LOG-01).</summary>
    private sealed class UtcTimestampConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
    }
}
