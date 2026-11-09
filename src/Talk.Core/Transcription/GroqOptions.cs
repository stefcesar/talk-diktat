namespace Talk.Core.Transcription;

/// <summary>
/// Einstellungen des Groq Erkenners. Modellname und Endpunkt sind Einstellungen, weil sie sich beim Anbieter ändern können.
/// </summary>
/// <param name="Endpoint">Adresse für die Transkription.</param>
/// <param name="Model">Name des Modells.</param>
/// <param name="TimeoutMs">Höchste Wartezeit für eine Anfrage in Millisekunden. Standard 15 Sekunden.</param>
/// <param name="MaxPromptChars">Höchste Länge des Hinweistexts aus dem Wörterbuch in Zeichen.</param>
public sealed record GroqOptions(
    string Endpoint = GroqOptions.DefaultEndpoint,
    string Model = GroqOptions.DefaultModel,
    int TimeoutMs = 15000,
    int MaxPromptChars = VocabularyPrompt.DefaultMaxChars)
{
    /// <summary>Standardadresse für die Transkription.</summary>
    public const string DefaultEndpoint = "https://api.groq.com/openai/v1/audio/transcriptions";

    /// <summary>Standardmodell.</summary>
    public const string DefaultModel = "whisper-large-v3-turbo";

    /// <summary>Wirft bei unbrauchbaren Werten.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint)) throw new ArgumentException("Die Adresse darf nicht leer sein.", nameof(Endpoint));
        if (string.IsNullOrWhiteSpace(Model)) throw new ArgumentException("Der Modellname darf nicht leer sein.", nameof(Model));
        if (TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(TimeoutMs), "Die Wartezeit muss größer als 0 sein.");
        if (MaxPromptChars <= 0) throw new ArgumentOutOfRangeException(nameof(MaxPromptChars), "Die Grenze muss größer als 0 sein.");
    }
}
