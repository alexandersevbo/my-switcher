using System.Diagnostics;
using System.Net;

namespace MySwitcher;

/// <summary>
/// Обновление по кнопке в меню. Единственное место, где программа ходит в сеть:
/// запрос к api.github.com за последним релизом и скачивание exe с github.com.
/// </summary>
internal static class Updater
{
    private const string LatestReleaseApi = "https://api.github.com/repos/alexandersevbo/my-switcher/releases/latest";
    private const string UpdateArg = "--update";

    public static Version CurrentVersion =>
        GitHubRelease.Normalize(typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0));

    private static string TempDir => Path.Combine(Path.GetTempPath(), "MySwitcher-update");

    /// <summary>Проверить и, если пользователь согласен, обновиться. true — текущей программе пора выйти.</summary>
    public static async Task<bool> CheckAndUpdateAsync()
    {
        GitHubRelease? release;
        try
        {
            using var web = CreateClient();
            web.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            string json = await web.DownloadStringTaskAsync(LatestReleaseApi);
            release = GitHubRelease.Parse(json);
        }
        catch (Exception ex)
        {
            Show($"Не удалось проверить обновления.\n\n{ex.Message}", MessageBoxIcon.Warning);
            return false;
        }

        if (release is null)
        {
            Show("Не удалось разобрать ответ GitHub — возможно, релиз ещё собирается.", MessageBoxIcon.Warning);
            return false;
        }

        if (release.Version <= CurrentVersion)
        {
            Show($"У вас последняя версия ({CurrentVersion}).", MessageBoxIcon.Information);
            return false;
        }

        var answer = MessageBox.Show(
            $"Доступна новая версия {release.Version} (у вас {CurrentVersion}).\n\nОбновить сейчас? Программа перезапустится.",
            "my-switcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes)
            return false;

        string newExe = Path.Combine(TempDir, "MySwitcher.exe");
        try
        {
            Directory.CreateDirectory(TempDir);
            using var web = CreateClient();
            await web.DownloadFileTaskAsync(release.ExeUrl, newExe);
            if (!LooksLikeExe(newExe))
                throw new InvalidDataException("Скачанный файл не похож на программу.");
        }
        catch (Exception ex)
        {
            Show($"Не удалось скачать обновление.\n\n{ex.Message}", MessageBoxIcon.Warning);
            return false;
        }

        // Новый exe дождётся нашего выхода, заменит текущий файл и запустит его.
        string target = Application.ExecutablePath;
        Process.Start(new ProcessStartInfo
        {
            FileName = newExe,
            Arguments = $"{UpdateArg} {Process.GetCurrentProcess().Id} \"{target}\"",
            UseShellExecute = false,
        });
        return true;
    }

    /// <summary>
    /// Режим, в котором запущен скачанный exe: заменить старый файл собой и запустить его.
    /// Возвращает true, если аргументы были командой обновления (тогда больше ничего не делаем).
    /// </summary>
    public static bool TryRunUpdateMode(string[] args)
    {
        if (args.Length < 3 || args[0] != UpdateArg)
            return false;

        string target = args[2];
        try
        {
            if (int.TryParse(args[1], out int pid))
            {
                try { Process.GetProcessById(pid).WaitForExit(10_000); }
                catch (ArgumentException) { /* уже вышел */ }
            }

            CopyWithRetry(Application.ExecutablePath, target);
            Process.Start(target);
        }
        catch (Exception ex)
        {
            Show($"Не удалось установить обновление.\n\n{ex.Message}\n\nСкачайте программу заново со страницы проекта на GitHub.",
                MessageBoxIcon.Error);
        }
        return true;
    }

    /// <summary>Убрать скачанный файл после успешного обновления (вызывается при обычном запуске).</summary>
    public static void CleanupTemp()
    {
        try
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, recursive: true);
        }
        catch
        {
            // занято или нет прав — не важно
        }
    }

    private static void CopyWithRetry(string from, string to)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(from, to, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(250); // старый процесс ещё отпускает файл
            }
        }
    }

    private static WebClient CreateClient()
    {
        // .NET Framework по умолчанию может не включать TLS 1.2, а GitHub требует его.
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var web = new WebClient();
        web.Headers[HttpRequestHeader.UserAgent] = "my-switcher-updater";
        return web;
    }

    private static bool LooksLikeExe(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < 10_000) return false;
        using var f = info.OpenRead();
        return f.ReadByte() == 'M' && f.ReadByte() == 'Z';
    }

    private static void Show(string text, MessageBoxIcon icon) =>
        MessageBox.Show(text, "my-switcher", MessageBoxButtons.OK, icon);
}
