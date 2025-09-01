using System.IO;
using System.Threading.Tasks;
using Server.Services;
using Xunit;

public class SecretStoreTests
{
    [Fact]
    public async Task RoundTripEncryptsAndDecrypts()
    {
        var store = new SecretStore();
        await store.SaveApiKeyAsync("abc123");
        Assert.True(await store.HasApiKeyAsync());
        var key = await store.GetApiKeyAsync();
        Assert.Equal("abc123", key);
        await store.DeleteApiKeyAsync();
        Assert.False(await store.HasApiKeyAsync());
    }

    [Fact]
    public void ThrowsWhenKeyFileMissing()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var keyFile = Path.GetFullPath(Path.Combine(root, "_secrets", "app_key.json"));
        if (!File.Exists(keyFile))
            keyFile = Path.GetFullPath(Path.Combine(root, "..", "_secrets", "app_key.json"));
        var backup = keyFile + ".bak";
        File.Move(keyFile, backup);
        try
        {
            Assert.Throws<FileNotFoundException>(() => new SecretStore());
        }
        finally
        {
            File.Move(backup, keyFile);
        }
    }
}
