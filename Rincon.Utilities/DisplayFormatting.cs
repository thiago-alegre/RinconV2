using Rincon.Utilities.Enums;

namespace Rincon.Utilities;

public static class DisplayFormatting
{
    public static string Quantity(decimal quantity) => quantity % 1 == 0
        ? quantity.ToString("N0")
        : quantity.ToString("N2");

    public static string PaymentMethodName(PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Efectivo => "Efectivo",
        PaymentMethod.Transferencia => "Transferencia",
        PaymentMethod.CuentaPersonal => "Cuenta personal",
        PaymentMethod.Combinado => "Combinado",
        _ => "Sin especificar"
    };
}
