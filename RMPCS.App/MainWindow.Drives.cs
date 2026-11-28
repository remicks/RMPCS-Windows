using System.IO;
using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;

namespace RMPCS.App;

public partial class MainWindow
{
    private DriveAnalysis? driveAnalysis;
    private void DriveSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        driveAnalysis = null;
        if (DriveDetails is not null) UpdateDriveControls();
    }
    private void UpdateDriveControls()
    {
        if (DriveChoice is null || DriveAnalyzeButton is null || DriveOptimizeButton is null) return;
        DriveRefreshButton.IsEnabled = !busy;
        var drive = DriveChoice.SelectedItem as DriveCandidate;
        var availability = drive is null ? null : DriveOptimization.Availability(drive);
        DriveChoice.IsEnabled = !busy;
        DriveAnalyzeButton.IsEnabled = !busy && admin && availability?.Analyze == true;
        bool recent = driveAnalysis is not null && drive is not null && driveAnalysis.Identity == drive.Identity &&
            DateTimeOffset.UtcNow - driveAnalysis.Time is var age && age >= TimeSpan.Zero && age <= TimeSpan.FromMinutes(15);
        DriveOptimizeButton.IsEnabled = !busy && admin && availability?.Optimize == true && (drive?.Medium != DriveMedium.HardDisk || recent);
        DriveOptimizeButton.Content = availability?.Analyze==true?FriendlyNames.DriveAction(drive!.Medium,preferences.ShowTechnicalNames):"Optimization unavailable";
        DriveDetails.Text = drive is null ? "Refresh drives to read storage information. Nothing runs automatically." :
            (drive.Capacity == 0 ? "" : $"{drive.Free / 1000000000d:F1} GB free of {drive.Capacity / 1000000000d:F1} GB ({100d * drive.Free / drive.Capacity:F0}%).\n") +
            SimpleDriveReason(drive,availability!,recent)+(availability?.Analyze!=true?"\nReason: "+(drive.Note.Length>0?drive.Note:availability!.Reason):"")+(!admin?"\nRun RMPCS as administrator to check or optimize drives.":"");
        DriveTechnicalText.Text=drive is null?"Select a drive to see its technical details.":$"Name: {drive.Display}\nDrive letter: {drive.Root}\nStable volume path: {(drive.VolumePath.Length>0?drive.VolumePath:"Unavailable")}\nMedia: {drive.Medium}; TRIM support: {(drive.Medium==DriveMedium.Unknown?"Not verified":drive.TrimSupported?"Yes":"No")}\nWindows safety checks: {availability?.Reason}\nSpace: {drive.Free/1073741824d:F1} GiB free / {drive.Capacity/1073741824d:F1} GiB total\n\nNames and labels are display-only. Actions use the verified volume identity. SSDs receive ReTRIM, never traditional defragmentation.";
    }
    private async void DriveRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (busy || TestMode) return;
        SetBusy(true); StopButton.IsEnabled = false; DriveStatus.Text = "Finding drives and checking which operations are safe. No optimization is running.";
        try
        {
            driveAnalysis = null;
            DriveChoice.ItemsSource = await Task.Run(DriveOptimizationService.Discover);
            DriveChoice.SelectedIndex = DriveChoice.Items.Count > 0 ? 0 : -1;
            DriveStatus.Text = "Drives refreshed. Choose a drive to see what is available. Unverified drives stay blocked; checking and optimization require administrator rights.";
        }
        catch (Exception ex) { DriveChoice.ItemsSource = null; DriveStatus.Text = "Drive discovery unavailable: " + ex.Message; }
        finally { SetBusy(false); }
    }
    private async void DriveAnalyze_Click(object sender, RoutedEventArgs e) => await RunDriveAsync(DriveAction.Analyze);
    private async void DriveOptimize_Click(object sender, RoutedEventArgs e)
    {
        if (DriveChoice.SelectedItem is DriveCandidate drive)
            await RunDriveAsync(drive.Medium == DriveMedium.HardDisk ? DriveAction.Defragment : DriveAction.ReTrim);
    }
    private async Task RunDriveAsync(DriveAction action)
    {
        if (busy || !admin || TestMode || DriveChoice.SelectedItem is not DriveCandidate drive) return;
        try { _ = DriveOptimization.Arguments(drive, action); }
        catch (InvalidOperationException ex) { DriveStatus.Text = ex.Message; return; }
        if (action != DriveAction.Analyze && !Confirm($"{FriendlyNames.DriveAction(drive.Medium,preferences.ShowTechnicalNames)} — {drive.Display}?\n\nSave your work and connect AC power. Windows will handle this drive at low priority. {(action==DriveAction.Defragment?"This hard drive will be defragmented.":"This SSD receives ReTRIM: unused-space information is refreshed. It will not be defragmented.")}\n\nKeep the PC powered on. RMPCS waits for the operation to finish; it does not forcibly stop it. Windows' optimization schedule is not changed.")) return;
        SetBusy(true); StopButton.IsEnabled = false;
        string operation = action switch { DriveAction.Analyze => "Checking drive", DriveAction.Defragment => "Optimizing hard drive", _ => "Optimizing SSD" };
        DriveStatus.Text = Status.Text = operation + " — Windows output appears in Results & activity. Wait for completion.";
        ShowSidebarResults();
        try
        {
            using var lease = RunLease.Acquire();
            if (lease is null) throw new InvalidOperationException("Another RMPCS maintenance session is active. Try again later.");
            using var session = new SessionLog("Drive " + action);
            void Report(string text) { session.Write(text); Log(text); }
            var previous = driveAnalysis;
            if (action == DriveAction.Analyze) driveAnalysis = null;
            Report($"Requested {action} on {drive.Root}; revalidating identity and safety checks before launch.");
            int code;
            try { code = await DriveOptimizationService.RunAsync(drive, action, previous, Report); }
            catch (Exception ex) { Report("Blocked or failed: " + ex.Message); throw; }
            if (code == 0 && action == DriveAction.Analyze) driveAnalysis = new(drive.Identity, DateTimeOffset.UtcNow);
            if (action != DriveAction.Analyze) driveAnalysis = null; // Never reuse authorization for repeated HDD passes.
            DriveStatus.Text = Status.Text = code == 0 ? "Windows completed the requested operation. Review the report; this does not guarantee improved performance." :
                $"Windows reported failure (exit code {code}). Review the report. No alternate operation was attempted.";
            Report(DriveStatus.Text);
            if (session.Error is not null) DriveStatus.Text += " The saved log may be incomplete; review the activity view.";
        }
        catch (Exception ex) { driveAnalysis = null; DriveStatus.Text = Status.Text = "Drive operation did not complete: " + ex.Message; Log(DriveStatus.Text); }
        finally { SetBusy(false); RefreshHistory(); }
    }

    internal void VerifyDriveUi(string output)
    {
        if (!TestMode) throw new InvalidOperationException("Drive UI checks require an isolated test session.");
        int checks = 0;
        var initial=preferences;int originalAge=Age.SelectedIndex;Age.SelectedIndex=3;var initialPlan=SelectedPlan();
        void Check(bool value) { if (!value) throw new InvalidDataException($"Drive UI guard {checks+1} failed."); checks++; }
        MainTabs.SelectedIndex = Array.IndexOf(AppPreferences.Pages, "Drives");
        Check(MainTabs.SelectedIndex >= 0 && PageCatalog.All[MainTabs.SelectedIndex].Key == "Drives");
        Check(DriveChoice.Items.Count == 0 && !DriveAnalyzeButton.IsEnabled && !DriveOptimizeButton.IsEnabled);
        var ssd = new DriveCandidate(@"Z:\", @"\\?\Volume{11111111-1111-1111-1111-111111111111}\", DriveMedium.SolidState, true, true, 1000000000000UL, 500000000000UL, "fixture", "Fixture only"){Label="Games"};
        DriveChoice.ItemsSource = new[] { ssd, ssd with { Medium = DriveMedium.HardDisk, TrimSupported = false }, ssd with { Eligible = false } };
        DriveChoice.SelectedIndex = 0; UpdateDriveControls();
        Check(DriveOptimizeButton.Content as string == "Optimize SSD" && DriveOptimizeButton.IsEnabled == admin);
        Check(ssd.Display=="Games (Z:) · SSD"&&!DriveTechnicalDetails.IsExpanded&&!DriveDetails.Text.Contains("Volume{"));
        DriveChoice.ApplyTemplate();UpdateLayout();
        Check(DriveChoice.Template.FindName("SelectionPresenter",DriveChoice) is ContentPresenter presenter&&HasText(presenter,ssd.Display));
        SaveDrivePreview("simple");
        preferences=preferences with{ShowTechnicalNames=true,ExpandTechnicalDetails=true};PutPreferenceControls();ApplyPreferences();
        Check(DriveOptimizeButton.Content as string=="Optimize SSD (ReTRIM)"&&DriveTechnicalDetails.IsExpanded&&DriveTechnicalText.Text.Contains(ssd.VolumePath));
        SaveDrivePreview("details");
        Check(Dns.Content.ToString()!.Contains("DNS")&&MaintenanceDetails.IsExpanded&&StartupDetails.IsExpanded&&OverlayDetails.IsExpanded&&DiagnosticDetails.IsExpanded&&ScheduleDetails.IsExpanded);
        Check(ReferenceEquals(DriveChoice.SelectedItem,ssd)&&SelectedPlan()==initialPlan);
        preferences=initial;PutPreferenceControls();ApplyPreferences();
        Check(!DriveTechnicalDetails.IsExpanded&&!Dns.Content.ToString()!.Contains("DNS")&&SelectedPlan()==initialPlan);
        DriveChoice.SelectedIndex = 1; Check(!DriveOptimizeButton.IsEnabled);
        driveAnalysis = new("fixture", DateTimeOffset.UtcNow); UpdateDriveControls(); Check(DriveOptimizeButton.IsEnabled == admin);
        SetBusy(true); Check(!DriveOptimizeButton.IsEnabled && !DriveAnalyzeButton.IsEnabled && !DriveRefreshButton.IsEnabled);
        SetBusy(false); DriveChoice.SelectedIndex = 2; Check(!DriveOptimizeButton.IsEnabled && !DriveAnalyzeButton.IsEnabled);
        DriveChoice.ItemsSource = null; driveAnalysis = null; UpdateDriveControls();
        Age.SelectedIndex=originalAge;
        File.WriteAllText(output, $"{checks} drive UI checks passed. Synthetic drives only; no discovery, device access, defrag process, preferences or maintenance invoked.\n");
        void SaveDrivePreview(string mode)
        {
            UpdateLayout();var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(this);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));using var stream=File.Create(Path.ChangeExtension(output,"."+mode+".png"));encoder.Save(stream);
        }
        static bool HasText(DependencyObject parent,string expected)
        {
            if(parent is TextBlock text && text.Text==expected)return true;
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)if(HasText(System.Windows.Media.VisualTreeHelper.GetChild(parent,i),expected))return true;
            return false;
        }
    }
}
