using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class LineParser
{
    static readonly Regex Tag = new(@"\[(\w+)((?:\s+\w+=\w+)*)\]");

    public static ParsedLine Parse(string raw)
    {
        var result = new ParsedLine();
        var sb = new StringBuilder();
        int last = 0;
        foreach (Match m in Tag.Matches(raw))
        {
            sb.Append(raw, last, m.Index - last); // see note below
            var args = new Dictionary<string, string>();
            foreach (Match kv in Regex.Matches(m.Groups[2].Value, @"(\w+)=(\w+)"))
                args[kv.Groups[1].Value] = kv.Groups[2].Value;
            result.Markers.Add(new GestureMarker(sb.Length, m.Groups[1].Value, args));
            last = m.Index + m.Length;
        }
        sb.Append(raw, last, raw.Length - last);
        result.CleanText = sb.ToString();
        return result;
    }
}