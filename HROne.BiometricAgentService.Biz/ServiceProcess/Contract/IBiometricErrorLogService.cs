using HROne.BiometricAgentModel;

namespace HROne.BiometricAgentService.Biz;

public interface IBiometricErrorLogService
{
    Task BiometricSettingValidationLog(BiometricServiceLogEntity model);
}
