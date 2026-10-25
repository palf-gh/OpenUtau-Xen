namespace OpenUtau.App.Controls {
    /// <summary>Width limits of the docked note-properties panel (from OpenUtau Lunai).</summary>
    public static class NotePropsPanelMetrics {
        public const double DefaultWidth = 400;
        public const double MinWidth = 350;
        public const double MaxWidth = 450;

        public static double ClampWidth(double width) =>
            System.Math.Clamp(width, MinWidth, MaxWidth);
    }
}
