using System.Drawing;
using System.IO;
using System.Reflection;

namespace stawp
{
    internal static class StawpIcons
    {
        private static Bitmap? _plugin24;
        private static Bitmap? _solverOn16;
        private static Bitmap? _solverOff16;

        public static Bitmap Plugin24 => _plugin24 ??= LoadBitmap("stawp.icons.plugin.png");

        public static Bitmap SolverOn16 => _solverOn16 ??= LoadBitmap("stawp.icons.solver-on.png");

        public static Bitmap SolverOff16 => _solverOff16 ??= LoadBitmap("stawp.icons.solver-off.png");

        private static Bitmap LoadBitmap(string resourceName)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException($"Embedded icon not found: {resourceName}");

            using var tmp = new Bitmap(stream);
            return new Bitmap(tmp);
        }
    }
}
