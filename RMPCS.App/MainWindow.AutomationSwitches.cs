using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace RMPCS.App;
public partial class MainWindow
{
    private bool autostartEnabled, scheduleEnabled, automationStatusKnown;
    private void AutostartToggle_Click(object sender,RoutedEventArgs e)
    {
        bool requested=AutostartSwitch.IsChecked==true;AutostartSwitch.SetCurrentValue(ToggleButton.IsCheckedProperty,autostartEnabled);
        if(busy||!admin||TestMode||!automationStatusKnown||!TaskSchedulerService.IsInstalled)return;
        if(requested)StartupOn_Click(sender,e);
        else if(Confirm("Turn off RMPCS sign-in startup for this Windows user?\n\nThe app will no longer open automatically. Maintenance schedules are separate and are left as they are."))ChangeAutomation(()=>TaskSchedulerService.Remove(true));
    }
    private void ScheduleEnabledToggle_Click(object sender,RoutedEventArgs e)
    {
        bool requested=ScheduleEnabledSwitch.IsChecked==true;ScheduleEnabledSwitch.SetCurrentValue(ToggleButton.IsCheckedProperty,scheduleEnabled);
        if(busy||!admin||TestMode||!automationStatusKnown||!TaskSchedulerService.IsInstalled)return;
        if(requested)SaveSchedule_Click(sender,e);
        else if(Confirm("Turn off automatic maintenance and remove its saved schedule for this Windows user?\n\nAn active repair will still be allowed to finish. Sign-in startup is separate."))ChangeAutomation(()=>TaskSchedulerService.Remove(false));
    }
}
