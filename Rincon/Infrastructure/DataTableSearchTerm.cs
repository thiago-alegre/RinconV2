using System.Globalization;
using System.Text.RegularExpressions;
using Rincon.Utilities;

namespace Rincon.Infrastructure;

public sealed class DataTableSearchTerm
{
    private static readonly Regex DatePattern = new(
        @"^(?<day>\d{1,2})[/-](?<month>\d{1,2})(?:[/-](?<year>\d{2}|\d{4}))?(?:\s+(?<hour>\d{1,2})(?::(?<minute>\d{1,2}))?)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private DataTableSearchTerm(string text)
    {
        Text = text;
        Pattern = $"%{text}%";
        HasNumber = DecimalParser.TryParse(text, out var number);
        Number = number;
        Date = ParseDate(text);
    }

    public string Text { get; }
    public string Pattern { get; }
    public bool HasNumber { get; }
    public decimal Number { get; }
    public DateParts? Date { get; }

    public static DataTableSearchTerm Create(string value)
    {
        return new DataTableSearchTerm(value.Trim());
    }

    public bool MatchesLabel(string label)
    {
        return label.Contains(Text, StringComparison.OrdinalIgnoreCase);
    }

    public bool EqualsLabel(string label)
    {
        return label.Equals(Text, StringComparison.OrdinalIgnoreCase);
    }

    private static DateParts? ParseDate(string value)
    {
        var match = DatePattern.Match(value);

        if (!match.Success ||
            !TryGetInt(match, "day", out var day) ||
            !TryGetInt(match, "month", out var month) ||
            day is < 1 or > 31 ||
            month is < 1 or > 12)
        {
            return null;
        }

        int? year = null;

        if (TryGetInt(match, "year", out var parsedYear))
        {
            year = parsedYear < 100 ? 2000 + parsedYear : parsedYear;
        }

        int? hour = null;

        if (TryGetInt(match, "hour", out var parsedHour))
        {
            if (parsedHour is < 0 or > 23)
            {
                return null;
            }

            hour = parsedHour;
        }

        int? minute = null;

        if (TryGetInt(match, "minute", out var parsedMinute))
        {
            if (parsedMinute is < 0 or > 59)
            {
                return null;
            }

            minute = parsedMinute;
        }

        return new DateParts(day, month, year, hour, minute);
    }

    private static bool TryGetInt(Match match, string groupName, out int value)
    {
        var group = match.Groups[groupName];

        if (!group.Success)
        {
            value = 0;
            return false;
        }

        return int.TryParse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    public sealed record DateParts(int Day, int Month, int? Year, int? Hour, int? Minute);
}
