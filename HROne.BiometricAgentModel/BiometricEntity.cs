using System.ComponentModel.DataAnnotations;

namespace HROne.BiometricAgentModel;

public class BiometricEntity
{
    public long BiometricSettingId { get; set; }
    public string BiometricName { get; set; } = string.Empty;
    public int ServerType { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string BiometricTableName { get; set; } = string.Empty;
    public int BiometricFrequency { get; set; }
    public string BiometricServerTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ServiceType { get; set; } = string.Empty;
}

public class BiometricSettingEntity
{
    public long BiometricSettingId { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string BiometricColumnName { get; set; } = string.Empty;
}

public class BiometricViewModel : BiometricEntity
{
    public IList<BiometricSettingEntity> BiometricSettingInfo { get; set; } = [];
}

public class BiometricServerSetting
{
    public DateTime? ServerSettingTime { get; set; }

    public IList<BiometricViewModel> ServerSettings { get; set; } = [];
}

public class TransferBiometricDataToServerEntity : BaseEntityModel
{
    [Required]
    public List<Dictionary<string, object>>? Data { get; set; } = new List<Dictionary<string, object>>();

    [Required]
    public IList<string> TableColumns { get; set; } = new List<string>();

    [Required]
    public string TableName { get; set; } = string.Empty;
}

public class BiometricServiceLogEntity : BaseEntityModel
{
    [Required]
    public long BiometricSettingId { get; set; }
    [Required]
    public string ErrorType { get; set; } = string.Empty;
    public string ErrorReason { get; set; } = string.Empty;
    [Required]
    public string ErrorSubject { get; set; } = string.Empty;
}
