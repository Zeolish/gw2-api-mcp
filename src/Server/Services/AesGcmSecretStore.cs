using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Server.Services;

public class AesGcmSecretStore : ISecretStore
{
    private const string SecretName = "GW2_API_KEY";
    private readonly string _connectionString;
    private readonly byte[] _key;

    public AesGcmSecretStore()
    {
        var baseDir = AppContext.BaseDirectory;
        var appData = Path.Combine(baseDir, "AppData");
        Directory.CreateDirectory(appData);
        _connectionString = $"Data Source={Path.Combine(appData, "app.db")}";

        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var keyPath = Path.Combine(repoRoot, "_secrets", "app_key.json");
        if (!File.Exists(keyPath))
        {
            keyPath = Path.GetFullPath(Path.Combine(repoRoot, "..", "_secrets", "app_key.json"));
        }
        if (!File.Exists(keyPath))
        {
            throw new InvalidOperationException($"Secret key file not found at {keyPath}. Create ./_secrets/app_key.json with {{\"key_base64\":\"<base64 32 bytes>\"}}");
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(keyPath));
        var b64 = doc.RootElement.GetProperty("key_base64").GetString();
        if (string.IsNullOrWhiteSpace(b64))
            throw new InvalidOperationException("key_base64 missing in secrets file");

        _key = Convert.FromBase64String(b64);
        if (_key.Length != 32)
            throw new InvalidOperationException("Secret key must be 32 bytes");

        using var conn = OpenConnection();
        conn.Execute(@"CREATE TABLE IF NOT EXISTS Secrets (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT UNIQUE NOT NULL,
            value BLOB NOT NULL,
            createdUtc TEXT NOT NULL
        );");
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public async Task SaveApiKeyAsync(string key)
    {
        using var aes = new AesGcm(_key);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(key);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, plain, cipher, tag);

        var value = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, value, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, value, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, value, nonce.Length + cipher.Length, tag.Length);

        using var conn = OpenConnection();
        await conn.ExecuteAsync(@"INSERT INTO Secrets(name,value,createdUtc)
            VALUES(@name,@value,@createdUtc)
            ON CONFLICT(name) DO UPDATE SET value=excluded.value, createdUtc=excluded.createdUtc;",
            new { name = SecretName, value, createdUtc = DateTime.UtcNow.ToString("o") });
    }

    public async Task<string?> GetApiKeyAsync()
    {
        using var conn = OpenConnection();
        var value = await conn.QuerySingleOrDefaultAsync<byte[]>(
            "SELECT value FROM Secrets WHERE name=@name", new { name = SecretName });
        if (value == null) return null;

        var nonce = value.AsSpan(0, 12).ToArray();
        var tag = value.AsSpan(value.Length - 16, 16).ToArray();
        var cipher = value.AsSpan(12, value.Length - 12 - 16).ToArray();

        using var aes = new AesGcm(_key);
        var plain = new byte[cipher.Length];
        try
        {
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public async Task DeleteApiKeyAsync()
    {
        using var conn = OpenConnection();
        await conn.ExecuteAsync("DELETE FROM Secrets WHERE name=@name", new { name = SecretName });
    }

    public async Task<bool> HasApiKeyAsync()
    {
        using var conn = OpenConnection();
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Secrets WHERE name=@name", new { name = SecretName });
        return count > 0;
    }
}

public class MissingApiKeyException : Exception
{
    public MissingApiKeyException() : base("Guild Wars 2 API key not configured") { }
}

