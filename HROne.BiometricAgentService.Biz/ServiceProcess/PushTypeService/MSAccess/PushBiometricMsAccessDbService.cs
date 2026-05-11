using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.Runtime.Versioning;

namespace HROne.BiometricAgentService.Biz;

[SupportedOSPlatform("windows")]
public class PushBiometricMsAccessDbService(AppSettings _settings, IAppLoggerFactory _loggerFactory,
                                            IBiometricErrorLogService _biometricErrorLogService,
                                            IBiometricMailService _biometricMailService,
                                            ITransferBiometricBulkDataService _transferBiometricData) : IBiometricService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<PushBiometricMsAccessDbService>();

    public async Task BiometricProcess(BiometricViewModel model, string domainCode)
    {
        if (string.IsNullOrEmpty(model.ConnectionString))
        {
            throw new ArgumentException(nameof(model.ConnectionString));
        }
        try
        {
            if (await TestConnectionString(model.ConnectionString, model.BiometricSettingId, domainCode))
            {
                var clientDbTableColumns = model.BiometricSettingInfo;
                if (await ValidateClientTableAndColumn(model.ConnectionString, model.BiometricTableName, clientDbTableColumns, model.BiometricSettingId, domainCode))
                {
                    if (!await CreateTableOnClientDb(model.ConnectionString, model.BiometricSettingId, domainCode))
                    {
                        return;
                    }
                    var selectQuery = MsAccessBiometricCreateQuery.OleDbSelectQuery(model.BiometricTableName, clientDbTableColumns, domainCode, false, _settings.PunchTransferStartDate);
                    if (!string.IsNullOrEmpty(selectQuery))
                    {
                        try
                        {
                            await CreateLogTableOnClientDb(model.ConnectionString, domainCode);
                            int transferDataCount = 0;
                            using (var conn = new OleDbConnection(model.ConnectionString))
                            {
                                await conn.OpenAsync();
                                while (true)
                                {
                                    //Maintain data into temp table with sync status 0
                                    MaintainTempTable(model, domainCode, clientDbTableColumns, selectQuery);

                                    bool startTransfer = false;
                                    //Get failure count and date
                                    var failureCountdataSetRow = (await GetMaintainFailureCount(ConstBiometric.LogStatusSelect, false, domainCode, conn)).Tables[0].Rows.OfType<DataRow>();

                                    var firstRow = failureCountdataSetRow.FirstOrDefault();
                                    int failureCount = firstRow != null ? Convert.ToInt32(firstRow["FAILURE_COUNTER"], CultureInfo.CurrentCulture) : 0;

                                    //Check failure count>3 then transfer server start when time interval is greater then or euqal to 6 hour
                                    if (failureCount > ConstBiometric.FailureThreshold)
                                    {
                                        DateTime failureTime = firstRow != null && firstRow["FAILURE_DATETIME"] != DBNull.Value
                                            ? Convert.ToDateTime(firstRow["FAILURE_DATETIME"], CultureInfo.CurrentCulture)
                                            : DateTime.MinValue;
                                        if ((DateTime.UtcNow - failureTime).TotalHours >= ConstBiometric.FailureRetryHours)//Start Service every 6 hour interval
                                        {
                                            startTransfer = true;
                                        }
                                    }
                                    else
                                    {
                                        startTransfer = true;
                                    }
                                    if (startTransfer) //Stop the service after 3 consecutive failure and send email
                                    {
                                        //Get data from temp table where sync status is 0
                                        DataSet dataSetTransfer = GetFromTempTable(model, domainCode, clientDbTableColumns, conn, _settings.PunchTransferStartDate);
                                        if (dataSetTransfer.Tables.Count > 0 && dataSetTransfer.Tables[0].Rows.Count > 0)
                                        {
                                            int insertInToServer = 0;
                                            insertInToServer = await _transferBiometricData.TransferBiometricBulkData(dataSetTransfer.Tables[0], clientDbTableColumns.Select(c => c.ColumnName).ToList(), domainCode, model.ServerName);
                                            if (insertInToServer == 1) // success instered into Hrone server 
                                            {
                                                UpdateSyncStatus(conn, dataSetTransfer, domainCode);
                                                insertInToServer = 0;
                                                transferDataCount = transferDataCount == 0
                                                                  ? dataSetTransfer.Tables[0].Rows.Count
                                                                  : transferDataCount + dataSetTransfer.Tables[0].Rows.Count;

                                                await GetMaintainFailureCount(ConstBiometric.LogStatusExecute, true, domainCode, conn);

                                                _logger.Info($"Method - BiometricProcess() FOR DOMAIN : [{domainCode}] [{transferDataCount}] Records Transferred Successfully END.", true);
                                            }
                                            else
                                            {
                                                await GetMaintainFailureCount(ConstBiometric.LogStatusExecute, false, domainCode, conn);

                                                if (failureCount == ConstBiometric.FailureThreshold)
                                                {
                                                    //Stop the service after 3 consecutive failure and send mail
                                                    var emailMessage = new BiometricMailEntity
                                                    {
                                                        IsMailSend = true,
                                                        Domain = domainCode,
                                                        Message = ConstBiometricMail.ConsecutiveFailureEmailBody,
                                                        ReceiverEmail = _settings.ServiceFailToEmailId,
                                                        IsEmailBlocked = false,
                                                        Subject = ConstBiometricMail.FailureEmailSubject,
                                                        MailHeader = ConstBiometricMail.FailureEmailSubject
                                                    };
                                                    await _biometricMailService.SendPushBiometricDataFailureMail(emailMessage);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            _logger.Info($"Method - BiometricProcess() FOR DOMAIN : [{domainCode}] [{transferDataCount}] Records Transferred Successfully END.", true);
                                            break;
                                        }
                                        await Task.Delay(5000);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Method - BiometricProcess() FOR DOMAIN : [{domainCode}] error in(catch) - ", ex);
                        }
                    }
                    else
                    {
                        _logger.Info($"Method - BiometricProcess() Transfer Data SelectQuery Can not be null FOR DOMAIN : [{domainCode}]  END.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - BiometricProcess() FOR DOMAIN : [{domainCode}] error in(catch) - ", ex);
        }
    }

    public async Task<bool> TestConnectionString(string connectionString, long biometricSettingId, string domainCode)
    {
        try
        {
            await using var conn = new OleDbConnection(connectionString);
            await conn.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.MSAccess,
                ErrorReason = ex.Message,
                ErrorSubject = ConstBiometricMail.ConnectionError,
                DomainCode = domainCode,
                LogOnUserId = 0
            };
            _logger.Error($"Method - TestConnectionString() FOR DOMAIN : {domainCode} - ", ex);
            await _biometricErrorLogService.BiometricSettingValidationLog(errorEntity);
            return false;
        }
    }

    public async Task<bool> ValidateClientTableAndColumn(string connectionString, string clientBiometricTableName, IList<BiometricSettingEntity> clientDbTableColumns, long biometricSettingId, string domainCode)
    {
        try
        {
            var columns = clientDbTableColumns.Where(c => !string.IsNullOrEmpty(c.BiometricColumnName)).Select(c => c.BiometricColumnName).ToList();
            if (columns.Count == 0)
                return false;

            var selectQuery = "SELECT TOP 1 " + string.Join(", ", columns) + " FROM " + clientBiometricTableName;
            await using var conn = new OleDbConnection(connectionString);
            await conn.OpenAsync();
            using var dataAdapter = new OleDbDataAdapter(selectQuery, conn);
            var dataSet = new DataSet();
            dataAdapter.Fill(dataSet);
            return dataSet.Tables.Count > 0;
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.MSAccess,
                ErrorReason = ex.Message,
                ErrorSubject = ConstBiometricMail.TableAndColumnNotMach,
                DomainCode = domainCode
            };
            _logger.Error($"Method - ValidateClientTableAndColumn() FOR DOMAIN : {domainCode} - ", ex);
            await _biometricErrorLogService.BiometricSettingValidationLog(errorEntity);
            return false;
        }
    }

    public async Task<bool> CreateTableOnClientDb(string connectionString, long biometricSettingId, string domainCode)
    {
        try
        {
            using (var conn = new OleDbConnection(connectionString))
            {
                await conn.OpenAsync();
                var IsExists = (await conn.GetSchemaAsync("Tables", new string[4] { null, null, ConstBiomertricMSAccessClientDb.CreateTableOnClient, "TABLE" })).Rows.Count > 0;
                if (!IsExists)
                {
                    using (var cmdCreateTable = new OleDbCommand(ConstBiomertricMSAccessClientDb.CreateTableQuery, conn))
                    {
                        await cmdCreateTable.ExecuteNonQueryAsync();
                    }
                }
                await conn.CloseAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.MSAccess,
                ErrorReason = ex.Message,
                ErrorSubject = ConstBiometricMail.TableCreateError,
                DomainCode = domainCode
            };
            _logger.Error($"Method - CreateTableOnClientDb() FOR DOMAIN : {domainCode} - ", ex);
            await _biometricErrorLogService.BiometricSettingValidationLog(errorEntity);
            return false;
        }
    }

    private async Task<bool> CreateLogTableOnClientDb(string connectionString, string domainCode)
    {
        try
        {
            using (var conn = new OleDbConnection(connectionString))
            {
                await conn.OpenAsync();
                var IsExists = (await conn.GetSchemaAsync("Tables", new string[4] { null, null, ConstBiomertricMSAccessClientDb.CreateStatusTableOnClient, "TABLE" })).Rows.Count > 0;
                if (!IsExists)
                {
                    using (var cmdCreateTable = new OleDbCommand(ConstBiomertricMSAccessClientDb.CreateStatusTableQuery, conn))
                    {
                        await cmdCreateTable.ExecuteNonQueryAsync();
                    }
                }
                await conn.CloseAsync();
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - CreateLogTableOnClientDb() FOR DOMAIN : {domainCode} - ", ex);
            return false;
        }
    }

    private void MaintainTempTable(BiometricEntity model, string domainCode, ICollection<BiometricSettingEntity> clientDbTableColumns, string selectQuery)
    {
        try
        {
            var result = TransferBiometricDataToClientTempTable(selectQuery, clientDbTableColumns.Select(x => x.ColumnName).ToList(),
                                                                                    model.ConnectionString, domainCode);
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - MaintainTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
    }

    private int TransferBiometricDataToClientTempTable(string selectQuery, List<string> tableColumns, string connectionString, string domainCode)
    {
        int result = 0;
        try
        {
            tableColumns[0] = ConstBiomertricMSAccessClientDb.ClientEIBIColumn;
            var insertInToQuery = string.Format(ConstBiomertricMSAccessClientDb.InsertInToQuery, ConstBiomertricMSAccessClientDb.CreateTableOnClient, string.Join(",", tableColumns)) + selectQuery;
            using (var conn = new OleDbConnection(connectionString))
            {
                conn.Open();
                using (var oleDbCommond = new OleDbCommand(insertInToQuery, conn))
                {
                    oleDbCommond.CommandTimeout = 6000;
                    oleDbCommond.ExecuteNonQuery();
                    result = 1;
                }
                conn.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - TransferBiometricDataToClientTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
        return result;
    }

    private async Task<DataSet> GetMaintainFailureCount(string logStatusType, bool isPass, string domainCode, OleDbConnection conn)
    {
        DataSet dataSet = new DataSet();
        try
        {
            bool isDataExistsInTransferStatusTable = false;
            var isDataExistsQuery = ConstBiomertricMSAccessClientDb.TransferStatusTableCheckHavingData;
            using (var command = new OleDbCommand(isDataExistsQuery, conn))
            {
                isDataExistsInTransferStatusTable = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            }
            if (logStatusType == ConstBiometric.LogStatusSelect)
            {
                var selectQuery = MsAccessBiometricCreateQuery.OleDbUpdateLogStatus(logStatusType, isPass, isDataExistsInTransferStatusTable);
                using (var dataAdapter = new OleDbDataAdapter(selectQuery.ToString(), conn))
                {
                    dataAdapter.Fill(dataSet);
                }
            }
            if (logStatusType == ConstBiometric.LogStatusExecute)
            {
                // insert/update data int SERVER_CLIENT_BIOMETRIC_TRANSFER_STATUS
                var selectQuery = MsAccessBiometricCreateQuery.OleDbUpdateLogStatus(logStatusType, isPass, isDataExistsInTransferStatusTable);
                using (var cmd = new OleDbCommand(selectQuery, conn))
                {
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetMaintainFailureCount() FOR DOMAIN : {domainCode} - ", ex);
        }
        return dataSet;
    }

    private DataSet GetFromTempTable(BiometricEntity model, string domainCode, IList<BiometricSettingEntity> clientDbTableColumns, OleDbConnection conn, string punchTransferStartDate)
    {
        DataSet dataSet = new DataSet();
        try
        {
            var selectQueryOnlyTemp = MsAccessBiometricCreateQuery.OleDbSelectQuery(model.BiometricTableName, clientDbTableColumns, domainCode, true, punchTransferStartDate);
            using (var dataAdapter = new OleDbDataAdapter(selectQueryOnlyTemp.ToString(), conn))
            {
                dataAdapter.Fill(dataSet);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetFromTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
        return dataSet;
    }

    private void UpdateSyncStatus(OleDbConnection conn, DataSet dataSet, string domainCode)
    {
        try
        {
            string Ids = string.Join(", ", dataSet.Tables[0].Rows.OfType<DataRow>().Select(r => r["ID"].ToString()));
            using (var cmdupdateStatus = new OleDbCommand(MsAccessBiometricCreateQuery.OleDbUpdateSyncStatus(Ids), conn))
            {
                cmdupdateStatus.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - UpdateSyncStatus() FOR DOMAIN : {domainCode} - ", ex);
        }
    }
}
