using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MySwitcher;

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly DoubleShiftHook _hook;
    private readonly SelectionFixer _fixer = new();
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly Icon _iconOn = MakeIcon(true);
    private readonly Icon _iconOff = MakeIcon(false);
    private bool _enabled = true;

    public TrayContext()
    {
        _enabledItem = new ToolStripMenuItem("Включено", null, (_, _) => SetEnabled(!_enabled)) { Checked = true };
        _autostartItem = new ToolStripMenuItem("Запускать при входе в Windows", null, (_, _) => ToggleAutostart())
        {
            Checked = IsAutostart(),
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add($"Проверить обновления… (у вас {Updater.CurrentVersion})", null, (_, _) => CheckUpdates());
        if (Installer.IsInstalledCopy)
            menu.Items.Add("Удалить с компьютера…", null, (_, _) => Uninstall());
        menu.Items.Add("Выход", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = _iconOn,
            Text = "my-switcher — двойной левый Shift",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => SetEnabled(!_enabled);

        _hook = new DoubleShiftHook(SynchronizationContext.Current!);
        _hook.DoubleShift += OnDoubleShift;
    }

    private async void OnDoubleShift()
    {
        if (!_enabled) return;
        try
        {
            await _fixer.FixSelectionAsync();
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(3000, "my-switcher", ex.Message, ToolTipIcon.Error);
        }
    }

    private void SetEnabled(bool value)
    {
        _enabled = value;
        _enabledItem.Checked = value;
        _tray.Icon = value ? _iconOn : _iconOff;
        _tray.Text = value ? "my-switcher — двойной левый Shift" : "my-switcher — выключен";
    }

    private static bool IsAutostart() => Installer.IsAutostartForCurrent();

    private void ToggleAutostart()
    {
        Installer.SetAutostartForCurrent(!IsAutostart());
        _autostartItem.Checked = IsAutostart();
    }

    private bool _checkingUpdates;

    private async void CheckUpdates()
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        try
        {
            if (await Updater.CheckAndUpdateAsync())
                ExitThread();
        }
        finally
        {
            _checkingUpdates = false;
        }
    }

    private void Uninstall()
    {
        var answer = MessageBox.Show(
            "Удалить my-switcher с компьютера?\nАвтозапуск, ярлык и файлы программы будут удалены.",
            "my-switcher", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        Installer.Uninstall();
        ExitThread();
    }

    private static Icon MakeIcon(bool on)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var bg = new SolidBrush(on ? Color.FromArgb(0, 120, 215) : Color.Gray);
            g.FillEllipse(bg, 0, 0, 31, 31);
            using var font = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Aя", font, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        _hook.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
