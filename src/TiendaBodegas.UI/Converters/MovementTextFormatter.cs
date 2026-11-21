using System.Globalization;
using System.Windows.Data;
using TiendaBodegas.Application.DTOs;
using TiendaBodegas.Domain.Enums;

namespace TiendaBodegas.UI.Converters;

/// <summary>
/// Builds the human-readable title and subtitle texts shown for each row of the sales/movements list.
/// </summary>
public static class MovementTextFormatter
{
    /// <summary>
    /// Builds the row title, e.g. <c>Venta #V-0007 — Salida de Stock</c>.
    /// </summary>
    public static string BuildTitle(StockMovementDisplayDto movement)
    {
        return movement.MovementType == MovementType.Exit
            ? $"Venta #V-{movement.Id:0000} — Salida de Stock"
            : $"Compra #C-{movement.Id:0000} — Entrada de Stock";
    }

    /// <summary>
    /// Builds a relative date label such as <c>Hoy, 10:15 AM</c> or <c>Ayer, 04:40 PM</c>.
    /// </summary>
    public static string BuildDateLabel(DateTime date, DateTime now)
    {
        var time = date.ToString("hh:mm tt", CultureInfo.InvariantCulture);

        if (date.Date == now.Date)
            return $"Hoy, {time}";

        if (date.Date == now.Date.AddDays(-1))
            return $"Ayer, {time}";

        return $"{date:dd/MM/yyyy}, {time}";
    }

    /// <summary>
    /// Builds the row subtitle, e.g. <c>Hoy, 10:15 AM • 5 unidades (Café Espresso Molido 1kg)</c>.
    /// </summary>
    public static string BuildSubtitle(StockMovementDisplayDto movement, DateTime now)
    {
        var units = movement.Quantity == 1 ? "unidad" : "unidades";
        var text = $"{BuildDateLabel(movement.Date, now)} • {movement.Quantity} {units} ({movement.ProductName})";

        var hasCustomNote = !string.IsNullOrWhiteSpace(movement.Notes)
            && !movement.Notes.StartsWith("Orden #", StringComparison.OrdinalIgnoreCase);

        return hasCustomNote ? $"{text} • {movement.Notes}" : text;
    }
}

/// <summary>
/// WPF converter producing the row title from a <see cref="StockMovementDisplayDto"/>.
/// </summary>
public sealed class MovementTitleConverter : IValueConverter
{
    /// <summary>
    /// Converts a movement DTO into its display title.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is StockMovementDisplayDto movement ? MovementTextFormatter.BuildTitle(movement) : string.Empty;

    /// <summary>
    /// Not supported (one-way converter).
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// WPF converter producing the row subtitle from a <see cref="StockMovementDisplayDto"/>.
/// </summary>
public sealed class MovementSubtitleConverter : IValueConverter
{
    /// <summary>
    /// Converts a movement DTO into its display subtitle.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is StockMovementDisplayDto movement ? MovementTextFormatter.BuildSubtitle(movement, DateTime.Now) : string.Empty;

    /// <summary>
    /// Not supported (one-way converter).
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
