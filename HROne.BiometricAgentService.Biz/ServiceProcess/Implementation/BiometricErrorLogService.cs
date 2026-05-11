using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using Newtonsoft.Json;
using System.Text;

namespace HROne.BiometricAgentService.Biz;

public class BiometricErrorLogService(IAppLoggerFactory _loggerFactory, AppSettings _appSettings,
                                  IHttpClientFactory _httpClientFactory) : IBiometricErrorLogService
{

    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<BiometricErrorLogService>();
    public async Task BiometricSettingValidationLog(BiometricServiceLogEntity model)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }
        try
        {
            var json = JsonConvert.SerializeObject(model, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_appSettings.ApiServicePath);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.HroneApiKey, _appSettings.HROneApiKey);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.DomainCode, _appSettings.DomainCode);
            await client.PostAsync(ConstBiometricApi.ApiBiometricServiceLog, content);
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - BiometricSettingValidationLog() FOR DOMAIN : [{model.DomainCode}] error in(catch) - ", ex);
        }
    }
}
