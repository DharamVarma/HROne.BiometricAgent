namespace HROne.BiometricAgentModel;

public class DomainDetailsEnitity
{
    public long CompnayId { get; set; }

    public string CompnayDomainCode { get; set; } = string.Empty;

    public string HROneApiKey { get; set; } = string.Empty;

    public string ServerSettingFilePath { get; set; } = string.Empty;
}
