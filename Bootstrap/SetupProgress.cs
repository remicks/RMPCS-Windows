using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
namespace RMPCS.Bootstrap
{
    internal sealed partial class SetupForm
    {
        private readonly Label operationHeading=new Label(),operationDetail=new Label(),operationHint=new Label();
        private void BuildOperationPage()
        {
            operationHeading.SetBounds(22,22,546,36);operationHeading.Font=new Font("Segoe UI",15,FontStyle.Bold);operationHeading.ForeColor=Ink;
            operationDetail.SetBounds(22,72,546,66);operationDetail.ForeColor=Muted;
            progress.SetBounds(22,155,546,22);progress.Minimum=0;progress.Maximum=100;progress.MarqueeAnimationSpeed=25;progress.AccessibleName="Setup progress";
            operationHint.SetBounds(22,202,546,94);operationHint.ForeColor=Muted;
            operationPage.Controls.AddRange(new Control[]{operationHeading,operationDetail,progress,operationHint});
            status.TextChanged+=delegate {operationDetail.Text=status.Text;};
        }
        private void Stage(string title,string message)
        {
            ShowPage(2);operationHeading.Text=title;status.Text=message;
            progress.Visible=true;progress.Style=ProgressBarStyle.Marquee;
            operationHint.Text="Setup stays open until the operation finishes. A moving bar indicates activity, not an overall percentage.";
        }
        private void PreviewOperation()
        {
            proceed.Text="Working...";proceed.Enabled=cancel.Enabled=false;
            operationHeading.Text="Installing or upgrading RMPCS";operationDetail.Text="Example progress display only. No installation is running.";
            progress.Visible=true;progress.Style=ProgressBarStyle.Blocks;progress.Value=40;
            operationHint.Text="During a real operation, Windows Installer shows detailed progress in its own window. Setup stays open until Windows finishes.";
        }
        private void OperationDidNotComplete()
        {
            ShowPage(2);heading.Text="Setup did not complete";caption.Text="Review the message below before retrying or closing setup.";
            operationHeading.Text="Changes were not confirmed";operationDetail.Text=status.Text;
            progress.Style=ProgressBarStyle.Blocks;progress.Value=0;progress.Visible=true;
            operationHint.Text="Any earlier completed prerequisite installation is kept. Windows Installer handles cancellation and rollback; it is never forcibly terminated by this launcher.";
        }
        private void PortableSessionFinished()
        {
            ShowPage(2);heading.Text="Session finished";caption.Text="The portable application has closed.";operationHeading.Text="RMPCS session finished";
            operationDetail.Text=status.Text;progress.Style=ProgressBarStyle.Blocks;progress.Value=100;
            operationHint.Text="You can close this launcher or open RMPCS again.";proceed.Text="Open again";
        }
        internal void EndProgressCheck(){busy=installing=false;}
        internal async Task<int> VerifyProgressUiAsync()
        {
            int count=0;Action<bool> check=delegate(bool ok){if(!ok)throw new InvalidOperationException("Progress UI check "+(count+1)+" failed.");count++;};
            bool versionFooter=false;foreach(Control parent in Controls)foreach(Control child in parent.Controls)if(child is Label&&child.Text=="DEGENERATES INC.\nVersion "+Program.VersionLabel)versionFooter=true;
            check(Text.EndsWith(Program.VersionLabel)&&versionFooter);
            ShowPage(1);Stage("Preparing setup","Checking fixture status...");
            check(operationPage.Visible&&!optionsPage.Visible&&!finishPage.Visible);
            check(progress.Visible&&progress.Height>=20&&progress.Style==ProgressBarStyle.Marquee);
            status.Text="Fixture phase changed";check(operationDetail.Text==status.Text);
            busy=installing=true;cancel.Enabled=false;
            Close();check(!IsDisposed&&Visible&&operationPage.Visible);
            check(operationDetail.Text.Contains("will not be terminated"));
            busy=installing=false;cancel.Enabled=true;
            status.Text="Fixture error";OperationDidNotComplete();
            check(operationPage.Visible&&!finishPage.Visible&&progress.Value==0&&!completed);
            CompleteSetup(0);check(finishPage.Visible&&!operationPage.Visible&&completed&&proceed.Text=="Finish");
            CompleteSetup(3010);check(finishPage.Visible&&!runAfter.Checked&&!runAfter.Enabled&&summary.Text.Contains("Restart Windows"));
            completed=false;bool failedCompletionBlocked=false;try{CompleteSetup(1603);}catch(InvalidOperationException){failedCompletionBlocked=true;}check(failedCompletionBlocked&&!completed);
            completed=false;status.Text="Fixture portable session finished";PortableSessionFinished();
            check(operationPage.Visible&&progress.Value==100&&proceed.Text=="Open again");
            cancellation.Cancel();bool blocked=false;
            try {await ExecuteAsync(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"never-launch-"+Guid.NewGuid().ToString("N")+".exe"),"",false);}
            catch(OperationCanceledException){blocked=true;}
            check(blocked&&!installing);
            return count;
        }
    }
}
