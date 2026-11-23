using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Diagnostics;
using Microsoft.Win32;

namespace RMPCS.Bootstrap
{
    internal sealed partial class SetupForm
    {
        private readonly Panel requirementsPage=new Panel(), optionsPage=new Panel(), operationPage=new Panel(), finishPage=new Panel();
        private readonly Label heading=new Label(), caption=new Label(), summary=new Label();
        private readonly CheckBox desktopChoice=new CheckBox(), menuChoice=new CheckBox(), adminChoice=new CheckBox(), runAfter=new CheckBox();
        private readonly Button back=new Button();
        private readonly Label[] steps=new Label[4];
        private int page;
        private bool completed;
        private static readonly Color Ink=Color.FromArgb(28,36,48), Muted=Color.FromArgb(94,105,121), Accent=Color.FromArgb(190,43,78);
        private static Label TextLabel(string text,int x,int y,int w,int h,float size,Color color)
        { return new Label { Text=text,Left=x,Top=y,Width=w,Height=h,ForeColor=color,Font=new Font("Segoe UI",size),BackColor=Color.Transparent }; }
        private static void StyleButton(Button button,bool primary)
        {
            button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderSize=primary?0:1;
            button.FlatAppearance.BorderColor=Color.FromArgb(211,218,227);
            button.BackColor=primary?Accent:Color.White; button.ForeColor=primary?Color.White:Ink;
            button.Font=new Font("Segoe UI",10); button.Cursor=Cursors.Hand; button.UseVisualStyleBackColor=false;
        }
        private static bool ReadChoice(string name)
        {
            try { using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64)) using(var key=machine.OpenSubKey(@"SOFTWARE\DegeneratesInc\RMPCS\InstallOptions")) { object value=key==null?null:key.GetValue(name); return value==null || (value as string)=="1"; } }
            catch { return true; }
        }
        private static void Choice(Panel page,CheckBox box,string text,string detail,int y,bool selected)
        {
            box.SetBounds(22,y,528,28); box.Text=text; box.Checked=selected; box.ForeColor=Ink; box.Font=new Font("Segoe UI",11); box.Cursor=Cursors.Hand;
            page.Controls.Add(box); page.Controls.Add(TextLabel(detail,45,y+30,494,36,9,Muted));
        }
        public SetupForm()
        {
            Text=Program.Installer?"RMPCS Setup":"RMPCS Portable";
            AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(900,610); StartPosition=FormStartPosition.CenterScreen;
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false;
            BackColor=Color.FromArgb(245,247,250); ForeColor=Ink; Font=new Font("Segoe UI",10);
            Icon=Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            Bitmap brandImage;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BrandArtwork")) using(var image=Image.FromStream(stream)) brandImage=new Bitmap(image);
            var rail=new Panel {Left=0,Top=0,Width=232,Height=610,BackColor=Color.White};
            var artwork=new PictureBox {Left=26,Top=30,Width=58,Height=58,SizeMode=PictureBoxSizeMode.Zoom,Image=brandImage,AccessibleName="RMPCS icon"};
            rail.Controls.Add(artwork);
            rail.Controls.Add(TextLabel("RMPCS",94,35,130,32,20,Ink));
            rail.Controls.Add(TextLabel("Rusty\u0027s PC Toolkit",96,70,128,24,9,Muted));
            rail.Controls.Add(TextLabel("Windows\nmaintenance",26,125,186,80,16,Ink));
            string[] labels={"01   Requirements","02   Your preferences","03   Installation","04   Finished"};
            for(int i=0;i<4;i++) {steps[i]=TextLabel(labels[i],26,250+i*52,190,30,11,Muted);rail.Controls.Add(steps[i]);}
            rail.Controls.Add(TextLabel("DEGENERATES INC.\nVersion 0.9",26,539,190,44,9,Muted));
            Controls.Add(rail);
            heading.SetBounds(270,34,590,44); heading.Font=new Font("Segoe UI",25,FontStyle.Bold);
            caption.SetBounds(272,92,588,50); caption.ForeColor=Muted;
            foreach(var panel in new[] {requirementsPage,optionsPage,operationPage,finishPage}) {panel.SetBounds(270,158,590,331);panel.BackColor=Color.White;Controls.Add(panel);}
            requirementsPage.Controls.Add(TextLabel("Everything you need, in one place",22,22,546,32,15,Ink));
            status.SetBounds(22,67,546,84); status.ForeColor=Muted;
            status.Text=Program.RuntimePresent()?"Microsoft .NET 10 Desktop Runtime is ready.\nContinue to choose how RMPCS is installed.":"Microsoft .NET 10 Desktop Runtime is required.\nSetup can install the verified Microsoft runtime for you.";
            requirementsPage.Controls.Add(status);
            prerequisite.SetBounds(22,160,546,48); prerequisite.Text="Install the missing Microsoft Desktop Runtime"; prerequisite.Checked=true; prerequisite.Visible=!Program.RuntimePresent();
            requirementsPage.Controls.Add(prerequisite);
            offline.SetBounds(22,215,265,36); offline.Text="Use offline runtime installer"; offline.Visible=!Program.RuntimePresent();
            offline.Click+=delegate {using(var dialog=new OpenFileDialog {Filter="Microsoft runtime installer (*.exe)|*.exe"}) if(dialog.ShowDialog(this)==DialogResult.OK){offlinePath=dialog.FileName; prerequisite.Checked=true;status.Text="Offline installer selected. Its checksum and Microsoft signature will be verified before it runs.";}};
            requirementsPage.Controls.Add(offline);
            var help=new LinkLabel {Left=22,Top=275,Width=546,Height=30,Text="Official Microsoft runtime downloads",LinkColor=Accent};
            help.LinkClicked+=delegate {Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/en-us/download/dotnet/10.0"){UseShellExecute=true});};requirementsPage.Controls.Add(help);
            Choice(optionsPage,desktopChoice,"Create a desktop shortcut","Keep RMPCS within easy reach on your desktop.",20,ReadChoice("Desktop"));
            Choice(optionsPage,menuChoice,"Add RMPCS to the Start Menu","Find the toolkit through Start and Windows Search.",110,ReadChoice("StartMenu"));
            Choice(optionsPage,adminChoice,"Always request administrator access","Windows will show its normal permission prompt when needed.",200,ReadChoice("AlwaysAdmin"));
            optionsPage.Controls.Add(TextLabel("Installs to Program Files. Startup and schedules remain off by default.",22,294,546,26,9,Muted));
            finishPage.Controls.Add(TextLabel("\u2713",22,20,62,58,34,Color.FromArgb(24,132,89)));
            finishPage.Controls.Add(TextLabel("Your toolkit is ready",95,34,455,38,18,Ink));
            summary.SetBounds(22,108,546,92); summary.ForeColor=Muted;finishPage.Controls.Add(summary);
            Choice(finishPage,runAfter,"Run RMPCS after installation","Opens the app only. No maintenance starts automatically.",221,true);
            BuildOperationPage();
            back.SetBounds(270,538,100,40);back.Text="Back";back.Click+=delegate {ShowPage(0);};
            cancel.SetBounds(638,538,100,40);cancel.Text="Cancel";cancel.Click+=delegate {if(busy && !installing){cancellation.Cancel();status.Text="Cancellation requested. Waiting for the current preparation step to finish.";}else if(!busy)Close();};
            proceed.SetBounds(750,538,110,40);proceed.Text="Continue";proceed.Click+=async delegate {
                if(completed){if(runAfter.Checked){try{Process.Start(new ProcessStartInfo(InstalledExe()){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(InstalledExe())});}catch(Exception ex){MessageBox.Show(this,"RMPCS is installed, but could not open.\n\n"+ex.Message,"Launch RMPCS");return;}}Close();}
                else if(Program.Installer && page==0)ShowPage(1);
                else await ContinueAsync();
            };
            StyleButton(back,false);StyleButton(cancel,false);StyleButton(offline,false);StyleButton(proceed,true);
            Controls.AddRange(new Control[]{heading,caption,back,cancel,proceed});
            AcceptButton=proceed;CancelButton=cancel;
            ShowPage(0);
            FormClosed+=delegate {brandImage.Dispose();cancellation.Dispose();};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){e.Cancel=true;if(!installing)cancellation.Cancel();status.Text=installing?"Finish or cancel the Windows/Microsoft installer first. It will not be terminated by this launcher.":"Cancellation requested. Waiting for the current preparation step to finish.";}};
        }
        private static string InstalledExe() {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Degenerates Inc","RMPCS","RMPCS.exe");}
        private void ShowPage(int next)
        {
            page=next;requirementsPage.Visible=next==0;optionsPage.Visible=next==1;operationPage.Visible=next==2;finishPage.Visible=next==3;
            heading.Text=next==0?"Welcome to RMPCS":next==1?"Make it yours":next==2?"Working on it":"You're all set";
            caption.Text=next==0?"A clean start for your Windows maintenance toolkit.":next==1?"Choose your shortcuts and how the app requests permissions.":next==2?"Keep setup open while Windows completes this operation.":"Installation is complete. Choose what happens next.";
            back.Visible=Program.Installer && next==1 && !busy;
            proceed.Text=next==3?"Finish":next==2?(busy?"Working...":"Retry"):next==1?"Install":Program.Installer?"Continue":"Open RMPCS";
            for(int i=0;i<steps.Length;i++){steps[i].ForeColor=i==next?Accent:Muted;steps[i].Font=new Font("Segoe UI",11,i==next?FontStyle.Bold:FontStyle.Regular);}
        }
        internal void PreviewPage(int index)
        {if(index==3){cancel.Text="Close";summary.Text="RMPCS installed successfully.\nYour shortcut and administrator preferences have been saved.";}ShowPage(index);if(index==2)PreviewOperation();}
        private void CompleteSetup(int code)
        {
            completed=true;
            summary.Text="Desktop shortcut: "+(desktopChoice.Checked?"Yes":"No")+"\nStart Menu entry: "+(menuChoice.Checked?"Yes":"No")+"\nAdministrator prompt: "+(adminChoice.Checked?"Always request when needed":"Request manually")+(code==3010?"\nWindows requests a restart when you are ready.":"");
            ShowPage(3);cancel.Text="Close";
        }
    }
}
