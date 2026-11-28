using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RMPCS.Core;

namespace RMPCS.App;

// Independent presentation controls; no sampling, commands or timers live in a metric view.
public sealed class MetricDisplay : StackPanel
{
    private readonly TextBlock title = new() { FontSize = 11, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock number = new() { TextWrapping = TextWrapping.Wrap, Margin = new(0, 3, 0, 4) };
    private readonly MetricRing circle = new(false), valueCircle = new(true);
    private readonly MetricBar bar = new();
    private readonly Polyline graph = new() { Height = 26, Stretch = Stretch.None, StrokeThickness = 1.5, Margin = new(0, 5, 0, 4), IsHitTestVisible = false };
    private readonly TextBlock legend = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new(0, 3, 0, 0) };
    private readonly Queue<double> history = new();
    private MetricViewPreferences views = new();
    private string reference = "";
    internal int HistoryCount => history.Count;
    internal bool[] VisibleViews => new[] { number.Visibility, circle.Visibility, valueCircle.Visibility, bar.Visibility, graph.Visibility }.Select(v => v == Visibility.Visible).ToArray();
    internal bool UnknownGauge => circle.Level is null && valueCircle.Level is null && bar.Level is null;
    public MetricDisplay()
    {
        Margin = new(0, 0, 0, 12);
        title.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        legend.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        graph.SetResourceReference(Shape.StrokeProperty, "AccentTextBrush");
        var rings = new WrapPanel(); rings.Children.Add(circle); rings.Children.Add(valueCircle);
        Children.Add(title); Children.Add(number); Children.Add(rings); Children.Add(bar); Children.Add(graph); Children.Add(legend);
        Clear();
    }
    internal void Configure(string label, string scale, bool enabled, MetricViewPreferences selected)
    {
        views = selected; reference = scale; title.Text = label;
        Visibility = enabled && selected.Any() ? Visibility.Visible : Visibility.Collapsed;
        number.Visibility = selected.Numbers ? Visibility.Visible : Visibility.Collapsed;
        circle.Visibility = selected.Circle ? Visibility.Visible : Visibility.Collapsed;
        valueCircle.Visibility = selected.CircleWithValue ? Visibility.Visible : Visibility.Collapsed;
        bar.Visibility = selected.Bar ? Visibility.Visible : Visibility.Collapsed;
        graph.Visibility = selected.Graph ? Visibility.Visible : Visibility.Collapsed;
        legend.Visibility = selected.Circle || selected.CircleWithValue || selected.Bar || selected.Graph ? Visibility.Visible : Visibility.Collapsed;
        if (!enabled || !selected.Graph) { history.Clear(); graph.Points = new(); }
        circle.InvalidateVisual(); valueCircle.InvalidateVisual(); bar.InvalidateVisual();
    }
    internal void Clear()
    {
        number.Text = "—"; legend.Text = reference + " · unavailable or warming up";
        circle.Update(null, "—"); valueCircle.Update(null, "—"); bar.Update(null);
        history.Clear(); graph.Points = new();
    }
    internal void Update(string numeric, string inside, double? level)
    {
        if (level is double invalid && (!double.IsFinite(invalid) || invalid < 0)) level = null;
        number.Text = numeric; ToolTip = title.Text + ": " + numeric + "\n" + reference;
        legend.Text = reference + (level is null ? " · gauge unavailable" : level > 100 ? " · above reference" : "");
        circle.Update(level, ""); valueCircle.Update(level, inside); bar.Update(level);
        if (!views.Graph || Visibility != Visibility.Visible || level is not double value) { history.Clear(); graph.Points = new(); return; }
        history.Enqueue(Math.Clamp(value, 0, 100)); while (history.Count > 60) history.Dequeue();
        var points = new PointCollection(); double width = Math.Max(1, ActualWidth); int i = 0;
        foreach (double sample in history) points.Add(new(i++ * width / 59, 25 - sample * .24));
        graph.Points = points;
    }
}

internal sealed class MetricRing : Control
{
    private readonly bool showValue;
    private string text = "—";
    internal double? Level { get; private set; }
    internal MetricRing(bool showValue) { this.showValue = showValue; Width = Height = 76; Margin = new(0, 2, 6, 4); IsHitTestVisible = false; }
    internal void Update(double? value, string label) { Level = value; text = label; InvalidateVisual(); }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        Brush Brush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
        var center = new Point(ActualWidth / 2, ActualHeight / 2); double radius = Math.Max(1, Math.Min(ActualWidth, ActualHeight) / 2 - 5);
        drawing.DrawEllipse(null, new Pen(Brush("BorderBrush"), 1), center, radius, radius);
        if (Level is double raw)
        {
            double value = Math.Clamp(raw, 0, 100); var pen = new Pen(Brush("AccentTextBrush"), 5);
            if (value >= 100) drawing.DrawEllipse(null, pen, center, radius, radius);
            else if (value > 0)
            {
                double angle = value / 100 * 2 * Math.PI;
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    context.BeginFigure(new(center.X, center.Y - radius), false, false);
                    context.ArcTo(new(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle)), new(radius, radius), 0, value > 50, SweepDirection.Clockwise, true, false);
                }
                geometry.Freeze(); drawing.DrawGeometry(null, pen, geometry);
            }
        }
        if (showValue)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
                Math.Min(14, FontSize), Brush("TextBrush"), VisualTreeHelper.GetDpi(this).PixelsPerDip) { TextAlignment = TextAlignment.Center, MaxTextWidth = 62, MaxTextHeight = 62, Trimming = TextTrimming.CharacterEllipsis };
            drawing.DrawText(formatted, new(center.X - 31, center.Y - formatted.Height / 2));
        }
    }
}

internal sealed class MetricBar : Control
{
    internal double? Level { get; private set; }
    internal MetricBar() { Height = 10; Margin = new(0, 3, 0, 4); IsHitTestVisible = false; }
    internal void Update(double? value) { Level = value; InvalidateVisual(); }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        Brush Brush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
        double width = Math.Max(0, ActualWidth - 1), height = Math.Max(0, ActualHeight - 1);
        drawing.DrawRoundedRectangle(Brush("RaisedBrush"), new Pen(Brush("BorderBrush"), 1), new(.5, .5, width, height), 4, 4);
        if (Level is double value && value > 0) drawing.DrawRoundedRectangle(Brush("AccentTextBrush"), null, new(.5, .5, width * Math.Clamp(value, 0, 100) / 100, height), 4, 4);
    }
}
