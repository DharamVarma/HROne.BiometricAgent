namespace HROne.BiometricAgent.Common;

/// <summary>
/// Factory for creating named loggers (Single Responsibility: create loggers for a given type).
/// Enables proper context (e.g. logger per class) without depending on log4net types.
/// </summary>
public interface IAppLoggerFactory
{
    /// <summary>
    /// Creates an <see cref="IAppLogger"/> for the given type (typically the consuming class).
    /// </summary>
    IAppLogger CreateLogger(Type type);

    /// <summary>
    /// Creates an <see cref="IAppLogger"/> for the given type.
    /// </summary>
    IAppLogger CreateLogger<T>();
}
