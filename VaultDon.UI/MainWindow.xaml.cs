using System.Windows;
using System.Windows.Input;
using VaultDon.Core.Drive;
using VaultDon.Core.Storage;
using VaultDon.UI.ViewModels;

namespace VaultDon.UI;

public partial class MainWindow : Window
{
    private readonly VaultDatabase _db;

    public MainWindow(VaultDatabase db, DriveBackupService drive)
    {
        _db = db;
        DataContext = new MainViewModel(db, drive);
        InitializeComponent();
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _db.Dispose();
        Close();
    }
}