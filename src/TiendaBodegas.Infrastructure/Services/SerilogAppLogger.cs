using System.IO;
using System.Text;
using Serilog;
using Serilog.Core;
using TiendaBodegas.Application.Interfaces;
using TiendaBodegas.Infrastructure.Persistence;

namespace TiendaBodegas.Infrastructure.Services;

/// <summary>
/// File-backed diagnostic logger using a dedicated Serilog pipeline writing UTF-8 rolling daily logs.
/// </summary>
public class SerilogAppLogger : IAppLogger, IDisposable
{
    private readonly Logger _logger;
    private bool _disposed;

    /// <summary>
    /// Gets the directory where daily rolling log files (<c>stockflow-*.log</c>) are written.
    /// </summary>
    public string LogsDirectory { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SerilogAppLogger"/> targeting <paramref name="logsDirectory"/>,
    /// or the default per-user logs directory when omitted.
    /// </summary>
    public SerilogAppLogger(string? logsDirectory = null)
    {
        LogsDirectory = string.IsNullOrWhiteSpace(logsDirectory)
            ? AppDataPaths.GetLogsDirectory()
            : logsDirectory;

        Directory.CreateDirectory(LogsDirectory);

        var logFilePattern = Path.Combine(LogsDirectory, "stockflow-.log");
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logFilePattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                encoding: Encoding.UTF8,
                shared: true)
            .CreateLogger();
    }

    /// <inheritdoc />
    public void Info(string message)
    {
        if (!_disposed)
            _logger.Information("{Message}", message);
    }

    /// <inheritdoc />
    public void Warning(string message)
    {
        if (!_disposed)
            _logger.Warning("{Message}", message);
    }

    /// <inheritdoc />
    public void Error(string message, Exception? exception = null)
    {
        if (_disposed)
            return;

        if (exception != null)
            _logger.Error(exception, "{Message}", message);
        else
            _logger.Error("{Message}", message);
    }

    /// <summary>
    /// Flushes and closes the Serilog file sink so log file handles are released immediately.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.Dispose();
        GC.SuppressFinalize(this);
    }
}
