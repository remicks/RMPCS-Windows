using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;
namespace RMPCS.App;
public partial class MainWindow
{
    private void ApplyInformationPreferences()
    {
        bool technical=preferences.ShowTechnicalNames;
        Dns.Content=technical?"Refresh website address cache (DNS)":"Refresh website address cache";
        Dism.Content=technical?"Repair Windows components (DISM)":"Repair Windows components";
        Sfc.Content=technical?"Check Windows system files (SFC)":"Check Windows system files";
        Prefetch.Content=technical?"Clear app startup cache (Prefetch .pf)":"Clear app startup cache";
        Dhcp.Content=technical?"Reconnect network adapters (DHCP release/renew)":"Reconnect network adapters";
        Thumbnails.Content=FriendlyNames.Cleanup(CleanupArea.Thumbnails,technical);
        CrashDumps.Content=FriendlyNames.Cleanup(CleanupArea.CrashDumps,technical);
        WindowsLogs.Content="Clean Windows troubleshooting logs";
        OverlayCpuChoice.Content=technical?"Processor usage (CPU)":"Processor usage";
        OverlayMemoryChoice.Content=technical?"Memory usage (RAM)":"Memory usage";
        OverlayUptimeChoice.Content="Time since Windows started";
        bool wasLoading=loadingOverlayViews;int metric=OverlayViewMetricChoice.SelectedIndex;loadingOverlayViews=true;
        try{OverlayViewMetricChoice.ItemsSource=new[]{technical?"Processor (CPU)":"Processor",technical?"Memory (RAM)":"Memory","Network (download and upload)","Windows drive free space","Time since Windows started","Temperature sensors"};OverlayViewMetricChoice.SelectedIndex=Math.Max(0,metric);}
        finally{loadingOverlayViews=wasLoading;}
        OverlayViewNumbers.Content=technical?"Standalone numeric values":"Numbers";
        OverlayViewCircle.Content=technical?"Plain circle gauge":"Circle";
        OverlayViewCircleValue.Content="Circle with value inside";
        OverlayViewBar.Content=technical?"Bar gauge":"Bar";
        OverlayViewGraph.Content="Trend graph (last 60 readings)";
        Dism.ToolTip="Uses Windows' DISM tool to repair the component store.";
        Sfc.ToolTip="Uses Windows' System File Checker (SFC).";
        Dns.ToolTip="Clears saved DNS lookups so Windows can fetch website addresses again.";
        ((ComboBoxItem)ScheduleProfile.Items[0]).Content=technical?"Quick cleanup: 7-day temp files + DNS":"Quick cleanup (files older than 7 days)";
        ((ComboBoxItem)ScheduleProfile.Items[1]).Content=technical?"Cleanup + Windows repairs (DISM + SFC)":"Clean up and repair Windows";
        foreach(var details in new[]{MaintenanceDetails,ScheduleDetails,StartupDetails,OverlayDetails,DiagnosticDetails,DriveTechnicalDetails})details.IsExpanded=preferences.ExpandTechnicalDetails;
        UpdateDriveControls();
    }
    private static string SimpleDriveReason(DriveCandidate drive,DriveAvailability allowed,bool recent)
    {
        if(!allowed.Analyze)return "This drive cannot be safely optimized here. See the reason below; no drive operation will run.";
        if(drive.Medium==DriveMedium.SolidState)return "Windows will refresh unused-space information on this SSD. It will not defragment it.";
        if(!allowed.Optimize)return "This hard drive needs at least 15% free space before optimization. You can still check it.";
        return recent?"The drive check completed. Review its report before deciding whether to optimize.":"Check this hard drive first, then review the report before optimizing. The check must be less than 15 minutes old.";
    }
}
