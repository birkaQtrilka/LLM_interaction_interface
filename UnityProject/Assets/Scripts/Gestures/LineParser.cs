using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
/// <summary>
/// Parses text into a list of gestures and clean text
/// </summary>
public static class LineParser
{
    // example: "Special thanks [point target=NPC1] to J"
    //captures zero or more arguments, each consisting of whitespace followed by a key=value pair.
    static readonly Regex Tag = new(@"\[(\w+)((?:\s+\w+=\w+)*)\]");

    public static ParsedLine Parse(string raw)
    {
        var result = new ParsedLine();
        var sb = new StringBuilder();
        int last = 0;
        foreach (Match m in Tag.Matches(raw))
        {
            sb.Append(raw, last, m.Index - last);
            var args = new Dictionary<string, string>();
            foreach (Match kv in Regex.Matches(m.Groups[2].Value, @"(\w+)=(\w+)")) // It looks for a word-character key, an equals sign, and a word-character value.
                args[kv.Groups[1].Value] = kv.Groups[2].Value;
            result.Markers.Add(new GestureMarker(sb.Length, m.Groups[1].Value, args));
            last = m.Index + m.Length;
        }
        sb.Append(raw, last, raw.Length - last);
        result.CleanText = sb.ToString();
        return result;
    }
}