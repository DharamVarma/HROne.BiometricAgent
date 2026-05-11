namespace HROne.BiometricAgent.Common;

public static class ConstBiomertricMySqlClientDb
{
    public const string CheckTableExistsQuery = @"SELECT COUNT(*) FROM information_schema.tables WHERE table_name = '{0}'";
    public const string MySqlCreateTableOnClient = "BIOMETRIC_AGENT_MAPPING_DATA";
    public const string CreateStatusTableOnClient = "SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS";
    public const string ClientEIBIColumn = "FK_EI_BI_CODE";
    public const string InsertInToQuery = @"INSERT INTO {0} ({1}) ";

    public const string MySqlCreateTableQuery = @"CREATE TABLE BIOMETRIC_AGENT_MAPPING_DATA ( 
                        `ID` bigint AUTO_INCREMENT NOT NULL PRIMARY KEY,
                        `FK_EI_BI_CODE` VARCHAR(20) NULL ,
                        `PUNCH_DATETIME` DATETIME NULL,
                        `MACHINE_NO` varchar(50) NULL,
                        `IPADDRESS` varchar(20) NULL,
                        `PUNCH_TYPE` varchar(50) NULL,
                        `UPDATE_DATE` DATETIME NULL,
                        `ISDATASYNCED` TINYINT DEFAULT 0,
                        CONSTRAINT `UC_BIOMETRIC_AGENT_MAPPING_DATA` UNIQUE(`FK_EI_BI_CODE`,`PUNCH_DATETIME`,`MACHINE_NO`,`IPADDRESS`,`PUNCH_TYPE`)
                        );";

    public const string MySqlCreateStatusTableQuery = @"CREATE TABLE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS ( 
                        `ID` bigint AUTO_INCREMENT NOT NULL PRIMARY KEY,
                        `FAILURE_STATUS` TINYINT NULL ,
                        `FAILURE_DATETIME` DATETIME NULL,
                        `FAILURE_COUNTER` varchar (50) NULL
                        );";
}
