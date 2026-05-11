using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using Newtonsoft.Json;

namespace HROne.BiometricAgentService.Biz;

public class BiometricDataService(IAppLoggerFactory _loggerFactory, AppSettings _appSettings,
                                  IHttpClientFactory _httpClientFactory, IPushBiometricDbFactory _pushBiometricDbFactory) : IBiometricDataService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<BiometricDataService>();

    public async Task BiometricProcess(string serverSettingFilePath)
    {
        try
        {
            _logger.Info($"Method - BiometricProcess() Start...");

            var domainData = new DomainDetailsEnitity
            {
                CompnayDomainCode = _appSettings.DomainCode,
                HROneApiKey = _appSettings.HROneApiKey,
                ServerSettingFilePath = serverSettingFilePath
            };

            if (!string.IsNullOrEmpty(domainData.HROneApiKey))
            {
                var biometricSettings = await GetClientServerSettingInfo(domainData);

                var serverSet = new HashSet<string>((_appSettings.OnPremisesBiometricServerNames ?? string.Empty)
                                    .Split('~', StringSplitOptions.RemoveEmptyEntries));

                if (biometricSettings == null || biometricSettings.ServerSettings.Count <= 0 || serverSet.Count == 0)
                    return;

                var machedServers = biometricSettings.ServerSettings.Where(x => serverSet.Contains(x.ServerName)).ToList();
                if (!machedServers.Any())
                {
                    _logger.Error($"Method - BiometricProcess() - Server name not found.");
                    return;
                }
                foreach (var biometricSetting in machedServers)
                {
                    try
                    {
                        var biometricProcessBiz = _pushBiometricDbFactory.GetBiometricServiceType(biometricSetting.ServerType);
                        await biometricProcessBiz.BiometricProcess(biometricSetting, domainData.CompnayDomainCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Method - BiometricProcess() FOR Domain : {domainData.CompnayDomainCode} - ", ex);
                    }
                }
            }
            else
            {
                _logger.Info($"Method- BiometricProcess() End. HROne Api key not register for [{domainData.CompnayDomainCode}]");
            }
            _logger.Info($"Method- BiometricProcess() END.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Method- BiometricProcess() - ", ex);
        }
    }

    private async Task<BiometricServerSetting?> GetClientServerSettingInfo(DomainDetailsEnitity domainData)
    {
        DateTime serverSettingTime = await GetClientServerSettingTime(domainData.CompnayDomainCode);
        
        // When API returned a time, check if cached file is still valid
        var (isValid, cachedSettings) = await TryGetValidCachedSettings(domainData.ServerSettingFilePath, serverSettingTime);
        if (isValid && cachedSettings != null)
        {
            return cachedSettings;
        }
        // Cache missing or invalid: fetch from API and persist
        var biometricSettings = await OnPremisesClientBiometricSettings(domainData.CompnayDomainCode);
        if (biometricSettings != null)
        {
            await WriteServerSettingToJsonFile(biometricSettings, domainData.ServerSettingFilePath);
        }
        return biometricSettings;
    }

    private async Task<DateTime> GetClientServerSettingTime(string domainCode)
    {
        if (string.IsNullOrEmpty(domainCode))
        {
            throw new ArgumentNullException(nameof(domainCode));
        }
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_appSettings.ApiServicePath);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.HroneApiKey, _appSettings.HROneApiKey);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.DomainCode, _appSettings.DomainCode);
            var response = await client.GetAsync(string.Format(ConstBiometricApi.ApiBiometricSettingTime, domainCode));
            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"Method - GetClientServerSettingTime() - Api calling failed. Status : {response.StatusCode}, Message : {result}");
                return DateTime.MinValue;
            }

            return !string.IsNullOrEmpty(result) ? JsonConvert.DeserializeObject<DateTime>(result) : DateTime.MinValue;
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - GetClientServerSettingTime()  FOR Domain : [{domainCode}] error in(catch) - ", ex);
        }
        return DateTime.MinValue;
    }

    private async Task<BiometricServerSetting?> OnPremisesClientBiometricSettings(string domainCode)
    {
        if (string.IsNullOrEmpty(domainCode))
        {
            throw new ArgumentNullException(nameof(domainCode));
        }
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_appSettings.ApiServicePath);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.HroneApiKey, _appSettings.HROneApiKey);
            client.DefaultRequestHeaders.Add(ConstBiometricApi.DomainCode, _appSettings.DomainCode);
            var response = await client.GetAsync(string.Format(ConstBiometricApi.ApiBiometricSetting, domainCode));
            var result = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"Method - OnPremisesClientBiometricSettings() - Api calling failed. Status : {response.StatusCode}, Message : {result}");
                return null;
            }

            return !string.IsNullOrEmpty(result) ? JsonConvert.DeserializeObject<BiometricServerSetting>(result) : null;
        }
        catch (Exception ex)
        {
            _logger.Error($"Method - OnPremisesClientBiometricSettings()  For Domain : [{domainCode}] error in(catch) - ", ex);
        }
        return null;
    }

    private static async Task WriteServerSettingToJsonFile(BiometricServerSetting entity, string filePath)
    {
        var jsonString = JsonConvert.SerializeObject(entity, new JsonSerializerSettings
        {
            Formatting = Formatting.Indented
        });
        await File.WriteAllTextAsync(filePath, jsonString);
    }

    private static async Task<(bool isValid, BiometricServerSetting? settings)> TryGetValidCachedSettings(string filePath, DateTime serverSettingTime)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var result = JsonConvert.DeserializeObject<BiometricServerSetting>(json);

            if (result?.ServerSettingTime != null && result?.ServerSettingTime == serverSettingTime)
            {
                return (true, result);
            }
        }
        catch
        {
            // File missing or invalid JSON
        }
        return (false, null);
    }
}
