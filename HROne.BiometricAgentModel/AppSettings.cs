using System.ComponentModel.DataAnnotations;

namespace HROne.BiometricAgentModel;

public class AppSettings
{
    public string DomainCode { get; set; } = string.Empty;
    public string HROneApiKey { get; set; } = string.Empty;
    public string ServiceFailToEmailId { get; set; } = string.Empty;
    public string AuthorizationServerPath { get; set; } = string.Empty;
    public string ApiServicePath { get; set; } = string.Empty;

    [Required(ErrorMessage = "OnPremisesBiometricServerNames is required")]
    [RegularExpression(@"^[a-zA-Z0-9\-]+\\[a-zA-Z0-9]+$", ErrorMessage = "OnPremisesBiometricServerNames must be in format: SERVER\\INSTANCE")]
    public string OnPremisesBiometricServerNames { get; set; } = string.Empty;
    public int TimeIntervalInSeconds { get; set; }
    public string PunchTransferStartDate { get; set; } = string.Empty;
    public bool AllowWriteFullLog { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class ClientAppSettings
{
    [Required(ErrorMessage = "OnPremisesBiometricServerNames is required")]
    [RegularExpression(@"^[a-zA-Z0-9\-]+\\[a-zA-Z0-9]+$", ErrorMessage = "OnPremisesBiometricServerNames must be in format: SERVER\\INSTANCE")]
    public string OnPremisesBiometricServerNames { get; set; } = string.Empty;
}
