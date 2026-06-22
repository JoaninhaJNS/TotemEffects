using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TotemEffects.Core;

namespace TotemEffects.UI;

public class MainWindow : Window
{
    private readonly Extension _ext;
    private readonly FlatButton _btnStartStop;

    private bool _running { get; set; } = false;
    private Border? _selectedBorder { get; set; } = null;
    private TextBlock? _effectCountLabel { get; set; } = null;
    private int _effectCount { get; set; } = 0;

    private readonly (int Combo, string Image, string Label, bool Santorini)[] _jungleCombos =
    [
        (1, "fx_245.png", "Duck",    false),
        (2, "fx_244.png", "Mystic", false),
        (3, "fx_242.png", "Leaves",   false),
        (4, "fx_243.png", "Lightning",    false),
    ];

    private readonly (int Combo, string Image, string Label, bool Santorini)[] _santoriniCombos =
    [
        (1, "fx_23.png", "Levitation", true),
        (2, "fx_24.png", "Rain",     true),
        (3, "fx_25.png", "Fire",      true),
        (4, "fx_26.png", "Stick",    true),
    ];

    public MainWindow(Extension extension)
    {
        _ext = extension;

        Title = "Totem Effects";
        Width = 380;
        Height = 450;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Background = Theme.BgDark;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = BuildHeader(out _btnStartStop);
        Grid.SetRow(header, 0);

        var body = BuildBody();
        Grid.SetRow(body, 1);

        root.Children.Add(header);
        root.Children.Add(body);
        Content = root;

        _ext.totemManager.SetCombo(1, false);

        _ext.totemManager.EffectReceived += () => Dispatcher.Invoke(() =>
        {
            _effectCount++;
            if (_effectCountLabel != null)
                _effectCountLabel.Text = _effectCount.ToString();
        });

        _ext.totemManager.Stopped += () => Dispatcher.Invoke(() =>
        {
            _running = false;
            _btnStartStop.SetLabel("Start");
            _btnStartStop.SetColors(Theme.Green, Theme.GreenHov);
        });

        _ext.totemManager.Started += () => Dispatcher.Invoke(() =>
        {
            _running = true;
            _effectCount = 0;
            if (_effectCountLabel != null)
                _effectCountLabel.Text = "0";
            _btnStartStop.SetLabel("Stop");
            _btnStartStop.SetColors(Theme.Red, Theme.RedHov);
        });

        _ext.totemManager.Unfocus += () => Dispatcher.Invoke(() => WindowState = WindowState.Minimized);
    }

    private Border BuildHeader(out FlatButton btnAction)
    {
        var header = new Border
        {
            Background = Theme.BgHeader,
            Padding = new Thickness(12, 0, 6, 0),
            Height = 38,
        };

        bool dragging = false;
        Point dragStart = default;
        header.MouseLeftButtonDown += (_, e) => { dragStart = e.GetPosition(this); dragging = true; header.CaptureMouse(); };
        header.MouseMove += (_, e) => { if (!dragging) return; var p = e.GetPosition(this); Left += p.X - dragStart.X; Top += p.Y - dragStart.Y; };
        header.MouseLeftButtonUp += (_, _) => { dragging = false; header.ReleaseMouseCapture(); };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new Image
        {
            Source = new BitmapImage(new Uri("pack://application:,,,/UI/assets/icon.ico")),
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);

        var title = new TextBlock
        {
            Text = "Totem Effects",
            Foreground = Theme.TextMain,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(title, 1);

        btnAction = new FlatButton("Start", Theme.Green, Theme.GreenHov, width: 72);
        btnAction.Clicked += async () => await OnStartStop();

        bool pinned = false;
        var btnPin = new FlatButton("📌", Theme.BgHeader, Theme.BgSurface, width: 26, fontSize: 10, fontFamily: new FontFamily("Segoe UI Symbol"))
        {
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = "Always on top"
        };
        btnPin.SetForeground(Theme.TextMuted);
        btnPin.Clicked += () =>
        {
            pinned = !pinned;
            Topmost = pinned;
            btnPin.SetForeground(pinned ? Theme.TextMain : Theme.TextMuted);
        };

        var btnMinimize = new FlatButton("─", Theme.BgHeader, Theme.BgSurface, width: 32, fontSize: 18) { Margin = new Thickness(4, 0, 0, 0) };
        btnMinimize.Clicked += () => WindowState = WindowState.Minimized;

        var btnClose = new FlatButton("✕", Theme.BgHeader, Theme.CloseHov, width: 32, fontSize: 13) { Margin = new Thickness(4, 0, 0, 0) };
        btnClose.Clicked += () => Close();

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        btnRow.Children.Add(btnAction);
        btnRow.Children.Add(btnPin);
        btnRow.Children.Add(btnMinimize);
        btnRow.Children.Add(btnClose);
        Grid.SetColumn(btnRow, 2);
        grid.Children.Add(icon);
        grid.Children.Add(title);
        grid.Children.Add(btnRow);
        header.Child = grid;
        return header;
    }

    private StackPanel BuildBody()
    {
        var body = new StackPanel { Margin = new Thickness(14) };

        body.Children.Add(BuildComboSection("JUNGLE", _jungleCombos, firstSelected: true));
        body.Children.Add(BuildComboSection("SANTORINI", _santoriniCombos, firstSelected: false));

        // checkboxes
        var checkboxLabel = new TextBlock
        {
            Text = "CONFIG",
            Foreground = Theme.TextMuted,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };
        body.Children.Add(checkboxLabel);

        var chkBottom = new FlatCheckBox("Use totem base from someone else", _ext.totemManager.UseBottomFromSomeoneElse);
        chkBottom.Changed += v => _ext.totemManager.UseBottomFromSomeoneElse = v;
        body.Children.Add(chkBottom);

        var chkCenter = new FlatCheckBox("Use totem body from someone else", _ext.totemManager.UseCenterFromSomeoneElse);
        chkCenter.Changed += v => _ext.totemManager.UseCenterFromSomeoneElse = v;
        body.Children.Add(chkCenter);

        // speed slider
        var sliderLabel = new TextBlock
        {
            Text = "SPEED",
            Foreground = Theme.TextMuted,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 10, 0, 6)
        };
        body.Children.Add(sliderLabel);

        var sliderRow = new Grid();
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sliderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var delayValues = new[] { 300, 250, 200, 150, 100, 50 };
        var speedLabels = new[] { "1x", "2x", "3x", "4x", "5x", "Max" };

        var valueLabel = new TextBlock
        {
            Text = "Max",
            Foreground = Theme.TextMuted,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
            MinWidth = 38,
        };

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 5,
            Value = 5,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            SmallChange = 1,
            LargeChange = 1,
            Style = BuildSliderStyle(),
        };

