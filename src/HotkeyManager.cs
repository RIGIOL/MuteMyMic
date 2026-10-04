using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MuteMyMic
{
    // System-wide hotkeys that work whichever window is focused.
    // Keyboard keys: RegisterHotKey (no hook, zero cost until pressed).
    // Mouse buttons (middle / side): a low-level mouse hook on its OWN thread. Windows calls a
    // low-level hook for every mouse move and waits for it, so it must never share a thread with
    // anything slow — otherwise the cursor stutters system-wide, or Windows silently drops the hook.
    // The same hidden window receives commands from "MuteMyMic.exe --toggle/--mute/--unmute".
    class HotkeyManager : NativeWindow, IDisposable
    {
        public const string WindowTitle = "MuteMyMic_Command_Window";
        public const int WM_COMMAND_ACTION = 0x8002; // WM_APP + 2, wParam = MicAction

        const int WM_HOTKEY = 0x0312;
        internal const int WM_MOUSE_HOTKEY = 0x8001;  // WM_APP + 1, wParam = MicAction
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

        readonly List<int> keyIds = new List<int>();
        MouseHookThread mouseHook;

        /// <summary>A hotkey was pressed or a command arrived from another MuteMyMic.exe. UI thread.</summary>
        public event Action<MicAction> Triggered;

        public HotkeyManager()
        {
            var cp = new CreateParams();
            cp.Caption = WindowTitle;
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE: invisible message-only window
            CreateHandle(cp);
        }

        /// <summary>Replaces all hotkeys. Returns the ones that could not be registered.</summary>
        public List<Keys> Apply(IEnumerable<KeyValuePair<MicAction, Keys>> bindings)
        {
            UnregisterAll();
            var busy = new List<Keys>();
            var mouse = new Dictionary<Keys, MicAction>();

            foreach (var b in bindings)
            {
                Keys hotkey = b.Value;
                if (hotkey == Keys.None) continue;
                Keys keyCode = hotkey & Keys.KeyCode;
                if (Settings.IsMouseButton(keyCode))
                {
                    mouse[hotkey] = b.Key;
                    continue;
                }

                uint mods = MOD_NOREPEAT;
                if ((hotkey & Keys.Control) != 0) mods |= MOD_CONTROL;
                if ((hotkey & Keys.Alt) != 0) mods |= MOD_ALT;
                if ((hotkey & Keys.Shift) != 0) mods |= MOD_SHIFT;

                int id = (int)b.Key;
                if (RegisterHotKey(Handle, id, mods, (uint)keyCode)) keyIds.Add(id);
                else busy.Add(hotkey);
            }

            if (mouse.Count > 0)
            {
                // The hook thread gets its own copy of the bindings: nothing is shared or mutated later.
                mouseHook = new MouseHookThread(Handle, mouse);
                if (!mouseHook.Start())
                {
                    mouseHook.Dispose();
                    mouseHook = null;
                    busy.AddRange(mouse.Keys);
                }
            }
            return busy;
        }

        public void UnregisterAll()
        {
            foreach (int id in keyIds) UnregisterHotKey(Handle, id);
            keyIds.Clear();
            if (mouseHook != null)
            {
                mouseHook.Dispose();
                mouseHook = null;
            }
        }

        protected override void WndProc(ref Message m)
        {
            int action = 0;
            if (m.Msg == WM_HOTKEY || m.Msg == WM_MOUSE_HOTKEY || m.Msg == WM_COMMAND_ACTION)
                action = m.WParam.ToInt32();
            if (action >= (int)MicAction.Toggle && action <= (int)MicAction.Unmute && Triggered != null)
                Triggered((MicAction)action);
            base.WndProc(ref m);
        }

        /// <summary>Sends an action to the copy of the app that is already running.
        /// Waits up to waitMs for it, in case that copy is still starting.</summary>
        public static bool SendToRunningInstance(MicAction action, int waitMs)
        {
            int waited = 0;
            while (true)
            {
                IntPtr wnd = FindWindowEx(new IntPtr(-3), IntPtr.Zero, null, WindowTitle);
                if (wnd != IntPtr.Zero && PostMessage(wnd, WM_COMMAND_ACTION, new IntPtr((int)action), IntPtr.Zero))
                    return true;
                if (waited >= waitMs) return false;
                Thread.Sleep(100);
                waited += 100;
            }
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
    }

    // A thread that does nothing but run the low-level mouse hook and its message loop.
    sealed class MouseHookThread : IDisposable
    {
        const int WH_MOUSE_LL = 14;
        const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208, WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C;
        const int WM_QUIT = 0x0012;

        readonly IntPtr target;                         // window that receives WM_MOUSE_HOTKEY
        readonly Dictionary<Keys, MicAction> bindings;  // read-only after construction
        Thread thread;
        uint threadId;
        IntPtr hook;
        LowLevelMouseProc proc;                         // field: keeps the delegate alive for native code
        Keys swallowUpOf = Keys.None;                   // touched only on the hook thread

        public MouseHookThread(IntPtr target, Dictionary<Keys, MicAction> bindings)
        {
            this.target = target;
            this.bindings = new Dictionary<Keys, MicAction>(bindings);
        }

        public bool Start()
        {
            bool ok = false;
            using (var ready = new ManualResetEvent(false))
            {
                thread = new Thread(() =>
                {
                    threadId = GetCurrentThreadId();
                    proc = HookProc;
                    hook = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(null), 0);
                    ok = hook != IntPtr.Zero;
                    ready.Set();
                    if (!ok) return;

                    MSG msg;
                    while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0) { } // hook callbacks run inside GetMessage
                    UnhookWindowsHookEx(hook);
                    hook = IntPtr.Zero;
                });
                thread.IsBackground = true;
                thread.Name = "MuteMyMic mouse hook";
                thread.Priority = ThreadPriority.AboveNormal; // input latency matters more than anything else here
                thread.Start();
                ready.WaitOne(3000);
            }
            return ok;
        }

        IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                Keys button = Keys.None;
                if (msg == WM_MBUTTONDOWN || msg == WM_MBUTTONUP)
                {
                    button = Keys.MButton;
                }
                else if (msg == WM_XBUTTONDOWN || msg == WM_XBUTTONUP)
                {
                    // mouseData is the 3rd field of MSLLHOOKSTRUCT (after the POINT): read it directly, no allocation.
                    int x = (int)((uint)Marshal.ReadInt32(lParam, 8) >> 16);
                    button = x == 1 ? Keys.XButton1 : x == 2 ? Keys.XButton2 : Keys.None;
                }

                if (button != Keys.None)
                {
                    bool down = msg == WM_MBUTTONDOWN || msg == WM_XBUTTONDOWN;
                    MicAction action;
                    if (down && bindings.TryGetValue(button | CurrentModifiers(), out action))
                    {
                        // Do the work on the UI thread after returning: hooks must be instant.
                        HotkeyManager.PostMessage(target, HotkeyManager.WM_MOUSE_HOTKEY, new IntPtr((int)action), IntPtr.Zero);
                        swallowUpOf = button;
                        return new IntPtr(1);
                    }
                    if (!down && swallowUpOf == button)
                    {
                        swallowUpOf = Keys.None;
                        return new IntPtr(1);
                    }
                }
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        // Real keyboard state right now (Control.ModifierKeys can be stale in a background app).
        static Keys CurrentModifiers()
        {
            Keys mods = Keys.None;
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) mods |= Keys.Control;
            if ((GetAsyncKeyState(0x12) & 0x8000) != 0) mods |= Keys.Alt;
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) mods |= Keys.Shift;
            return mods;
        }

        public void Dispose()
        {
            if (thread == null) return;
            if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            thread.Join(1000);
            thread = null;
        }

        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam, lParam;
            public uint time;
            public int x, y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")]
        static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
    }
}
