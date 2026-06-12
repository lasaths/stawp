namespace stawp
{
    /// <summary>
    /// Ctrl+Alt+K — kill solver (K). Avoids Ctrl+Shift+L (Unlock Selected).
    /// </summary>
    internal static class ShortcutConfig
    {
        internal const string Hint = "Ctrl+Alt+K";
        internal const uint VkKey = 0x4B; // K
        internal const int HotkeyId = 1;
        internal const uint Modifiers = 0x0002 | 0x0001; // Ctrl + Alt
    }
}
