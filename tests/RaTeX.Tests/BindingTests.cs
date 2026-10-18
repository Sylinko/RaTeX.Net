using System.Runtime.InteropServices;
using System.Text;
using RaTeX.Interop;
using RaTeX.Serialization;

namespace RaTeX.Tests;

public class BindingTests
{
    [Fact]
    public void NativeStructures_On64BitPlatforms_MatchAbiLayout()
    {
        Assert.Equal(24, Marshal.SizeOf<NativeOptions>());
        Assert.Equal(8, Marshal.OffsetOf<NativeOptions>(nameof(NativeOptions.DisplayMode)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<NativeOptions>(nameof(NativeOptions.Color)).ToInt32());
        Assert.Equal(16, Marshal.SizeOf<NativeColor>());
        Assert.Equal(16, Marshal.SizeOf<NativeResult>());
        Assert.Equal(8, Marshal.OffsetOf<NativeResult>(nameof(NativeResult.ErrorCode)).ToInt32());
        Assert.Equal(32, Marshal.SizeOf<NativeFallbackGlyphMetrics>());
        Assert.Equal(8, Marshal.OffsetOf<NativeFallbackGlyphMetrics>(nameof(NativeFallbackGlyphMetrics.Width)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<NativeFallbackGlyphMetrics>(nameof(NativeFallbackGlyphMetrics.Height)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<NativeFallbackGlyphMetrics>(nameof(NativeFallbackGlyphMetrics.Depth)).ToInt32());
    }

    [Fact]
    public void ParseAndLayout_AfterAnotherParse_PreservesSnapshot()
    {
        var list = Ratex.ParseAndLayout(@"\frac{1}{\sqrt{2}}");
        Assert.Equal(1, list.ProtocolVersion);
        Assert.NotEmpty(list.Items);
        Assert.True(list.Width > 0);
        Assert.All(list.Items, item => Assert.Equal(RatexColor.Black, item.Color));
        var saved = list.Items.ToArray();
        Ratex.ParseAndLayout("y_2");
        Assert.Equal(saved, list.Items);
    }

    [Fact]
    public void ParseAndLayout_WithOutOfRangeLatexColor_ThrowsProtocolError()
    {
        var error = Assert.Throws<RatexProtocolException>(() => Ratex.ParseAndLayout(@"\textcolor[gray]{2}{x}"));
        Assert.Contains("Invalid RGBA", error.Message);
    }

    [Fact]
    public void ParseAndLayout_WithOptions_AppliesModeAndColors()
    {
        var block = Ratex.ParseAndLayout(@"\frac{1}{2}");
        var inline = Ratex.ParseAndLayout(@"\frac{1}{2}", new()
        {
            Mode = RatexMode.Inline
        });
        Assert.NotEqual(block.TotalHeight, inline.TotalHeight);
        var blue = new RatexColor(0, 0, 1, .5f);
        var colored = Ratex.ParseAndLayout(@"x+\textcolor{red}{y}", new()
        {
            DefaultColor = blue
        });
        Assert.Contains(colored.Items, i => i.Color == blue);
        Assert.Contains(colored.Items, i => i.Color is { R: 1, B: 0 });
    }

    [Fact]
    public void ParseAndLayout_AfterFailure_PreservesErrorAndRecovers()
    {
        Assert.False(Ratex.TryParseAndLayout(@"\frac{", null, out var noList, out var first));
        Assert.Null(noList);
        Assert.NotNull(first);
        var message = first.NativeMessage;
        Assert.False(Ratex.TryParseAndLayout(@"\definitelyUnknownCommand", null, out _, out var second));
        Assert.NotEqual(message, second.NativeMessage);
        Assert.True(Ratex.TryParseAndLayout("x", null, out var valid, out var noError));
        Assert.NotNull(valid);
        Assert.Null(noError);
        Assert.Equal(message, first.NativeMessage);
        var exception = Assert.Throws<RatexException>(() => Ratex.ParseAndLayout(@"\frac{"));
        Assert.Equal(first, exception.Error);
    }

    [Fact]
    public void TryParseAndLayout_WithInvalidArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Ratex.TryParseAndLayout(null!, null, out _, out _));
        foreach (var input in new[] { "x\0y", "\ud800", "\udfff" })
        {
            Assert.Throws<ArgumentException>(() => Ratex.TryParseAndLayout(input, null, out _, out _));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => Ratex.ParseAndLayout("x", new RatexOptions { Mode = (RatexMode)55 }));
        foreach (var component in new[] { float.NaN, float.PositiveInfinity, -1, 2 })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Ratex.ParseAndLayout("x", new RatexOptions { DefaultColor = new RatexColor(component, 0, 0, 1) }));
        }
    }

    [Theory]
    [InlineData("中")]
    [InlineData("😀")]
    public void ParseAndLayoutWithFallbackMetrics_WithScalar_UsesHostDimensions(string character)
    {
        var source = $@"\text{{{character}}}";
        var metrics = new FallbackGlyphMetrics[] { new((uint)char.ConvertToUtf32(character, 0), 1.2, .8, .2) };
        var measured = Ratex.ParseAndLayoutWithFallbackMetrics(source, metrics);
        Assert.Equal(1.2, measured.Width, 8);
        Assert.Equal(.8, measured.Height, 8);
        Assert.Equal(.2, measured.Depth, 8);
        Assert.Equal(1.44, Ratex.ParseAndLayoutWithFallbackMetrics($@"\frac{{{source}}}{{x}}", metrics).Width, 8);
        var script = Ratex.ParseAndLayoutWithFallbackMetrics($@"{{\scriptstyle {source}}}", metrics);
        Assert.Equal(.84, script.Width, 8);
        Assert.Equal(.56, script.Height, 8);
        Assert.Equal(.14, script.Depth, 8);
    }

    [Fact]
    public void ParseAndLayoutWithFallbackMetrics_WithLargeTable_PreservesDefaultsAndUsesLastDuplicate()
    {
        var metrics = new FallbackGlyphMetrics[65_537];
        Array.Fill(metrics, new('中', 1.2, .8, .2));
        metrics[^2] = new('中', 1.4, .9, .1);
        metrics[^1] = new('x', 5, 5, 5);
        Assert.Equal(1.4, Ratex.ParseAndLayoutWithFallbackMetrics(@"\text{中}", metrics).Width, 8);
        var baseline = Ratex.ParseAndLayout(@"\text{中}");
        Assert.Equal(baseline.Width, Ratex.ParseAndLayoutWithFallbackMetrics(@"\text{中}", []).Width);
        foreach (var source in new[] { "x", @"\text{x}", @"\text{日}" })
        {
            baseline = Ratex.ParseAndLayout(source);
            var actual = Ratex.ParseAndLayoutWithFallbackMetrics(source, metrics);
            Assert.Equal(baseline.Width, actual.Width);
            Assert.Equal(baseline.Items.ToArray(), actual.Items.ToArray());
        }
    }

    [Fact]
    public void TryParseAndLayoutWithFallbackMetrics_WithInvalidTable_Throws()
    {
        foreach (var metric in new FallbackGlyphMetrics[]
        {
            new(0xD800, 1, 1, 0), new(0x110000, 1, 1, 0),
            new('中', double.NaN, 1, 0), new('中', 1, double.PositiveInfinity, 0), new('中', 1, 1, double.NegativeInfinity)
        })
        {
            Assert.Throws<ArgumentException>(() => Ratex.TryParseAndLayoutWithFallbackMetrics("x", [metric], null, out _, out _));
        }
    }

    [Fact]
    public void TryParseAndLayout_WhenConcurrent_PreservesThreadLocalErrors()
    {
        Parallel.For(0, 128, i =>
        {
            var command = $@"\unknownCommand{(char)('a' + i % 26)}";
            Assert.False(Ratex.TryParseAndLayout(command, null, out _, out var error));
            Assert.Contains(command, error.NativeMessage);
            Assert.NotEmpty(Ratex.ParseAndLayout($"x+{i}").Items);
        });
    }

    [Fact]
    public void Decode_WithAllVariants_PreservesGeometryAndUnicode()
    {
        var list = Decode(Items);
        Assert.Collection(list.Items,
            i => Assert.Equal(0x1D400u, Assert.IsType<GlyphPathItem>(i).CodePoint),
            i => Assert.False(Assert.IsType<LineItem>(i).Dashed),
            i => Assert.Equal(-1, Assert.IsType<RectItem>(i).X),
            i => Assert.Equal(5, Assert.IsType<PathItem>(i).Commands.Length));
        var commands = ((PathItem)list.Items[3]).Commands;
        Assert.IsType<CubicTo>(commands[2]);
    }

    [Theory]
    [InlineData("{\"version\":2,\"width\":0,\"height\":0,\"depth\":0,\"items\":[]}")]
    [InlineData("{\"version\":1,\"width\":0,\"height\":1e308,\"depth\":1e308,\"items\":[]}")]
    public void Decode_WithInvalidProtocol_Throws(string json)
    {
        Assert.Throws<RatexProtocolException>(() => DisplayListDecoder.Decode(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("\"GlyphPath\"", "\"FutureGlyph\"")]
    [InlineData("\"Close\"", "\"ArcTo\"")]
    public void Decode_WithUnsupportedVariant_Throws(string from, string to)
    {
        Assert.Throws<RatexProtocolException>(() => Decode(Items.Replace(from, to)));
    }

    [Theory]
    [InlineData("{\"version\":1,\"width\":0,\"height\":0,\"depth\":0,\"items\":[]}", false)]
    [InlineData("invalid JSON", true)]
    public void DecodeOwned_OnSuccessOrFailure_ReleasesExactlyOnce(string json, bool fails)
    {
        var pointer = Marshal.StringToCoTaskMemUTF8(json);
        var frees = 0;
        void Free(nint p)
        {
            Assert.Equal(pointer, p);
            frees++;
            Marshal.FreeCoTaskMem(p);
        }
        if (fails)
        {
            Assert.Throws<RatexProtocolException>(() => Ratex.DecodeOwned(pointer, Free));
        }
        else
        {
            Assert.Empty(Ratex.DecodeOwned(pointer, Free).Items);
        }

        Assert.Equal(1, frees);
    }

    private static DisplayList Decode(string items) => DisplayListDecoder.Decode(Encoding.UTF8.GetBytes(
        "{\"items\":" + items + ",\"width\":3,\"height\":2,\"depth\":1,\"future\":{},\"version\":1}"));

    private const string Items = """
    [
      {"x":0,"y":1,"scale":1,"font":"Main-Bold","char_code":119808,"color":{"r":0,"g":0,"b":0,"a":1},"type":"GlyphPath","future":42},
      {"x":0,"y":1,"width":2,"thickness":0.04,"color":{"r":0,"g":0,"b":0,"a":1},"type":"Line"},
      {"x":-1,"y":1,"width":2,"height":0.04,"color":{"r":0,"g":0,"b":0,"a":1},"type":"Rect"},
      {"x":0,"y":0,"fill":true,"color":{"r":0,"g":0,"b":0,"a":1},"type":"Path","commands":[
        {"x":0,"y":0,"type":"MoveTo"},{"type":"LineTo","x":1,"y":1},
        {"type":"CubicTo","x1":1,"y1":2,"x2":2,"y2":3,"x":4,"y":5},
        {"type":"QuadTo","x1":1,"y1":2,"x":4,"y":5},{"type":"Close"}
      ]}
    ]
    """;
}
