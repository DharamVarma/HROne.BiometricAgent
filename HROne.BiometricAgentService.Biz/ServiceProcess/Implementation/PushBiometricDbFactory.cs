using Microsoft.Extensions.DependencyInjection;

namespace HROne.BiometricAgentService.Biz;

public class PushBiometricDbFactory(IServiceProvider _serviceProvider) : IPushBiometricDbFactory
{
    public IBiometricService GetBiometricServiceType(int serverType)
    {
#pragma warning disable CA1416 // Validate platform compatibility
        return serverType switch
        {
            1 => _serviceProvider.GetRequiredService<PushBiometricSqlDbService>(),
            2 => _serviceProvider.GetRequiredService<PushBiometricMsAccessDbService>(),
            3 => _serviceProvider.GetRequiredService<PushBiometricMySqlDbService>(),
            4 => _serviceProvider.GetRequiredService<PushBiometricPostgreSqlDbService>(),
            _ => throw new ArgumentException("Invalid serverType")
        };
#pragma warning restore CA1416 // Validate platform compatibility
    }
}
