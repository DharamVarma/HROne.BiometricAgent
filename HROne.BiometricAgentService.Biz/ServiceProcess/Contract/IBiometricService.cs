using HROne.BiometricAgentModel;

namespace HROne.BiometricAgentService.Biz;

public interface IBiometricService
{
    Task BiometricProcess(BiometricViewModel model, string domainCode);

    Task<bool> TestConnectionString(string connectionString, long biometricSettingId, string domainCode);

    Task<bool> ValidateClientTableAndColumn(string connectionString, string clientBiometricTableName, IList<BiometricSettingEntity> clientDbTableColumns,
                                      long biometricSettingId, string domainCode);

    Task<bool> CreateTableOnClientDb(string connectionString, long biometricSettingId, string domainCode);
}
