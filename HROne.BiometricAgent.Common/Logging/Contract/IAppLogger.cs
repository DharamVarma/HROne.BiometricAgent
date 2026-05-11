namespace HROne.BiometricAgent.Common;

public interface IAppLogger
{
    void Debug(string message);
    void DebugFormat(string format, params object[] args);

    void Info(string message);
    void Info(string message, bool isSuccess);
    void InfoFormat(string format, params object[] args);

    void Warn(string message);
    void WarnFormat(string format, params object[] args);

    void Error(string message);
    void Error(string message, Exception exception);
    void ErrorFormat(string format, params object[] args);

    void Fatal(string message);
    void Fatal(string message, Exception exception);
}
