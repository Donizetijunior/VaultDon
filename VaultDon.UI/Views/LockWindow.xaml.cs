using System.IO;
using System.Windows;
using System.Windows.Input;
using VaultDon.Core.Drive;
using VaultDon.Core.Storage;

namespace VaultDon.UI.Views;

public partial class LockWindow : Window
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VAULTDON", "vault.db");

    private static readonly string ClientJsonPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VAULTDON", "google_client.json");

    private VaultDatabase? _db;
    private bool _isFirstRun;

    public LockWindow()
    {
        InitializeComponent();
        _db = new VaultDatabase(DbPath);
        _isFirstRun = _db.IsFirstRun();

        if (_isFirstRun)
        {
            TitleLabel.Text = "Create Master Password";
            SubLabel.Text = "Choose a strong master password";
            UnlockButton.Content = "Create Vault";
            ConfirmBox.Visibility = Visibility.Visible;
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _db?.Dispose();
        Application.Current.Shutdown();
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return) Unlock_Click(sender, e);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var password = PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Password cannot be empty.");
            return;
        }

        if (_isFirstRun)
        {
            if (ConfirmBox.Password != password)
            {
                ShowError("Passwords do not match.");
                return;
            }
            _db!.SetMasterPassword(password);
        }
        else
        {
            if (!_db!.TryUnlock(password))
            {
                ShowError("Incorrect master password.");
                return;
            }
        }

        var drive = new DriveBackupService(ClientJsonPath);
        var main = new MainWindow(_db!, drive);
        _db = null; // ownership transferred
        main.Show();
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.Visibility = Visibility.Visible;
    }
}
