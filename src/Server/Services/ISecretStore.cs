using System.Threading.Tasks;

namespace Server.Services;

public interface ISecretStore
{
    Task SaveApiKeyAsync(string key);
    Task<string?> GetApiKeyAsync();
    Task DeleteApiKeyAsync();
    Task<bool> HasApiKeyAsync();
}
