// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.ComponentModel;

using Meziantou.Framework.Win32;

using Talk.Core.Secrets;

namespace Talk.App.Secrets;

/// <summary>
/// Verwahrt Geheimnisse in der Windows Anmeldeinformationsverwaltung, für den angemeldeten Benutzer auf diesem Rechner.
/// Der Eintrag heißt "Talk:" plus Name, für den Groq Schlüssel also "Talk:GroqApiKey".
/// </summary>
/// <remarks>
/// Diese Klasse protokolliert nichts und schreibt nichts in Dateien. Kein Wert steht je in einer Meldung oder Ausnahme.
/// Windows nimmt je Eintrag höchstens 2.560 Byte an, das sind 1.280 Zeichen.
/// </remarks>
internal sealed class CredentialSecretStore : ISecretStore
{
    /// <summary>Vorsatz aller Einträge von Talk in der Anmeldeinformationsverwaltung.</summary>
    public const string TargetPrefix = "Talk:";

    /// <summary>Höchste Länge eines Werts in Zeichen.</summary>
    public const int MaxSecretLength = 1280;

    private const string UserName = "Talk";
    private const string Comment = "Talk Diktier App";

    // Windows Fehler ERROR_NOT_FOUND: Es gibt keinen Eintrag mit diesem Namen.
    private const int ErrorNotFound = 1168;

    /// <summary>
    /// Liest den Wert eines Eintrags. Fehlt der Eintrag oder scheitert das Lesen, kommt null zurück.
    /// </summary>
    /// <param name="name">Name des Eintrags ohne den Vorsatz "Talk:".</param>
    /// <returns>Der Wert oder null.</returns>
    /// <exception cref="ArgumentException">Wenn der Name leer ist.</exception>
    public string? Get(string name)
    {
        string target = ToTarget(name);

        try
        {
            return CredentialManager.ReadCredential(target)?.Password;
        }
        catch
        {
            // Ein Lesefehler zählt wie ein fehlender Eintrag. Die Ursache wird nicht weitergegeben.
            return null;
        }
    }

    /// <summary>
    /// Legt einen Eintrag an oder überschreibt ihn.
    /// </summary>
    /// <param name="name">Name des Eintrags ohne den Vorsatz "Talk:".</param>
    /// <param name="value">Der Wert, höchstens 1.280 Zeichen. Steht nie in einer Meldung.</param>
    /// <exception cref="ArgumentException">Wenn der Name leer ist, der Wert leer ist oder der Wert zu lang ist.</exception>
    public void Set(string name, string value)
    {
        string target = ToTarget(name);

        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException("Der Wert darf nicht leer sein.", nameof(value));
        }

        if (value.Length > MaxSecretLength)
        {
            throw new ArgumentException("Der Wert ist zu lang, erlaubt sind höchstens 1.280 Zeichen.", nameof(value));
        }

        CredentialManager.WriteCredential(target, UserName, value, Comment, CredentialPersistence.LocalMachine);
    }

    /// <summary>
    /// Entfernt einen Eintrag. Gibt es ihn nicht, passiert nichts.
    /// </summary>
    /// <param name="name">Name des Eintrags ohne den Vorsatz "Talk:".</param>
    /// <exception cref="ArgumentException">Wenn der Name leer ist.</exception>
    /// <exception cref="Win32Exception">Wenn es den Eintrag gibt, Windows ihn aber nicht löschen kann.</exception>
    public void Remove(string name)
    {
        string target = ToTarget(name);

        try
        {
            CredentialManager.DeleteCredential(target);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorNotFound)
        {
            // Der Eintrag fehlt schon, das Ziel ist erreicht.
        }
    }

    private static string ToTarget(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TargetPrefix + name.Trim();
    }
}
