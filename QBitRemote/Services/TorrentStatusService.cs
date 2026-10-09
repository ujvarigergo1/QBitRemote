namespace QBitRemote.Services;

public static class TorrentStatusService
{
    // Values are ordered: English, Hungarian, German, Spanish, French, Italian, Polish.
    private static readonly Dictionary<string, string[]> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["error"] = ["Error", "Hiba", "Fehler", "Error", "Erreur", "Errore", "Błąd"],
        ["missingFiles"] = ["Missing files", "Hiányzó fájlok", "Fehlende Dateien", "Archivos faltantes", "Fichiers manquants", "File mancanti", "Brakujące pliki"],
        ["uploading"] = ["Seeding", "Feltöltés", "Seeden", "Compartiendo", "Partage", "Seeding", "Udostępnianie"],
        ["pausedUP"] = ["Paused (seeding)", "Szüneteltetve (feltöltés)", "Pausiert (Seeden)", "En pausa (compartiendo)", "En pause (partage)", "In pausa (seeding)", "Wstrzymano (udostępnianie)"],
        ["queuedUP"] = ["Queued (seeding)", "Várólistán (feltöltés)", "Warteschlange (Seeden)", "En cola (compartiendo)", "En file d'attente (partage)", "In coda (seeding)", "W kolejce (udostępnianie)"],
        ["stalledUP"] = ["Seeding", "Feltöltés", "Seeden", "Compartiendo", "Partage", "Seeding", "Udostępnianie"],
        ["checkingUP"] = ["Checking (seeding)", "Ellenőrzés (feltöltés)", "Prüfen (Seeden)", "Comprobando (compartiendo)", "Vérification (partage)", "Verifica (seeding)", "Sprawdzanie (udostępnianie)"],
        ["forcedUP"] = ["Forced seeding", "Kényszerített feltöltés", "Erzwungenes Seeden", "Compartición forzada", "Partage forcé", "Seeding forzato", "Wymuszone udostępnianie"],
        ["allocating"] = ["Allocating", "Terület lefoglalása", "Speicher wird reserviert", "Asignando espacio", "Allocation de l'espace", "Allocazione spazio", "Przydzielanie miejsca"],
        ["downloading"] = ["Downloading", "Letöltés", "Herunterladen", "Descargando", "Téléchargement", "Download", "Pobieranie"],
        ["metaDL"] = ["Fetching metadata", "Metaadatok lekérése", "Metadaten werden abgerufen", "Obteniendo metadatos", "Récupération des métadonnées", "Recupero metadati", "Pobieranie metadanych"],
        ["forcedDL"] = ["Forced download", "Kényszerített letöltés", "Erzwungener Download", "Descarga forzada", "Téléchargement forcé", "Download forzato", "Wymuszone pobieranie"],
        ["pausedDL"] = ["Paused", "Szüneteltetve", "Pausiert", "En pausa", "En pause", "In pausa", "Wstrzymano"],
        ["queuedDL"] = ["Queued", "Várólistán", "Warteschlange", "En cola", "En file d'attente", "In coda", "W kolejce"],
        ["stalledDL"] = ["Stalled", "Elakadt", "Blockiert", "Estancado", "Bloqué", "Bloccato", "Zatrzymano"],
        ["checkingDL"] = ["Checking", "Ellenőrzés", "Prüfen", "Comprobando", "Vérification", "Verifica", "Sprawdzanie"],
        ["checkingResumeData"] = ["Checking resume data", "Folytatási adatok ellenőrzése", "Fortsetzungsdaten werden geprüft", "Comprobando datos de reanudación", "Vérification des données de reprise", "Verifica dati di ripresa", "Sprawdzanie danych wznowienia"],
        ["moving"] = ["Moving", "Áthelyezés", "Verschieben", "Moviendo", "Déplacement", "Spostamento", "Przenoszenie"],
        ["unknown"] = ["Unknown", "Ismeretlen", "Unbekannt", "Desconocido", "Inconnu", "Sconosciuto", "Nieznany"],
        ["checking"] = ["Checking", "Ellenőrzés", "Prüfen", "Comprobando", "Vérification", "Verifica", "Sprawdzanie"],
        ["paused"] = ["Paused", "Szüneteltetve", "Pausiert", "En pausa", "En pause", "In pausa", "Wstrzymano"],
        ["stoppedDL"] = ["Stopped", "Leállítva", "Angehalten", "Detenido", "Arrêté", "Fermato", "Zatrzymano"],
        ["stoppedUP"] = ["Stopped", "Leállítva", "Angehalten", "Detenido", "Arrêté", "Fermato", "Zatrzymano"]
    };
    private static readonly Dictionary<string, int> LanguageIndexes = new(StringComparer.OrdinalIgnoreCase) { ["en"] = 0, ["hu"] = 1, ["de"] = 2, ["es"] = 3, ["fr"] = 4, ["it"] = 5, ["pl"] = 6 };

    public static string GetLabel(string state)
    {
        var language = LocalizationService.Instance.Language;
        var index = LanguageIndexes.GetValueOrDefault(language, 0);
        return Labels.TryGetValue(state, out var labels) ? labels[index] : $"{Labels["unknown"][index]} ({state})";
    }
    public static string GetIcon(string state) => state switch
    {
        "downloading" or "forcedDL" => "\u2193",
        "metaDL" or "allocating" or "moving" => "\u2026",
        "uploading" or "forcedUP" or "stalledUP" => "\u2191",
        "pausedDL" or "pausedUP" or "paused" => "\u2161",
        "queuedDL" or "queuedUP" => "\u29D6",
        "stalledDL" => "!",
        "checkingDL" or "checkingUP" or "checking" or "checkingResumeData" => "\u21BB",
        "error" or "missingFiles" => "\u00D7",
        "stoppedDL" or "stoppedUP" => "\u25A0",
        _ => "?"
    };

    private static string GetLegacyIcon(string state) => state switch
    {
        "downloading" or "forcedDL" => "↓",
        "metaDL" => "…",
        "uploading" or "forcedUP" or "stalledUP" => "↑",
        "pausedDL" or "pausedUP" or "paused" => "Ⅱ",
        "queuedDL" or "queuedUP" => "⌛",
        "stalledDL" => "!",
        "checkingDL" or "checkingUP" or "checking" or "checkingResumeData" => "↻",
        "allocating" or "moving" => "…",
        "error" or "missingFiles" => "×",
        "stoppedDL" or "stoppedUP" => "■",
        _ => "?"
    };
}
