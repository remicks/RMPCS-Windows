using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;

namespace RMPCS.App;

internal sealed class SetupGuidePreview : Window
{
    private readonly SetupGuide guide = new();
    private readonly TextBlock title = new() { FontSize = 22, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock body = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 18), FontSize = 15 };
    private readonly TextBlock progress = new() { Margin = new Thickness(0, 0, 0, 12) };
    private readonly Button back = new() { Content = "Back" }, next = new() { Content = "Next" };
    internal SetupGuidePreview(Window owner)
    {
        Owner = owner; Title = "Getting started — guide preview"; Width = 570; SizeToContent = SizeToContent.Height;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        SourceInitialized += (_, _) => ThemeManager.ApplyTitleBar(this);
        SetResourceReference(BackgroundProperty, "WindowBrush"); SetResourceReference(ForegroundProperty, "TextBrush"); SetResourceReference(FontFamilyProperty, "UiFontFamily");
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(progress); panel.Children.Add(title); panel.Children.Add(body);
        var note = new TextBlock { Text = "v0.14.0 foundation preview · no choices are applied", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) };
        note.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush"); panel.Children.Add(note);
        var buttons = new WrapPanel(); var close = new Button { Content = "Close preview", IsCancel = true };
        back.Click += (_, _) => { guide.Back(); Refresh(); }; next.Click += (_, _) => { if (guide.CanNext) { guide.Next(); Refresh(); } else Close(); };
        close.Click += (_, _) => Close(); buttons.Children.Add(back); buttons.Children.Add(next); buttons.Children.Add(close); panel.Children.Add(buttons);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Refresh();
    }
    private void Refresh() { title.Text = guide.Current.Title; body.Text = guide.Current.Body; progress.Text = $"STEP {guide.Index + 1} OF {SetupGuide.Steps.Count}"; back.IsEnabled = guide.CanBack; next.Content = guide.CanNext ? "Next" : "Finish preview"; }
    internal int VerifyNavigationForChecks()
    {
        if (Owner is not MainWindow { TestMode: true }) throw new InvalidOperationException("Guide checks require a test owner.");
        int checks = 0;
        for (int i = 0; i < SetupGuide.Steps.Count; i++)
        {
            if (title.Text != SetupGuide.Steps[i].Title || body.Text != SetupGuide.Steps[i].Body || back.IsEnabled != (i > 0)) throw new InvalidOperationException("Guide navigation mismatch.");
            checks++; if (i + 1 < SetupGuide.Steps.Count) next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        for (int i = 1; i < SetupGuide.Steps.Count; i++) back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (guide.Index != 0 || back.IsEnabled) throw new InvalidOperationException("Guide Back did not return to the first step.");
        return checks + 1;
    }
}
