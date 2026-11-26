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

        await using var stream = System.IO.File.OpenRead(localDbPath);

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
