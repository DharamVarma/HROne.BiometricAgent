namespace HROne.BiometricAgentModel;

public class BaseEntityModel
{
    public string LogOnUserCode { get; set; } = string.Empty;
    public string DomainCode { get; set; } = string.Empty;
    public int LogOnUserId { get; set; }
    public string LogOnUserName { get; set; } = string.Empty;
    public string CurrentUserLogOnType { get; set; } = string.Empty;
}
