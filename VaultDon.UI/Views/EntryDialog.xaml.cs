using System.Windows;
using System.Windows.Input;
using VaultDon.Core.Models;
using VaultDon.UI.ViewModels;

namespace VaultDon.UI.Views;

public partial class EntryDialog : Window
{
    private readonly EntryDialogViewModel _vm;
    public VaultEntry? Result { get; private set; }

    public EntryDialog(VaultEntry? entry = null)
    {
        _vm = new EntryDialogViewModel();
        _vm.CloseDialog = ok =>
        {
            if (ok)
            {
                // sync password from PasswordBox if hidden
                if (!_vm.ShowPassword && PasswordHidden.Password.Length > 0)
                    _vm.Password = PasswordHidden.Password;
                Result = _vm.ToEntry();
                DialogResult = true;
            }
            else
            {
                DialogResult = false;
            }
        };

        if (entry is not null) _vm.LoadEntry(entry);
        DataContext = _vm;
        InitializeComponent();
        if (entry is not null) PasswordHidden.Password = entry.Password;
        PasswordHidden.PasswordChanged += (_, _) => _vm.Password = PasswordHidden.Password;
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
}
