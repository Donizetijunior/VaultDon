using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultDon.Core.Drive;
using VaultDon.Core.Models;
using VaultDon.Core.Storage;

namespace VaultDon.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly VaultDatabase _db;
    private readonly DriveBackupService _drive;
    private List<VaultEntry> _allEntries = [];

    [ObservableProperty] private ObservableCollection<string> companies = [];
    [ObservableProperty] private string? selectedCompany;
    [ObservableProperty] private ObservableCollection<VaultEntry> filteredEntries = [];
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;

    public MainViewModel(VaultDatabase db, DriveBackupService drive)
    {
        _db = db;
        _drive = drive;
        LoadEntries();
    }

    private void LoadEntries()
    {
        _allEntries = _db.GetAll();
        RefreshCompanies();
        ApplyFilter();
    }

    private void RefreshCompanies()
    {
        var companies = _allEntries.Select(e => e.Company).Distinct().OrderBy(c => c).ToList();
        Companies = new ObservableCollection<string>(companies);
    }

    partial void OnSelectedCompanyChanged(string? value) => ApplyFilter();
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = _allEntries.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SelectedCompany))
            query = query.Where(e => e.Company == SelectedCompany);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var lower = SearchText.ToLower();
            query = query.Where(e =>
                e.Label.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                e.Username.Contains(lower, StringComparison.OrdinalIgnoreCase) ||
                (e.Url?.Contains(lower, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        FilteredEntries = new ObservableCollection<VaultEntry>(query);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SelectedCompany = null;
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void CopyUsername(VaultEntry entry)
    {
        CopyToClipboard(entry.Username, "Username copied");
    }

    [RelayCommand]
    private void CopyPassword(VaultEntry entry)
    {
        CopyToClipboard(entry.Password, "Password copied — clears in 30s");
    }

    private System.Threading.Timer? _clearTimer;

    private void CopyToClipboard(string text, string message)
    {
        Clipboard.SetText(text);
        StatusMessage = message;
        _clearTimer?.Dispose();
        _clearTimer = new System.Threading.Timer(_ =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try { if (Clipboard.GetText() == text) Clipboard.Clear(); } catch { }
                StatusMessage = string.Empty;
            });
        }, null, 30_000, System.Threading.Timeout.Infinite);
    }

    [RelayCommand]
    private void AddEntry()
    {
        var dialog = new Views.EntryDialog();
        if (dialog.ShowDialog() == true)
        {
            _db.Add(dialog.Result!);
            LoadEntries();
        }
    }

    [RelayCommand]
    private void EditEntry(VaultEntry entry)
    {
        var dialog = new Views.EntryDialog(entry);
        if (dialog.ShowDialog() == true)
        {
            dialog.Result!.Id = entry.Id;
            dialog.Result.CreatedAt = entry.CreatedAt;
            _db.Update(dialog.Result);
            LoadEntries();
        }
    }

    [RelayCommand]
    private void DeleteEntry(VaultEntry entry)
    {
        var result = MessageBox.Show(
            $"Delete entry \"{entry.Label}\"?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _db.Delete(entry.Id);
            LoadEntries();
        }
    }

    [RelayCommand]
    private async Task BackupToDrive()
    {
        StatusMessage = "Backing up to Google Drive...";
        try
        {
            await _drive.BackupAsync(_db.DbPath);
            StatusMessage = "Backup complete!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Backup failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreFromDrive()
    {
        var confirm = MessageBox.Show(
            "This will overwrite your local vault with the Drive backup. Continue?",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        StatusMessage = "Restoring from Google Drive...";
        try
        {
            await _drive.RestoreAsync(_db.DbPath);
            LoadEntries();
            StatusMessage = "Restore complete! Restart the app to re-unlock with your master password.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }
    }
}
