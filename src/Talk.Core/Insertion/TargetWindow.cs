// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Insertion;

/// <summary>
/// Das Fenster, das beim Start der Aufnahme den Fokus hatte und den Text erhalten soll.
/// </summary>
/// <param name="Handle">Fensterkennung von Windows.</param>
/// <param name="ProcessName">Name des Prozesses, zum Beispiel "WINWORD".</param>
/// <param name="ClassName">Fensterklasse, hilft bei Sonderfällen wie Terminals.</param>
public sealed record TargetWindow(
    nint Handle,
    string ProcessName,
    string ClassName);
