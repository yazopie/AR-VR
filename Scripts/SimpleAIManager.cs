using UnityEngine;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// ═══════════════════════════════════════════════════════════════════════════
// SimpleAIManager.cs — Orchestre le pipeline STT → LLM → TTS
//
// CORRECTIONS / AMÉLIORATIONS :
//   ① FeedbackManager.DemarrerSession() appelé au chargement de victime
//   ② Audio WAV transmis depuis SimpleSTT vers FeedbackManager
//   ③ llmResponseClean correctement nettoyé via FeedbackManager.SupprimerTousTags
//   ④ Clés API récupérées depuis APIProviderManager (un provider par pipeline)
// ═══════════════════════════════════════════════════════════════════════════

public class SimpleAIManager : MonoBehaviour
{
    [Header("── Managers ──────────────────────────────────")]
    [SerializeField] private SimpleAPIKeys      apiKeys;
    [SerializeField] private APIProviderManager apiProviderMgr;
    [SerializeField] private VictimManager      victimMgr;
    [SerializeField] private FeedbackManager    feedbackMgr;

    [Header("── Pipeline ─────────────────────────────────")]
    [SerializeField] private SimpleSTT stt;
    [SerializeField] private SimpleLLM llm;
    [SerializeField] private SimpleTTS tts;

    [Header("── UI (optionnel) ───────────────────────────")]
    [SerializeField] private DebugPanel debugPanel;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    // ── État ──────────────────────────────────────────────────────────────
    private string                  _derniereLLMBrute = "";
    private string                  _derniereQuestion = "";
    private CancellationTokenSource _ctsPipeline;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    private async void Start()
    {
        if (!ValiderComposants()) return;

        // 1. Clés
        await apiKeys.Init();
        apiProviderMgr.Init(apiKeys);

        // 2. Victimes
        victimMgr.Init();

        // 3. Feedback
        feedbackMgr.Init();

        // 4. Pipeline STT
        string cleSTS = apiProviderMgr.ObtenirCleSTT();
        if (string.IsNullOrEmpty(cleSTS))
        {
            Debug.LogError("[SimpleAIManager] ❌ Clé STT manquante — vérifiez Resources/Secure/APIkeys.txt");
            return;
        }
        stt.Init(apiProviderMgr, cleSTS);

        // 5. Pipeline LLM
        string cleLLM = apiProviderMgr.ObtenirCleeLLM();
        if (string.IsNullOrEmpty(cleLLM))
        {
            Debug.LogError("[SimpleAIManager] ❌ Clé LLM manquante.");
            return;
        }
        llm.Init(apiProviderMgr, cleLLM, victimMgr);

        // 6. Pipeline TTS
        string cleTTS = apiProviderMgr.ObtenirCleTTS();
        if (string.IsNullOrEmpty(cleTTS))
        {
            Debug.LogError("[SimpleAIManager] ❌ Clé TTS manquante.");
            return;
        }
        tts.Init(apiProviderMgr, cleTTS);

        // 7. Abonnements événements
        llm.OnReponseRecue  += GererReponseLLM;
        llm.OnErreur        += GererErreur;
        tts.OnParoleTerminee += GererParoleTerminee;
        tts.OnErreur        += GererErreur;

        // 8. Démarrer la session feedback avec la victime active
        // et la recharger à chaque changement de victime
        victimMgr.OnVictimeChargee += OnVictimeChargee;
        if (victimMgr.ActiveVictim != null)
            DemarrerSessionFeedback(victimMgr.ActiveVictim);

        Debug.Log("[SimpleAIManager] ✅ Pipeline complet prêt — STT → LLM → TTS");
    }

    // ════════════════════════════════════════════════════════════════════
    // CHANGEMENT DE VICTIME
    // ════════════════════════════════════════════════════════════════════

    private void OnVictimeChargee(VictimData victime)
    {
        // Recharge le prompt LLM avec la nouvelle victime
        llm.RechargerSystemPrompt();

        // Applique la voix TTS de la victime
        tts.AppliquerVoixVictime(victime);

        // Démarre/renouvelle la session feedback
        DemarrerSessionFeedback(victime);

        if (debug) Debug.Log($"[SimpleAIManager] Victime changée → {victime.victimName}");
    }

    private void DemarrerSessionFeedback(VictimData victime)
    {
        feedbackMgr?.DemarrerSession(victime);
    }

