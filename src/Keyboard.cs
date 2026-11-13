using System.Globalization;
using System.Runtime.InteropServices;
using static MySwitcher.Native;

namespace MySwitcher;

/// <summary>Отправка синтетических нажатий и работа с раскладками.</summary>
internal static class Keyboard
{
    private const int LangRussian = 0x19;
    private const int LangEnglish = 0x09;

    public static void Chord(ushort modifier, ushort key)
    {
        Send(
            KeyInput(modifier, false), KeyInput(key, false),
            KeyInput(key, true), KeyInput(modifier, true));
    }

    public static void Press(ushort key) => Send(KeyInput(key, false), KeyInput(key, true));

    /// <summary>Выделить только что вставленный текст (Shift+← по числу символов).</summary>
    public static void SelectBack(string text)
    {
        int count = new StringInfo(text).LengthInTextElements;
        var inputs = new List<INPUT>(count * 2 + 2) { KeyInput(VK_SHIFT, false) };
        for (int i = 0; i < count; i++)
        {
            inputs.Add(KeyInput(VK_LEFT, false, extended: true));
            inputs.Add(KeyInput(VK_LEFT, true, extended: true));
        }
        inputs.Add(KeyInput(VK_SHIFT, true));
        Send([.. inputs]);
    }

    public static bool ForegroundLayoutIsRussian()
    {
        uint tid = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        return PrimaryLang(GetKeyboardLayout(tid)) == LangRussian;
    }

    /// <summary>Попросить активное окно переключиться на русскую или английскую раскладку.</summary>
    public static void SwitchForegroundLayout(bool russian)
    {
        int want = russian ? LangRussian : LangEnglish;
        int n = GetKeyboardLayoutList(0, null);
        if (n <= 0) return;
        var list = new IntPtr[n];
        GetKeyboardLayoutList(n, list);
        foreach (var hkl in list)
        {
            if (PrimaryLang(hkl) == want)
            {
                PostMessage(GetForegroundWindow(), WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
                return;
            }
        }
    }

    private static int PrimaryLang(IntPtr hkl) => (int)((long)hkl & 0x3FF);

    private static INPUT KeyInput(ushort vk, bool up, bool extended = false) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = (up ? KEYEVENTF_KEYUP : 0) | (extended ? KEYEVENTF_EXTENDEDKEY : 0),
            },
        },
    };

    private static void Send(params INPUT[] inputs) =>
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
}
