// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

using Talk.Core.Localization;

using Xunit;

namespace Talk.Core.Tests.Localization;

/// <summary>
/// Prüft Regel 14 aus Playbook Abschnitt 29: In den XAML Dateien der Nutzerfenster steht kein fest
/// eingetragener Text mehr, und jeder Schlüssel in <c>{ui:T schluessel}</c> steht in der Tabelle.
/// Die Dateien werden direkt aus dem Quellordner gelesen; Talk.App wird nicht referenziert.
/// </summary>
public sealed class XamlTextTests
{
    private static readonly string[] Ausgenommen = ["ThemeSampleWindow.xaml", "Theme.xaml"];

    private static readonly string[] Pflichtdateien =
    [
        "SettingsWindow.xaml", "HistoryWindow.xaml", "HotkeyTestWindow.xaml",
        "KeyDialog.xaml", "WordFixDialog.xaml", "OverlayWindow.xaml",
    ];

    private static readonly HashSet<string> TextAttribute =
        new(StringComparer.Ordinal) { "Text", "Content", "Header", "Title", "ToolTip", "AutomationProperties.Name" };

    // {ui:T schluessel} oder {ui:T Key=schluessel}, auch eingebettet in einen größeren Wert.
    private static readonly Regex UiTVerweis =
        new(@"\{\s*ui:T\s+(?:Key\s*=\s*)?(?<key>[^\s,}]+)\s*\}", RegexOptions.CultureInvariant);

    /// <summary>Ein Befund: Datei, Zeile (0, wenn unbekannt) und der gefundene Text.</summary>
    public sealed record Befund(string Datei, int Zeile, string Art, string Text)
    {
        public override string ToString() =>
            $"{Datei}, Zeile {(Zeile > 0 ? Zeile.ToString() : "?")}: {Art}: \"{Text}\"";
    }

    [Fact]
    public void Gepruefte_Dateien_sind_nicht_leer_und_enthalten_alle_Nutzerfenster()
    {
        var namen = NutzerXamlDateien().Select(Path.GetFileName).ToList();

        Assert.NotEmpty(namen);
        foreach (var pflicht in Pflichtdateien)
        {
            Assert.Contains(pflicht, namen);
        }

        foreach (var aus in Ausgenommen)
        {
            Assert.DoesNotContain(aus, namen);
        }
    }

    [Fact]
    public void Nutzerfenster_enthalten_keinen_festen_Text()
    {
        var befunde = NutzerXamlDateien()
            .SelectMany(pfad => FesteTexte(XDocument.Load(pfad, LoadOptions.SetLineInfo), Path.GetFileName(pfad)))
            .ToList();

        Assert.True(befunde.Count == 0,
            $"Fest eingetragener Text in {befunde.Count} Stellen:{Environment.NewLine}"
            + string.Join(Environment.NewLine, befunde));
    }

    [Fact]
    public void Jeder_ui_T_Schluessel_existiert_in_der_Tabelle()
    {
        var befunde = NutzerXamlDateien()
            .SelectMany(pfad => FehlendeSchluessel(XDocument.Load(pfad, LoadOptions.SetLineInfo), Path.GetFileName(pfad)))
            .ToList();

        Assert.True(befunde.Count == 0,
            $"Unbekannte Schlüssel in {befunde.Count} Stellen:{Environment.NewLine}"
            + string.Join(Environment.NewLine, befunde));
    }

    [Fact]
    public void Nutzerfenster_verwenden_ui_T_ueberhaupt()
    {
        // Schutz gegen einen still leer laufenden Schlüsseltest.
        var anzahl = NutzerXamlDateien()
            .SelectMany(pfad => UiTSchluessel(XDocument.Load(pfad, LoadOptions.SetLineInfo)))
            .Count();

        Assert.True(anzahl > 0, "Keine {ui:T ...} Verweise gefunden.");
    }

