// Code was originally found in marcolink/MarkdownTable, but the nuget package does not work.
// The original license is Apache 2.0: https://github.com/marcolink/MarkdownTable/blob/1fcf2935ab6dbfe4a3c56434f4c191d0003d0320/LICENSE
// The original code has been modified.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace InfrastructureTools.MarkdownTable;

public class MarkdownTableBuilder
{
    private string[] header = Array.Empty<string>();
    private readonly List<object[]> rows = new();

    private readonly char verticalChar;
    private readonly char horizontalChar;
    private readonly char outerBorderChar;
    private readonly int padding;
    private readonly StringBuilder rowBuilder;

    private enum Align
    {
        Left,
        Right,
        Center
    }

    public MarkdownTableBuilder()
    {
        rowBuilder = new StringBuilder();
        horizontalChar = '-';
        outerBorderChar = ' ';
        verticalChar = '|';
        padding = 1;
    }

    public MarkdownTableBuilder WithHeader(params string[] header)
    {
        this.header = header;
        return this;
    }

    public MarkdownTableBuilder WithRow(params object[] row)
    {
        rows.Add(row);
        return this;
    }

    public MarkdownTableBuilder Clear()
    {
        header = Array.Empty<string>();
        rows.Clear();
        return this;
    }

    public override string ToString()
    {
        var output = new StringBuilder();
        var maxCols = MaxColumns();

        if (header.Length > 0)
        {
            output.AppendLine(Row(header, maxCols));
        }

        output.AppendLine(HorizontalLine());

        rows.ForEach(row => { output.AppendLine(Row(row, maxCols)); });

        return output.ToString();
    }

    private int ColumnWidth(int index)
    {
        var width = 1;

        if (header != null && index < header.Length)
        {
            width = header[index].Length;
        }

        return Column(index).Length == 0
            ? 1
            : Math.Max(width,
                Column(index).Max(r => r != null ? r.Length : 0));
    }

    private int[] SizeRow()
    {
        var row = new List<int>();
        var maxCols = MaxColumns();
        for (var i = 0; i < maxCols; i++)
        {
            row.Add(ColumnWidth(i));
        }

        return row.ToArray();
    }

    private int MaxColumns()
    {
        var result = 0;
        if (header != null)
        {
            result = header.Length;
        }

        rows.ForEach(row => { result = Math.Max(row.Length, result); });
        return result;
    }

    private string[] Column(int index)
    {
        var column = new List<string>();
        rows.ForEach(row => { column.Add(index < row.Length ? row[index].ToString() ?? string.Empty : string.Empty); });
        return column.ToArray();
    }

    private static string Fill(int size, char fillChar = ' ')
    {
        return new string(fillChar, Math.Max(size, 0));
    }

    private string HorizontalLine()
    {
        var format = Fill(1, outerBorderChar) + "{0}" + Fill(1, outerBorderChar);
        var content = SizeRow()
            .Select(col => Fill(col + 2 * padding, horizontalChar))
            .Aggregate((a, b) => a + Fill(1, verticalChar) + b);
        return string.Format(format, content);
    }

    private string Row(object[] row, int maxCols)
    {
        rowBuilder.Length = 0;
        rowBuilder.Append(outerBorderChar);

        for (var i = 0; i < row.Length; i++)
        {
            var maxColWidth = ColumnWidth(i);
            var format = "{0,-" + maxColWidth + "}";

            rowBuilder.Append(Fill(padding));
            rowBuilder.Append(string.Format(format, row[i]));
            rowBuilder.Append(Fill(padding));
            rowBuilder.Append(i == maxCols - 1 ? outerBorderChar : verticalChar);
        }

        var j = row.Length - 1;
        while (j++ < maxCols - 1)
        {
            var maxColWidth = ColumnWidth(j);
            rowBuilder.Append(Fill(maxColWidth + 2 * padding));
            rowBuilder.Append(j == maxCols - 1 ? outerBorderChar : verticalChar);
        }

        return rowBuilder.ToString();
    }
}