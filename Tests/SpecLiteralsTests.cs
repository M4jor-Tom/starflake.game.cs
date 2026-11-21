using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Starflake.Tests;

/// <summary>
/// Fails when hand-written code, content or the README restates a display text or a number of spec/*.json
/// instead of reading it from the spec. Ids may appear; the numbers 0, 1 and 2 are not checked.
/// </summary>
public partial class SpecLiteralsTests
{
    static readonly string[] DisplayKeys = ["text", "title", "label"];
    static readonly double[] Unchecked = [0, 1, 2];
    static readonly char[] WordSeparators = ['_', ':'];

    // Directories that hold no hand-written files: build output, the generated classes, tool state.
    static readonly string[] NotHandWritten = ["bin/", "obj/", "Domain/Generated/", "Design/Generated/"];

    // Placeholder art: its sizes are not design values.
    const string Scenery = "Game/Scenery.cs";

    static readonly string[] NumberDirectories = ["Sim/", "Design/", "Game/", "Domain/Bindings/"];

    [Fact]
    public void No_display_text_of_the_spec_is_restated()
    {
        var texts = DisplayTexts();
        var files = HandWritten("*.cs").Where(f => f != $"Tests/{nameof(SpecLiteralsTests)}.cs")
            .Select(f => (f, StringLiterals(Read(f))))
            .Concat(HandWritten("*.json").Where(f => f.StartsWith("content/")).Select(f => (f, Whole(f))))
            .Append(("README.md", Whole("README.md")));
        Assert.Empty(files.SelectMany(f => TextFindings(f.Item1, f.Item2, texts)));
    }

    [Fact]
    public void No_number_of_the_spec_is_a_literal_in_the_game()
    {
        var numbers = SpecLeaves().Where(l => l.Value.GetValueKind() == JsonValueKind.Number)
            .Select(l => l.Value.GetValue<double>()).Except(Unchecked).ToHashSet();
        var files = HandWritten("*.cs").Where(f => NumberDirectories.Any(f.StartsWith) && f != Scenery);
        Assert.Empty(files.SelectMany(f => NumberFindings(f, Read(f), numbers)));
    }

    [Fact]
    public void The_scan_sees_a_planted_text_and_number()
    {
        const string text = "Planted words", sample = "var shown = \"Planted words here\"; var size = 1234.5f;";
        const double number = 1234.5;
        Assert.DoesNotContain(text, DisplayTexts());

        var findings = TextFindings("sample.cs", StringLiterals(sample), [text])
            .Concat(NumberFindings("sample.cs", sample, [number]));

        Assert.Equal(["sample.cs: Planted words", "sample.cs: 1234.5"], findings);
    }

    // Each display text split at its {placeholders}: the trimmed fragments longer than three characters, except
    // vocabulary: an id or a placeholder name, or one of their words (the fragments "pause" and "star").
    static HashSet<string> DisplayTexts()
    {
        var leaves = SpecLeaves().Where(l => l.Value.GetValueKind() == JsonValueKind.String).ToList();
        var texts = leaves.Where(l => DisplayKeys.Contains(l.Key)).Select(l => l.Value.GetValue<string>()).ToList();
        var vocabulary = leaves.Where(l => l.Key == "id").Select(l => l.Value.GetValue<string>())
            .Concat(texts.SelectMany(t => Placeholder().Matches(t).Select(m => m.Value.Trim('{', '}'))))
            .SelectMany(name => name.Split(WordSeparators).Append(name)).ToHashSet();
        return texts.SelectMany(t => Placeholder().Split(t))
            .Select(f => f.Trim()).Where(f => f.Length > 3 && !vocabulary.Contains(f)).ToHashSet();
    }

    // Whole words only: the fragment "Fuel" is not in "FuelPods".
    static IEnumerable<string> TextFindings(string file, IEnumerable<string> haystacks, IEnumerable<string> texts) =>
        texts.Where(t => haystacks.Any(h => Regex.IsMatch(h, $@"(?<!\w){Regex.Escape(t)}(?!\w)"))).Select(t => $"{file}: {t}");

    static IEnumerable<string> NumberFindings(string file, string source, ICollection<double> numbers) =>
        NumericLiteral().Matches(source).Select(m => m.Groups[1].Value)
            .Where(n => numbers.Contains(double.Parse(n, CultureInfo.InvariantCulture))).Distinct()
            .Select(n => $"{file}: {n}");

    static IEnumerable<(string? Key, JsonValue Value)> SpecLeaves() =>
        Directory.GetFiles(Path.Combine(Paths.Root, "spec"), "*.json").SelectMany(f => Leaves(JsonNode.Parse(File.ReadAllText(f)), null));

    static IEnumerable<(string? Key, JsonValue Value)> Leaves(JsonNode? node, string? key) => node switch
    {
        JsonObject o => o.SelectMany(p => Leaves(p.Value, p.Key)),
        JsonArray a => a.SelectMany(n => Leaves(n, key)),
        JsonValue v => [(key, v)],
        _ => [],
    };

    // Paths relative to the game root, with '/', outside build output, generated code and dot-directories.
    static IEnumerable<string> HandWritten(string pattern) =>
        Directory.EnumerateFiles(Paths.Root, pattern, SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(Paths.Root, f).Replace('\\', '/'))
            .Where(f => !f.StartsWith('.') && !NotHandWritten.Any(d => f.StartsWith(d) || f.Contains($"/{d}")));

    static string Read(string file) => File.ReadAllText(Path.Combine(Paths.Root, file));

    static IEnumerable<string> Whole(string file) => [Read(file)];

    // The text of each string literal without its {holes}: an interpolated expression is code, not text.
    static IEnumerable<string> StringLiterals(string source) =>
        StringLiteral().Matches(source).Select(m => Placeholder().Replace(m.Value, ""));

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex Placeholder();

    // Raw, verbatim and regular string literals (interpolated ones included).
    [GeneratedRegex(@"""""""[\s\S]*?""""""|@""(?:[^""]|"""")*""|""(?:[^""\\\n]|\\.)*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"(?<![\w.])(\d+(\.\d+)?)[fFdD]?(?![\w.])")]
    private static partial Regex NumericLiteral();
}
