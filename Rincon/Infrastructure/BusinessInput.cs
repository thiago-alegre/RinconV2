using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Rincon.Infrastructure;

public static class BusinessInput
{
    public static (DateTime From, DateTime To) NormalizeDateRange(DateTime? dateFrom, DateTime? dateTo)
    {
        var from = (dateFrom ?? DateTime.Today.AddMonths(-1)).Date;
        var to = (dateTo ?? DateTime.Today).Date;
        return to < from ? (to, from) : (from, to);
    }

    // Exact numeric(18,2) range; reject precision loss instead of silently rounding money.
    public static bool IsMoney(decimal value) => value >= 0 && value <= 9999999999999999.99m && decimal.Round(value, 2) == value;
    public static void Money(ModelStateDictionary state, string field, decimal value, bool positive = false)
    {
        if (!IsMoney(value) || (positive && value == 0))
            state.AddModelError(field, "Ingrese un importe válido con un máximo de dos decimales.");
    }
    public static void Date(ModelStateDictionary state, DateTime value)
    {
        if (value == default || value.Year >= 9999)
            state.AddModelError("Date", "Ingrese una fecha válida anterior al año 9999.");
    }
    public static DateTime ExclusiveEnd(DateTime value) => value.Date == DateTime.MaxValue.Date ? DateTime.MaxValue : value.Date.AddDays(1);
}
