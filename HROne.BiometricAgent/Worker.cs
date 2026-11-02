using HROne.BiometricAgent.Common;
using HROne.BiometricAgentService.Biz;

namespace HROne.BiometricAgent
{
    public class Worker(IServiceScopeFactory _scopeFactory, IAppLoggerFactory _loggerFactory,
                        IHostEnvironment _env, IHostApplicationLifetime _hostLifetime) : BackgroundService
    {
        private readonly IAppLogger _logger = _loggerFactory.CreateLogger<Worker>();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.Info($"Worker running...");

                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var biometricService = scope.ServiceProvider.GetRequiredService<IBiometricDataService>();
                    var serverSettingFilePath = Path.Combine(_env.ContentRootPath, "biometricServerInfoSetting.json");
                    await biometricService.BiometricProcess(serverSettingFilePath);

                    // Stop the application after work is done
                    _hostLifetime.StopApplication();

                    await Task.Delay(TimeSpan.FromSeconds(5)); // Delay for 10 seconds before the next iteration
                }
                catch (Exception ex)
                {
                    _logger.Error($"Method - ExecuteAsync() \n", ex);
                }
            }
            _logger.Info($"Worker stop.");
        }
    }
}
