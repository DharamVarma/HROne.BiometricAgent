using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using System.Globalization;

namespace HROne.BiometricAgentService.Biz;

public static class SqlServerBiometricCreateQuery
{
    internal static string SqlDbSelectQuery(string biometricTableName, IList<BiometricSettingEntity> clientDbTableColumns, string domainCode, bool IsTemp, string punchTransferStartDate)
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
                       $" ON CAST( CT.{columnItem[0].BiometricColumnName} AS VARCHAR(20)) = TT.FK_EI_BI_CODE " +
                       $" AND ISNULL( CT.{columnItem[1].BiometricColumnName},'')  = TT.PUNCH_DATETIME " +
                       $" WHERE TT.FK_EI_BI_CODE IS NULL AND ISNULL(CT.{columnItem[0].BiometricColumnName},'') <> '' " +
                       $" AND CT.{columnItem[1].BiometricColumnName} >= '{punchTransferStartDate}'";
            }
        }
        else
        {
            string query = "SELECT TOP(1000) FK_EI_BI_CODE,PUNCH_DATETIME";
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
            return query + ",'" + domainCode + "' " + ConstBiometric.DomainCodeColumn + ",100 [CREATEDBY],ID FROM BIOMETRIC_AGENT_MAPPING_DATA WHERE ISDATASYNCED=0";
        }
        return string.Empty;
    }

    internal static string SqlDbUpdateLogStatus(string Type, bool IsPass)
    {
        if (Type == "E")
        {
            return @"
                DECLARE @FAILURE_STATUS BIT=0
                DECLARE @FAILURE_DATETIME DATETIME=NULL
                DECLARE @FAILURE_COUNTER INT=NULL

                " + (IsPass ? " SELECT @FAILURE_STATUS=0,@FAILURE_DATETIME=NULL,@FAILURE_COUNTER=NULL " :
            " SELECT @FAILURE_STATUS=1,@FAILURE_DATETIME=GETUTCDATE() " +
            " SELECT @FAILURE_COUNTER=ISNULL(FAILURE_COUNTER,0)+1 FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS " +
            " SET @FAILURE_COUNTER=ISNULL(@FAILURE_COUNTER,1) ") + @"

                IF NOT EXISTS (SELECT 1 FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS)
                BEGIN
                    INSERT INTO SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS(FAILURE_STATUS,FAILURE_DATETIME,FAILURE_COUNTER)
                    VALUES (@FAILURE_STATUS,@FAILURE_DATETIME,@FAILURE_COUNTER)
                END
                ELSE
                BEGIN
                    UPDATE SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS SET 
                    FAILURE_STATUS=@FAILURE_STATUS,FAILURE_DATETIME=@FAILURE_DATETIME,
                    FAILURE_COUNTER=@FAILURE_COUNTER
                END
                ";
        }
        else
        {
            return @"
                IF EXISTS (SELECT 1 FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS)
                BEGIN
                    SELECT ISNULL(FAILURE_STATUS,0) [FAILURE_STATUS],
                    CASE WHEN ISNULL(FAILURE_STATUS,0)=0 THEN NULL ELSE FAILURE_DATETIME END [FAILURE_DATETIME],
                    ISNULL(FAILURE_COUNTER,0) [FAILURE_COUNTER] 
                    FROM SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS
                END
                ELSE
                BEGIN
                    SELECT 1 [FAILURE_STATUS],NULL [FAILURE_DATETIME],0 [FAILURE_COUNTER]
                END
                ";
        }
    }

    internal static string SqlDbUpdateSyncStatus(string Ids)
    {
        return $"UPDATE BIOMETRIC_AGENT_MAPPING_DATA SET ISDATASYNCED=1 WHERE ID IN ({Ids})";
    }
}
