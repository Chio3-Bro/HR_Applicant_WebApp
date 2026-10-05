using Amazon.S3.Model;

namespace MyHRExample.Service;

public interface IAnalysisStorage
{
    Task<List<S3Object>> GetFilesAsync(string? prefix = null);
    Task<string?> ReadResultAsync(string key);
}
