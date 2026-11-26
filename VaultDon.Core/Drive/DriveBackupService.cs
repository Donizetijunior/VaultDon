using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;

namespace VaultDon.Core.Drive;

public class DriveBackupService
{
    private const string BackupFileName = "vaultdon_backup.db";
    private const string AppFolder = "VAULTDON";

    private readonly string _clientJsonPath;

    public DriveBackupService(string clientJsonPath)
    {
        _clientJsonPath = clientJsonPath;
    }

    private async Task<DriveService> GetServiceAsync()
    {
        if (!System.IO.File.Exists(_clientJsonPath))
            throw new FileNotFoundException($"Google client JSON not found at {_clientJsonPath}. Please place your OAuth client config there.");

        var json = await System.IO.File.ReadAllTextAsync(_clientJsonPath);
        var secrets = JsonConvert.DeserializeObject<ClientSecrets>(json);
        if (string.IsNullOrEmpty(secrets?.ClientId))
            secrets = ParseFromInstalledApp(json);

        var credentialPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VAULTDON", "google_tokens");

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            [DriveService.Scope.DriveFile],
            "user",
            CancellationToken.None,
            new FileDataStore(credentialPath, true));

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "VAULTDON"
        });
    }

    private static ClientSecrets ParseFromInstalledApp(string json)
    {
        dynamic obj = JsonConvert.DeserializeObject<dynamic>(json)!;
        string clientId = obj.installed?.client_id ?? obj.web?.client_id;
        string clientSecret = obj.installed?.client_secret ?? obj.web?.client_secret;
        return new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };
    }

    public async Task BackupAsync(string localDbPath)
    {
        var service = await GetServiceAsync();
        var folderId = await GetOrCreateFolderAsync(service);

        var existingId = await FindBackupFileAsync(service, folderId);

        // SQLite keeps an exclusive lock on vault.db while the app runs.
        // Use the SQLite online-backup API to get a consistent copy we can upload.
        var tempPath = Path.Combine(Path.GetTempPath(), $"vaultdon_backup_{Guid.NewGuid():N}.db");
        try
        {
            using (var src = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={localDbPath};Mode=ReadOnly;Pooling=False"))
            using (var dst = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempPath};Pooling=False"))
            {
                src.Open();
                dst.Open();
                src.BackupDatabase(dst);
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            await using var stream = System.IO.File.OpenRead(tempPath);

            if (existingId is not null)
            {
                var updateReq = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingId, stream, "application/octet-stream");
                await updateReq.UploadAsync();
            }
            else
            {
                var meta = new Google.Apis.Drive.v3.Data.File
                {
                    Name = BackupFileName,
                    Parents = [folderId]
                };
                var createReq = service.Files.Create(meta, stream, "application/octet-stream");
                await createReq.UploadAsync();
            }
        }
        finally
        {
            if (System.IO.File.Exists(tempPath))
                System.IO.File.Delete(tempPath);
        }
    }

    public async Task RestoreAsync(string localDbPath)
    {
        var service = await GetServiceAsync();
        var folderId = await GetOrCreateFolderAsync(service);
        var fileId = await FindBackupFileAsync(service, folderId)
            ?? throw new Exception("No backup found on Google Drive.");

        await using var stream = System.IO.File.Create(localDbPath);
        await service.Files.Get(fileId).DownloadAsync(stream);
    }

    private async Task<string> GetOrCreateFolderAsync(DriveService service)
    {
        var listReq = service.Files.List();
        listReq.Q = $"name='{AppFolder}' and mimeType='application/vnd.google-apps.folder' and trashed=false";
        listReq.Fields = "files(id)";
        var result = await listReq.ExecuteAsync();
        if (result.Files.Count > 0) return result.Files[0].Id;

        var folder = new Google.Apis.Drive.v3.Data.File
        {
            Name = AppFolder,
            MimeType = "application/vnd.google-apps.folder"
        };
        var created = await service.Files.Create(folder).ExecuteAsync();
        return created.Id;
    }

    private async Task<string?> FindBackupFileAsync(DriveService service, string folderId)
    {
        var listReq = service.Files.List();
        listReq.Q = $"name='{BackupFileName}' and '{folderId}' in parents and trashed=false";
        listReq.Fields = "files(id)";
        var result = await listReq.ExecuteAsync();
        return result.Files.Count > 0 ? result.Files[0].Id : null;
    }
}
