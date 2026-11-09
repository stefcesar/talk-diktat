// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
namespace Talk.Core.Secrets;

/// <summary>
/// Verwahrt Geheimnisse wie den Schlüssel für den Erkenner.
/// Die Umsetzung kommt später über die Windows Anmeldeinformationsverwaltung.
/// Werte werden nie protokolliert, nie im Chat abgefragt und nie in Dateien des Projekts geschrieben.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Liest ein Geheimnis.
    /// </summary>
    /// <param name="name">Name des Eintrags.</param>
    /// <returns>Der Wert oder null, wenn kein Eintrag vorhanden ist.</returns>
    string? Get(string name);

    /// <summary>
    /// Legt ein Geheimnis an oder überschreibt es.
    /// </summary>
    /// <param name="name">Name des Eintrags.</param>
    /// <param name="value">Der Wert. Wird nie protokolliert.</param>
    void Set(string name, string value);

    /// <summary>
    /// Entfernt ein Geheimnis.
    /// </summary>
    /// <param name="name">Name des Eintrags.</param>
    void Remove(string name);
}
