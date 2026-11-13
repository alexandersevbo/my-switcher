using System.Diagnostics;

namespace MySwitcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Запущен скачанный обновлятором exe: заменить старую версию и перезапустить её.
        if (Updater.TryRunUpdateMode(args))
            return;

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

        Updater.CleanupTemp();
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.Run(new TrayContext());
    }
}
