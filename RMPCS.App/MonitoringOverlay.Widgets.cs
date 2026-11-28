using System.Windows;
using RMPCS.Core;
namespace RMPCS.App;

internal partial class MonitoringOverlay
{
    private readonly List<MonitoringOverlay> widgets = new();
    private bool changingWidgets, activeSession, suppressVisibility;
    private readonly Dictionary<string,MetricDisplay> temperatureDisplays = new(StringComparer.Ordinal);
    private readonly TemperaturePoller temperaturePoller = new();
    private IReadOnlyDictionary<string,double?> temperatureValues = new Dictionary<string,double?>();
    internal bool SessionVisible => activeSession;
    internal event Action? SessionVisibilityChanged;
    internal int WidgetCountForChecks => widgets.Count(w=>w.IsVisible);
    internal bool ChildSamplingForChecks => widgets.Any(w=>w.Sampling);
    internal MonitoringOverlay[] WidgetsForChecks => widgets.ToArray();
    internal bool TransparentForChecks => OverlayFrame.Background is System.Windows.Media.SolidColorBrush brush && brush.Color.A==0;
    internal bool HeaderVisibleForChecks => OverlayHeader.Visibility==Visibility.Visible;
    private IEnumerable<string> DesiredWidgets()
    {
        if(options.Cpu && options.CpuView!.Any()) yield return "CPU";
        if(options.Memory && options.MemoryView!.Any()) yield return "RAM";
        if(options.Network && options.NetworkView!.Any()) {yield return "Download";yield return "Upload";}
        if(options.DiskSpace && options.DiskView!.Any()) yield return "Drive";
        if(options.Uptime && options.UptimeView!.Any()) yield return "Uptime";
        if(options.Temperatures.Enabled && options.Temperatures.Views.Any()) foreach(var source in options.Temperatures.Selected()) yield return "Temp:"+source.Key;
    }
    internal void ShowSession()
    {
        if(widgetKey is not null) throw new InvalidOperationException("Only the shared overlay host owns a session.");
        changingWidgets=true;
        try
        {
            if(options.Widgets)
            {
                suppressVisibility=true; try{Hide();}finally{suppressVisibility=false;}
                var desired=DesiredWidgets().ToArray();
                foreach(var stale in widgets.Where(w=>!desired.Contains(w.widgetKey)).ToArray()){widgets.Remove(stale);stale.Dispose();}
                for(int i=0;i<desired.Length;i++)
                {
                    string key=desired[i]; var child=widgets.FirstOrDefault(w=>w.widgetKey==key);
                    if(child is null){child=new(main,testMode,key);child.Issue+=text=>Issue?.Invoke(text);child.IsVisibleChanged+=WidgetVisibilityChanged;widgets.Add(child);}
                    var saved=WidgetLayout.Parse(options.WidgetLayoutJson).FirstOrDefault(p=>p.Key==key);
                    child.Configure(options with { Widgets=false, Position="Custom", Monitor=saved?.Monitor??options.Monitor, X=saved?.X??(.04+(i%3)*.32),Y=saved?.Y??(.04+(i/3)*.2),Locked=saved?.Locked??options.Locked });
                    child.Show();if(child.Error is not null)throw new InvalidOperationException(child.Error);
                }
                activeSession=widgets.Any(w=>w.IsVisible);
            }
            else {foreach(var child in widgets)child.Dispose();widgets.Clear();Show();activeSession=IsVisible;}
            SyncSampling(); SessionVisibilityChanged?.Invoke();
        }
        finally {changingWidgets=false;}
    }
    internal void HideSession()
    {
        if(widgetKey is not null){Hide();return;}
        changingWidgets=true;
        try {activeSession=false;Hide();foreach(var child in widgets)child.Hide();SyncSampling();temperaturePoller.Reset();ClearReadings();SessionVisibilityChanged?.Invoke();}
        finally {changingWidgets=false;}
    }
    private void WidgetVisibilityChanged(object sender,DependencyPropertyChangedEventArgs e)
    {
        if(changingWidgets || disposed)return;
        activeSession=widgets.Any(w=>w.IsVisible);SyncSampling();
        if(!activeSession){generation++;previous=null;ClearReadings();}
        if(!widgets.Any(w=>w.IsVisible && w.widgetKey!.StartsWith("Temp:",StringComparison.Ordinal)))temperaturePoller.Reset();
        SessionVisibilityChanged?.Invoke();
    }
    private void SyncSampling()
    {
        if(widgetKey is null && activeSession && !testMode && options.HasVisibleMetrics())timer.Start();else timer.Stop();
    }
    internal string CaptureWidgetLayouts()
    {
        var selected=options.Temperatures.Selected().Select(s=>"Temp:"+s.Key).ToHashSet();
        var saved=WidgetLayout.Parse(options.WidgetLayoutJson).Where(p=>WidgetLayout.Metrics.Contains(p.Key)||selected.Contains(p.Key)).ToDictionary(p=>p.Key);
        foreach(var child in widgets){var position=child.CapturePosition();saved[child.widgetKey!]=new(child.widgetKey!,position.Monitor,position.X,position.Y,child.options.Locked);}
        return WidgetLayout.Save(saved.Values);
    }
    internal void UnlockWidgets()
    {
        foreach(var child in widgets)child.Configure(child.options with{Locked=false,ClickThrough=false});
    }
    private void Lock_Click(object sender,RoutedEventArgs e){e.Handled=true;options=options with{Locked=!options.Locked};ApplyVisuals();}
    private OverlayPreferences EffectiveSampling()
    {
        var chosen=options.SamplingChoices();if(!options.Widgets)return chosen;
        var visible=widgets.Where(w=>w.IsVisible).Select(w=>w.widgetKey!).ToHashSet();
        return chosen with{Cpu=chosen.Cpu&&visible.Contains("CPU"),Memory=chosen.Memory&&visible.Contains("RAM"),Network=chosen.Network&&(visible.Contains("Download")||visible.Contains("Upload")),DiskSpace=chosen.DiskSpace&&visible.Contains("Drive"),Uptime=chosen.Uptime&&visible.Contains("Uptime"),
            Temperatures=chosen.Temperatures with{SelectionJson=System.Text.Json.JsonSerializer.Serialize(chosen.Temperatures.Selected().Where(s=>visible.Contains("Temp:"+s.Key)))}};
    }
    private bool Owns(string key)=>widgetKey is null || widgetKey==key;
    private void ConfigureTemperatures()
    {
        var selected=options.Temperatures.Selected().Where(s=>options.Temperatures.Enabled && options.Temperatures.Views.Any() && Owns("Temp:"+s.Key)).ToArray();
        foreach(var stale in temperatureDisplays.Keys.Where(k=>!selected.Any(s=>s.Key==k)).ToArray()){TemperaturePanel.Children.Remove(temperatureDisplays[stale]);temperatureDisplays.Remove(stale);}
        foreach(var source in selected)
        {
            if(!temperatureDisplays.TryGetValue(source.Key,out var display)){display=new();temperatureDisplays.Add(source.Key,display);TemperaturePanel.Children.Add(display);}
            display.Configure(source.Category+" · "+source.Name+" ["+source.Provider+"]","°C · display reference 0–120, not a safety limit",true,options.Temperatures.Views);
        }
    }
    private void RenderTemperatures(IReadOnlyDictionary<string,double?> values)
    {
        foreach(var pair in temperatureDisplays)
        {double? value=values.TryGetValue(pair.Key,out var n)?n:null;string label=value is double c?$"{c:F1} °C":"—";pair.Value.Update(label,label.Replace(" ","\n"),Monitoring.RelativePercent(value,120));}
    }
}