        slider.ValueChanged += (_, e) =>
        {
            int index = (int)e.NewValue;
            valueLabel.Text = speedLabels[index];
            _ext.totemManager.LoopDelay = delayValues[index];
        };

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(valueLabel, 1);
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(valueLabel);
        body.Children.Add(sliderRow);

        // effects farmed counter
        var countRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 10)
        };

        var countTitleLabel = new TextBlock
        {
            Text = "EFFECTS FARMED:",
            Foreground = Theme.TextMuted,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _effectCountLabel = new TextBlock
        {
            Text = "0",
            Foreground = Theme.TextMuted,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };

        countRow.Children.Add(countTitleLabel);
        countRow.Children.Add(_effectCountLabel);
        body.Children.Add(countRow);

        var disclaimer = new TextBlock
        {
            Text = "Effects are blocked client-side to prevent lag. After farming, re-enter the game to see your updated total in the avatar editor.",
            Foreground = Theme.TextMuted,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.6,
        };
        body.Children.Add(disclaimer);

        return body;
    }

    private static Style BuildSliderStyle()
    {
        const string xaml = """
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
               TargetType="Slider">
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Slider">
                        <Grid>
                            <Border Height="4" Background="#323241" CornerRadius="2"
                                    VerticalAlignment="Center"/>
                            <Track Name="PART_Track">
                                <Track.Thumb>
                                    <Thumb Width="14" Height="14" Cursor="Hand">
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Ellipse Name="circle" Fill="#6e6e82"/>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property="IsMouseOver" Value="True">
                                                        <Setter TargetName="circle" Property="Fill" Value="#9e9eb2"/>
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        """;

        return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
    }

    private StackPanel BuildComboSection(string label, (int Combo, string Image, string Label, bool Santorini)[] combos, bool firstSelected)
    {
        var section = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        var sectionLabel = new TextBlock
        {
            Text = label,
            Foreground = Theme.TextMuted,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        section.Children.Add(sectionLabel);

        var grid = new UniformGrid { Columns = 4, Rows = 1 };

        foreach (var (combo, image, name, isSantorini) in combos)
        {
            var card = BuildComboCard(image, name);
            int comboIndex = combo;
            bool santorini = isSantorini;

            if (firstSelected && combo == 1)
            {
                card.BorderBrush = Theme.Green;
                _selectedBorder = card;
            }

            card.MouseLeftButtonUp += (_, _) =>
            {
                if (_selectedBorder != null) _selectedBorder.BorderBrush = Brushes.Transparent;
                card.BorderBrush = Theme.Green;
                _selectedBorder = card;
                _ext.totemManager.SetCombo(comboIndex, santorini);
            };

            grid.Children.Add(card);
        }

        section.Children.Add(grid);
        return section;
    }

    private static Border BuildComboCard(string imageName, string label)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(30, 30, 40)),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 8, 8, 6),
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = Cursors.Hand,
        };

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

        var img = new Image
        {
            Width = 36,
            Height = 36,
            Source = new BitmapImage(new Uri($"pack://application:,,,/UI/assets/{imageName}")),
        };

        var lbl = new TextBlock
        {
            Text = label,
            Foreground = Theme.TextMuted,
            FontSize = 10,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0),
        };

        stack.Children.Add(img);
        stack.Children.Add(lbl);
        card.Child = stack;
        return card;
    }

    private async Task OnStartStop()
    {
        if (!_ext.IsConnected) return;

        if (_running)
            _ext.totemManager.Stop();
        else
            await _ext.totemManager.Start();
    }
}