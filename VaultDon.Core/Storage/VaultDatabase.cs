using Microsoft.Data.Sqlite;
using VaultDon.Core.Crypto;
using VaultDon.Core.Models;

namespace VaultDon.Core.Storage;

public class VaultDatabase : IDisposable
{
    private readonly SqliteConnection _conn;
    private byte[] _encryptionKey = [];

    public string DbPath { get; }

    public VaultDatabase(string dbPath)
    {
        DbPath = dbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS entries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company TEXT NOT NULL,
                label TEXT NOT NULL,
                username TEXT NOT NULL,
                password TEXT NOT NULL,
                url TEXT,
                notes TEXT,
                created_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public bool IsFirstRun()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = 'password_hash'";
        return cmd.ExecuteScalar() is null;
    }

    public void SetMasterPassword(string password)
    {
        var salt = CryptoService.GenerateSalt();
        var hash = CryptoService.HashPassword(password, salt);
        _encryptionKey = CryptoService.DeriveKey(password, salt);

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO meta (key, value) VALUES ('password_hash', $h);
            INSERT OR REPLACE INTO meta (key, value) VALUES ('salt', $s);
            """;
        cmd.Parameters.AddWithValue("$h", hash);
        cmd.Parameters.AddWithValue("$s", Convert.ToBase64String(salt));
        cmd.ExecuteNonQuery();
    }

    public bool TryUnlock(string password)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM meta WHERE key = 'salt'";
        var saltB64 = cmd.ExecuteScalar() as string;
        if (saltB64 is null) return false;
        var salt = Convert.FromBase64String(saltB64);

        cmd.CommandText = "SELECT value FROM meta WHERE key = 'password_hash'";
        var storedHash = cmd.ExecuteScalar() as string;
        if (storedHash is null) return false;

        if (!CryptoService.VerifyPassword(password, salt, storedHash)) return false;
        _encryptionKey = CryptoService.DeriveKey(password, salt);
        return true;
    }

    public List<VaultEntry> GetAll()
    {
        var list = new List<VaultEntry>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id, company, label, username, password, url, notes, created_at FROM entries ORDER BY company, label";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new VaultEntry
            {
                Id = reader.GetInt32(0),
                Company = reader.GetString(1),
                Label = reader.GetString(2),
                Username = Decrypt(reader.GetString(3)),
                Password = Decrypt(reader.GetString(4)),
                Url = reader.IsDBNull(5) ? null : reader.GetString(5),
                Notes = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = DateTime.Parse(reader.GetString(7))
            });
        }
        return list;
    }

    public void Add(VaultEntry entry)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO entries (company, label, username, password, url, notes, created_at)
            VALUES ($co, $la, $un, $pw, $ur, $no, $ca)
            """;
        cmd.Parameters.AddWithValue("$co", entry.Company);
        cmd.Parameters.AddWithValue("$la", entry.Label);
        cmd.Parameters.AddWithValue("$un", Encrypt(entry.Username));
        cmd.Parameters.AddWithValue("$pw", Encrypt(entry.Password));
        cmd.Parameters.AddWithValue("$ur", (object?)entry.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$no", (object?)entry.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ca", entry.CreatedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void Update(VaultEntry entry)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            UPDATE entries SET company=$co, label=$la, username=$un, password=$pw, url=$ur, notes=$no
            WHERE id=$id
            """;
        cmd.Parameters.AddWithValue("$co", entry.Company);
        cmd.Parameters.AddWithValue("$la", entry.Label);
        cmd.Parameters.AddWithValue("$un", Encrypt(entry.Username));
        cmd.Parameters.AddWithValue("$pw", Encrypt(entry.Password));
        cmd.Parameters.AddWithValue("$ur", (object?)entry.Url ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$no", (object?)entry.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$id", entry.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "DELETE FROM entries WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    private string Encrypt(string plain) => CryptoService.EncryptString(plain, _encryptionKey);
    private string Decrypt(string cipher) => CryptoService.DecryptString(cipher, _encryptionKey);

    public void Dispose() => _conn.Dispose();
}
