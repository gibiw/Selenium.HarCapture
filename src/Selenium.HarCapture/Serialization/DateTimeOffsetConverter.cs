using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Selenium.HarCapture.Serialization;

/// <summary>
/// Custom JSON converter for DateTimeOffset that uses ISO 8601 format with timezone preservation.
/// HAR 1.2 spec requires ISO 8601 timestamps that preserve the original timezone offset.
/// </summary>
public sealed class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    /// <summary>
    /// Reads a DateTimeOffset from JSON in ISO 8601 format.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert (DateTimeOffset).</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>Parsed DateTimeOffset value.</returns>
    /// <exception cref="JsonException">Thrown when the value cannot be parsed.</exception>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("DateTimeOffset value cannot be null or empty.");
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            throw new JsonException($"Unable to parse '{value}' as DateTimeOffset.");
        }

        return result;
    }

    /// <summary>
    /// Writes a DateTimeOffset to JSON in ISO 8601 round-trip format ("o").
    /// Preserves the original timezone offset without converting to UTC.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The DateTimeOffset value to write.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("o"));
    }
}

/// <summary>
/// Custom JSON converter for nullable DateTimeOffset.
/// System.Text.Json does not automatically apply T converters to Nullable&lt;T&gt; in older versions,
/// so we need an explicit converter for DateTimeOffset?.
/// </summary>
public sealed class DateTimeOffsetNullableConverter : JsonConverter<DateTimeOffset?>
{
    /// <summary>
    /// Reads a nullable DateTimeOffset from JSON.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert (DateTimeOffset?).</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>Parsed DateTimeOffset value or null.</returns>
    /// <exception cref="JsonException">Thrown when a non-null value cannot be parsed.</exception>
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
        {
            throw new JsonException("DateTimeOffset value cannot be empty.");
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            throw new JsonException($"Unable to parse '{value}' as DateTimeOffset.");
        }

        return result;
    }

    /// <summary>
    /// Writes a nullable DateTimeOffset to JSON.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The nullable DateTimeOffset value to write.</param>
    /// <param name="options">Serializer options.</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value.Value.ToString("o"));
        }
    }
}
