namespace HROne.BiometricAgent.Common;

public static class ConstBiomertricMSAccessClientDb
{
    public const string CheckTableExistsQuery = @"SELECT COUNT(*) FROM MSysObjects WHERE Name='{0}' AND Type=1";
    public const string CreateTableOnClient = "BIOMETRIC_AGENT_MAPPING_DATA";
    public const string ClientEIBIColumn = "FK_EI_BI_CODE";
    public const string CreateStatusTableOnClient = "SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS";

    public const string CreateTableQuery = @"CREATE TABLE BIOMETRIC_AGENT_MAPPING_DATA (
                                                   ID AUTOINCREMENT PRIMARY KEY,
                                                   FK_EI_BI_CODE TEXT(20),
                                                   PUNCH_DATETIME DATETIME,
                                                   MACHINE_NO TEXT(50),
                                                   IPADDRESS TEXT(20),
                                                   PUNCH_TYPE TEXT(50),
                                                   UPDATE_DATE DATETIME,
                                                   ISDATASYNCED INTEGER DEFAULT 0)";

    public const string CreateUniqueIndex = @"CREATE UNIQUE INDEX UC_BIOMETRIC_AGENT_MAPPING_DATA ON BIOMETRIC_AGENT_MAPPING_DATA
                                                    (FK_EI_BI_CODE, PUNCH_DATETIME, MACHINE_NO, IPADDRESS, PUNCH_TYPE)";

    public const string CreateStatusTableQuery = @"CREATE TABLE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS (
                                                         ID AUTOINCREMENT PRIMARY KEY,
                                                         FAILURE_STATUS INTEGER,
                                                         FAILURE_DATETIME DATETIME,
                                                         FAILURE_COUNTER TEXT(50))";
    public const string InsertInToQuery = @"INSERT INTO {0} ({1}) ";

    public const string TransferStatusTableCheckHavingData = @"SELECT COUNT(*) FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS";
}
