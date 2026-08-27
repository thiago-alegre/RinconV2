namespace Rincon.Infrastructure;

public sealed record DataTableRequest(
    int Draw,
    int Start,
    int Length,
    string Search,
    int OrderColumn,
    bool IsAscending)
{
    private const int DefaultPageLength = 5;
    private const int MaximumPageLength = 100;

    public static DataTableRequest From(HttpRequest request)
    {
        var length = ReadInt(request, "length", DefaultPageLength);

        return new DataTableRequest(
            Draw: Math.Max(0, ReadInt(request, "draw")),
            Start: Math.Max(0, ReadInt(request, "start")),
            Length: Math.Clamp(length, 1, MaximumPageLength),
            Search: request.Query["search[value]"].ToString().Trim(),
            OrderColumn: Math.Max(0, ReadInt(request, "order[0][column]")),
            IsAscending: string.Equals(
                request.Query["order[0][dir]"],
                "asc",
                StringComparison.OrdinalIgnoreCase));
    }

    private static int ReadInt(HttpRequest request, string key, int defaultValue = 0)
    {
        return int.TryParse(request.Query[key], out var value)
            ? value
            : defaultValue;
    }
}
