namespace HROne.BiometricAgent.Common;

public static class ConstBiometric
{
    public const string PullTypeService = "PL";
    public const string PushTypeService = "PS";
    public const string ServerBiometricTableName = "TIME_BIOMETRIC_PUNCHES_DETAILS";
    public const string DomainCodeColumn = "DOMAIN_CODE";
    public const string IsDataSynced = "ISDATASYNCED";
    public const string UpdateDateColumn = "UPDATE_DATE";
    public const string CreateByColumn = "CREATEDBY";
    public const string BiometricServerName = "BIOMETRIC_SERVER_NAME";

    public const string LogStatusSelect = "S";
    public const string LogStatusExecute = "E";
    public const int FailureThreshold = 3;
    public const double FailureRetryHours = 6;
}

public static class ConstLogger
{
    public const string LogConfigFile = "log4net.config";
}
