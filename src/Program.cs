using System.Diagnostics;

namespace MySwitcher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Запуск скачанного exe: предложить установить и перезапуститься уже из папки установки.
        if (Installer.OfferInstall())
        {
            if (File.Exists(Installer.InstalledExe))
                Process.Start(Installer.InstalledExe);
            return;
        }

        // Только один экземпляр.
        using var mutex = new Mutex(true, @"Local\MySwitcher.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("my-switcher уже запущен — иконка «Aя» в трее.", "my-switcher",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.Run(new TrayContext());
    }
}
