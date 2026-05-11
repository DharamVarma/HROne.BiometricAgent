namespace HROne.BiometricAgent.Common
{
    public static class ConstBiometricDbType
    {
        public const string SqlServer = "SqlServer";
        public const string MSAccess = "MSAccess";
        public const string MySql = "MySql";
        public const string PostgraySql = "PostgraySql";
    }

    public static class ConstBiometricApi
    {
        public const string ApiBiometricSettingTime = "/api/biometric/ServerInfo/SettingTime/{0}";
        public const string ApiBiometricSetting = "/api/biometric/ClientServerInfo/{0}";
        public const string ApiTransferBulkBiometricData = "/api/biometric/Transfer/Bulk/Data";
        public const string ApiBiometricServiceLog = "/api/biometric/ServiceLog";
        public const string SendPushBiometricDataFailureMail = "/api/biometric/SendPushBiometricDataFailureMail";

        public const string DomainCode = "domainCode";
        public const string HroneApiKey = "hroneApiKey";
    }
}
