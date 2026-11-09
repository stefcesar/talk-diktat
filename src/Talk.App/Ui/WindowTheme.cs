// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Talk.App.Ui;

/// <summary>
/// Gibt einem Fenster die Optik aus DESIGN.md: den Stil "TalkWindow" aus Ui\Theme.xaml und eine dunkle Titelleiste.
/// Jedes Fenster ruft das direkt nach InitializeComponent auf; das Overlay nicht, es hat keine Titelleiste.
/// </summary>
/// <remarks>
/// Die Titelleiste färbt Windows über DwmSetWindowAttribute. Windows 10 kennt nur den dunklen Modus, nicht die eigenen Farben;
/// lehnt Windows einen Wert ab, bleibt es bei dem, was es kann. Die Knöpfe oben rechts bleiben die von Windows.
/// </remarks>
internal static class WindowTheme
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmBorderColor = 34;
    private const int DwmCaptionColor = 35;
    private const int DwmTextColor = 36;

    // Farben als COLORREF (0x00BBGGRR), dieselben Werte wie BgColor, LineColor und TextColor in Theme.xaml.
    private const int CaptionColor = 0x001B1715;
    private const int BorderColor = 0x0038322E;
    private const int TextColor = 0x00E7EBEC;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.SetResourceReference(FrameworkElement.StyleProperty, "TalkWindow");
        window.SourceInitialized += OnSourceInitialized;
    }

    /// <summary>
    /// Setzt die Dauer der Rückmeldung auf null, wenn in Windows "Animationen anzeigen" aus ist. Muss vor dem ersten Fenster laufen,
    /// weil die Vorlagen den Wert beim ersten Laden übernehmen.
    /// </summary>
    public static void ApplyMotionPreference(System.Windows.Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (SystemParameters.ClientAreaAnimation) return;

        foreach (ResourceDictionary dictionary in application.Resources.MergedDictionaries)
        {
            if (dictionary.Contains("FeedbackDuration")) dictionary["FeedbackDuration"] = new Duration(TimeSpan.Zero);
        }
    }

    private static void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;
        window.SourceInitialized -= OnSourceInitialized;

        try
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            SetInt(handle, DwmUseImmersiveDarkMode, 1);
            SetInt(handle, DwmCaptionColor, CaptionColor);
            SetInt(handle, DwmBorderColor, BorderColor);
            SetInt(handle, DwmTextColor, TextColor);
        }
        catch (Exception)
        {
            // Ohne dunkle Titelleiste geht alles weiter; das Fenster selbst ist trotzdem dunkel.
        }
    }

    private static void SetInt(IntPtr handle, int attribute, int value) =>
        _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
