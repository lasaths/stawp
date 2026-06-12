using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Grasshopper;
using Grasshopper.Kernel;

namespace stawp
{
    internal readonly struct SolutionSnapshot
    {
        public SolutionSnapshot(
            bool isSolving,
            string headline,
            string documentName,
            string solutionState,
            bool abortRequested,
            bool solverLocked,
            int computedCount,
            int totalCount,
            int computingCount,
            int collectingCount,
            int progressPercent,
            string waitingOn,
            string note)
        {
            IsSolving = isSolving;
            Headline = headline;
            DocumentName = documentName;
            SolutionState = solutionState;
            AbortRequested = abortRequested;
            SolverLocked = solverLocked;
            ComputedCount = computedCount;
            TotalCount = totalCount;
            ComputingCount = computingCount;
            CollectingCount = collectingCount;
            ProgressPercent = progressPercent;
            WaitingOn = waitingOn;
            Note = note;
        }

        public bool IsSolving { get; }
        public string Headline { get; }
        public string DocumentName { get; }
        public string SolutionState { get; }
        public bool AbortRequested { get; }
        public bool SolverLocked { get; }
        public int ComputedCount { get; }
        public int TotalCount { get; }
        public int ComputingCount { get; }
        public int CollectingCount { get; }
        public int ProgressPercent { get; }
        public string WaitingOn { get; }
        public string Note { get; }

        public static SolutionSnapshot Idle { get; } = new SolutionSnapshot(
            false, "", "", "", false, true, 0, 0, 0, 0, -1, "", "");

        public string BuildSummary()
        {
            if (!IsSolving)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine($"Document: {DocumentName}");
            sb.AppendLine($"Solver state: {SolutionState}");
            sb.Append($"Flags: solver {(SolverLocked ? "locked" : "enabled")}");
            sb.Append(AbortRequested ? ", abort requested" : ", abort pending");
            sb.AppendLine();

            if (TotalCount > 0)
            {
                sb.Append($"Objects: {ComputedCount}/{TotalCount} computed");
                if (ComputingCount > 0)
                    sb.Append($", {ComputingCount} computing");
                if (CollectingCount > 0)
                    sb.Append($", {CollectingCount} collecting");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(WaitingOn))
            {
                sb.AppendLine();
                sb.AppendLine("Waiting on:");
                sb.Append(WaitingOn);
            }
            else
            {
                sb.AppendLine();
                sb.Append("Waiting on: solver checkpoint or native Rhino geometry…");
            }

            if (!string.IsNullOrWhiteSpace(Note))
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(Note);
            }

            return sb.ToString().TrimEnd();
        }
    }

    internal static class SolutionInspector
    {
        private static bool IsDocumentActive(GH_Document doc)
        {
            if (doc.SolutionState == GH_ProcessStep.Process)
                return true;

            if (doc.SolutionDepth > 0)
                return true;

            if (!GH_Document.EnableSolutions && doc.ScheduleDelay >= 0)
                return true;

            return false;
        }

        public static bool IsAnythingSolving()
        {
            var server = Instances.DocumentServer;
            if (server == null)
                return false;

            for (int i = 0; i < server.DocumentCount; i++)
            {
                if (IsDocumentActive(server[i]))
                    return true;
            }

            return false;
        }

        public static SolutionSnapshot Capture(string? note = null)
        {
            var server = Instances.DocumentServer;
            if (server == null)
                return SolutionSnapshot.Idle;

            var waitingLines = new List<string>();
            bool solving = false;
            string documentName = "(unnamed)";
            string solutionState = string.Empty;
            bool abortRequested = false;
            int progress = -1;
            int computed = 0;
            int total = 0;
            int computing = 0;
            int collecting = 0;

            for (int i = 0; i < server.DocumentCount; i++)
            {
                var doc = server[i];
                if (!IsDocumentActive(doc))
                    continue;

                solving = true;
                documentName = string.IsNullOrWhiteSpace(doc.DisplayName) ? "(unnamed)" : doc.DisplayName;
                solutionState = doc.SolutionState.ToString();
                abortRequested = abortRequested || doc.AbortRequested;

                var counts = CountPhases(doc);
                computed += counts.Computed;
                total += counts.Total;
                computing += counts.Computing;
                collecting += counts.Collecting;
                progress = Math.Max(progress, counts.ProgressPercent);

                foreach (var obj in doc.Objects)
                {
                    if (obj is not IGH_ActiveObject active || active.Locked)
                        continue;

                    if (active.Phase == GH_SolutionPhase.Computing)
                        waitingLines.Add(FormatActive(active, "computing"));
                    else if (active.Phase == GH_SolutionPhase.Collecting)
                        waitingLines.Add(FormatActive(active, "collecting data"));
                }
            }

            if (!solving)
                return SolutionSnapshot.Idle;

            string waitingOn = waitingLines.Count > 0
                ? string.Join(Environment.NewLine, waitingLines.Take(5))
                : string.Empty;

            if (waitingLines.Count > 5)
                waitingOn += Environment.NewLine + $"…and {waitingLines.Count - 5} more";

            return new SolutionSnapshot(
                true,
                $"Stopping — {documentName}",
                documentName,
                solutionState,
                abortRequested,
                !GH_Document.EnableSolutions,
                computed,
                total,
                computing,
                collecting,
                progress,
                waitingOn,
                note ?? string.Empty);
        }

        private static string FormatActive(IGH_ActiveObject active, string verb)
        {
            string nick = string.IsNullOrWhiteSpace(active.NickName) ? active.Name : active.NickName;
            string type = active.Name;
            bool showType = !string.Equals(type, nick, StringComparison.OrdinalIgnoreCase);

            var line = new StringBuilder("  • ");
            line.Append(nick);
            if (showType)
                line.Append($" ({type})");

            line.Append($" — {verb}");

            if (active.ProcessorTime.TotalMilliseconds > 1)
                line.Append($" [{active.ProcessorTime.TotalSeconds:0.0}s]");

            return line.ToString();
        }

        private static (int Total, int Computed, int Computing, int Collecting, int ProgressPercent) CountPhases(GH_Document doc)
        {
            int total = 0;
            int computed = 0;
            int computing = 0;
            int collecting = 0;

            foreach (var obj in doc.Objects)
            {
                if (obj is not IGH_ActiveObject active || active.Locked)
                    continue;

                total++;
                switch (active.Phase)
                {
                    case GH_SolutionPhase.Computed:
                        computed++;
                        break;
                    case GH_SolutionPhase.Computing:
                        computing++;
                        break;
                    case GH_SolutionPhase.Collecting:
                        collecting++;
                        break;
                }
            }

            int progress = total == 0 ? -1 : (int)Math.Round(100.0 * computed / total);
            return (total, computed, computing, collecting, progress);
        }
    }
}
