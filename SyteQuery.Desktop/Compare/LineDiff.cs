using System.Text;

namespace SyteQuery.Desktop.Compare;

public enum DiffKind { Same, LeftOnly, RightOnly }

public sealed record DiffRow(int? LeftLineNumber, string? LeftText, int? RightLineNumber, string? RightText, DiffKind Kind);

/// <summary>
/// Line-based diff (longest-common-subsequence), ported as-is from the old Blazor
/// CompareStoredProcedureDialog - rows are paired so the left/right sides line up, with a
/// one-sided row wherever a line exists on only one side.
/// </summary>
public static class LineDiff
{
    public static List<DiffRow> Compute(string leftText, string rightText, bool ignoreWhitespace)
    {
        var leftLines = SplitLines(leftText);
        var rightLines = SplitLines(rightText);
        var leftCompare = leftLines.Select(l => Normalize(l, ignoreWhitespace)).ToArray();
        var rightCompare = rightLines.Select(l => Normalize(l, ignoreWhitespace)).ToArray();

        var lcs = new int[leftLines.Count + 1, rightLines.Count + 1];
        for (var i = leftLines.Count - 1; i >= 0; i--)
            for (var j = rightLines.Count - 1; j >= 0; j--)
                lcs[i, j] = leftCompare[i] == rightCompare[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var rows = new List<DiffRow>();
        int li = 0, ri = 0;

        while (li < leftLines.Count && ri < rightLines.Count)
        {
            if (leftCompare[li] == rightCompare[ri])
            {
                rows.Add(new DiffRow(li + 1, leftLines[li], ri + 1, rightLines[ri], DiffKind.Same));
                li++; ri++;
            }
            else if (lcs[li + 1, ri] >= lcs[li, ri + 1])
            {
                rows.Add(new DiffRow(li + 1, leftLines[li], null, null, DiffKind.LeftOnly));
                li++;
            }
            else
            {
                rows.Add(new DiffRow(null, null, ri + 1, rightLines[ri], DiffKind.RightOnly));
                ri++;
            }
        }

        for (; li < leftLines.Count; li++)
            rows.Add(new DiffRow(li + 1, leftLines[li], null, null, DiffKind.LeftOnly));
        for (; ri < rightLines.Count; ri++)
            rows.Add(new DiffRow(null, null, ri + 1, rightLines[ri], DiffKind.RightOnly));

        return rows;
    }

    private static List<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();

    private static string Normalize(string line, bool ignoreWhitespace)
    {
        var trimmed = line.TrimEnd();
        if (!ignoreWhitespace)
            return trimmed;

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
            if (!char.IsWhiteSpace(ch))
                sb.Append(ch);
        return sb.ToString();
    }
}
