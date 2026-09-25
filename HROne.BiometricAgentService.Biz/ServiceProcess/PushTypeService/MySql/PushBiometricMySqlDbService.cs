using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using MySqlConnector;
using System.Data;
using System.Globalization;

namespace HROne.BiometricAgentService.Biz;

public class PushBiometricMySqlDbService(AppSettings _settings, IAppLoggerFactory _loggerFactory,
                                        IBiometricErrorLogService _biometricErrorLogService,
                                        IBiometricMailService _biometricMailService,
                                        ITransferBiometricBulkDataService _transferBiometricData) : IBiometricService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<PushBiometricMySqlDbService>();

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
                    var selectQuery = MySqlBiometricCreateQuery.MySqlDbSelectQuery(model.BiometricTableName, clientDbTableColumns, domainCode, false, _settings.PunchTransferStartDate);
                    if (!string.IsNullOrEmpty(selectQuery))
                    {
                        try
                        {
                            await CreateLogTableOnClientDb(model.ConnectionString, domainCode);
                            int transferDataCount = 0;
                            using (var conn = new MySqlConnection(model.ConnectionString))
                            {
                                await conn.OpenAsync();
                                while (true)
                                {
                                    //Maintain data into temp table with sync status 0
                                    await MaintainTempTable(model, domainCode, clientDbTableColumns, selectQuery);

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
                                                        Message = string.Format(ConstBiometricMail.ConsecutiveFailureEmailBody, domainCode, model.ServerName),
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
                                            _logger.Info($"Method - BiometricProcess() FOR DOMAIN  [{domainCode}] [{transferDataCount}] Records Transferred Successfully END.", true);
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
                        _logger.Info($"Method - BiometricProcess() Transfer Data SelectQuery Can not be null FOR DOMAIN : [{domainCode}] END.");
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
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.MySql,
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

            var selectQuery = "SELECT " + string.Join(", ", columns) + " FROM " + clientBiometricTableName + " LIMIT 1;";
            await using var conn = new MySqlConnection(connectionString);
            await conn.OpenAsync();
            using var sqlDataAdapter = new MySqlDataAdapter(selectQuery, conn);
            var dataSet = new DataSet();
            sqlDataAdapter.Fill(dataSet);
            return dataSet.Tables.Count > 0;
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.MySql,
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
            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string checkTableQuery = string.Format(ConstBiomertricMySqlClientDb.CheckTableExistsQuery, ConstBiomertricMySqlClientDb.MySqlCreateTableOnClient);
                using (var cmdClient = new MySqlCommand(checkTableQuery, conn))
                {
                    int isExists = Convert.ToInt32(await cmdClient.ExecuteScalarAsync());
                    if (isExists == 0)
                    {
                        using var cmdCreateTable = new MySqlCommand(ConstBiomertricMySqlClientDb.MySqlCreateTableQuery, conn);
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
                ErrorType = ConstBiometricDbType.MySql,
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
            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string checkTableQuery = string.Format(ConstBiomertricMySqlClientDb.CheckTableExistsQuery, ConstBiomertricMySqlClientDb.CreateStatusTableOnClient);
                using (var cmdClient = new MySqlCommand(checkTableQuery, conn))
                {
                    int isExists = Convert.ToInt32(await cmdClient.ExecuteScalarAsync());
                    if (isExists == 0)
                    {
                        using var cmdCreateTabel = new MySqlCommand(ConstBiomertricMySqlClientDb.MySqlCreateStatusTableQuery, conn);
                        await cmdCreateTabel.ExecuteNonQueryAsync();
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

    private async Task MaintainTempTable(BiometricEntity model, string domainCode, IList<BiometricSettingEntity> clientDbTableColumns, string selectQuery)
    {
        try
        {
            var result = await TransferBiometricDataToClientTempTable(selectQuery, clientDbTableColumns.Select(x => x.ColumnName).ToList(),
                                                                                                        model.ConnectionString, domainCode);
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - MaintainTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
    }

    private async Task<int> TransferBiometricDataToClientTempTable(string selectQuery, List<string> tableColumns, string connectionString, string domainCode)
    {
        int result = 0;
        try
        {
            tableColumns[0] = ConstBiomertricMySqlClientDb.ClientEIBIColumn;
            var insertInToQuery = string.Format(ConstBiomertricMySqlClientDb.InsertInToQuery, ConstBiomertricMySqlClientDb.MySqlCreateTableOnClient, string.Join(",", tableColumns)) + selectQuery;
            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var mySqlCommond = new MySqlCommand(insertInToQuery, conn))
                {
                    mySqlCommond.CommandTimeout = 6000;
                    await mySqlCommond.ExecuteNonQueryAsync();
                    result = 1;
                }
                await conn.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - TransferBiometricDataToClientTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
        return result;
    }

    private async Task<DataSet> GetMaintainFailureCount(string logStatusType, bool isPass, string domainCode, MySqlConnection conn)
    {
        DataSet dataSet = new DataSet();
        try
        {
            bool isDataExistsInTransferStatusTable = false;
            var isDataExistsQuery = MySqlBiometricCreateQuery.MySqlTransferStatusTableCheckHavingData;
            using (var command = new MySqlCommand(isDataExistsQuery, conn))
            {
                isDataExistsInTransferStatusTable = Convert.ToBoolean(await command.ExecuteScalarAsync());
            }
            if (logStatusType == ConstBiometric.LogStatusSelect)
            {
                var selectQuery = MySqlBiometricCreateQuery.MySqlDbUpdateLogStatus(logStatusType, isPass, isDataExistsInTransferStatusTable);
                using (var sqlDataAdapter = new MySqlDataAdapter(selectQuery.ToString(), conn))
                {
                    sqlDataAdapter.Fill(dataSet);
                }
            }
            if (logStatusType == ConstBiometric.LogStatusExecute)
            {
                var selectQuery = MySqlBiometricCreateQuery.MySqlDbUpdateLogStatus(logStatusType, isPass, isDataExistsInTransferStatusTable);
                using (var cmdCreateTabel = new MySqlCommand(selectQuery, conn))
                {
                    await cmdCreateTabel.ExecuteNonQueryAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetMaintainFailureCount() FOR DOMAIN : {domainCode} - ", ex);
        }
        return dataSet;
    }

    private DataSet GetFromTempTable(BiometricEntity model, string domainCode, IList<BiometricSettingEntity> clientDbTableColumns, MySqlConnection conn, string punchTransferStartDate)
    {
        DataSet dataSet = new DataSet();
        try
        {
            var selectQueryOnlyTemp = MySqlBiometricCreateQuery.MySqlDbSelectQuery(model.BiometricTableName, clientDbTableColumns, domainCode, true, punchTransferStartDate);
            using (var sqlDataAdapter = new MySqlDataAdapter(selectQueryOnlyTemp.ToString(), conn))
            {
                sqlDataAdapter.Fill(dataSet);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetFromTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
        return dataSet;
    }

    private void UpdateSyncStatus(MySqlConnection conn, DataSet dataSet, string domainCode)
    {
        try
        {
            string Ids = string.Join(", ", dataSet.Tables[0].Rows.OfType<DataRow>().Select(r => r["ID"].ToString()));
            using (var cmdupdateStatus = new MySqlCommand(MySqlBiometricCreateQuery.MySqlDbUpdateSyncStatus(Ids), conn))
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
