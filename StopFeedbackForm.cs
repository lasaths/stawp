using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace stawp
{
    internal sealed class StopFeedbackForm : Form
    {
        private readonly Label _title;
        private readonly Label _detail;
        private readonly Panel _progressPanel;
        private readonly Label _progressLabel;
        private readonly ProgressBar _progress;
        private readonly Label _footer;

        private static Thread? _uiThread;
        private static StopFeedbackForm? _instance;
        private static readonly object Gate = new object();

        public static void Ensure()
        {
            if (_instance != null && !_instance.IsDisposed)
                return;

            lock (Gate)
            {
                if (_instance != null && !_instance.IsDisposed)
                    return;

                var ready = new ManualResetEventSlim(false);
                _uiThread = new Thread(() =>
                {
                    _instance = new StopFeedbackForm();
                    ready.Set();
                    Application.Run(_instance);
                })
                {
                    IsBackground = true,
                    Name = "stawp-stop-feedback"
                };
                _uiThread.SetApartmentState(ApartmentState.STA);
                _uiThread.Start();
                ready.Wait(2000);
            }
        }

        public static void ShowStopping(int attempt, TimeSpan elapsed, SolutionSnapshot snapshot)
        {
            Ensure();
            InvokeOnForm(form =>
            {
                form._title.Text = $"stawp — stopping solver (attempt {attempt})";
                form._detail.Text = snapshot.BuildSummary();
                form._footer.Text = BuildFooter(elapsed, attempt);
                form.ApplyProgress(snapshot, attempt);
                form.AutoSizeToContent();
                form.ShowOverlay();
            });
        }

        public static void ShowSuccess(string message)
        {
            Ensure();
            InvokeOnForm(form =>
            {
                form._title.Text = message;
                form._detail.Text = $"Solver is locked. Press {SolverService.ShortcutHint} to unlock.";
                form._footer.Text = string.Empty;
                form._progressLabel.Text = "Progress: complete";
                form._progress.Style = ProgressBarStyle.Continuous;
                form._progress.Value = 100;
                form._progressPanel.Visible = true;
                form.Height = 150;
                form.ShowOverlay();

                var timer = new System.Windows.Forms.Timer { Interval = 2200 };
                timer.Tick += (_, __) =>
                {
                    timer.Stop();
                    timer.Dispose();
                    form.HideOverlay();
                };
                timer.Start();
            });
        }

        public static void Dismiss()
        {
            InvokeOnForm(form => form.HideOverlay());
        }

        private static string BuildFooter(TimeSpan elapsed, int attempt)
        {
            return $"{elapsed.TotalSeconds:0.0}s elapsed  ·  {attempt} abort requests  ·  {SolverService.ShortcutHint} to unlock";
        }

        private static void InvokeOnForm(Action<StopFeedbackForm> action)
        {
            var form = _instance;
            if (form == null || form.IsDisposed)
                return;

            if (form.InvokeRequired)
                form.BeginInvoke(new Action(() => action(form)));
            else
                action(form);
        }

        private StopFeedbackForm()
        {
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(28, 28, 30);
            ForeColor = Color.White;
            Padding = new Padding(14);
            Width = 480;
            MinimumSize = new Size(480, 180);
            MaximumSize = new Size(520, 380);

            _title = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 26,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(130, 210, 130)
            };

            _detail = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 8.75f),
                ForeColor = Color.FromArgb(220, 220, 220),
                Padding = new Padding(0, 6, 0, 6)
            };

            _progressLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 200, 255),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 22,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };

            _progressPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(0, 4, 0, 0)
            };
            _progressPanel.Controls.Add(_progress);
            _progressPanel.Controls.Add(_progressLabel);

            _footer = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 34,
                Font = new Font("Segoe UI", 8f),
                ForeColor = Color.FromArgb(150, 150, 155),
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 4, 0, 0)
            };

            Controls.Add(_detail);
            Controls.Add(_progressPanel);
            Controls.Add(_footer);
            Controls.Add(_title);
        }

        private void ApplyProgress(SolutionSnapshot snapshot, int attempt)
        {
            _progressPanel.Visible = true;

            if (snapshot.TotalCount > 0)
            {
                int pct = snapshot.ProgressPercent >= 0
                    ? snapshot.ProgressPercent
                    : (int)Math.Round(100.0 * snapshot.ComputedCount / snapshot.TotalCount);

                pct = Math.Max(0, Math.Min(100, pct));
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = pct;
                _progressLabel.Text = $"Progress: {snapshot.ComputedCount} / {snapshot.TotalCount}  ({pct}%)";
                return;
            }

            // No object counts yet — show activity via marquee + attempt pulse.
            _progress.Style = ProgressBarStyle.Marquee;
            _progressLabel.Text = $"Progress: waiting on checkpoint… (attempt {attempt})";
        }

        private void AutoSizeToContent()
        {
            using var g = CreateGraphics();
            var size = g.MeasureString(_detail.Text, _detail.Font, _detail.Width - 8);
            int target = (int)Math.Ceiling(size.Height)
                + _title.Height
                + _progressPanel.Height
                + _footer.Height
                + Padding.Vertical
                + 24;
            Height = Math.Max(180, Math.Min(380, target));
        }

        private void ShowOverlay()
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 800, 600);
            Location = new Point(area.Right - Width - 20, area.Bottom - Height - 20);

            if (!Visible)
                Show();

            BringToFront();
        }

        private void HideOverlay()
        {
            if (Visible)
                Hide();
        }
    }
}
