using HROne.BiometricAgentModel;
using log4net;

namespace HROne.BiometricAgent.Common;

/// <summary>
/// Factory that creates <see cref="Log4NetLogger"/> instances backed by log4net (one logger per type).
/// </summary>
public sealed class Log4NetLoggerFactory(AppSettings _settings) : IAppLoggerFactory
{
    public IAppLogger CreateLogger(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var log = LogManager.GetLogger(type);
        return new Log4NetLogger(log, _settings.AllowWriteFullLog);
    }

    public IAppLogger CreateLogger<T>() => CreateLogger(typeof(T));
}
