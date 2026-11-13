using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

namespace MySwitcher;

/// <summary>
/// Самоустановка в профиль пользователя (права администратора не нужны):
/// копия в %LOCALAPPDATA%\Programs\MySwitcher, ярлык в меню «Пуск», автозапуск через HKCU\...\Run.
/// </summary>
internal static class Installer
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "MySwitcher";

    public static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MySwitcher");

    public static string InstalledExe => Path.Combine(InstallDir, "MySwitcher.exe");

    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "my-switcher.lnk");

    private static string CurrentExe => Application.ExecutablePath;

    public static bool IsInstalledCopy => SamePath(CurrentExe, InstalledExe);

    /// <summary>
    /// Предлагает установку, если exe запущен не из папки установки и не прописан в автозапуске.
    /// Возвращает true, если текущий процесс должен завершиться.
    /// </summary>
    public static bool OfferInstall()
    {
        if (IsInstalledCopy || SamePath(GetAutostartPath(), CurrentExe))
            return false;

        var answer = MessageBox.Show(
            "Установить my-switcher на этот компьютер?\n\n" +
            "Программа будет запускаться автоматически при входе в Windows.\n" +
            "Права администратора не нужны.\n\n" +
            "Как пользоваться: выделите текст, набранный не в той раскладке\n" +
            "(или с включённым CapsLock), и дважды нажмите левый Shift.\n\n" +
            "«Нет» — просто запустить без установки.",
            "my-switcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
            return false;

        try
        {
            Directory.CreateDirectory(InstallDir);
            File.Copy(CurrentExe, InstalledExe, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                "Не удалось скопировать программу — вероятно, my-switcher уже запущен.\n" +
                "Закройте его (иконка «Aя» в трее → Выход) и запустите установку ещё раз.",
                "my-switcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return true;
        }

        SetAutostart(InstalledExe);
        TryCreateShortcut();
        return true;
    }

    public static bool IsAutostartForCurrent() => SamePath(GetAutostartPath(), CurrentExe);

    public static void SetAutostartForCurrent(bool enable)
    {
        if (enable)
            SetAutostart(CurrentExe);
        else
            RemoveAutostart();
    }

    /// <summary>Удаление: автозапуск, ярлык и папка (папка — после выхода процесса).</summary>
    public static void Uninstall()
    {
        RemoveAutostart();
        try { File.Delete(ShortcutPath); } catch { /* нет ярлыка — и ладно */ }

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"{InstallDir}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        });
    }

    private static string? GetAutostartPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return (key?.GetValue(RunValue) as string)?.Trim('"');
    }

    private static void SetAutostart(string exe)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(RunValue, $"\"{exe}\"");
    }

    private static void RemoveAutostart()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(RunValue, throwOnMissingValue: false);
    }

    private static void TryCreateShortcut()
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null) return;
            object shell = Activator.CreateInstance(shellType);
            object link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, [ShortcutPath]);
            var linkType = link.GetType();
            linkType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, link, [InstalledExe]);
            linkType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, link, [InstallDir]);
            linkType.InvokeMember("Description", BindingFlags.SetProperty, null, link,
                ["Исправление раскладки по двойному левому Shift"]);
            linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
        }
        catch
        {
            // ярлык — не обязателен
        }
    }

    private static bool SamePath(string? a, string? b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
