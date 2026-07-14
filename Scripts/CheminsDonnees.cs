using UnityEngine;
using System.IO;

// ═══════════════════════════════════════════════════════════════════════════
// CheminsDonnees.cs — Centralise tous les chemins de stockage du projet GAIA
// Tous les dossiers de sortie sont regroupés sous Application.persistentDataPath
// ═══════════════════════════════════════════════════════════════════════════

public static class CheminsDonnees
{
    // ── Racine ────────────────────────────────────────────────────────────
    public const string DossierRacine      = "GAIA";
    public const string DossierLatence     = "GAIA/Latence";
    public const string DossierFeedback    = "GAIA/Feedback";
    public const string DossierAudio       = "GAIA/Audio";
    public const string DossierVictimes    = "GAIA/Victimes";
    public const string DossierConfig      = "GAIA/Config";

    // ── Chemins dans Resources (lecture seule) ────────────────────────────
    public const string ResourcesVictimes  = "Victime-Store";   // JSON victimes
    public const string ResourcesCles      = "Secure/APIkeys";  // Clés API
    public const string ResourcesConfig    = "Config/gaia_config"; // Config JSON

    // ── Fichiers CSV / JSON ───────────────────────────────────────────────
    public const string FichierLatence     = "latence_log.csv";
    public const string FichierConfig      = "gaia_config.json";

    // ─────────────────────────────────────────────────────────────────────
    // Méthodes utilitaires : retournent le chemin absolu et créent le dossier
    // ─────────────────────────────────────────────────────────────────────

    public static string ObtenirDossierLatence()
    {
        string path = Path.Combine(Application.persistentDataPath, DossierLatence);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ObtenirDossierFeedback()
    {
        string path = Path.Combine(Application.persistentDataPath, DossierFeedback);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ObtenirDossierAudio()
    {
        string path = Path.Combine(Application.persistentDataPath, DossierAudio);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ObtenirDossierVictimes()
    {
        string path = Path.Combine(Application.persistentDataPath, DossierVictimes);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ObtenirDossierConfig()
    {
        string path = Path.Combine(Application.persistentDataPath, DossierConfig);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ObtenirCheminLatence()
        => Path.Combine(ObtenirDossierLatence(), FichierLatence);

    public static string ObtenirCheminConfig()
        => Path.Combine(ObtenirDossierConfig(), FichierConfig);

    public static string ObtenirDossierRacine()
    {
        string path = Path.Combine(Application.persistentDataPath, DossierRacine);
        Directory.CreateDirectory(path);
        return path;
    }
}
