using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Globalization;

namespace HROne.BiometricAgentService.Biz;

public class PushBiometricSqlDbService(AppSettings _settings, IAppLoggerFactory _loggerFactory,
                                       IBiometricErrorLogService _biometricErrorLogService,
                                       IBiometricMailService _biometricMailService,
                                       ITransferBiometricBulkDataService _transferBiometricData) : IBiometricService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<PushBiometricSqlDbService>();

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
                    var selectQuery = SqlServerBiometricCreateQuery.SqlDbSelectQuery(model.BiometricTableName, clientDbTableColumns, domainCode, false, _settings.PunchTransferStartDate);
                    if (!string.IsNullOrEmpty(selectQuery))
                    {
                        try
                        {
                            await CreateLogTableOnClientDb(model.ConnectionString, domainCode);
                            int transferDataCount = 0;
                            using (var conn = new SqlConnection(model.ConnectionString))
                            {
                                await conn.OpenAsync();
                                while (true)
                                {
                                    //Maintain data into temp table with sync status 0
                                    MaintainTempTable(model, domainCode, clientDbTableColumns, selectQuery, conn);

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

                                                _logger.Info($"Method - BiometricProcess() FOR DOMAIN  [{domainCode}] [{transferDataCount}] Records Transferred Successfully END.", true);
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
                        _logger.Info($"Method  - BiometricProcess() Transfer Data SelectQuery Can not be null FOR DOMAIN : [{domainCode}].");
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
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.SqlServer,
                ErrorReason = ex.Message,
                ErrorSubject = ConstBiometricMail.ConnectionError,
                DomainCode = domainCode,
                LogOnUserId = 0
            };
            _logger.Error($"Method- TestConnectionString() FOR DOMAIN : {domainCode} - ", ex);
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

            var selectQuery = "SELECT TOP(1) " + string.Join(" , ", columns) + " FROM " + clientBiometricTableName;
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();
            using var sqlDataAdapter = new SqlDataAdapter(selectQuery, conn);
            var dataSet = new DataSet();
            sqlDataAdapter.Fill(dataSet);
            return dataSet.Tables.Count > 0;
        }
        catch (Exception ex)
        {
            var errorEntity = new BiometricServiceLogEntity
            {
                BiometricSettingId = biometricSettingId,
                ErrorType = ConstBiometricDbType.SqlServer,
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
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string checkTableQuery = string.Format(ConstBiomertricSqlServerClientDb.CheckTableExistsQuery, ConstBiomertricSqlServerClientDb.SqlCreateTableOnClient);
                using (var cmdClient = new SqlCommand(checkTableQuery, conn))
                {
                    int isExists = Convert.ToInt32(await cmdClient.ExecuteScalarAsync());
                    if (isExists == 0)
                    {
                        using var cmdCreateTable = new SqlCommand(ConstBiomertricSqlServerClientDb.SqlCreateTableQuery, conn);
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
                ErrorType = ConstBiometricDbType.SqlServer,
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
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string checkTableQuery = string.Format(ConstBiomertricSqlServerClientDb.CheckTableExistsQuery, ConstBiomertricSqlServerClientDb.CreateStatusTableOnClient);
                using (var cmdClient = new SqlCommand(checkTableQuery, conn))
                {
                    int isExists = Convert.ToInt32(await cmdClient.ExecuteScalarAsync());
                    if (isExists == 0)
                    {
                        using var cmdCreateTabel = new SqlCommand(ConstBiomertricSqlServerClientDb.SqlCreateStatusTableQuery, conn);
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

    private void MaintainTempTable(BiometricEntity model, string domainCode, ICollection<BiometricSettingEntity> clientDbTableColumns, string selectQuery, SqlConnection conn)
    {
        try
        {
            DataSet dataSetForTempTable = new DataSet();
            using (var sqlDataAdapter = new SqlDataAdapter(selectQuery.ToString(), conn))
            {
                sqlDataAdapter.SelectCommand.CommandTimeout = 6000;
                sqlDataAdapter.Fill(dataSetForTempTable);
            }
            if (dataSetForTempTable.Tables.Count > 0 && dataSetForTempTable.Tables[0].Rows.Count > 0)
            {
                var insertInToClientServer = TransferBiometricDataToClientTempTable(dataSetForTempTable.Tables[0],
                                                                                    clientDbTableColumns.Select(x => x.ColumnName).ToList(),
                                                                                    model.ConnectionString,
                                                                                    domainCode);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - MaintainTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
    }

    private int TransferBiometricDataToClientTempTable(DataTable dataTable, List<string> tableColumns, string connectionString, string domainCode)
    {
        if (!dataTable.Columns.Contains(ConstBiometric.IsDataSynced))
        {
            DataColumn newColumn = new DataColumn(ConstBiometric.IsDataSynced, typeof(bool));
            newColumn.DefaultValue = 0;
            dataTable.Columns.Add(newColumn);
        }
        tableColumns.Add(ConstBiometric.IsDataSynced);
        if (!dataTable.Columns.Contains(ConstBiometric.UpdateDateColumn))
        {
            DataColumn newColumn = new DataColumn(ConstBiometric.UpdateDateColumn, typeof(DateTime));
            newColumn.DefaultValue = DateTime.Now;
            dataTable.Columns.Add(newColumn);
        }
        tableColumns.Add(ConstBiometric.UpdateDateColumn);
        tableColumns[0] = ConstBiomertricSqlServerClientDb.ClientEIBIColumn;
        int result = 0;
        try
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var sqlTransaction = conn.BeginTransaction())
                {
                    using (var bulkCopy = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, sqlTransaction))
                    {
                        bulkCopy.BulkCopyTimeout = 6000;

                        for (int i = 0; i < tableColumns.Count; i++)
                        {
                            bulkCopy.ColumnMappings.Add(i, tableColumns[i]);
                        }
                        bulkCopy.DestinationTableName = ConstBiomertricSqlServerClientDb.SqlCreateTableOnClient;
                        try
                        {
                            bulkCopy.WriteToServer(dataTable);
                            sqlTransaction.Commit();
                            result = 1;
                        }
                        catch (SqlException ex)
                        {
                            sqlTransaction.Rollback();
                            _logger.Error($"Method - TransferBiometricDataToClientTempTable() FOR DOMAIN : {domainCode} - ", ex);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - TransferBiometricDataToClientTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
        return result;
    }

    private async Task<DataSet> GetMaintainFailureCount(string logStatusType, bool isPass, string domainCode, SqlConnection conn)
    {
        DataSet dataSet = new DataSet();
        try
        {
            if (logStatusType == ConstBiometric.LogStatusSelect)
            {
                var selectQuery = SqlServerBiometricCreateQuery.SqlDbUpdateLogStatus(logStatusType, isPass);
                using var sqlDataAdapter = new SqlDataAdapter(selectQuery.ToString(), conn);
                sqlDataAdapter.Fill(dataSet);
            }
            if (logStatusType == ConstBiometric.LogStatusExecute)
            {
                var selectQuery = SqlServerBiometricCreateQuery.SqlDbUpdateLogStatus(logStatusType, isPass);
                using var cmdCreateTabel = new SqlCommand(selectQuery, conn);
                await cmdCreateTabel.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetMaintainFailureCount() FOR DOMAIN : {domainCode} - ", ex);
        }
        return dataSet;
    }

    private DataSet GetFromTempTable(BiometricEntity model, string domainCode, IList<BiometricSettingEntity> clientDbTableColumns, SqlConnection conn, string punchTransferStartDate)
    {
        DataSet dataSet = new DataSet();
        try
        {
            var selectQueryOnlyTemp = SqlServerBiometricCreateQuery.SqlDbSelectQuery(model.BiometricTableName, clientDbTableColumns, domainCode, true, punchTransferStartDate);
            using var sqlDataAdapter = new SqlDataAdapter(selectQueryOnlyTemp.ToString(), conn);
            sqlDataAdapter.Fill(dataSet);
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetFromTempTable() FOR DOMAIN : {domainCode} - ", ex);
        }
        return dataSet;
    }

    private void UpdateSyncStatus(SqlConnection conn, DataSet dataSet, string domainCode)
    {
        try
        {
            string Ids = string.Join(", ", dataSet.Tables[0].Rows.OfType<DataRow>().Select(r => r["ID"].ToString()));
            using (var cmdupdateStatus = new SqlCommand(SqlServerBiometricCreateQuery.SqlDbUpdateSyncStatus(Ids), conn))
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
