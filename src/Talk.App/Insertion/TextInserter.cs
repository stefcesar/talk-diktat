// Talk – Created by Stefano Cesaretti · https://github.com/stefcesar/talk-diktat
// SPDX-License-Identifier: MIT
using System.Diagnostics;

using Talk.App.Diagnostics;
using Talk.Core.Insertion;

namespace Talk.App.Insertion;

/// <summary>
/// Fügt den Text in das Zielfenster ein, nach den Regeln aus Playbook Abschnitt 17: erst die reine Entscheidung über den Weg,
/// dann Fenster nach vorn, Passwortfeld prüfen, Zwischenablage, sonst tippen. Gehört auf den UI Thread.
/// </summary>
/// <remarks>
/// Der Aufrufer legt den Text vorher im Verlauf ab. Im Protokoll steht je Versuch eine Zeile mit festen Wörtern und Zahlen,
/// nie der Text, nie ein Prozessname. Die Methode wirft nicht: Ein Abbruch und jeder unerwartete Fehler werden zu einem Ergebnis.
/// </remarks>
internal sealed class TextInserter : ITextInserter
{
    private readonly ClipboardPaster _paster = new();

    /// <summary>
    /// Das laufende Zurücklegen der Zwischenablage. Wer die App beendet, wartet vorher darauf.
    /// </summary>
    public Task PendingRestore => _paster.PendingRestore;

    public async Task<InsertOutcome> InsertAsync(string text, TargetWindow? target, CancellationToken ct, bool startRaised = false)
    {
        Stopwatch watch = Stopwatch.StartNew();
        int length = text?.Length ?? 0;
        bool isTerminal = false;
        string targetWord = "keins";
        string contextWord = "unbekannt";
        string adaptedWord = "nein";

        InsertOutcome Done(InsertResult result, InsertReason reason)
        {
            InsertOutcome outcome = new(result, reason, isTerminal, watch.ElapsedMilliseconds);
            AppLog.Write(
                "insert",
                "result",
                $"weg={result.ToWord()} grund={reason.ToWord()} zeichen={length} ms={outcome.Milliseconds} ziel={targetWord} kontext={contextWord} angepasst={adaptedWord}");
            return outcome;
        }

        try
        {
            InsertTargetFacts facts = ForegroundWindowProbe.ReadFacts(target);
            isTerminal = facts.IsTerminal;
            if (facts.HasTarget && !facts.IsOwnWindow) targetWord = isTerminal ? "terminal" : "normal";

            if (!InsertText.IsInsertable(text)) return Done(InsertResult.HistoryOnly, InsertReason.None);

            InsertPlan plan = InsertPlanner.Decide(facts);
            if (plan.Way is InsertResult.HistoryOnly or InsertResult.Blocked) return Done(plan.Way, plan.Reason);

            nint handle = target!.Handle;
            if (!await ForegroundWindowProbe.EnsureForegroundAsync(handle)) return Done(InsertResult.HistoryOnly, InsertReason.NotForeground);

            // Das Passwortfeld lässt sich erst lesen, wenn das Fenster vorn ist. Die Entscheidung trifft wieder die reine Regel.
            bool password = await ForegroundWindowProbe.IsPasswordFocusedAsync(handle);
            plan = InsertPlanner.Decide(facts with { PasswordField = password });
            if (plan.Way is InsertResult.HistoryOnly or InsertResult.Blocked) return Done(plan.Way, plan.Reason);

            // Leerzeichen und Großschreibung nach den Zeichen vor dem Cursor (Playbook Abschnitt 24). Nie im Terminal, nie im
            // Passwortfeld (das ist oben schon gesperrt). Die gelesenen Zeichen bleiben in dieser Methode.
            if (!isTerminal)
            {
                string? before = await ForegroundWindowProbe.ReadTextBeforeCaretAsync(handle);
                if (before is not null)
                {
                    contextWord = "gelesen";
                    string adapted = InsertText.AdaptToContext(text, before, startRaised);
                    if (!string.Equals(adapted, text, StringComparison.Ordinal)) adaptedWord = "ja";
                    text = adapted;
                    length = text.Length;
                }
            }

            if (!await KeyboardTyper.WaitForModifiersReleasedAsync()) return Done(InsertResult.HistoryOnly, InsertReason.InputFailed);

            // Letzte Stelle für einen Abbruch. Ab hier läuft die Eingabe durch, damit kein halber Text stehen bleibt.
            if (ct.IsCancellationRequested) return Done(InsertResult.HistoryOnly, InsertReason.Canceled);

            // Mit gedrücktem oder eingerastetem Umschalt würde aus Strg+V ein anderer Befehl. Dann wird getippt.
            if (plan.Way == InsertResult.Inserted && !KeyboardTyper.IsShiftDown)
            {
                switch (await _paster.PasteAsync(InsertText.ForClipboard(text), handle))
                {
                    case PasteResult.Pasted:
                        return Done(InsertResult.Inserted, InsertReason.None);

                    case PasteResult.LostFocus:
                        return Done(InsertResult.HistoryOnly, InsertReason.NotForeground);
                }
            }

            // Im Terminal wird aus dem Satz ein Befehl: ohne Satzzeichen am Ende, erstes Wort klein.
            string typed = isTerminal ? InsertText.ForTerminal(text) : InsertText.ForTyping(text);
            if (typed.Length == 0) return Done(InsertResult.HistoryOnly, InsertReason.None);

            return await KeyboardTyper.TypeAsync(typed, handle) switch
            {
                TypeResult.Done => Done(InsertResult.Typed, InsertReason.None),
                TypeResult.LostFocus => Done(InsertResult.HistoryOnly, InsertReason.NotForeground),
                _ => Done(InsertResult.HistoryOnly, InsertReason.InputFailed),
            };
        }
        catch (Exception)
        {
            // Die Meldung der Ausnahme wird nicht protokolliert. Der Text steht im Verlauf.
            return Done(InsertResult.HistoryOnly, InsertReason.InputFailed);
        }
    }
}
