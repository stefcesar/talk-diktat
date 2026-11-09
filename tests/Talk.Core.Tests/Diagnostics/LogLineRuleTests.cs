// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using Talk.Core.Diagnostics;

using Xunit;

namespace Talk.Core.Tests.Diagnostics;

/// <summary>
/// Prüft die Regel für saubere Protokollzeilen: feste Wörter und Zahlen gehen durch, Text, Pfade und Namen fallen auf.
/// </summary>
public sealed class LogLineRuleTests
{
    [Theory]
    [InlineData("2026-10-11 07:49:00.280 | app | start | version=0.1.0 args=0 pid=23588")]
    [InlineData("2026-10-11 07:49:00.365 | hook | installed")]
    [InlineData("2026-10-11 07:49:00.365 | caps | state | off")]
    [InlineData("2026-10-11 07:54:00.370 | hook | reinstalled | grund=intervall")]
    [InlineData("2026-10-11 07:55:13.165 | stt | result | engine=lokal ms=865 encode_ms=0 chars=19 lang=de fallback=NoKey")]
    [InlineData("2026-10-11 08:30:00.432 | insert | result | weg=zwischenablage grund=keiner zeichen=40 ms=27 ziel=normal")]
    [InlineData("2026-10-15 23:00:00.432 | insert | result | weg=zwischenablage grund=keiner zeichen=41 ms=31 ziel=normal kontext=gelesen angepasst=ja")]
    [InlineData("2026-10-11 09:47:01.508 | hotkey | shift-caps-passthrough")]
    [InlineData("2026-10-11 09:47:01.508 | audio | level | rms=0.12 peak=0.40")]
    [InlineData("2026-10-11 09:47:01.508 | overlay | selftest | ok sichtbar=3")]
    [InlineData("2026-10-11 09:47:01.508 | stt | local | geladen ms=500 laufzeit=vulkan")]
    [InlineData("2026-10-11 09:47:01.508 | audio | error | grund=NoDevice")]
    [InlineData("")]
    public void Saubere_Zeilen_gehen_durch(string zeile)
    {
        Assert.True(LogLineRule.IsClean(zeile));
    }

    [Theory]
    [InlineData("2026-10-11 07:55:13.165 | stt | result | Heute ist Sonntag, der elfte Oktober.")]
    [InlineData("2026-10-11 07:55:13.165 | stt | result | text=Das ist ein Satz mit Umlaut ä")]
    [InlineData("2026-10-11 07:55:13.165 | audio | devices | name=Mikrofon (Beispiel)")]
    [InlineData("2026-10-11 07:55:13.165 | app | args | C:\\Users\\jemand\\datei.wav")]
    [InlineData("2026-10-11 07:55:13.165 | key | check | gsk_abc/def+ghi")]
    [InlineData("2026-10-11 07:55:13.165 | stt | error | System.Exception: etwas ging schief")]
    [InlineData("2026-10-11 07:55:13.165 | stt | result | ich komme morgen")]
    [InlineData("2026-10-11 07:55:13.165 | stt | result | text=ich komme morgen")]
    [InlineData("2026-10-11 07:55:13.165 | key | check | gsk_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGH")]
    [InlineData("2026-10-11 07:55:13.165 | key | check | wert=gsk_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGH")]
    [InlineData("irgendein Text ohne Zeitstempel")]
    [InlineData("2026-10-11 07:55:13.165 | STT | result")]
    public void Text_Pfade_und_Namen_fallen_auf(string zeile)
    {
        Assert.False(LogLineRule.IsClean(zeile));
    }

    [Fact]
    public void Null_gilt_als_sauber()
    {
        Assert.True(LogLineRule.IsClean(null));
    }

    [Theory]
    [InlineData("2026-10-08 21:46:44 | start | version=0.1.0 | args=1", true)]
    [InlineData("2026-10-08 21:46:44 | selftest | ok", true)]
    [InlineData("2026-10-08 21:46:44 | start | Das ist ein Satz, mit Komma.", false)]
    [InlineData("2026-10-11 07:49:00.365 | hook | installed", false)]
    [InlineData("", false)]
    public void Altes_Format_wird_erkannt_aber_nur_ohne_Inhalt(string zeile, bool erwartet)
    {
        Assert.Equal(erwartet, LogLineRule.IsLegacy(zeile));
        if (erwartet) Assert.False(LogLineRule.IsClean(zeile));
    }
}
