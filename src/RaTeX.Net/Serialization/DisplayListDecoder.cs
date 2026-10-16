using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RaTeX.Serialization;

/// <summary>
/// Decodes the pinned native engine's protocol into immutable models from a borrowed UTF-8 span.
/// </summary>
internal static class DisplayListDecoder
{
    /// <summary>
    /// Decodes a display list without retaining its source buffer or constructing a JSON DOM.
    /// </summary>
    internal static DisplayList Decode(ReadOnlySpan<byte> json)
    {
        try
        {
            var value = JsonSerializer.Deserialize(json, RatexJsonContext.Default.WireDisplayList)!;
            if (value.Version != 1)
            {
                throw new JsonException($"Unsupported protocol version {value.Version}.");
            }

            // This sum is computed by .NET, so this is not guaranteed.
            if (!double.IsFinite(value.Height + value.Depth))
            {
                throw new JsonException("Non-finite total height.");
            }

            return new DisplayList(1, value.Width, value.Height, value.Depth, value.Items);
        }
        catch (JsonException ex)
        {
            throw new RatexProtocolException($"Invalid RaTeX DisplayList: {ex.Message}", ex);
        }
    }
}

/// <summary>
/// Defines the required root fields for source-generated protocol deserialization.
/// </summary>
internal sealed class WireDisplayList
{
    public required int Version { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }
    public required double Depth { get; init; }
    public required ImmutableArray<DisplayItem> Items { get; init; }
}

/// <summary>
/// Reads native color channels; LaTeX color models do not enforce normalized channel ranges upstream.
/// </summary>
internal sealed class WireColor
{
    public required float R { get; init; }
    public required float G { get; init; }
    public required float B { get; init; }
    public required float A { get; init; }

    internal RatexColor ToColor()
    {
        var c = new RatexColor(R, G, B, A);
        return Ratex.IsValidColor(c) ? c : throw new JsonException("Invalid RGBA.");
    }
}

/// <summary>
/// Reads the Rust enum's fields without depending on JSON property order.
/// </summary>
internal sealed class WireItem
{
    public required string Type { get; init; }
    public required double X { get; init; }
    public required double Y { get; init; }
    public required WireColor Color { get; init; }
    public double Scale { get; init; }
    public string Font { get; init; } = null!;
    public uint CharCode { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double Thickness { get; init; }
    public bool Dashed { get; init; }
    public bool Fill { get; init; }
    public ImmutableArray<PathCommand> Commands { get; init; }
}

/// <summary>
/// Captures the Rust path command enum's coordinates.
/// </summary>
internal sealed class WireCommand
{
    public required string Type { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
}

/// <summary>
/// Maps native enum variants, trusting the fields emitted by Rust's typed serializer.
/// </summary>
internal sealed class DisplayItemConverter : JsonConverter<DisplayItem>
{
    /// <inheritdoc />
    public override DisplayItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var v = JsonSerializer.Deserialize(ref reader, RatexJsonContext.Default.WireItem)!;
        var color = v.Color.ToColor();
        return v.Type switch
        {
            "GlyphPath" => new GlyphPathItem(v.X, v.Y, v.Scale, v.Font, v.CharCode, color),
            "Line" => new LineItem(v.X, v.Y, v.Width, v.Thickness, v.Dashed, color),
            "Rect" => new RectItem(v.X, v.Y, v.Width, v.Height, color),
            "Path" => new PathItem(v.X, v.Y, v.Commands, v.Fill, color),
            _ => throw new JsonException($"Unsupported display item '{v.Type}'.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DisplayItem value, JsonSerializerOptions options) => throw new NotSupportedException();
}

/// <summary>
/// Maps the path command variants produced by Rust.
/// </summary>
internal sealed class PathCommandConverter : JsonConverter<PathCommand>
{
    /// <inheritdoc />
    public override PathCommand Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var v = JsonSerializer.Deserialize(ref reader, RatexJsonContext.Default.WireCommand)!;
        return v.Type switch
        {
            "MoveTo" => new MoveTo(v.X, v.Y),
            "LineTo" => new LineTo(v.X, v.Y),
            "QuadTo" => new QuadTo(v.X1, v.Y1, v.X, v.Y),
            "CubicTo" => new CubicTo(v.X1, v.Y1, v.X2, v.Y2, v.X, v.Y),
            "Close" => new Close(),
            _ => throw new JsonException($"Unsupported path command '{v.Type}'.")
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, PathCommand value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}

/// <summary>
/// Provides generated metadata so native JSON decoding does not require reflection-based discovery.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = [typeof(DisplayItemConverter), typeof(PathCommandConverter)])]
[JsonSerializable(typeof(WireDisplayList))]
[JsonSerializable(typeof(WireItem))]
[JsonSerializable(typeof(WireCommand))]
internal partial class RatexJsonContext : JsonSerializerContext;
