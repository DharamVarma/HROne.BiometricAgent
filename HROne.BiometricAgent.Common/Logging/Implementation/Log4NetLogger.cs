using log4net;

namespace HROne.BiometricAgent.Common;

/// <summary>
/// log4net implementation of <see cref="IAppLogger"/> (Open/Closed: extend via interface, not by modifying callers).
/// </summary>
public sealed class Log4NetLogger : IAppLogger
{
    private readonly ILog _log;
    private readonly bool _allowWriteFullLog;

    public Log4NetLogger(ILog log, bool allowWriteFullLog)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _allowWriteFullLog = allowWriteFullLog;
    }

    public void Debug(string message)
    {
        if (_allowWriteFullLog)
        {
            _log.Debug(message);
        }
    }
    
    public void DebugFormat(string format, params object[] args)
    {
        if (_allowWriteFullLog)
        {
            _log.DebugFormat(format, args);
        }
    }    

    public void Info(string message)
    {
        if (_allowWriteFullLog)
        {
            _log.Info(message);
        }
    }

    public void Info(string message, bool isSuccess)
    {
        if (_allowWriteFullLog || isSuccess)
        {
            _log.Info(message);
        }
    }

    public void InfoFormat(string format, params object[] args)
    {
        if (_allowWriteFullLog)
        {
            _log.InfoFormat(format, args);
        }
    }

    public void Warn(string message)
    {
        if (_allowWriteFullLog)
        {
            _log.Warn(message);
        }
    }
    
    public void WarnFormat(string format, params object[] args)
    {
        if (_allowWriteFullLog)
        {
            _log.WarnFormat(format, args);
        }
    }    

    public void Error(string message) => _log.Error(message);
    public void Error(string message, Exception exception) => _log.Error(message, exception);
    public void ErrorFormat(string format, params object[] args) => _log.ErrorFormat(format, args);

    public void Fatal(string message) => _log.Fatal(message);
    public void Fatal(string message, Exception exception) => _log.Fatal(message, exception);
}
