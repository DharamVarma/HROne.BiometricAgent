using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using System.Globalization;

namespace HROne.BiometricAgentService.Biz;

public static class MsAccessBiometricCreateQuery
{
    public static string OleDbSelectQuery(string biometricTableName, IList<BiometricSettingEntity> clientDbTableColumns, string domainCode, bool IsTemp, string punchTransferStartDate)
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
                return $"SELECT DISTINCT {columns} FROM {biometricTableName} AS CT " +
                       $" LEFT JOIN BIOMETRIC_AGENT_MAPPING_DATA AS TT " +
                       $" ON CStr(CT.{columnItem[0].BiometricColumnName}) = TT.FK_EI_BI_CODE " +
                       $" AND CT.{columnItem[1].BiometricColumnName} = TT.PUNCH_DATETIME " +
                       $" WHERE TT.FK_EI_BI_CODE IS NULL " +
                       $" AND IIf(IsNull(CT.{columnItem[0].BiometricColumnName}), '', CT.{columnItem[0].BiometricColumnName}) <> '' " +
                       $" AND CT.{columnItem[1].BiometricColumnName} >= #{punchTransferStartDate}#";
            }
        }
        else
        {
            string query = "SELECT TOP 1000 FK_EI_BI_CODE, PUNCH_DATETIME";
            if (columnItem.Any(x => x.ColumnName == "MACHINE_NO"))
            {
                query += ", MACHINE_NO";
            }
            if (columnItem.Any(x => x.ColumnName == "IPADDRESS"))
            {
                query += ", IPADDRESS";
            }
            if (columnItem.Any(x => x.ColumnName == "PUNCH_TYPE"))
            {
                query += ", PUNCH_TYPE";
            }
            return query + ", '" + domainCode + "' AS " + ConstBiometric.DomainCodeColumn + ", 100 AS [CREATEDBY], ID " + "FROM BIOMETRIC_AGENT_MAPPING_DATA WHERE ISDATASYNCED = 0";

        }
        return string.Empty;
    }

    public static string OleDbUpdateLogStatus(string logStatusType, bool isPass, bool isDataExists)
    {
        if (logStatusType == "E")
        {
            string query = "";

            if (!isDataExists)
            {
                if (isPass)
                {
                    query = @"INSERT INTO SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS (FAILURE_STATUS, FAILURE_DATETIME, FAILURE_COUNTER)
                              VALUES (0, NULL, NULL);";
                }
                else
                {
                    query = @"INSERT INTO SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS (FAILURE_STATUS, FAILURE_DATETIME, FAILURE_COUNTER)
                              VALUES (1, NOW(), 1);";
                }
            }
            else
            {
                if (isPass)
                {
                    query = @"UPDATE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS SET FAILURE_STATUS=0,FAILURE_DATETIME=NULL,FAILURE_COUNTER=NULL;";
                }
                else
                {
                    query = @"UPDATE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS SET FAILURE_STATUS=1,FAILURE_DATETIME=NOW(),
                              FAILURE_COUNTER = IIF(ISNULL(FAILURE_COUNTER),1,FAILURE_COUNTER+1);";
                }
            }
            return query;
        }
        else
        {
            return isDataExists
                ? @"SELECT IIF(ISNULL(FAILURE_STATUS),0,FAILURE_STATUS) AS FAILURE_STATUS,
                    IIF(IIF(ISNULL(FAILURE_STATUS),0,FAILURE_STATUS)=0,NULL,FAILURE_DATETIME) AS FAILURE_DATETIME,
                    IIF(ISNULL(FAILURE_COUNTER),0,FAILURE_COUNTER) AS FAILURE_COUNTER FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS;"
                : @"SELECT 1 AS FAILURE_STATUS, NULL AS FAILURE_DATETIME, 0 AS FAILURE_COUNTER;";
        }
    }

    public static string OleDbUpdateSyncStatus(string Ids)
    {
        return @"UPDATE BIOMETRIC_AGENT_MAPPING_DATA SET ISDATASYNCED=1 WHERE ID IN (" + Ids + ")";
    }
}
