using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using System.Globalization;

namespace HROne.BiometricAgentService.Biz;

public static class MySqlBiometricCreateQuery
{
    internal static string MySqlDbSelectQuery(string biometricTableName, IList<BiometricSettingEntity> clientDbTableColumns, string domainCode, bool IsTemp, string punchTransferStartDate)
    {
        var columnCount = clientDbTableColumns.Count;
        var columnItem = clientDbTableColumns.ToList();
        if (!IsTemp)
        {
            if ((Convert.ToDateTime(DateTime.Now, CultureInfo.CurrentCulture) - Convert.ToDateTime(punchTransferStartDate, CultureInfo.CurrentCulture)).TotalDays > 90)
            {
                punchTransferStartDate = Convert.ToDateTime(DateTime.Now.AddDays(-90), CultureInfo.CurrentCulture).ToString("yyyy/MM/dd");
            }
            if (columnCount >= 2)
            {
                var columns = string.Join(",", columnItem.Select(x => "CT." + x.BiometricColumnName));
                return $"SELECT DISTINCT {columns} FROM {biometricTableName} CT " +
                       $" LEFT OUTER JOIN BIOMETRIC_AGENT_MAPPING_DATA TT " +
                       $" ON CAST(CT.{columnItem[0].BiometricColumnName} AS CHAR(20)) = TT.FK_EI_BI_CODE " +
                       $" AND IFNULL(CT.{columnItem[1].BiometricColumnName},'')  = TT.PUNCH_DATETIME " +
                       $" WHERE TT.FK_EI_BI_CODE IS NULL AND IFNULL(CT.{columnItem[0].BiometricColumnName},'') <> '' " +
                       $" AND CT.{columnItem[1].BiometricColumnName} >= '{punchTransferStartDate}'";
            }
        }
        else
        {
            string query = "SELECT FK_EI_BI_CODE,PUNCH_DATETIME";
            if (columnItem.FirstOrDefault(x => x.ColumnName == "MACHINE_NO") != null)
            {
                query += ",MACHINE_NO";
            }
            if (columnItem.FirstOrDefault(x => x.ColumnName == "IPADDRESS") != null)
            {
                query += ",IPADDRESS";
            }
            if (columnItem.FirstOrDefault(x => x.ColumnName == "PUNCH_TYPE") != null)
            {
                query += ",PUNCH_TYPE";
            }
            return $"{query},'{domainCode}' {ConstBiometric.DomainCodeColumn},100 'CREATEDBY',ID FROM BIOMETRIC_AGENT_MAPPING_DATA WHERE ISDATASYNCED=0 LIMIT 1000;";
        }
        return string.Empty;
    }

    public static string MySqlDbUpdateLogStatus(string logStatusType, bool isPass, bool isDataExists)
    {
        if (logStatusType == "E")
        {
            string selectQuery = @"CREATE TEMPORARY TABLE TEMP_TRANSFER_STATUS
                                  (
                                      ID BIGINT DEFAULT 0,
	                                  FAILURE_STATUS bit DEFAULT 0,
                                      FAILURE_DATETIME datetime DEFAULT NULL,
                                      FAILURE_COUNTER int DEFAULT NULL
                                  );
                                  INSERT INTO TEMP_TRANSFER_STATUS (ID,FAILURE_STATUS,FAILURE_DATETIME,FAILURE_COUNTER)
                                  ";
            if (!isDataExists)
            {
                if (isPass)
                {
                    selectQuery += @"SELECT 1,0,NULL,NULL;";
                }
                else
                {
                    selectQuery += @"SELECT 1,1,UTC_TIMESTAMP,1;";
                }
            }
            else
            {
                if (isPass)
                {
                    selectQuery += @"SELECT ID,0,NULL,NULL FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS;";
                }
                else
                {
                    selectQuery += @"SELECT ID,1,UTC_TIMESTAMP,IFNULL(FAILURE_COUNTER,0)+1 FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS;";
                }
            }
            selectQuery += (!isDataExists ? @"
                                   INSERT INTO SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS (FAILURE_STATUS,FAILURE_DATETIME,FAILURE_COUNTER)
                                   SELECT FAILURE_STATUS,FAILURE_DATETIME," + (isPass ? "FAILURE_COUNTER" : "IFNULL(FAILURE_COUNTER,1)") + " FROM TEMP_TRANSFER_STATUS;"
                           : @"
                                   UPDATE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS T1
                                   INNER JOIN TEMP_TRANSFER_STATUS T2 ON T1.ID=T2.ID 
                                   SET T1.FAILURE_STATUS=T2.FAILURE_STATUS,
	                                   T1.FAILURE_DATETIME=T2.FAILURE_DATETIME,
                                       T1.FAILURE_COUNTER=" + (!isDataExists ? "IFNULL(T2.FAILURE_COUNTER,1);" : "T2.FAILURE_COUNTER;")
            ) +

            @"
            DROP TEMPORARY TABLE TEMP_TRANSFER_STATUS;";

            return selectQuery;
        }
        else
        {
            return isDataExists ? @"SELECT IFNULL(FAILURE_STATUS,0) `FAILURE_STATUS`,
                                        CASE WHEN IFNULL(FAILURE_STATUS,0)=0 THEN NULL ELSE FAILURE_DATETIME END `FAILURE_DATETIME`,
                                        IFNULL(FAILURE_COUNTER,0) `FAILURE_COUNTER` 
                                        FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS;"
                                : "SELECT 1 `FAILURE_STATUS`,NULL `FAILURE_DATETIME`,0 `FAILURE_COUNTER`;";
        }
    }

    public static string MySqlDbUpdateSyncStatus(string Ids)
    {
        return @"UPDATE BIOMETRIC_AGENT_MAPPING_DATA SET ISDATASYNCED=1 WHERE ID IN (" + Ids + ")";
    }

    public const string MySqlTransferStatusTableCheckHavingData = @"SELECT EXISTS (SELECT 1 FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS)";
}
