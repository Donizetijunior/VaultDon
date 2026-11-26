using System.Threading;
using System.Windows;

namespace VaultDon.UI;

public partial class App : Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "VaultDon_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("VAULTDON já está aberto.", "Instância duplicada",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
