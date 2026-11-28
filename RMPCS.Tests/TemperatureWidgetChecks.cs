using System.Text.Json;
using RMPCS.Core;

internal static class TemperatureWidgetChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var source=new TemperatureSource("In-app hardware","CPU",TemperaturePolicy.Key("In-app hardware","/cpu/0/temperature/1"),"Package");
        string Json(params TemperatureSource[] rows)=>JsonSerializer.Serialize(rows);
        check(source.Key.Length==64 && source.Key!=TemperaturePolicy.Key("Windows firmware","/cpu/0/temperature/1"),"sensor identities are stable and isolated by provider");
        check(TemperaturePolicy.Category("Cpu")=="CPU" && TemperaturePolicy.Category("CPU")=="CPU" && TemperaturePolicy.Category("Motherboard")=="Motherboard" && TemperaturePolicy.Category("GpuAmd")=="GPU" && TemperaturePolicy.Category("unknown")=="Other","pinned LHM and legacy WMI hardware categories map accurately without guessing unknown types");
        check(TemperaturePolicy.Clean("A\n\u202eB",2)=="AB","sensor labels reject controls and directional overrides and obey bounds");
        check(Math.Abs(TemperaturePolicy.Celsius("Windows firmware",3032)!.Value-30.05)<.001 && TemperaturePolicy.Celsius("Windows storage",45)==45,"firmware tenths-Kelvin and storage Celsius convert separately");
        foreach(object? value in new object?[]{null,"bad",0,-100,201,double.NaN,double.PositiveInfinity})check(TemperaturePolicy.Celsius("In-app hardware",value) is null,"missing, sentinel and out-of-range temperatures stay unavailable");
        check(TemperaturePolicy.Celsius("In-app hardware",-20)==-20 && TemperaturePolicy.Celsius("Windows firmware",0) is null,"plausible subzero readings survive while firmware sentinels are rejected");
        foreach(string invalid in new[]{"bad","null",new string(' ',8193),Json(source with{Provider="remote host"}),Json(source with{Category="Guess CPU"}),Json(source with{Key="../driver"}),Json(source with{Key=source.Key.ToUpperInvariant()})})check(TemperaturePolicy.Selections(invalid).Length==0,"invalid, oversized and unapproved sensor selections are rejected");
        check(TemperaturePolicy.Selections(Json(source,source)).Length==1,"duplicate sensor selections cannot multiply polling");
        var many=Enumerable.Range(0,12).Select(i=>source with{Key=TemperaturePolicy.Key(source.Provider,i.ToString())}).ToArray();
        check(TemperaturePolicy.Selections(Json(many)).Length==8,"temperature selection is capped at eight sources");
        var temp=new TemperaturePreferences{Enabled=true,Onboard=true,SelectionJson=Json(source),Views=new(){Numbers=true,CircleWithValue=true}}.Normalize();
        var layout=new[]{new WidgetPlacement("CPU","Current",-.5,3,true),new WidgetPlacement("Temp:"+source.Key,"Current",.7,.2,false)};
        var normalized=WidgetLayout.Parse(WidgetLayout.Save(layout));
        check(normalized.Length==2 && normalized[0].X==0 && normalized[0].Y==1 && normalized[0].Locked && !normalized[1].Locked,"widget positions clamp to screen fractions and preserve independent locks");
        check(WidgetLayout.Parse(WidgetLayout.Save([layout[0],layout[0],layout[0] with{Key="../bad"},layout[0] with{Key="RAM",X=double.NaN}])).Length==1,"duplicate, nonfinite and unknown widget keys are excluded");
        check(WidgetLayout.Parse(new string(' ',4097)).Length==0 && WidgetLayout.Parse("[null]").Length==0 && WidgetLayout.Parse("{}").Length==0,"malformed and oversized widget layouts fail closed");
        var choices=new OverlayPreferences{Widgets=true,TransparentBackground=true,Temperatures=temp,WidgetLayoutJson=WidgetLayout.Save(layout)}.Normalize();
        var prefs=new AppPreferences{Overlay=choices};
        check(AppPreferences.Parse(prefs.ToJson()).Overlay==choices,"temperature source/style and widget layout preferences round-trip with value equality");
        var unicodeSources=many.Take(8).Select(s=>s with{Name=new string('\u6e29',48)}).ToArray();
        var fullLayout=WidgetLayout.Metrics.Select(k=>new WidgetPlacement(k,@"\\.\DISPLAY1234567890123",.1234567890123456,.9876543210987654,true)).Concat(unicodeSources.Select(s=>new WidgetPlacement("Temp:"+s.Key,@"\\.\DISPLAY1234567890123",.1234567890123456,.9876543210987654,true)));
        var full=prefs with{Overlay=choices with{Temperatures=temp with{SelectionJson=Json(unicodeSources)},WidgetLayoutJson=WidgetLayout.Save(fullLayout)}};
        check(AppPreferences.Parse(full.ToJson()).Overlay.Temperatures.Selected().Length==8 && WidgetLayout.Parse(AppPreferences.Parse(full.ToJson()).Overlay.WidgetLayoutJson).Length==14,"maximal Unicode sensor selections and all fourteen layouts fit bounded preferences");
        var defaults=new OverlayPreferences().Normalize();
        check(!defaults.Widgets && !defaults.TransparentBackground && !defaults.Temperatures.Enabled && !defaults.Temperatures.Onboard && defaults.Temperatures.Selected().Length==0,"optional temperatures, low-level reads and widgets default off for existing installations");
        var only=choices with{Cpu=false,Memory=false,Network=false,DiskSpace=false,Uptime=false};
        check(only.HasVisibleMetrics() && !(only with{Temperatures=temp with{Enabled=false}}).HasVisibleMetrics(),"selected temperature-only layouts count as visible metrics only while enabled");
        var keys=FunctionTree.Roots.SelectMany(r=>r.Children).Select(n=>n.Page).ToArray();
        check(keys.Length==PageCatalog.All.Count && keys.Distinct().Count()==keys.Length && PageCatalog.All.All(p=>keys.Contains(p.Key)) && FunctionTree.Roots.All(r=>r.Page is null),"categorized navigation includes every existing page once and groups have no actionable page");
    }
}
