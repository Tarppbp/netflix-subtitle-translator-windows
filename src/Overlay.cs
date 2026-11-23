using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

internal sealed class SubtitleOverlay : Window
{
    private readonly Caption surface = new Caption();
    internal static readonly string[] FontChoices = new[] { "Noto Sans SC", "Netflix Sans", "Microsoft YaHei UI", "DengXian", "SimHei", "Segoe UI" }
        .Where(name => Fonts.SystemFontFamilies.Any(font => font.Source == name)).ToArray();
    internal static string DefaultFont { get { return FontChoices.Contains("Noto Sans SC") ? "Noto Sans SC" : "Microsoft YaHei UI"; } }
    public string ActualFont { get { return surface.ActualFont ?? ""; } }
    public int Painted { get { return surface.Painted; } }
    public int AlignedAbove { get; private set; }
    private IntPtr handle;
    private Rect previous;
    public SubtitleOverlay()
    {
        Title = "Netflix 中文字幕浮层";
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Focusable = false;
        Width = 1;
        Height = 1;
        Content = surface;
        TextOptions.SetTextFormattingMode(surface, TextFormattingMode.Display);
        surface.IsHitTestVisible = false;
        SourceInitialized += delegate
        {
            handle = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(handle, -20);
            SetWindowLong(handle, -20, style | 0x08000000 | 0x00000020 | 0x00000080);
        };
    }

    public void Present(Rect bounds, Rect sourceBounds, uint targetDpi, string text, Preferences settings)
    {
        if (String.IsNullOrEmpty(text) || bounds.IsEmpty)
        {
            if (IsVisible) Hide();
            return;
        }
        surface.Text = text;
        surface.Settings = settings;
        surface.InvalidateVisual();
        double scale = targetDpi == 0 ? 1 : targetDpi / 96d;
        int lines = text.Split('\n').Length;
        double width = Math.Ceiling(bounds.Width * .86);
        double height = Math.Ceiling(Math.Min(bounds.Height * .55, (lines + 2) * settings.Size * 1.45 * scale + 28 * scale));
        double bottom = bounds.Bottom - Math.Max(0, bounds.Height * settings.Bottom / 100d - 10 * scale);
        if (!sourceBounds.IsEmpty)
        {
            // The caption itself is drawn 10 DIPs above the bottom of this window.
            bottom = sourceBounds.Top - 4 * scale;
            double spaceAbove = bottom - bounds.Top;
            if (spaceAbove < height)
            {
                double minimumHeight = (lines * settings.Size * 1.4 + 25) * scale;
                if (spaceAbove >= minimumHeight) height = spaceAbove;
                else bottom = Math.Min(bounds.Bottom, sourceBounds.Bottom + 12 * scale + height);
            }
        }
        bounds = new Rect(bounds.Left + (bounds.Width - width) / 2, Math.Max(bounds.Top, bottom - height), width, height);
        if (!sourceBounds.IsEmpty && bounds.Bottom <= sourceBounds.Top) AlignedAbove++;
        bool wasHidden = !IsVisible;
        if (wasHidden) Show();
        if (wasHidden || bounds != previous)
        {
            SetWindowPos(handle, new IntPtr(-1), (int)bounds.X, (int)bounds.Y, (int)bounds.Width,
                (int)bounds.Height, 0x0010);
            previous = bounds;
        }
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr above,
        int x, int y, int width, int height, uint flags);

    private sealed class Caption : FrameworkElement
    {
        public string Text;
        public Preferences Settings;
        public int Painted;
        public string ActualFont;
        protected override void OnRender(DrawingContext drawing)
        {
            if (String.IsNullOrEmpty(Text) || ActualWidth < 100 || ActualHeight < 35) return;
            var color = (Color)ColorConverter.ConvertFromString(Settings.Color);
            var family = new FontFamily(Settings.Font == "Netflix Sans" ? "Netflix Sans, " + DefaultFont : Settings.Font);
            var weight = Settings.Font == "Noto Sans SC" ? FontWeights.Medium : FontWeights.SemiBold;
            var font = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
            ActualFont = family.Source;
            var formatted = new FormattedText(Text, CultureInfo.GetCultureInfo("zh-CN"),
                FlowDirection.LeftToRight, font, Settings.Size, new SolidColorBrush(color),
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            formatted.MaxTextWidth = ActualWidth * 0.97;
            formatted.MaxTextHeight = Math.Max(1, ActualHeight - 20);
            formatted.TextAlignment = TextAlignment.Center;
            var y = Math.Max(5, ActualHeight - 10 - formatted.Height);
            var origin = new Point((ActualWidth - formatted.MaxTextWidth) / 2, y);
            if (Settings.Opacity > 0)
            {
                var width = Math.Min(ActualWidth * .88, formatted.Width + 22);
                var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * Settings.Opacity / 100), 0, 0, 0));
                drawing.DrawRoundedRectangle(brush, null,
                    new Rect((ActualWidth - width) / 2, y - 5, width, formatted.Height + 10), 6, 6);
            }
            var geometry = formatted.BuildGeometry(origin);
            var pen = Settings.Stroke == 0 ? null : new Pen(Brushes.Black, Settings.Stroke * 2) { LineJoin = PenLineJoin.Round };
            if (pen != null) drawing.DrawGeometry(null, pen, geometry);
            drawing.DrawGeometry(new SolidColorBrush(color), null, geometry);
            Painted++;
        }
    }
}
