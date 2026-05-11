using HROne.BiometricAgentModel;

namespace HROne.BiometricAgentService.Biz;

public interface IBiometricMailService
{
    Task SendPushBiometricDataFailureMail(BiometricMailEntity mailEntity);
}
