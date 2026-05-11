using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using Newtonsoft.Json;
using System.Text;

namespace HROne.BiometricAgentService.Biz;

public class BiometricMailService(IAppLoggerFactory _loggerFactory, AppSettings _appSettings,
                                  IHttpClientFactory _httpClientFactory) : IBiometricMailService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<BiometricMailService>();

    public async Task SendPushBiometricDataFailureMail(BiometricMailEntity mailEntity)
    {
        try
        {
            var json = JsonConvert.SerializeObject(mailEntity, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_appSettings.ApiServicePath);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.HroneApiKey, _appSettings.HROneApiKey);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.DomainCode, _appSettings.DomainCode);
            await client.PostAsync(ConstBiometricApi.SendPushBiometricDataFailureMail, content);
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - SendPushBiometricDataFailureMail() FOR DOMAIN : [{mailEntity.Domain}] error in(catch) - ", ex);
        }
    }
}
