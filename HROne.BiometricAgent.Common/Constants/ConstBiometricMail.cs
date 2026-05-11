namespace HROne.BiometricAgent.Common;

public static class ConstBiometricMail
{
    public const string ConnectionError = "Connection could not be established correctly";
    public const string TableCreateError = "Write permission disable";
    public const string TableAndColumnNotMach = "Provided Table and Column not match with Db";
    public const string FailureEmailSubject = "Biometric Service Status";
    public const string ConsecutiveFailureEmailBody = "Please check Biometric Service there were consecutive failures.";
}
