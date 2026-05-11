using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using System.Globalization;

namespace HROne.BiometricAgentService.Biz;

public static class PostgreBiometricCreateQuery
{
    public static string SqlDbSelectQuery(string biometricTableName, IList<BiometricSettingEntity> clientDbTableColumns, string domainCode, bool IsTemp, string punchTransferStartDate)
    {
        var columnCount = clientDbTableColumns.Count;
        var columnItem = clientDbTableColumns.ToList();

        if (!IsTemp)
        {
            if ((Convert.ToDateTime(DateTime.Now, CultureInfo.CurrentCulture) - Convert.ToDateTime(punchTransferStartDate, CultureInfo.CurrentCulture)).TotalDays > 90)
            {
                punchTransferStartDate = DateTime.Now.AddDays(-90).ToString("yyyy/MM/dd");
            }

            if (columnCount >= 2)
            {
                var columns = string.Join(",", columnItem.Select(x => $"CT.{x.BiometricColumnName}"));

                return $@"SELECT DISTINCT {columns} FROM {biometricTableName} CT
                          LEFT JOIN biometric_agent_mapping_data TT ON CAST(CT.{columnItem[0].BiometricColumnName} AS VARCHAR(20)) = TT.FK_EI_BI_CODE
                          AND CT.{columnItem[1].BiometricColumnName} = TT.PUNCH_DATETIME
                          WHERE TT.FK_EI_BI_CODE IS NULL
                          AND COALESCE(CT.{columnItem[0].BiometricColumnName}, '') <> ''
                          AND CT.{columnItem[1].BiometricColumnName} >= '{punchTransferStartDate}'";
            }
        }
        else
        {
            string query = "SELECT FK_EI_BI_CODE, PUNCH_DATETIME";

            if (columnItem.FirstOrDefault(x => x.ColumnName == "MACHINE_NO") != null)
            {
                query += ", MACHINE_NO";
            }

            if (columnItem.FirstOrDefault(x => x.ColumnName == "IPADDRESS") != null)
            {
                query += ", IPADDRESS";
            }

            if (columnItem.FirstOrDefault(x => x.ColumnName == "PUNCH_TYPE") != null)
            {
                query += ", PUNCH_TYPE";
            }

            return $"{query},'{domainCode}' {ConstBiometric.DomainCodeColumn}, 100 AS CREATEDBY, id FROM biometric_agent_mapping_data WHERE ISDATASYNCED = false LIMIT 1000";
        }

        return string.Empty;
    }

    public const string TransferStatusTableCheckHavingData = @"SELECT EXISTS (SELECT 1 FROM server_client_biometric_transfer_status)";

    public static string DbUpdateLogStatus(string Type, bool IsPass, bool isDataExists)
    {
        if (Type == "E")
        {
            string query = "";

            if (!isDataExists)
            {
                if (IsPass)
                {
                    query = @"INSERT INTO server_client_biometric_transfer_status (FAILURE_STATUS, FAILURE_DATETIME, FAILURE_COUNTER)
                              VALUES (0, NULL, NULL);";
                }
                else
                {
                    query = @"INSERT INTO server_client_biometric_transfer_status (FAILURE_STATUS, FAILURE_DATETIME, FAILURE_COUNTER)
                              VALUES (1, TIMEZONE('UTC', NOW()), 1);";
                }
            }
            else
            {
                if (IsPass)
                {
                    query = @"UPDATE server_client_biometric_transfer_status SET FAILURE_STATUS=0,FAILURE_DATETIME=NULL,FAILURE_COUNTER=NULL;";
                }
                else
                {
                    query = @"UPDATE server_client_biometric_transfer_status
                              SET 
                              FAILURE_STATUS = 1,
                              FAILURE_DATETIME = TIMEZONE('UTC', NOW()),
                              FAILURE_COUNTER = COALESCE(FAILURE_COUNTER, 0) + 1;";
                }
            }
            return query;
        }
        else
        {
            return isDataExists ? @"SELECT COALESCE(FAILURE_STATUS,0) AS FAILURE_STATUS,
                                        CASE WHEN COALESCE(FAILURE_STATUS,0)=0 THEN NULL ELSE FAILURE_DATETIME END AS FAILURE_DATETIME,
                                        COALESCE(FAILURE_COUNTER,0) AS FAILURE_COUNTER 
                                        FROM server_client_biometric_transfer_status;"
                                : "SELECT 1 AS FAILURE_STATUS,NULL AS FAILURE_DATETIME,0 AS FAILURE_COUNTER;";
        }
    }

    public static string DbUpdateSyncStatus(string Ids)
    {
        return @"UPDATE BIOMETRIC_AGENT_MAPPING_DATA SET ISDATASYNCED=true WHERE ID IN (" + Ids + ")";
    }
}
