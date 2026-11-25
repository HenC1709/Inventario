namespace TiendaBodegas.Application.Interfaces;

/// <summary>
/// Application logging contract for recording operational diagnostics and unhandled exceptions.
/// </summary>
public interface IAppLogger
{
    /// <summary>
    /// Writes an informational diagnostic entry.
    /// </summary>
    void Info(string message);

    /// <summary>
    /// Writes a warning diagnostic entry.
    /// </summary>
    void Warning(string message);

    /// <summary>
    /// Writes an error diagnostic entry with optional exception details.
    /// </summary>
    void Error(string message, Exception? exception = null);
}
