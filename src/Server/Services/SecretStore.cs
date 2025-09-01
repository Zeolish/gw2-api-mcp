using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Server.Services;

public class SecretStore : ISecretStore
{
    private readonly string _dbPath;
    private readonly byte[] _key;

    public SecretStore()
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "AppData");
        Directory.CreateDirectory(baseDir);
        _dbPath = Path.Combine(baseDir, "app.db");

        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var keyFile = Path.GetFullPath(Path.Combine(root, "_secrets", "app_key.json"));
        if (!File.Exists(keyFile))
            keyFile = Path.GetFullPath(Path.Combine(root, "..", "_secrets", "app_key.json"));
        if (!File.Exists(keyFile)) throw new FileNotFoundException("Key file not found", keyFile);
        using var doc = JsonDocument.Parse(File.ReadAllText(keyFile));
        var b64 = doc.RootElement.GetProperty("key_base64").GetString();
        if (string.IsNullOrWhiteSpace(b64)) throw new InvalidOperationException("key_base64 missing");
        _key = Convert.FromBase64String(b64);
        if (_key.Length != 32) throw new InvalidOperationException("Key must be 32 bytes");

        using var connection = OpenConnection();
        connection.Execute("CREATE TABLE IF NOT EXISTS Secrets (Name TEXT PRIMARY KEY, Nonce BLOB NOT NULL, Cipher BLOB NOT NULL, Tag BLOB NOT NULL)");
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    public async Task SaveApiKeyAsync(string key)
    {
        using var aes = new AesGcm(_key);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = System.Text.Encoding.UTF8.GetBytes(key);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, plain, cipher, tag);

        using var connection = OpenConnection();
        await connection.ExecuteAsync("REPLACE INTO Secrets(Name, Nonce, Cipher, Tag) VALUES('ApiKey', @nonce, @cipher, @tag)", new { nonce, cipher, tag });
    }

    public async Task<string?> GetApiKeyAsync()
    {
        using var connection = OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(byte[] Nonce, byte[] Cipher, byte[] Tag)>(
            "SELECT Nonce, Cipher, Tag FROM Secrets WHERE Name='ApiKey'");
        if (row == default) return null;
        byte[] nonce = row.Nonce;
        byte[] cipher = row.Cipher;
        byte[] tag = row.Tag;
        using var aes = new AesGcm(_key);
        var plain = new byte[cipher.Length];
        try
        {
            aes.Decrypt(nonce, cipher, tag, plain);
            return System.Text.Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task DeleteApiKeyAsync()
    {
        using var connection = OpenConnection();
        await connection.ExecuteAsync("DELETE FROM Secrets WHERE Name='ApiKey'");
    }

    public async Task<bool> HasApiKeyAsync() => (await GetApiKeyAsync()) != null;
}
