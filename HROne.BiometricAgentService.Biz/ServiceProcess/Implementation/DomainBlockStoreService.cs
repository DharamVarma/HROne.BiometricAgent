using Newtonsoft.Json;

namespace HROne.BiometricAgentService.Biz;

public static class DomainBlockStoreService
{
    private static readonly object _lock = new object();
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "domain-block.json");

    public static void Block(string domainCode, int blockDuration)
    {
        lock (_lock)
        {
            var data = Load();
            data[domainCode.ToLowerInvariant()] = DateTime.UtcNow.Add(TimeSpan.FromHours(blockDuration));
            Save(data);
        }
    }

    public static bool IsBlocked(string domainCode, out DateTime blockedUntilUtc)
    {
        lock (_lock)
        {
            var data = Load();
            var key = domainCode.ToLowerInvariant();

            if (data.TryGetValue(key, out blockedUntilUtc))
            {
                if (DateTime.UtcNow < blockedUntilUtc)
                    return true;

                // Block expired: remove it so the next run calls the API again
                data.Remove(key);
                Save(data);
            }

            blockedUntilUtc = DateTime.MinValue;
            return false;
        }
    }

    public static void Unblock(string domainCode)
    {
        lock (_lock)
        {
            var data = Load();
            if (data.Remove(domainCode.ToLowerInvariant()))
                Save(data);
        }
    }

    private static Dictionary<string, DateTime> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new Dictionary<string, DateTime>();

            return JsonConvert.DeserializeObject<Dictionary<string, DateTime>>(File.ReadAllText(FilePath))
                   ?? new Dictionary<string, DateTime>();
        }
        catch
        {
            // Corrupt or unreadable file: treat as not blocked so the job is never stuck
            return new Dictionary<string, DateTime>();
        }
    }

    private static void Save(Dictionary<string, DateTime> data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(data));
        if (File.Exists(FilePath))
            File.Replace(tempPath, FilePath, null);
        else
            File.Move(tempPath, FilePath);
    }
}
