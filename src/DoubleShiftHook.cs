using System.Runtime.InteropServices;
using static MySwitcher.Native;

namespace MySwitcher;

/// <summary>
/// Ловит двойное нажатие левого Shift «вхолостую» (без других клавиш между ними).
/// Никакие нажатия не запоминаются и не сохраняются: для остальных клавиш хук
/// лишь сбрасывает счётчик, чтобы Shift+стрелки и т.п. не считались двойным Shift.
/// Хук работает в своём потоке, чтобы паузы в основном потоке не тормозили клавиатуру.
/// </summary>
internal sealed class DoubleShiftHook : IDisposable
{
    private const uint MaxTapMs = 350;     // сколько можно держать Shift, чтобы это считалось «тапом»
    private const uint MaxIntervalMs = 450; // между отпусканиями первого и второго Shift

    private readonly SynchronizationContext _uiContext;
    private readonly Thread _thread;
    private LowLevelKeyboardProc? _proc; // держим ссылку, иначе GC соберёт делегат
    private uint _threadId;

    private bool _shiftDown;
    private bool _dirty;
    private uint _downTime;
    private bool _haveFirstTap;
    private uint _firstTapTime;

    public event Action? DoubleShift;

    public DoubleShiftHook(SynchronizationContext uiContext)
    {
        _uiContext = uiContext;
        using var started = new ManualResetEventSlim();
        _thread = new Thread(() => Run(started)) { IsBackground = true, Name = "DoubleShiftHook" };
        _thread.Start();
        started.Wait();
    }

    private void Run(ManualResetEventSlim started)
    {
        _threadId = GetCurrentThreadId();
        _proc = Callback;
        IntPtr hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        started.Set();
        if (hook == IntPtr.Zero)
            return;

        while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0)
        {
            // У низкоуровневого хука сообщения не нужно разбирать — достаточно крутить цикл.
        }

        UnhookWindowsHookEx(hook);
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var k = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Свои же синтетические нажатия (Ctrl+C / Ctrl+V) игнорируем.
            if ((k.flags & LLKHF_INJECTED) == 0)
            {
                int msg = (int)wParam;
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = msg is WM_KEYUP or WM_SYSKEYUP;
                if (down || up)
                    Handle(k.vkCode, down, k.time);
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void Handle(uint vk, bool down, uint time)
    {
        if (vk == VK_LSHIFT)
        {
            if (down)
            {
                if (!_shiftDown) // автоповтор пропускаем
                {
                    _shiftDown = true;
                    _dirty = false;
                    _downTime = time;
                }
                return;
            }

            _shiftDown = false;
            bool cleanTap = !_dirty && unchecked(time - _downTime) <= MaxTapMs;
            if (!cleanTap)
            {
                _haveFirstTap = false;
                return;
            }

            if (_haveFirstTap && unchecked(time - _firstTapTime) <= MaxIntervalMs)
            {
                _haveFirstTap = false;
                _uiContext.Post(_ => DoubleShift?.Invoke(), null);
            }
            else
            {
                _haveFirstTap = true;
                _firstTapTime = time;
            }
            return;
        }

        if (down)
        {
            _haveFirstTap = false;
            if (_shiftDown)
                _dirty = true;
        }
    }

    public void Dispose()
    {
        if (_threadId != 0)
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(1000);
    }
}
