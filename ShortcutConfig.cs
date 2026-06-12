namespace stawp
{
    /// <summary>
    /// Ctrl+Shift+K — not assigned in Rhino 7/8 defaults
    /// (Ctrl+Shift+L = UnlockSelected, Ctrl+Alt+L = Unlock).
    /// </summary>
    internal static class ShortcutConfig
    {
        internal const string Hint = "Ctrl+Shift+K";
        internal const uint VkKey = 0x4B; // K
        internal const int HotkeyId = 1;
        internal const uint Modifiers = 0x0002 | 0x0004; // Ctrl + Shift
    }
}
