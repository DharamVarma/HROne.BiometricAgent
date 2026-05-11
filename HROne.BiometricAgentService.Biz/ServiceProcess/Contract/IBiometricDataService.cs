namespace HROne.BiometricAgentService.Biz;

public interface IBiometricDataService
{
    Task BiometricProcess(string serverSettingFilePath);
}
