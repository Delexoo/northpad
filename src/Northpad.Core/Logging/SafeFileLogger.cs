using Microsoft.Extensions.Logging;

namespace Northpad.Core.Logging;

public sealed class SafeFileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _gate = new();

    public SafeFileLoggerProvider(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ILogger CreateLogger(string categoryName) => new SafeFileLogger(_path, categoryName, _gate);

    public void Dispose()
    {
    }

    private sealed class SafeFileLogger : ILogger
    {
        private readonly string _path;
        private readonly string _category;
        private readonly object _gate;

        public SafeFileLogger(string path, string category, object gate)
        {
            _path = path;
            _category = category;
            _gate = gate;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message;
            try
            {
                message = formatter(state, null);
            }
            catch (Exception)
            {
                message = "Log message could not be formatted.";
            }

            message = message.Replace('\r', ' ').Replace('\n', ' ');
            var line = $"{DateTimeOffset.UtcNow:O} {logLevel} {_category} {message}";
            if (exception is not null)
            {
                line += " exceptionType=" + exception.GetType().Name;
            }

            try
            {
                lock (_gate)
                {
                    RotateIfNeeded();
                    File.AppendAllText(_path, line + Environment.NewLine);
                }
            }
            catch (Exception)
            {
                // Logging must not take down the workspace.
            }
        }

        private void RotateIfNeeded()
        {
            var info = new FileInfo(_path);
            if (!info.Exists || info.Length < 1_048_576)
            {
                return;
            }

            var previous = _path + ".1";
            if (File.Exists(previous))
            {
                File.Delete(previous);
            }

            File.Move(_path, previous);
        }
    }
}
