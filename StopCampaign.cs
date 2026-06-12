using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Grasshopper.Kernel;
using Rhino;
using Rhino.UI;

namespace stawp
{
    /// <summary>
    /// Keeps requesting abort until solving stops, with live feedback on a separate UI thread.
    /// </summary>
    internal static class StopCampaign
    {
        private const int RetryIntervalMs = 20;
        private const int GiveUpAfterMs = 180_000;

        private static readonly object Sync = new object();
        private static Thread? _thread;
        private static volatile bool _running;
        private static int _cancelRequested;
        public static bool IsActive => _running;

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        public static void Start()
        {
            Interlocked.Exchange(ref _cancelRequested, 0);
            Kick();

            lock (Sync)
            {
                if (_thread != null && _thread.IsAlive)
                    return;

                _running = true;
                _thread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "stawp-stop-campaign",
                    Priority = ThreadPriority.AboveNormal
                };
                _thread.Start();
            }
        }

        public static void Kick()
        {
            SolverService.PanicStopCore();
        }

        public static void Cancel()
        {
            Interlocked.Exchange(ref _cancelRequested, 1);
            StopFeedbackForm.Dismiss();
        }

        private static void Run()
        {
            var stopwatch = Stopwatch.StartNew();
            int attempt = 0;
            string lastDetail = string.Empty;

            timeBeginPeriod(1);
            try
            {
                while (Interlocked.CompareExchange(ref _cancelRequested, 0, 0) == 0)
                {
                    if (!SolutionInspector.IsAnythingSolving())
                    {
                        if (attempt > 0)
                        {
                            StopFeedbackForm.ShowSuccess("stawp — solver stopped");
                            WriteStatus("stawp: solver stopped.");
                        }

                        break;
                    }

                    if (stopwatch.ElapsedMilliseconds > GiveUpAfterMs)
                    {
                        var snap = SolutionInspector.Capture(
                            "Still blocked after 3 minutes. Native Rhino geometry may not interrupt until it finishes.");
                        StopFeedbackForm.ShowStopping(attempt, stopwatch.Elapsed, snap);
                        WriteStatus("stawp: still waiting on native geometry.");
                        break;
                    }

                    attempt++;
                    SolverService.PanicStopCore();

                    var snapshot = SolutionInspector.Capture();
                    StopFeedbackForm.ShowStopping(attempt, stopwatch.Elapsed, snapshot);

                    string summary = snapshot.BuildSummary();
                    if (!string.Equals(lastDetail, summary, StringComparison.Ordinal))
                    {
                        lastDetail = summary;
                        string oneLine = summary.Replace('\n', ' ');
                        if (oneLine.Length > 120)
                            oneLine = oneLine.Substring(0, 117) + "…";
                        WriteStatus($"stawp: stopping… {oneLine}");
                    }

                    Thread.Sleep(RetryIntervalMs);
                }
            }
            finally
            {
                _running = false;
                timeEndPeriod(1);
            }
        }

        private static void WriteStatus(string message)
        {
            try
            {
                RhinoApp.InvokeOnUiThread((Action)(() =>
                {
                    try
                    {
                        StatusBar.SetMessagePane(message);
                    }
                    catch
                    {
                        RhinoApp.WriteLine(message);
                    }
                }));
            }
            catch
            {
                RhinoApp.WriteLine(message);
            }
        }
    }
}
