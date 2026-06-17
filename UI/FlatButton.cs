using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TotemEffects.UI;

public class FlatButton : Border
{
    private readonly TextBlock _label;
    private SolidColorBrush _bg;
    private SolidColorBrush _hov;
    private bool _isHovered;

    public event Action? Clicked;

    public FlatButton(string text, SolidColorBrush bg, SolidColorBrush hov, double width, double fontSize = 11, FontFamily? fontFamily = null)
    {
        _bg = bg;
        _hov = hov;

        Width = width;
        Height = 26;
        CornerRadius = new CornerRadius(3);
        Background = _bg;
        Cursor = Cursors.Hand;

        _label = new TextBlock
        {
            Text = text,
            Foreground = Theme.TextMain,
            FontSize = fontSize,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (fontFamily != null)
        {
            _label.FontFamily = fontFamily;
        }
        Child = _label;

        MouseEnter += (_, _) => { _isHovered = true; Background = _hov; };
        MouseLeave += (_, _) => { _isHovered = false; Background = _bg; };
        MouseLeftButtonDown += (_, e) => e.Handled = true;
        MouseLeftButtonUp += (_, e) => { if (_isHovered) Clicked?.Invoke(); e.Handled = true; };
    }

    public void SetLabel(string text) => _label.Text = text;

    public void SetColors(SolidColorBrush bg, SolidColorBrush hov)
    {
        _bg = bg;
        _hov = hov;
        Background = _isHovered ? _hov : _bg;
    }

    public void SetForeground(SolidColorBrush fg) => _label.Foreground = fg;
}