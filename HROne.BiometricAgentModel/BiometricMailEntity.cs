namespace HROne.BiometricAgentModel;

public class BiometricMailEntity
{
    public bool IsMailSend { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ReceiverEmail { get; set; } = string.Empty;
    public bool IsEmailBlocked { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string MailHeader { get; set; } = string.Empty;
}
