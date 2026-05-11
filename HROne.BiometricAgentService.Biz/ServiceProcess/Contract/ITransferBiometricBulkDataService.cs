using System.Data;

namespace HROne.BiometricAgentService.Biz;

public interface ITransferBiometricBulkDataService
{
    Task<int> TransferBiometricBulkData(DataTable dataTable, IList<string> clientDbTableColumns, string domainCode, string biometricServerName);
}
