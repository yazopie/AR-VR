using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

// ═══════════════════════════════════════════════════════════════════════════
// FeedbackManager.cs — Enregistre sessions victime + échanges Q/R + audio
//
// CORRECTIONS :
//   ① victimInfo : rempli via StartSession(VictimData) et mis à jour sur
//                  changement de victime
//   ② audio      : chemin centralisé (CheminsDonnees), bytes passés depuis STT
//   ③ llmResponseClean : suppression de tous les tags [] ET <>
// ═══════════════════════════════════════════════════════════════════════════

[Serializable]
public class EchangeFeedback
{
    public int    exchangeIndex;
    public string timestamp;
    public string userQuestion;
    public string llmResponseRaw;
    public string llmResponseClean;  // Sans aucun tag [..] ou <..>
    public string audioFileName;     // null si pas d'audio
    public string audioCheminComplet;// Chemin absolu du fichier WAV
}

[Serializable]
public class SessionFeedback
{
    public string                  sessionId;
    public string                  startTimestamp;
    public VictimData              victimInfo;       // JAMAIS null après StartSession
    public List<EchangeFeedback>   exchanges = new();
}

public class FeedbackManager : MonoBehaviour
{
    [Header("Dossiers (centralisés dans CheminsDonnees)")]
    [SerializeField] private bool debug = false;

    private string         _dossierFeedback;
    private string         _dossierAudio;
    private SessionFeedback _sessionCourante;
    private int            _compteurEchanges = 0;

    // Regex pour supprimer TOUS les tags [] et <>
    private static readonly Regex _regexTagsCrochets  = new(@"\[[^\]]*\]",   RegexOptions.Compiled);
    private static readonly Regex _regexTagsAngle     = new(@"<[^>]+>",      RegexOptions.Compiled);
    private static readonly Regex _regexEspaces       = new(@"\s{2,}",       RegexOptions.Compiled);

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    public void Init()
    {
        _dossierFeedback = CheminsDonnees.ObtenirDossierFeedback();
        _dossierAudio    = CheminsDonnees.ObtenirDossierAudio();
        Debug.Log($"[FeedbackManager] ✓ Dossiers initialisés\n  Feedback : {_dossierFeedback}\n  Audio    : {_dossierAudio}");
    }

    // ════════════════════════════════════════════════════════════════════
    // GESTION DE SESSION
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Démarre une nouvelle session pour la victime donnée.
    /// victimInfo ne sera JAMAIS null dans le JSON de sortie.
    /// </summary>
    public void DemarrerSession(VictimData victime)
    {
        _sessionCourante = new SessionFeedback
        {
            sessionId      = Guid.NewGuid().ToString("N").Substring(0, 12),
            startTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            victimInfo     = victime ?? new VictimData
            {
                id          = "inconnu",
                victimName  = "Victime inconnue",
                currentState = "Non défini"
            }
        };
        _compteurEchanges = 0;

        if (debug)
            Debug.Log($"[FeedbackManager] Session démarrée : {_sessionCourante.sessionId}" +
                      $" | Victime : {_sessionCourante.victimInfo.victimName}");
    }

    /// <summary>Met à jour la victimInfo si la victime change en cours de session.</summary>
    public void MettreAJourVictime(VictimData victime)
    {
        if (_sessionCourante == null) return;
        _sessionCourante.victimInfo = victime;
        SauvegarderSession();
    }

    // ════════════════════════════════════════════════════════════════════
    // ENREGISTREMENT D'UN ÉCHANGE
    // ════════════════════════════════════════════════════════════════════

    /// <param name="question">Question de l'utilisateur</param>
    /// <param name="llmBrut">Réponse brute du LLM (avec tags)</param>
    /// <param name="octetsAudio">Bytes WAV de l'enregistrement (peut être null)</param>
    public void EnregistrerEchange(string question, string llmBrut, byte[] octetsAudio = null)
    {
        if (_sessionCourante == null)
        {
            Debug.LogWarning("[FeedbackManager] ⚠ Aucune session active. Appele DemarrerSession() d'abord.");
            return;
        }

        _compteurEchanges++;

        // ① Nettoyage de la réponse — supprime TOUS les tags [] et <>
        string llmPropre = SupprimerTousTags(llmBrut);

        // ② Enregistrement audio
        string nomFichierAudio    = null;
        string cheminFichierAudio = null;

        if (octetsAudio != null && octetsAudio.Length > 0)
        {
            nomFichierAudio    = $"{_sessionCourante.sessionId}_echange_{_compteurEchanges:D3}.wav";
            cheminFichierAudio = Path.Combine(_dossierAudio, nomFichierAudio);

            try
            {
                File.WriteAllBytes(cheminFichierAudio, octetsAudio);
                if (debug) Debug.Log($"[FeedbackManager] Audio sauvegardé : {cheminFichierAudio}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FeedbackManager] ❌ Impossible d'écrire l'audio : {ex.Message}");
                nomFichierAudio    = null;
                cheminFichierAudio = null;
            }
        }

        // ③ Ajout de l'échange
        var echange = new EchangeFeedback
        {
            exchangeIndex    = _compteurEchanges,
            timestamp        = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            userQuestion     = question,
            llmResponseRaw   = llmBrut,
            llmResponseClean = llmPropre,
            audioFileName    = nomFichierAudio,
            audioCheminComplet = cheminFichierAudio
        };

        _sessionCourante.exchanges.Add(echange);
        SauvegarderSession();

        if (debug) Debug.Log($"[FeedbackManager] Échange #{_compteurEchanges} enregistré.");
    }

    // ════════════════════════════════════════════════════════════════════
    // SAUVEGARDE JSON
    // ════════════════════════════════════════════════════════════════════

    public void SauvegarderSession()
    {
        if (_sessionCourante == null) return;

        string json    = JsonConvert.SerializeObject(_sessionCourante, Formatting.Indented);
        string chemin  = Path.Combine(_dossierFeedback, $"session_{_sessionCourante.sessionId}.json");

        try
        {
            File.WriteAllText(chemin, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FeedbackManager] ❌ Sauvegarde impossible : {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // NETTOYAGE DES TAGS
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Supprime tous les tags entre crochets [TAG] et balises chevrons <tag>
    /// de la réponse LLM avant stockage dans llmResponseClean.
    /// </summary>
    public static string SupprimerTousTags(string texte)
    {
        if (string.IsNullOrEmpty(texte)) return texte;

        string resultat = _regexTagsCrochets.Replace(texte, "");
        resultat        = _regexTagsAngle.Replace(resultat, "");
        resultat        = _regexEspaces.Replace(resultat, " ");
        return resultat.Trim();
    }

    // ════════════════════════════════════════════════════════════════════
    // ACCESSEURS
    // ════════════════════════════════════════════════════════════════════

    public string ObtenirCheminFeedback()    => _dossierFeedback;
    public string ObtenirIdSessionCourante() => _sessionCourante?.sessionId;
    public bool   SessionActive              => _sessionCourante != null;
}
