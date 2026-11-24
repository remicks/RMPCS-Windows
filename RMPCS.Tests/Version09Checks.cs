using RMPCS.Core;
internal static class Version09Checks
{
    public static void Run(Action<bool,string> check)
    {
        check(ThemeCatalog.Presets.Count==14&&ThemeCatalog.Presets.Select(x=>x.Name).Distinct().Count()==14&&ThemeCatalog.Presets.First().Hex=="#D58AFF","fourteen palettes retain the original Midnight Purple color");
        check(ThemeCatalog.ValidHex("#12abEF")&&ThemeCatalog.ValidHex("12abEF")&&!ThemeCatalog.ValidHex("#123")&&!ThemeCatalog.ValidHex("##123456")&&!ThemeCatalog.ValidHex("#12zzff")&&!ThemeCatalog.ValidHex("red")&&!ThemeCatalog.ValidHex("https://example.test"),"custom accents accept only six-digit RGB hex data");
        check(ThemeCatalog.NormalizeHex(" 12abef ")=="#12ABEF"&&ThemeCatalog.NormalizeHex(null)=="#D58AFF"&&ThemeCatalog.Rgb(0,128,255)=="#0080FF"&&ThemeCatalog.Components("#0080FF")== (0,128,255),"HEX and RGB round-trip with bounded defaults");
        bool bad=false;try{ThemeCatalog.Rgb(256,0,0);}catch(ArgumentOutOfRangeException){bad=true;}check(bad,"RGB values outside 0-255 are rejected");
        foreach(string hex in ThemeCatalog.Presets.Select(x=>x.Hex).Concat(new[]{"#000000","#FFFFFF","#777777","#00FF00","#FF00FF"}))
        {
            check(ThemeCatalog.Contrast(hex,ThemeCatalog.OnAccent(hex))>=4.5,"accent label contrast is readable: "+hex);
            check(ThemeCatalog.Contrast(ThemeCatalog.ReadableAccent(hex,"#FFFFFF"),"#FFFFFF")>=4.5&&ThemeCatalog.Contrast(ThemeCatalog.ReadableAccent(hex,"#171522"),"#171522")>=4.5,"accent text adapts for light and dark: "+hex);
        }
        var prefs=AppPreferences.Parse("{\"ThemeMode\":\"arbitrary\",\"AccentName\":\"unknown\",\"CustomAccent\":\"<script>\"}");
        check(prefs.ThemeMode=="System"&&prefs.AccentName=="Crimson Red"&&prefs.CustomAccent=="#D58AFF","untrusted appearance preferences normalize to supported data");
        var custom=new AppPreferences{ThemeMode="Dark",AccentName="Custom",CustomAccent="#03abfe"}.Normalize();
        check(AppPreferences.Parse(custom.ToJson())==custom&&new PreferenceUndo(new(),custom).Restore(custom)==new AppPreferences(),"theme preferences persist and participate in conflict-protected undo");
        var item=new StartupItem(StartupPolicy.Id("User64","Example","S-1-5-21-1"),"Example","must not execute","User64",true,false,"S-1-5-21-1");
        int writes=0,asks=0;bool Confirm(){asks++;return true;}void Write(StartupItem value){writes++;}
        check(StartupSwitchFlow.Change(item,true,true,Confirm,Write,Write)==StartupSwitchResult.Unchanged&&writes==0&&asks==0,"startup switches do not write or ask on refresh/unchanged state");
        check(StartupSwitchFlow.Change(item,false,false,Confirm,Write,Write)==StartupSwitchResult.Denied&&writes==0&&asks==0,"startup switches reject non-authorized changes before confirmation/actions");
        check(StartupSwitchFlow.Change(item,false,true,()=>false,Write,Write)==StartupSwitchResult.Cancelled&&writes==0&&item.Enabled,"cancelled startup switches preserve the original snapshot without writing");
        int disabled=0,restored=0;
        check(StartupSwitchFlow.Change(item,false,true,()=>true,_=>disabled++,_=>restored++)==StartupSwitchResult.Changed&&disabled==1&&restored==0,"turning off routes only through protected disable");
        check(StartupSwitchFlow.Change(item with {Enabled=false},true,true,()=>true,_=>disabled++,_=>restored++)==StartupSwitchResult.Changed&&disabled==1&&restored==1,"turning on routes only through protected restore");
        bad=false;try{StartupSwitchFlow.Change(item,false,true,()=>true,_=>throw new IOException("conflict"),Write);}catch(IOException){bad=true;}check(bad&&item.Enabled,"failed startup writes propagate without changing the displayed source snapshot");
        check(item.ScopeLabel=="This Windows user"&&item.AccessibleName.Contains("On")&&(item with {Enabled=false}).SwitchLabel=="Off","startup switches expose user-friendly scope and textual/accessibility state");
    }
}
