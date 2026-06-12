using System;

using System.Drawing;

using System.Runtime.InteropServices;

using System.Threading;

using System.Windows.Forms;

using Grasshopper;

using Grasshopper.Kernel;

using Rhino;



namespace stawp

{

    internal static class SolverService

    {

        private const int VkControl = 0x11;

        private const int VkShift = 0x10;



        internal const string ShortcutHint = ShortcutConfig.Hint;

        private const int PollIntervalMs = 8;

        private const int ToggleDebounceMs = 250;



        private static readonly object Sync = new object();

        private static Thread? _pollerThread;

        private static volatile bool _pollerRunning;

        private static bool _shortcutWasDown;

        private static bool _syncingToolbar;

        private static long _lastToggleMs;

        private static ToolStripButton? _toolbarButton;



        public static bool IsEnabled => GH_Document.EnableSolutions;



        [DllImport("user32.dll")]

        private static extern short GetAsyncKeyState(int vKey);



        [DllImport("winmm.dll")]

        private static extern uint timeBeginPeriod(uint uPeriod);



        [DllImport("winmm.dll")]

        private static extern uint timeEndPeriod(uint uPeriod);



        public static void StartShortcuts()

        {

            ShortcutHost.Start();



            for (int i = 0; i < 20 && !ShortcutHost.HotkeysRegistered; i++)

                Thread.Sleep(10);



            if (!ShortcutHost.HotkeysRegistered)

                StartKeyPoller();

        }



        public static void RegisterToolbarButton(ToolStripButton button)

        {

            _toolbarButton = button;

            ApplyToolbarVisuals(button, IsEnabled);

        }



        public static void SetEnabled(bool enabled, bool fromToolbar = false)

        {

            if (!enabled)

            {

                PanicStop(fromToolbar);

                return;

            }



            StopCampaign.Cancel();

            GH_Document.EnableSolutions = true;



            if (!fromToolbar)

                SyncToolbarButton(true);

        }



        public static void PanicStop(bool fromToolbar = false)

        {

            PanicStopCore();



            if (SolutionInspector.IsAnythingSolving() || StopCampaign.IsActive)

                StopCampaign.Start();

            else

                StopCampaign.Kick();



            if (!fromToolbar)

                SyncToolbarButton(false);

        }



        internal static void PanicStopCore()

        {

            GH_Document.EnableSolutions = false;

            AbortAllInFlight();



            try

            {

                RhinoApp.InvokeOnUiThread((Action)AbortAllInFlight);

            }

            catch

            {

                // UI thread may be blocked during heavy solves.

            }

        }



        public static void ToggleFromShortcut()

        {

            if (SolutionInspector.IsAnythingSolving() || StopCampaign.IsActive)

            {

                PanicStop();

                return;

            }



            if (!TryAcquireToggle())

                return;



            SetEnabled(!IsEnabled);

        }



        internal static void AbortAllInFlight()

        {

            var server = Instances.DocumentServer;

            if (server == null)

                return;



            for (int i = 0; i < server.DocumentCount; i++)

            {

                var doc = server[i];

                if (doc.SolutionState == GH_ProcessStep.Process || doc.SolutionDepth > 0)

                    doc.RequestAbortSolution();

            }

        }



        private static void PollerLoop()

        {

            timeBeginPeriod(1);

            try

            {

                while (_pollerRunning)

                {

                    try

                    {

                        bool down = IsShortcutDown();

                        if (down && !_shortcutWasDown)

                            HandleShortcutPress();



                        _shortcutWasDown = down;

                    }

                    catch

                    {

                        // Grasshopper may not be loaded yet.

                    }



                    Thread.Sleep(PollIntervalMs);

                }

            }

            finally

            {

                timeEndPeriod(1);

            }

        }



        private static void HandleShortcutPress()

        {

            if (SolutionInspector.IsAnythingSolving() || StopCampaign.IsActive)

            {

                PanicStop();

                return;

            }



            if (!TryAcquireToggle())

                return;



            SetEnabled(!IsEnabled);

        }



        private static bool TryAcquireToggle()

        {

            long now = Environment.TickCount;

            lock (Sync)

            {

                if (now - _lastToggleMs < ToggleDebounceMs)

                    return false;



                _lastToggleMs = now;

                return true;

            }

        }



        private static void StartKeyPoller()

        {

            lock (Sync)

            {

                if (_pollerThread != null && _pollerThread.IsAlive)

                    return;



                _pollerRunning = true;

                _pollerThread = new Thread(PollerLoop)

                {

                    IsBackground = true,

                    Name = "stawp-key-poller"

                };

                _pollerThread.Start();

            }

        }



        private static bool IsShortcutDown()

        {

            return IsKeyDown(VkControl)

                && IsKeyDown(VkShift)

                && IsKeyDown((int)ShortcutConfig.VkKey);

        }



        private static bool IsKeyDown(int virtualKey)

        {

            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

        }



        private static void SyncToolbarButton(bool enabled)

        {

            var button = _toolbarButton;

            if (button == null)

                return;



            try

            {

                RhinoApp.InvokeOnUiThread((Action)(() =>

                {

                    if (button.IsDisposed)

                        return;



                    _syncingToolbar = true;

                    try

                    {

                        button.Checked = enabled;

                        ApplyToolbarVisuals(button, enabled);

                    }

                    finally

                    {

                        _syncingToolbar = false;

                    }

                }));

            }

            catch

            {

                // UI thread may be blocked during heavy solves.

            }

        }



        public static void OnToolbarCheckedChanged(bool enabled)

        {

            if (_syncingToolbar)

                return;



            lock (Sync)

                _lastToggleMs = Environment.TickCount;



            SetEnabled(enabled, fromToolbar: true);

            if (_toolbarButton != null && !_toolbarButton.IsDisposed)

                ApplyToolbarVisuals(_toolbarButton, enabled);

        }



        public static void ApplyToolbarVisuals(ToolStripButton button, bool enabled)

        {

            button.Image = enabled ? StawpIcons.SolverOn16 : StawpIcons.SolverOff16;

            button.ToolTipText = enabled

                ? $"Solver enabled — lock-open ({ShortcutHint})"

                : $"Solver locked — {ShortcutHint} reinforces stop while solving, unlocks when idle";

        }

    }

}


