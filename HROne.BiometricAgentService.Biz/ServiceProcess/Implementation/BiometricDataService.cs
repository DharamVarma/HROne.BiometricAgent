using HROne.BiometricAgent.Common;
using HROne.BiometricAgentModel;
using Newtonsoft.Json;
using System.Net;

namespace HROne.BiometricAgentService.Biz;

public class BiometricDataService(IAppLoggerFactory _loggerFactory, AppSettings _appSettings,
                                  IHttpClientFactory _httpClientFactory, IPushBiometricDbFactory _pushBiometricDbFactory) : IBiometricDataService
{
    private readonly IAppLogger _logger = _loggerFactory.CreateLogger<BiometricDataService>();
    private const string DomainNotFoundPrefix = "Domain connection not found :";

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

            if (DomainBlockStoreService.IsBlocked(domainData.CompnayDomainCode, out var blockedUntilUtc))
            {
                _logger.Info($"Domain : [{domainData.CompnayDomainCode}] is blocked until {blockedUntilUtc.ToLocalTime():yyyy-MM-dd HH:mm}. Skipping Biometric Process.");
                Console.WriteLine($"Domain : [{domainData.CompnayDomainCode}] is blocked until {blockedUntilUtc.ToLocalTime():yyyy-MM-dd HH:mm}. Skipping Biometric Process.");
                return;
            }

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
        try
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
        catch (DomainNotFoundException ex)
        {
            DomainBlockStoreService.Block(ex.DomainCode, _appSettings.ClientBlockedTillHours);
            _logger.Error($"{DomainNotFoundPrefix} {ex.DomainCode}. Blocked the process for 24 hours.");
            return null;
        }
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
            if (response.IsSuccessStatusCode)
            {
                return !string.IsNullOrEmpty(result) ? JsonConvert.DeserializeObject<DateTime>(result) : DateTime.MinValue;
            }
            if (response.StatusCode == HttpStatusCode.InternalServerError && IsDomainNotFound(result, domainCode))
            {
                // Domain is inactive or removed: stop calling the API for this client

                Console.WriteLine($"Domain inactive: {domainCode}. Stopping further calls.");
                throw new DomainNotFoundException(domainCode);
            }
            else
            {
                _logger.Error($"Method - GetClientServerSettingTime() - Api calling failed. Status : {response.StatusCode}, Message : {result}");
                return DateTime.MinValue;
            }
        }
        catch (DomainNotFoundException)
        {
            throw; // Handled by the caller (blocks the domain for 24 hours)
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
            if (response.IsSuccessStatusCode)
            {
                return !string.IsNullOrEmpty(result) ? JsonConvert.DeserializeObject<BiometricServerSetting>(result) : null;
            }
            if (response.StatusCode == HttpStatusCode.InternalServerError && IsDomainNotFound(result, domainCode))
            {
                // Domain is inactive or removed: stop calling the API for this client

                Console.WriteLine($"Domain inactive: {domainCode}. Stopping further calls.");
                throw new DomainNotFoundException(domainCode);
            }
            else
            {
                _logger.Error($"Method - OnPremisesClientBiometricSettings() - Api calling failed. Status : {response.StatusCode}, Message : {result}");
                return null;
            }
        }
        catch (DomainNotFoundException)
        {
            throw; // Handled by the caller (blocks the domain for 24 hours)
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

    private static bool IsDomainNotFound(string body, string domainCode)
    {
        if (string.IsNullOrEmpty(body))
            return false;

        try
        {
            var msg = JsonConvert.DeserializeObject<MessageModel>(body);
            return msg?.Message != null
                && msg.Message.Trim().Equals($"{DomainNotFoundPrefix} {domainCode}", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // Response was not JSON, so it cannot be the domain-not-found message
            return false;
        }
    }

    public class DomainNotFoundException : Exception
    {
        public string DomainCode { get; }
        public DomainNotFoundException(string domainCode)
            : base($"{DomainNotFoundPrefix} {domainCode}")
        {
            DomainCode = domainCode;
        }
    }
}
