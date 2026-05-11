namespace HROne.BiometricAgentService.Biz;

public interface IPushBiometricDbFactory
{
    IBiometricService GetBiometricServiceType(int serverType);
}
