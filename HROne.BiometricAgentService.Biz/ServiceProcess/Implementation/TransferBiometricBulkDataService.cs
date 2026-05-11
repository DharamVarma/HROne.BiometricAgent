using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using Newtonsoft.Json;
using System.Data;
using System.Text;

namespace HROne.BiometricAgentService.Biz;

public class TransferBiometricBulkDataService(IAppLoggerFactory _loggerFactory,
                                              AppSettings _appSettings, IHttpClientFactory _httpClientFactory) : ITransferBiometricBulkDataService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<TransferBiometricBulkDataService>();
    public async Task<int> TransferBiometricBulkData(DataTable dataTable, IList<string> tableColumns, string domainCode, string biometricServerName)
    {
        if (dataTable == null || dataTable.Rows.Count == 0)
        {
            _logger.Error($"Method - TransferBiometricBulkData() FOR DOMAIN : [{domainCode}] - DataTable {nameof(dataTable)} is empty.");
            return 0;
        }
        try
        {
            if (!dataTable.Columns.Contains(ConstBiometric.DomainCodeColumn))
            {
                DataColumn newColumn = new DataColumn(ConstBiometric.DomainCodeColumn, typeof(string));
                newColumn.AllowDBNull = true;
                newColumn.DefaultValue = domainCode;
                dataTable.Columns.Add(newColumn);
            }
            if (!dataTable.Columns.Contains(ConstBiometric.BiometricServerName))
            {
                DataColumn newColumn = new DataColumn(ConstBiometric.BiometricServerName, typeof(string));
                newColumn.AllowDBNull = true;
                newColumn.DefaultValue = biometricServerName;
                dataTable.Columns.Add(newColumn);
            }
            tableColumns.Add(ConstBiometric.DomainCodeColumn);
            tableColumns.Add(ConstBiometric.BiometricServerName);
            tableColumns.Add(ConstBiometric.CreateByColumn);
            dataTable.Columns[ConstBiometric.BiometricServerName].SetOrdinal(tableColumns.IndexOf(ConstBiometric.BiometricServerName));

            var columns = dataTable.Columns.Cast<DataColumn>().ToList();
            var data = dataTable.AsEnumerable().Select(row => columns.ToDictionary(col => col.ColumnName, col => row[col])).ToList();

            var model = new TransferBiometricDataToServerEntity
            {
                DomainCode = domainCode,
                Data = data,
                TableColumns = tableColumns,
                TableName = ConstBiometric.ServerBiometricTableName
            };
            var json = JsonConvert.SerializeObject(model, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_appSettings.ApiServicePath);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.HroneApiKey, _appSettings.HROneApiKey);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.DomainCode, _appSettings.DomainCode);
            var response = await client.PostAsync(ConstBiometricApi.ApiTransferBulkBiometricData, content);
            var result = await response.Content.ReadAsStringAsync();
            return !string.IsNullOrEmpty(result) ? JsonConvert.DeserializeObject<int>(result) : 0;
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - TransferBiometricBulkData() FOR DOMAIN : [{domainCode}] error in(catch) - ", ex);
            return 0;
        }
    }
}