    // ════════════════════════════════════════════════════════════════════
    // RÉPONSE LLM → TTS
    // ════════════════════════════════════════════════════════════════════

    private async void GererReponseLLM(string reponse)
    {
        if (_ctsPipeline?.IsCancellationRequested == true) return;

        _ctsPipeline      = new CancellationTokenSource();
        _derniereLLMBrute = reponse;

        if (debug) Debug.Log($"[SimpleAIManager] LLM → {reponse}");

        // Affiche dans le panel de debug
        debugPanel?.SetResponse(reponse);

        // Si c'est un ORDRE → pas de TTS, mais on libère quand même
        if (reponse.Contains("[ACTION:"))
        {
            if (debug) Debug.Log("[SimpleAIManager] Ordre détecté → TTS ignoré.");
            GererParoleTerminee();
            return;
        }

        // Supprime les tags avant le TTS
        string texteLu = SupprimerTagsAction(reponse);

        if (!string.IsNullOrWhiteSpace(texteLu))
            await tts.Dire(texteLu);
        else
            GererParoleTerminee();
    }

    // ════════════════════════════════════════════════════════════════════
    // FIN DE PAROLE
    // ════════════════════════════════════════════════════════════════════

    private void GererParoleTerminee()
    {
        if (_ctsPipeline?.IsCancellationRequested == true) return;

        // ② Récupère les bytes WAV du dernier enregistrement
        byte[] octetsAudio = stt?.DerniersOctetsWAV;

        // ③ Enregistre dans le feedback avec la réponse nettoyée
        feedbackMgr?.EnregistrerEchange(
            question:   _derniereQuestion,
            llmBrut:    _derniereLLMBrute,
            octetsAudio: octetsAudio
        );

        _ctsPipeline?.Dispose();
        _ctsPipeline = null;

        if (debug) Debug.Log("[SimpleAIManager] Pipeline libéré.");
    }

    /// <summary>Appelé par RaycastPTT pour transmettre la question posée.</summary>
    public void DefinirDerniereQuestion(string question)
    {
        _derniereQuestion = question;
        debugPanel?.SetQuestion(question);
    }

    // ════════════════════════════════════════════════════════════════════
    // ANNULATION
    // ════════════════════════════════════════════════════════════════════

    public void AnnulerPipeline()
    {
        _ctsPipeline?.Cancel();
        stt?.Annuler();
        tts?.Annuler();
        Debug.Log("[SimpleAIManager] Pipeline annulé.");
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private void GererErreur(string erreur)
    {
        Debug.LogError("[SimpleAIManager] " + erreur);
        _ctsPipeline?.Dispose();
        _ctsPipeline = null;
    }

    private static string SupprimerTagsAction(string texte)
    {
        if (string.IsNullOrEmpty(texte)) return texte;
        // Supprime [ACTION:XXX] et tous les autres tags
        return FeedbackManager.SupprimerTousTags(texte);
    }

    private bool ValiderComposants()
    {
        bool ok = true;
        if (apiKeys       == null) { Debug.LogError("[SimpleAIManager] SimpleAPIKeys manquant.");      ok = false; }
        if (apiProviderMgr == null){ Debug.LogError("[SimpleAIManager] APIProviderManager manquant."); ok = false; }
        if (victimMgr     == null) { Debug.LogError("[SimpleAIManager] VictimManager manquant.");      ok = false; }
        if (feedbackMgr   == null) { Debug.LogError("[SimpleAIManager] FeedbackManager manquant.");    ok = false; }
        if (stt           == null) { Debug.LogError("[SimpleAIManager] SimpleSTT manquant.");          ok = false; }
        if (llm           == null) { Debug.LogError("[SimpleAIManager] SimpleLLM manquant.");          ok = false; }
        if (tts           == null) { Debug.LogError("[SimpleAIManager] SimpleTTS manquant.");          ok = false; }
        return ok;
    }

    private void OnDestroy()
    {
        _ctsPipeline?.Cancel();
        _ctsPipeline?.Dispose();
        if (llm != null) llm.OnReponseRecue  -= GererReponseLLM;
        if (tts != null) tts.OnParoleTerminee -= GererParoleTerminee;
        if (victimMgr != null) victimMgr.OnVictimeChargee -= OnVictimeChargee;
    }
}
