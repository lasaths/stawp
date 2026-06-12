using System;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

namespace stawp
{
    public sealed class StawpInfo : GH_AssemblyInfo
    {
        public override string Name => "stawp";
        public override Bitmap Icon => StawpIcons.Plugin24;
        public override string Description => $"Solver kill switch — canvas toolbar toggle and {SolverService.ShortcutHint} emergency lock.";
        public override Guid Id => new Guid("8f4e2b1a-9c3d-4e5f-a6b7-1c2d3e4f5a6b");
        public override string AuthorName => "lasaths";
        public override string AuthorContact => "";
        public override string AssemblyVersion => "0.1.0";
    }

    public sealed class StawpBootstrap : GH_AssemblyPriority
    {
        private static bool _toolbarInstalled;

        public override GH_LoadingInstruction PriorityLoad()
        {
            Instances.CanvasCreated += OnCanvasCreated;
            SolverService.StartShortcuts();
            return GH_LoadingInstruction.Proceed;
        }

        private static void OnCanvasCreated(GH_Canvas canvas)
        {
            if (_toolbarInstalled)
                return;

            Instances.CanvasCreated -= OnCanvasCreated;

            var editor = Instances.DocumentEditor;
            if (editor == null)
            {
                Instances.CanvasCreated += OnCanvasCreated;
                return;
            }

            var toolbar = CanvasToolbarFinder.Find(editor);
            if (toolbar == null)
            {
                Instances.CanvasCreated += OnCanvasCreated;
                return;
            }

            if (FindExistingButton(toolbar) != null)
            {
                _toolbarInstalled = true;
                return;
            }

            var button = new ToolStripButton
            {
                CheckOnClick = true,
                Checked = SolverService.IsEnabled,
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                Tag = "stawp-solver"
            };

            SolverService.ApplyToolbarVisuals(button, button.Checked);
            button.CheckedChanged += (_, __) => SolverService.OnToolbarCheckedChanged(button.Checked);

            toolbar.Items.Add(button);
            SolverService.RegisterToolbarButton(button);
            _toolbarInstalled = true;
        }

        private static ToolStripButton? FindExistingButton(ToolStrip toolbar)
        {
            foreach (ToolStripItem item in toolbar.Items)
            {
                if (item is ToolStripButton btn && "stawp-solver".Equals(btn.Tag))
                    return btn;
            }

            return null;
        }
    }

    internal static class CanvasToolbarFinder
    {
        public static ToolStrip? Find(Form editor)
        {
            if (editor.Controls.Count > 0)
            {
                var host = editor.Controls[0];
                if (host.Controls.Count > 1 && host.Controls[1] is ToolStrip known)
                    return known;
            }

            return FindToolStrip(editor);
        }

        private static ToolStrip? FindToolStrip(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is ToolStrip strip && child is not MenuStrip)
                    return strip;

                var nested = FindToolStrip(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }

}
