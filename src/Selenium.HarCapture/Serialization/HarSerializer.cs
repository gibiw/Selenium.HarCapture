using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Serialization;

/// <summary>
/// Static serializer for HAR (HTTP Archive) format.
/// Provides methods to serialize/deserialize Har objects to/from JSON strings and files.
/// </summary>
public static class HarSerializer
{
    /// <summary>
    /// Serializes a Har object to a JSON string.
    /// </summary>
    /// <param name="har">The HAR object to serialize.</param>
    /// <param name="writeIndented">If true, formats the JSON with indentation for readability. Default is true.</param>
    /// <returns>JSON string representation of the HAR object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when har is null.</exception>
    public static string Serialize(Har har, bool writeIndented = true)
    {
        if (har == null)
        {
            throw new ArgumentNullException(nameof(har));
        }

        return JsonSerializer.Serialize(har, CreateOptions(writeIndented));
    }

    /// <summary>
    /// Deserializes a JSON string to a Har object.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized Har object.</returns>
    /// <exception cref="ArgumentException">Thrown when json is null or empty.</exception>
    /// <exception cref="JsonException">Thrown when the JSON cannot be deserialized or results in null.</exception>
    public static Har Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<Har>(json, CreateOptions(false))
            ?? throw new JsonException("Deserialization resulted in a null Har object.");
    }

    /// <summary>
    /// Asynchronously saves a Har object to a file.
    /// </summary>
    /// <param name="har">The HAR object to save.</param>
    /// <param name="filePath">The path to the file where the HAR will be saved.</param>
    /// <param name="writeIndented">If true, formats the JSON with indentation for readability. Default is true.</param>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when har is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    public static async Task SaveAsync(Har har, string filePath, bool writeIndented = true, CancellationToken cancellationToken = default)
    {
        if (har == null)
        {
            throw new ArgumentNullException(nameof(har));
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true);

        if (IsGzipPath(filePath))
        {
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            await JsonSerializer.SerializeAsync(gzipStream, har, CreateOptions(writeIndented), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await JsonSerializer.SerializeAsync(fileStream, har, CreateOptions(writeIndented), cancellationToken).ConfigureAwait(false);
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Synchronously saves a Har object to a file.
    /// </summary>
    /// <param name="har">The HAR object to save.</param>
    /// <param name="filePath">The path to the file where the HAR will be saved.</param>
    /// <param name="writeIndented">If true, formats the JSON with indentation for readability. Default is true.</param>
    /// <exception cref="ArgumentNullException">Thrown when har is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    public static void Save(Har har, string filePath, bool writeIndented = true)
    {
        if (har == null)
        {
            throw new ArgumentNullException(nameof(har));
        }

        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (IsGzipPath(filePath))
        {
            var json = Serialize(har, writeIndented);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Compress);
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        else
        {
            var json = Serialize(har, writeIndented);
            File.WriteAllText(filePath, json);
        }
    }

    /// <summary>
    /// Asynchronously loads a Har object from a file.
    /// </summary>
    /// <param name="filePath">The path to the HAR file to load.</param>
    /// <param name="cancellationToken">Cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the asynchronous load operation. The task result contains the loaded Har object.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the file cannot be deserialized or results in null.</exception>
    public static async Task<Har> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"HAR file not found at path: {filePath}", filePath);
        }

        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

        Har? result;
        if (IsGzipPath(filePath))
        {
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            result = await JsonSerializer.DeserializeAsync<Har>(gzipStream, CreateOptions(false), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result = await JsonSerializer.DeserializeAsync<Har>(fileStream, CreateOptions(false), cancellationToken).ConfigureAwait(false);
        }

        return result ?? throw new JsonException("Deserialization resulted in a null Har object.");
    }

    /// <summary>
    /// Synchronously loads a Har object from a file.
    /// </summary>
    /// <param name="filePath">The path to the HAR file to load.</param>
    /// <returns>The loaded Har object.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the file cannot be deserialized or results in null.</exception>
    public static Har Load(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"HAR file not found at path: {filePath}", filePath);
        }

        if (IsGzipPath(filePath))
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var memoryStream = new MemoryStream();
            gzipStream.CopyTo(memoryStream);
            var json = Encoding.UTF8.GetString(memoryStream.ToArray());
            return Deserialize(json);
        }
        else
        {
            var json = File.ReadAllText(filePath);
            return Deserialize(json);
        }
    }

    /// <summary>
    /// Determines if the file path indicates a gzip-compressed file based on extension.
    /// </summary>
    /// <param name="filePath">The file path to check.</param>
    /// <returns>True if the file path ends with .gz extension (case-insensitive), otherwise false.</returns>
    private static bool IsGzipPath(string filePath)
        => Path.GetExtension(filePath).Equals(".gz", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates JsonSerializerOptions configured for HAR serialization.
    /// </summary>
    /// <param name="writeIndented">Whether to format output with indentation.</param>
    /// <returns>Configured JsonSerializerOptions instance.</returns>
    internal static JsonSerializerOptions CreateOptions(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // UnsafeRelaxedJsonEscaping only escapes " and \ (instead of <, >, &, +, ' etc.)
            // This dramatically reduces buffer allocation in WriteStringEscapeValue for large
            // response bodies (HTML/JS). Safe for HAR files which are consumed programmatically.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // Register custom converters for DateTimeOffset handling
        options.Converters.Add(new DateTimeOffsetConverter());
        options.Converters.Add(new DateTimeOffsetNullableConverter());

        return options;
    }
}
