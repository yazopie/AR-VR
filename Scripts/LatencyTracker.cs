using UnityEngine;
using System;
using System.Diagnostics;
using System.IO;

// ═══════════════════════════════════════════════════════════════════════════
// LatencyTracker.cs — Mesure ASR / LLM / TTS / TOTAL
//
// NOUVEAU : Colonne "Modèle" pour chaque pipeline (STT, LLM, TTS)
// CSV : Session,ModèleLLM,ModèleSTT,ModèleTTS,ASR_ms,LLM_ms,TTS_ms,Total_ms
// Chemin : CheminsDonnees.ObtenirCheminLatence()
// ═══════════════════════════════════════════════════════════════════════════

public class LatencyTracker : MonoBehaviour
{
    [Header("Composants à mesurer")]
    [SerializeField] private SimpleSTT stt;
    [SerializeField] private SimpleLLM llm;
    [SerializeField] private SimpleTTS tts;

    [Header("Export CSV")]
    [SerializeField] private bool exportCSV = true;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    // ── Chronomètres ─────────────────────────────────────────────────────
    private readonly Stopwatch _swASR   = new();
    private readonly Stopwatch _swLLM   = new();
    private readonly Stopwatch _swTTS   = new();
    private readonly Stopwatch _swTotal = new();

    // ── État ──────────────────────────────────────────────────────────────
    private string _cheminCSV;
    private int    _indexSession = 0;

    // Modèles capturés au moment de l'enregistrement
    private string _modeleSTTCapture  = "";
    private string _modeleLLMCapture  = "";
    private string _modeleTTSCapture  = "";

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (stt == null || llm == null || tts == null)
        {
            UnityEngine.Debug.LogError("[LatencyTracker] ❌ Assigne STT, LLM et TTS dans l'Inspector !");
            return;
        }

        // Abonnements aux événements du pipeline
        stt.OnEnregistrementCommence += OnEnregistrementCommence;
        stt.OnTranscriptionRecue     += OnTranscriptionRecue;
        llm.OnDemandeCommencee       += OnDemandeCommencee;
        llm.OnReponseRecue           += OnReponseRecue;
        tts.OnParoleCommencee        += OnParoleCommencee;
        tts.OnParoleTerminee         += OnParoleTerminee;

        if (exportCSV)
        {
            _cheminCSV = CheminsDonnees.ObtenirCheminLatence();
            if (!File.Exists(_cheminCSV))
            {
                // En-tête avec colonnes Modèle
                File.WriteAllText(_cheminCSV,
                    "Session,ModèleLLM,ModèleSTT,ModèleTTS,ASR_ms,LLM_ms,TTS_ms,Total_ms\n");
            }
            UnityEngine.Debug.Log($"[LatencyTracker] CSV → {_cheminCSV}");
        }

        UnityEngine.Debug.Log("[LatencyTracker] ✓ Prêt.");
    }

    // ════════════════════════════════════════════════════════════════════
    // ÉVÉNEMENTS DU PIPELINE
    // ════════════════════════════════════════════════════════════════════

    private void OnEnregistrementCommence()
    {
        // Capture les modèles actifs au départ de la session
        _modeleSTTCapture = stt?.ModeleActif ?? "inconnu";
        _modeleLLMCapture = llm?.ModeleActif ?? "inconnu";
        _modeleTTSCapture = tts?.ModeleActif ?? "inconnu";

        _swTotal.Restart();
        _swASR.Restart();

        if (debug) UnityEngine.Debug.Log($"[Latence] ▶ Session démarrée\n  STT={_modeleSTTCapture} | LLM={_modeleLLMCapture} | TTS={_modeleTTSCapture}");
    }

    private void OnTranscriptionRecue(string transcription)
    {
        _swASR.Stop();
        _swLLM.Restart();
        UnityEngine.Debug.Log($"[Latence] ASR : {_swASR.ElapsedMilliseconds} ms | \"{Tronquer(transcription, 60)}\"");
    }

    private void OnDemandeCommencee()
    {
        // Synchronisation si besoin (chrono LLM déjà démarré dans OnTranscriptionRecue)
    }

    private void OnReponseRecue(string reponse)
    {
        _swLLM.Stop();
        _swTTS.Restart();
        UnityEngine.Debug.Log($"[Latence] LLM : {_swLLM.ElapsedMilliseconds} ms | \"{Tronquer(reponse, 60)}\"");
    }

    private void OnParoleCommencee()
    {
        // TTS a déjà démarré dans OnReponseRecue — rien à faire
    }

    private void OnParoleTerminee()
    {
        _swTTS.Stop();
        _swTotal.Stop();

        long msASR   = _swASR.ElapsedMilliseconds;
        long msLLM   = _swLLM.ElapsedMilliseconds;
        long msTTS   = _swTTS.ElapsedMilliseconds;
        long msTotal = _swTotal.ElapsedMilliseconds;

        UnityEngine.Debug.Log(
            $"[Latence] ─────────────────────────────────────────\n" +
            $"[Latence]  Modèle LLM : {_modeleLLMCapture}\n" +
            $"[Latence]  Modèle STT : {_modeleSTTCapture}\n" +
            $"[Latence]  Modèle TTS : {_modeleTTSCapture}\n" +
            $"[Latence]  ASR        : {msASR,6} ms\n" +
            $"[Latence]  LLM        : {msLLM,6} ms\n" +
            $"[Latence]  TTS        : {msTTS,6} ms\n" +
            $"[Latence]  TOTAL      : {msTotal,6} ms\n" +
            $"[Latence] ─────────────────────────────────────────"
        );

        if (exportCSV)
        {
            string ligne = $"{++_indexSession}," +
                           $"\"{_modeleLLMCapture}\"," +
                           $"\"{_modeleSTTCapture}\"," +
                           $"\"{_modeleTTSCapture}\"," +
                           $"{msASR},{msLLM},{msTTS},{msTotal}\n";
            try { File.AppendAllText(_cheminCSV, ligne); }
            catch (Exception ex) { UnityEngine.Debug.LogError($"[LatencyTracker] ❌ Écriture CSV : {ex.Message}"); }
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // NETTOYAGE
    // ════════════════════════════════════════════════════════════════════

    private void OnDestroy()
    {
        if (stt != null)
        {
            stt.OnEnregistrementCommence -= OnEnregistrementCommence;
            stt.OnTranscriptionRecue     -= OnTranscriptionRecue;
        }
        if (llm != null)
        {
            llm.OnDemandeCommencee -= OnDemandeCommencee;
            llm.OnReponseRecue     -= OnReponseRecue;
        }
        if (tts != null)
        {
            tts.OnParoleCommencee -= OnParoleCommencee;
            tts.OnParoleTerminee  -= OnParoleTerminee;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPER
    // ════════════════════════════════════════════════════════════════════

    private string Tronquer(string s, int max)
        => s == null ? "" : s.Length <= max ? s : s.Substring(0, max) + "...";
}
