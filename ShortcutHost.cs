using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace stawp
{
    /// <summary>
    /// Registers global hotkeys on a dedicated STA thread so WM_HOTKEY is delivered
    /// even when the Grasshopper UI thread is blocked by solving.
    /// </summary>
    internal sealed class ShortcutHost : Form
    {
        private const int WmHotkey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;

        private static Thread? _thread;
        private static ShortcutHost? _instance;

        public static bool HotkeysRegistered { get; private set; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static void Start()
        {
            if (_thread != null && _thread.IsAlive)
                return;

            _thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "stawp-hotkeys"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        private static void RunMessageLoop()
        {
            _instance = new ShortcutHost();
            Application.Run(_instance);
        }

        private ShortcutHost()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            Size = new Size(0, 0);
            Opacity = 0;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            HotkeysRegistered = RegisterHotKey(
                Handle,
                ShortcutConfig.HotkeyId,
                ShortcutConfig.Modifiers,
                ShortcutConfig.VkKey);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, ShortcutConfig.HotkeyId);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
                SolverService.ToggleFromShortcut();

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Never show — message sink only.
        }
    }
}
