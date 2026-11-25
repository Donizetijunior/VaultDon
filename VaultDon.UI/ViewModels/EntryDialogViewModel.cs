using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VaultDon.Core.Models;

namespace VaultDon.UI.ViewModels;

public partial class EntryDialogViewModel : ObservableObject
{
    [ObservableProperty] private string company = string.Empty;
    [ObservableProperty] private string label = string.Empty;
    [ObservableProperty] private string username = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private bool showPassword;

    public int EditId { get; private set; }
    public bool IsEdit => EditId > 0;
    public string Title => IsEdit ? "Edit Entry" : "Add Entry";

    public Action<bool>? CloseDialog { get; set; }

    public void LoadEntry(VaultEntry entry)
    {
        EditId = entry.Id;
        Company = entry.Company;
        Label = entry.Label;
        Username = entry.Username;
        Password = entry.Password;
        Url = entry.Url ?? string.Empty;
        Notes = entry.Notes ?? string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Company) || string.IsNullOrWhiteSpace(Label) ||
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            return;
        CloseDialog?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseDialog?.Invoke(false);

    [RelayCommand]
    private void TogglePassword() => ShowPassword = !ShowPassword;

    public VaultEntry ToEntry() => new()
    {
        Id = EditId,
        Company = Company.Trim(),
        Label = Label.Trim(),
        Username = Username.Trim(),
        Password = Password,
        Url = string.IsNullOrWhiteSpace(Url) ? null : Url.Trim(),
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        CreatedAt = DateTime.UtcNow
    };
}
