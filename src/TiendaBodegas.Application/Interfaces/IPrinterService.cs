using TiendaBodegas.Application.DTOs;

namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application service contract for printing 80 mm thermal receipts for stock movements and sales.
/// </summary>
public interface IPrinterService
{
    /// <summary>
    /// Opens the system print dialog and dispatches an 80 mm thermal receipt for the given movement.
    /// Returns <c>true</c> if the user confirmed printing; <c>false</c> if cancelled.
    /// </summary>
    /// <param name="movement">Enriched stock movement projection to print.</param>
    /// <param name="businessName">Optional store/warehouse header name; falls back to persisted settings when omitted.</param>
    /// <param name="currencyCode">Optional active ISO currency code; falls back to persisted settings when omitted.</param>
    bool PrintMovementTicket(
        StockMovementDisplayDto movement,
        string? businessName = null,
        string? currencyCode = null);
}
