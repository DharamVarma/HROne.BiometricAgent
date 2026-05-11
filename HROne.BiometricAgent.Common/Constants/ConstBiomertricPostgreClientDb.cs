namespace HROne.BiometricAgent.Common;

public static class ConstBiomertricPostgreClientDb
{
    public const string CheckTableExistsQuery =@"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name='{0}');";
    public const string CreateTableOnClient = "biometric_agent_mapping_data";
    public const string ClientEIBIColumn = "FK_EI_BI_CODE";
    public const string CreateStatusTableOnClient = "server_client_biometric_transfer_status";

    public const string CreateTableQuery = @"CREATE TABLE biometric_agent_mapping_data (
                    ID BIGSERIAL NOT NULL PRIMARY KEY,
                    FK_EI_BI_CODE VARCHAR(20) NULL,
                    PUNCH_DATETIME TIMESTAMP NULL,
                    MACHINE_NO VARCHAR(50) NULL,
                    IPADDRESS VARCHAR(20) NULL,
                    PUNCH_TYPE VARCHAR(50) NULL,
                    UPDATE_DATE TIMESTAMP NULL,
                    ISDATASYNCED BOOLEAN DEFAULT FALSE,
                    CONSTRAINT uc_biometric_agent_mapping_data 
                    UNIQUE(FK_EI_BI_CODE,PUNCH_DATETIME,MACHINE_NO,IPADDRESS,PUNCH_TYPE))";

    public const string CreateStatusTableQuery = @"CREATE TABLE server_client_biometric_transfer_status (
                    ID SERIAL NOT NULL PRIMARY KEY,
                    FAILURE_STATUS INTEGER NULL,
                    FAILURE_DATETIME TIMESTAMP NULL,
                    FAILURE_COUNTER INTEGER NULL
                    );";

    public const string InsertInToQuery = @"INSERT INTO {0} ({1}) ";
}
