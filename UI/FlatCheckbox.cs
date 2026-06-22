using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TotemEffects.UI;

public class FlatCheckBox : Border
{
    private readonly Border _check;
    private bool _checked;
    public bool IsChecked
    {
        get => _checked;
        set { _checked = value; UpdateVisual(); }
    }
    public event Action<bool>? Changed;

    public FlatCheckBox(string label, bool isChecked = false)
    {
        _checked = isChecked;
        Cursor = Cursors.Hand;
        Margin = new Thickness(0, 0, 0, 6);

        var row = new StackPanel { Orientation = Orientation.Horizontal };

        _check = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            BorderThickness = new Thickness(1.5),
            BorderBrush = Theme.TextMuted,
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        var lbl = new TextBlock
        {
            Text = label,
            Foreground = Theme.TextMuted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };

        row.Children.Add(_check);
        row.Children.Add(lbl);
        Child = row;

        UpdateVisual();

        MouseLeftButtonUp += (_, _) =>
        {
            IsChecked = !IsChecked;
            Changed?.Invoke(IsChecked);
        };
    }

    private void UpdateVisual()
    {
        _check.Background = _checked
            ? Theme.Green
            : Brushes.Transparent;
        _check.BorderBrush = _checked
            ? Theme.Green
            : Theme.TextMuted;
    }
}