using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;
namespace RMPCS.App;

public partial class MainWindow
{
    private readonly Dictionary<string,TemperatureSource> sensorCatalog=new(StringComparer.Ordinal),selectedSensors=new(StringComparer.Ordinal);
    private bool loadingSensors;
    private string widgetLayoutDraft="[]";
    private void InitializeTemperatureControls()
    {
        TemperatureProviderChoice.ItemsSource=TemperaturePolicy.Providers;TemperatureCategoryChoice.ItemsSource=new[]{"All categories"}.Concat(TemperaturePolicy.Categories).ToArray();
        TemperatureProviderChoice.SelectedIndex=TemperatureCategoryChoice.SelectedIndex=0;
    }
    private void PutTemperatureControls()
    {
        loadingSensors=true;
        try
        {
            var options=preferences.Overlay.Temperatures;TemperatureEnabledChoice.IsChecked=options.Enabled;TemperatureOnboardChoice.IsChecked=options.Onboard;
            selectedSensors.Clear();foreach(var source in options.Selected()){selectedSensors[source.Key]=source;sensorCatalog[source.Key]=source;}
        }
        finally{loadingSensors=false;}
        RefreshSensorRows();
    }
    private void RefreshSensorRows()
    {
        if(TemperatureSourcesList is null || loadingSensors)return;
        loadingSensors=true;
        try
        {
            string? provider=TemperatureProviderChoice.SelectedItem as string,category=TemperatureCategoryChoice.SelectedItem as string;
            var rows=sensorCatalog.Values.Where(s=>s.Provider==provider&&(category=="All categories"||s.Category==category)).OrderBy(s=>s.Category).ThenBy(s=>s.Name).ToArray();
            TemperatureSourcesList.ItemsSource=rows;foreach(var row in rows)if(selectedSensors.ContainsKey(row.Key))TemperatureSourcesList.SelectedItems.Add(row);
        }
        finally{loadingSensors=false;}
    }
    private void TemperatureFilter_Changed(object sender,SelectionChangedEventArgs e)=>RefreshSensorRows();
    private void TemperatureSelection_Changed(object sender,SelectionChangedEventArgs e)
    {
        if(loadingSensors)return;
        foreach(TemperatureSource source in e.RemovedItems)selectedSensors.Remove(source.Key);
        foreach(TemperatureSource source in e.AddedItems)selectedSensors[source.Key]=source;
        if(selectedSensors.Count>8)
        {
            foreach(TemperatureSource source in e.AddedItems)selectedSensors.Remove(source.Key);
            RefreshSensorRows();TemperatureStatus.Text="Select at most eight sensors across all categories/providers.";
        }
        else TemperatureStatus.Text=$"{selectedSensors.Count}/8 temperature sources selected. Apply overlay settings to save.";
    }
    private TemperaturePreferences TemperatureChoices(MetricViewPreferences views)=>new TemperaturePreferences{Enabled=TemperatureEnabledChoice.IsChecked==true,Onboard=TemperatureOnboardChoice.IsChecked==true,Views=views,SelectionJson=JsonSerializer.Serialize(selectedSensors.Values.Select(s=>s with{Name=TemperaturePolicy.Clean(s.Name,48)}))}.Normalize();
    private async void TemperatureDiscover_Click(object sender,RoutedEventArgs e)
    {
        if(busy||TestMode)return;
        string provider=TemperatureProviderChoice.SelectedItem as string??TemperaturePolicy.Providers[0];
        if(provider=="In-app hardware" && TemperatureOnboardChoice.IsChecked!=true){TemperatureStatus.Text="Explicitly enable in-app low-level reads after reviewing/installing the optional components.";return;}
        SetBusy(true);StopButton.IsEnabled=false;TemperatureStatus.Text="Reading the selected local provider. No driver will be installed by discovery…";
        try
        {
            var readings=await TemperatureProbe.QueryAsync(provider,TemperatureOnboardChoice.IsChecked==true);
            foreach(var key in sensorCatalog.Values.Where(s=>s.Provider==provider&&!selectedSensors.ContainsKey(s.Key)).Select(s=>s.Key).ToArray())sensorCatalog.Remove(key);
            foreach(var row in readings)sensorCatalog[row.Source.Key]=row.Source;
            RefreshSensorRows();TemperatureStatus.Text=$"{readings.Count} sensor entries reported. Select category/sensors, then Apply. Provider values are not independently hardware-verified.";
        }
        catch(Exception ex)when(ex is not OutOfMemoryException){TemperatureStatus.Text="Provider unavailable, blocked, busy or timed out. No fallback or driver installation was attempted.";}
        finally{if(provider=="In-app hardware")TemperatureProbe.CloseOnboard();SetBusy(false);}
    }
    private async void TemperatureSetup_Click(object sender,RoutedEventArgs e)
    {
        if(busy||TestMode)return;
        SetBusy(true);StopButton.IsEnabled=false;
        try
        {
            int code=await SensorSetupWindow.OpenAsync(this);
            TemperatureStatus.Text=code==0?"Optional components verified. Enable in-app reads, discover/select sensors and Apply overlay settings. Restart first if the vendor requested it.":code==3010?"Restart Windows before using in-app sensors. Monitoring remains unchanged.":"Optional setup was skipped, cancelled or unsuccessful. RMPCS remains available; see Help > Sensor setup troubleshooting or retry this button.";
        }
        catch(Exception ex)when(ex is not OutOfMemoryException){TemperatureStatus.Text="Optional setup could not open: "+ex.Message+" See Help > Sensor setup troubleshooting.";}
        finally{SetBusy(false);}
    }
    private void UnlockWidgets_Click(object sender,RoutedEventArgs e)
    {
        monitoringOverlay?.UnlockWidgets();OverlayClickThroughChoice.IsChecked=false;OverlayStatus.Text="Visible widgets unlocked and click-through disabled for this session. Remember positions & locks, then Apply to keep changes.";
    }
}
