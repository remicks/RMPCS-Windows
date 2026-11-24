using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RMPCS.Core;
namespace RMPCS.App;
public partial class MainWindow
{
    private bool loadingAppearance;
    private void InitializeAppearance()
    {
        loadingAppearance=true;ThemeModeChoice.ItemsSource=ThemeCatalog.Modes;
        FontChoice.ItemsSource = FontCatalog.Names;
        AccentChoice.ItemsSource=ThemeCatalog.Presets.Select(x=>x.Name).Append("Custom").ToArray();loadingAppearance=false;
    }
    private void PutAppearanceControls()
    {
        loadingAppearance=true;
        ThemeModeChoice.SelectedItem=preferences.ThemeMode;AccentChoice.SelectedItem=preferences.AccentName;CustomAccentHex.Text=preferences.CustomAccent;
        FontChoice.SelectedItem = preferences.UiFont;
        DescribeFont();
        var (r,g,b)=ThemeCatalog.Components(preferences.CustomAccent);AccentRed.Text=r.ToString(CultureInfo.InvariantCulture);AccentGreen.Text=g.ToString(CultureInfo.InvariantCulture);AccentBlue.Text=b.ToString(CultureInfo.InvariantCulture);
        loadingAppearance=false;
    }
    private AppPreferences AppearancePreview() => preferences with { ThemeMode=ThemeModeChoice.SelectedItem as string??"System",AccentName=AccentChoice.SelectedItem as string??"Crimson Red",CustomAccent=ThemeCatalog.NormalizeHex(CustomAccentHex.Text), UiFont=FontCatalog.Normalize(FontChoice.SelectedItem as string) };
    private bool ValidCustomControls() => ThemeCatalog.ValidHex(CustomAccentHex.Text)&&ReadRgb(out var rgb)&&rgb==ThemeCatalog.NormalizeHex(CustomAccentHex.Text);
    private bool ReadRgb(out string rgb)
    {
        rgb="";
        bool Read(TextBox box,out int value)=>int.TryParse(box.Text,NumberStyles.None,CultureInfo.InvariantCulture,out value)&&value is >=0 and <=255;
        if(AccentRed is null||AccentGreen is null||AccentBlue is null||!Read(AccentRed,out int r)||!Read(AccentGreen,out int g)||!Read(AccentBlue,out int b))return false;
        rgb=ThemeCatalog.Rgb(r,g,b);return true;
    }
    private void PreviewAppearance()
    {
        if(loadingAppearance||ThemeStatus is null)return;
        DescribeFont();
        if(AccentChoice.SelectedItem as string=="Custom"&&!ValidCustomControls()){ThemeStatus.Text="Enter a six-digit HEX color or RGB numbers from 0 to 255. The last valid preview is kept.";return;}
        ThemeManager.Apply(AppearancePreview());ThemeStatus.Text="Preview only. Save preferences to keep this look. Windows High Contrast overrides custom colors.";
    }
    private void ThemeChoice_Changed(object sender,SelectionChangedEventArgs e)=>PreviewAppearance();
    private void DescribeFont()
    {
        if (FontDescription is null) return;
        FontDescription.Text = (FontChoice.SelectedItem as string) switch
        {
            "Rajdhani" => "Compact, technical letterforms. Bundled with RMPCS; no Windows font installation needed.",
            "Atkinson Hyperlegible" => "Distinct letter shapes designed for readability. Bundled with RMPCS; no Windows font installation needed.",
            "Lato" => "Balanced, open letterforms with a warm humanist feel. Bundled with RMPCS; no Windows font installation needed.",
            "Poppins" => "Clean geometric shapes with a modern, rounded character. Bundled with RMPCS; no Windows font installation needed.",
            "Hind" => "Calm, clear letterforms for comfortable interface reading. Bundled with RMPCS; no Windows font installation needed.",
            "PT Sans" => "A practical, familiar sans serif with distinct letterforms. Bundled with RMPCS; no Windows font installation needed.",
            "Oxygen" => "A crisp desktop-oriented font with a compact footprint. Bundled with RMPCS; no Windows font installation needed.",
            "Windows system" => "Uses the Segoe UI family supplied by Windows.",
            _ => "Modern, balanced letterforms. Bundled with RMPCS; no Windows font installation needed."
        };
    }
    private void CustomHex_Changed(object sender,TextChangedEventArgs e)
    {
        if(loadingAppearance||AccentRed is null||!ThemeCatalog.ValidHex(CustomAccentHex.Text))return;
        loadingAppearance=true;var (r,g,b)=ThemeCatalog.Components(CustomAccentHex.Text);AccentRed.Text=r.ToString(CultureInfo.InvariantCulture);AccentGreen.Text=g.ToString(CultureInfo.InvariantCulture);AccentBlue.Text=b.ToString(CultureInfo.InvariantCulture);AccentChoice.SelectedItem="Custom";loadingAppearance=false;PreviewAppearance();
    }
    private void CustomRgb_Changed(object sender,TextChangedEventArgs e)
    {
        if(loadingAppearance||CustomAccentHex is null||!ReadRgb(out string rgb))return;
        loadingAppearance=true;CustomAccentHex.Text=rgb;AccentChoice.SelectedItem="Custom";loadingAppearance=false;PreviewAppearance();
    }
    private void SaveAppearance_Click(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        if(AccentChoice.SelectedItem as string=="Custom"&&!ValidCustomControls()){ThemeStatus.Text="Enter a valid HEX color or RGB values from 0 to 255 before saving.";return;}
        try {var next=AppearancePreview().Normalize();if(!TestMode)SettingsStore.Save(next);preferences=next;ThemeManager.Apply(preferences);ThemeStatus.Text="Appearance saved. Other unsaved settings were left alone.";}
        catch {ThemeStatus.Text="Appearance could not be saved. Your saved preferences were preserved.";}
    }
    private void DiscardTheme_Click(object sender,RoutedEventArgs e){PutAppearanceControls();ThemeManager.Apply(preferences);ThemeStatus.Text="Saved appearance restored. Other unsaved settings were left alone.";}
}