    // Gegenprobe: Die Prüffunktion schlägt bei festem Text an (XAML nur im Speicher).
    [Fact]
    public void Gegenprobe_fester_Text_wird_gefunden()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="clr-namespace:Talk.App.Ui"
                    Title="Einstellungen">
              <!-- Kommentar mit Buchstaben zählt nicht -->
              <StackPanel>
                <TextBlock Text="{ui:T common.close}" />
                <TextBlock Text="{Binding Name}" />
                <TextBlock Text="×" />
                <TextBlock Text="" />
                <TextBlock Text="12:30" />
                <Button Content="Speichern" ToolTip="{}Wörtlich" />
                <Button AutomationProperties.Name="Schließen" />
                <TextBlock>Hallo</TextBlock>
                <TextBlock><Run>Welt</Run></TextBlock>
                <TextBlock>  42  </TextBlock>
              </StackPanel>
            </Window>
            """;

        var befunde = FesteTexte(XDocument.Parse(xaml, LoadOptions.SetLineInfo), "Speicher.xaml");
        var texte = befunde.Select(b => b.Text).ToList();

        Assert.Equal(["Einstellungen", "Speichern", "{}Wörtlich", "Schließen", "Hallo", "Welt"], texte);
        Assert.All(befunde, b => Assert.True(b.Zeile > 0, $"Zeile fehlt bei {b}"));
        Assert.Equal(4, befunde.Single(b => b.Text == "Einstellungen").Zeile);
    }

    [Fact]
    public void Gegenprobe_unbekannter_Schluessel_wird_gefunden()
    {
        const string xaml = """
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="clr-namespace:Talk.App.Ui"
                    Title="{ui:T gibt.es.nicht}">
              <Button Content="{ui:T common.close}" />
            </Window>
            """;

        var befunde = FehlendeSchluessel(XDocument.Parse(xaml, LoadOptions.SetLineInfo), "Speicher.xaml");

        var befund = Assert.Single(befunde);
        Assert.Equal("gibt.es.nicht", befund.Text);
    }

    // ---------- Prüffunktionen ----------

    /// <summary>Findet fest eingetragenen Text in Textattributen und im Elementinhalt.</summary>
    public static IEnumerable<Befund> FesteTexte(XDocument doc, string datei)
    {
        foreach (var element in doc.Descendants())
        {
            foreach (var attr in element.Attributes())
            {
                if (attr.IsNamespaceDeclaration || !IstTextAttribut(attr))
                {
                    continue;
                }

                if (IstFesterText(attr.Value))
                {
                    yield return new Befund(datei, Zeile(attr), $"Attribut {element.Name.LocalName}.{attr.Name.LocalName}", attr.Value);
                }
            }

            // Elementinhalt zwischen Tags; XComment ist kein XText und zählt daher nicht.
            foreach (var text in element.Nodes().OfType<XText>())
            {
                var wert = text.Value.Trim();
                if (wert.Any(char.IsLetter))
                {
                    yield return new Befund(datei, Zeile(text), $"Inhalt von <{element.Name.LocalName}>", wert);
                }
            }
        }
    }

    /// <summary>Findet Schlüssel in <c>{ui:T ...}</c>, die es in <see cref="UiText"/> nicht gibt.</summary>
    public static IEnumerable<Befund> FehlendeSchluessel(XDocument doc, string datei) =>
        UiTSchluessel(doc)
            .Where(t => !UiText.Has(t.Key))
            .Select(t => new Befund(datei, t.Zeile, "unbekannter Schlüssel", t.Key));

    private static IEnumerable<(string Key, int Zeile)> UiTSchluessel(XDocument doc) =>
        doc.Descendants()
            .SelectMany(e => e.Attributes())
            .Where(a => !a.IsNamespaceDeclaration)
            .SelectMany(a => UiTVerweis.Matches(a.Value).Select(m => (m.Groups["key"].Value, Zeile(a))));

    private static bool IstTextAttribut(XAttribute attr)
    {
        var name = attr.Name.LocalName;
        return TextAttribute.Contains(name) || name.EndsWith(".ToolTip", StringComparison.Ordinal);
    }

    private static bool IstFesterText(string wert)
    {
        if (wert.Length == 0)
        {
            return false;
        }

        // "{}" am Anfang ist die XAML Schreibweise für wörtlichen Text, keine Markuperweiterung.
        if (wert.StartsWith('{') && !wert.StartsWith("{}", StringComparison.Ordinal))
        {
            return false;
        }

        return wert.Any(char.IsLetter);
    }

    private static int Zeile(XObject obj) =>
        obj is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;

    private static List<string> NutzerXamlDateien()
    {
        var ui = Path.Combine(RepoWurzel(), "src", "Talk.App", "Ui");
        Assert.True(Directory.Exists(ui), $"Ordner fehlt: {ui}");

        return Directory.EnumerateFiles(ui, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !Ausgenommen.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    private static string RepoWurzel()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Talk.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir.FullName;
    }
}
