using System.Runtime.InteropServices;
using static MySwitcher.Native;

namespace MySwitcher;

/// <summary>
/// Копирует выделение через буфер обмена, исправляет и вставляет обратно,
/// после чего возвращает прежнее содержимое буфера.
/// </summary>
internal sealed class SelectionFixer
{
    private const int CopyTimeoutMs = 600;
    private const int MaxReselectLength = 300;

    private bool _busy;

    public async Task FixSelectionAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await FixCoreAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task FixCoreAsync()
    {
        var backup = ClipboardBackup.Save();

        uint seq = GetClipboardSequenceNumber();
        Keyboard.Chord(VK_CONTROL, 'C');
        if (!await WaitClipboardChangedAsync(seq))
            return; // ничего не выделено — буфер не тронут

        string? text = await ReadTextAsync();
        if (text is null || text.Length == 0)
        {
            backup.Restore();
            return;
        }

        bool capsOn = Control.IsKeyLocked(Keys.CapsLock);
        string result;
        bool? switchToRussian = null;

        if (capsOn || TextConverter.LooksLikeCapsTyped(text))
        {
            result = TextConverter.InvertCase(text);
            if (capsOn)
                Keyboard.Press(VK_CAPITAL); // выключить CapsLock
        }
        else
        {
            var dir = TextConverter.DetectDirection(text, Keyboard.ForegroundLayoutIsRussian());
            result = TextConverter.ConvertLayout(text, dir);
            switchToRussian = dir == LayoutDirection.ToRussian;
        }

        if (result == text)
        {
            backup.Restore();
            return;
        }

        Clipboard.SetDataObject(new DataObject(DataFormats.UnicodeText, result), true, 10, 30);
        Keyboard.Chord(VK_CONTROL, 'V');
        await Task.Delay(80);

        if (switchToRussian is bool ru)
            Keyboard.SwitchForegroundLayout(ru);

        if (result.Length <= MaxReselectLength && result.IndexOfAny(['\r', '\n']) < 0)
            Keyboard.SelectBack(result);

        // Даём приложению время забрать текст из буфера, потом возвращаем старое содержимое.
        await Task.Delay(400);
        backup.Restore();
    }

    private static async Task<bool> WaitClipboardChangedAsync(uint seq)
    {
        for (int waited = 0; waited < CopyTimeoutMs; waited += 20)
        {
            if (GetClipboardSequenceNumber() != seq)
            {
                await Task.Delay(20); // приложение может дописывать форматы
                return true;
            }
            await Task.Delay(20);
        }
        return false;
    }

    private static async Task<string?> ReadTextAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (ExternalException)
            {
                await Task.Delay(30); // буфер занят другим процессом
            }
        }
        return null;
    }

    private sealed class ClipboardBackup
    {
        private DataObject? _data;

        public static ClipboardBackup Save()
        {
            var backup = new ClipboardBackup();
            try
            {
                var src = Clipboard.GetDataObject();
                if (src is null) return backup;
                var copy = new DataObject();
                foreach (string format in src.GetFormats(false))
                {
                    try
                    {
                        object? value = src.GetData(format, false);
                        if (value is not null)
                            copy.SetData(format, false, value);
                    }
                    catch
                    {
                        // некоторые форматы не читаются — пропускаем
                    }
                }
                backup._data = copy;
            }
            catch
            {
                // буфер недоступен — восстанавливать будет нечего
            }
            return backup;
        }

        public void Restore()
        {
            try
            {
                if (_data is null || _data.GetFormats().Length == 0)
                    Clipboard.Clear();
                else
                    Clipboard.SetDataObject(_data, true, 10, 30);
            }
            catch
            {
                // не удалось — оставляем как есть
            }
        }
    }
}
