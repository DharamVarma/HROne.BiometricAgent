namespace HROne.BiometricAgent.Common;

public static class ConstBiomertricSqlServerClientDb
{
    public const string CheckTableExistsQuery = @"SELECT COUNT(*) FROM information_schema.tables WHERE  table_name = '{0}'";
    public const string SqlCreateTableOnClient = "BIOMETRIC_AGENT_MAPPING_DATA";
    public const string ClientEIBIColumn = "FK_EI_BI_CODE";
    public const string CreateStatusTableOnClient = "SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS";

    public const string SqlCreateTableQuery = @"CREATE TABLE BIOMETRIC_AGENT_MAPPING_DATA ( 
                        [ID] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [FK_EI_BI_CODE] VARCHAR(20)  NULL ,
                        [PUNCH_DATETIME] [datetime] NULL,
                        [MACHINE_NO] [varchar] (50) NULL,
                        [IPADDRESS] [varchar] (20) NULL,
                        [PUNCH_TYPE] [varchar] (50) NULL,
                        [UPDATE_DATE] DATETIME  NULL,
                        [ISDATASYNCED] BIT DEFAULT 0,
                        CONSTRAINT [UC_BIOMETRIC_AGENT_MAPPING_DATA] UNIQUE([FK_EI_BI_CODE],[PUNCH_DATETIME],[MACHINE_NO],[IPADDRESS],[PUNCH_TYPE])
                        )";

    public const string SqlCreateStatusTableQuery = @"CREATE TABLE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS ( 
                        [ID] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [FAILURE_STATUS] BIT NULL ,
                        [FAILURE_DATETIME] [DATETIME] NULL,
                        [FAILURE_COUNTER] [varchar] (50) NULL
                        )";
}
