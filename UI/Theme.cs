using System.Windows.Media;

namespace TotemEffects.UI
{
    public static class Theme
    {
        private static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

        public static readonly SolidColorBrush BgDark = B(18, 18, 24);
        public static readonly SolidColorBrush BgSurface = B(40, 40, 40);
        public static readonly SolidColorBrush BgHeader = B(22, 22, 32);
        public static readonly SolidColorBrush Green = B(31, 122, 67);
        public static readonly SolidColorBrush GreenHov = B(37, 156, 85);
        public static readonly SolidColorBrush Red = B(158, 47, 43);
        public static readonly SolidColorBrush RedHov = B(201, 55, 50);
        public static readonly SolidColorBrush TextMain = B(230, 230, 230);
        public static readonly SolidColorBrush TextMuted = B(110, 110, 130);
        public static readonly SolidColorBrush TextGreen = B(80, 200, 120);
        public static readonly SolidColorBrush CloseHov = B(180, 40, 40);
    }
}