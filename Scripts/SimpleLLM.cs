using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;

// ═══════════════════════════════════════════════════════════════════════════
// SimpleLLM.cs — Pipeline LLM multi-providers
//
// PROVIDERS : Groq Cloud | Unsloth | Gemini
//   → Groq & Unsloth  : format OpenAI standard (Bearer token)
//   → Gemini           : endpoint OpenAI-compatible + clé en param ?key=...
//
// MODES (préfixe du message) :
//   [QA]    → répond EN TANT QUE la victime chargée
//   [ORDRE] → détecte une commande, ajoute [ACTION:...]
// ═══════════════════════════════════════════════════════════════════════════

public class SimpleLLM : MonoBehaviour
{
    [Header("Modèle actif")]
    [SerializeField] private string modeleActif = "llama-3.3-70b-versatile";

    [Header("Paramètres de génération")]
    [SerializeField] private float temperature    = 0.7f;
    [SerializeField] private int   maxTokens      = 512;
    [SerializeField] private bool  conserHistory  = true;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    // ── Références ────────────────────────────────────────────────────────
    private APIProviderManager _apiMgr;
    private VictimManager      _victimMgr;
    private string             _cleApi;
    private bool               _pret = false;

    // ── Historique de conversation ─────────────────────────────────────────
    private List<MessageJSON> _historique = new();

    // ── Événements ────────────────────────────────────────────────────────
    public event Action         OnDemandeCommencee;
    public event Action<string> OnReponseRecue;
    public event Action<string> OnErreur;

    // ── Propriétés publiques ─────────────────────────────────────────────
    public string ModeleActif => modeleActif;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    public void Init(APIProviderManager apiMgr, string cleApi, VictimManager victimMgr)
    {
        _apiMgr   = apiMgr;
        _cleApi   = cleApi;
        _victimMgr = victimMgr;
        _pret     = true;
        Debug.Log($"[SimpleLLM] ✓ Prêt | Provider={_apiMgr.LLMActif} | Modèle={modeleActif}");
    }

    public void DefinirModele(string modele)
    {
        modeleActif = modele;
        Debug.Log($"[SimpleLLM] Modèle changé → {modele}");
    }

    public string[] ObtenirModelesDisponibles()
        => _apiMgr?.ObtenirModelesLLM() ?? new[] { modeleActif };

    // ════════════════════════════════════════════════════════════════════
    // PROMPT SYSTÈME — Construit selon la victime active et le mode
    // ════════════════════════════════════════════════════════════════════

    private string ConstruireSystemPrompt()
    {
        var victime = _victimMgr?.ActiveVictim;

        string promptVictime = victime != null
            ? $@"Tu joues le rôle d'une victime d'accident.
Nom           : {victime.victimName}
Catégorie     : {victime.victimCategory}
Rôle          : {victime.roleDescription}
Contexte      : {victime.accidentContext}
État actuel   : {victime.currentState}
Profil émot.  : {victime.emotionProfile}
Règles        : {victime.behaviorRules}"
            : "Tu joues le rôle d'une victime d'accident. Réponds de façon réaliste et émotionnelle.";

        string promptOrdres = @"
=== COMMANDES DE MOUVEMENT ===
Si le message commence par [ORDRE], analyse l'intention et ajoute le tag AVANT ta réponse :

[ACTION:STAND_UP]          → se lever et attendre
[ACTION:WALK_LEFT]         → marcher à gauche (déjà debout)
[ACTION:WALK_RIGHT]        → marcher à droite (déjà debout)
[ACTION:WALK_RANDOM]       → marcher sans direction précise
[ACTION:STAND_WALK_LEFT]   → se lever et marcher à gauche
[ACTION:STAND_WALK_RIGHT]  → se lever et marcher à droite
[ACTION:STAND_WALK_RANDOM] → se lever et marcher (direction aléatoire)
[ACTION:RESET]             → s'arrêter / retour position initiale
[ACTION:WAVE]              → lever le bras / faire signe
[ACTION:STOP_WAVE]         → arrêter le geste

Exemples :
  [ORDRE] Levez-vous                    → [ACTION:STAND_UP] Bien compris.
  [ORDRE] Tout le monde à droite        → [ACTION:WALK_RIGHT] D'accord.
  [ORDRE] Faites signe si vous m'entendez → [ACTION:WAVE] Compris.

=== QUESTIONS À LA VICTIME ===
Si le message commence par [QA], réponds EN TANT QUE la victime.
Réponses courtes (1-2 phrases), réalistes et émotionnelles. Aucun tag [ACTION].

Si aucun préfixe → Ne réponds pas.";

        return promptVictime + promptOrdres;
    }

    // ════════════════════════════════════════════════════════════════════
    // RECHARGEMENT DU PROMPT (après changement de victime)
    // ════════════════════════════════════════════════════════════════════

    public void RechargerSystemPrompt()
    {
        _historique.Clear();
        if (debug) Debug.Log($"[SimpleLLM] Prompt rechargé → {_victimMgr?.ActiveVictim?.victimName ?? "aucune victime"}");
    }

    public void ReinitialiserConversation()
    {
        _historique.Clear();
        if (debug) Debug.Log("[SimpleLLM] Conversation réinitialisée.");
    }

    // ════════════════════════════════════════════════════════════════════
    // ENVOI D'UN MESSAGE
    // ════════════════════════════════════════════════════════════════════

    public async System.Threading.Tasks.Task Ask(string messageUtilisateur)
    {
        if (!_pret) { Debug.LogWarning("[SimpleLLM] Non initialisé."); return; }
        if (string.IsNullOrWhiteSpace(messageUtilisateur)) return;

        OnDemandeCommencee?.Invoke();

        if (conserHistory)
            _historique.Add(new MessageJSON { role = "user", content = messageUtilisateur });

        string json  = ConstruireCorpsRequete();
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        string url   = ObtenirURLAvecCle();

        if (debug) Debug.Log($"[SimpleLLM] Envoi → {messageUtilisateur}\nURL={url}");

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        // Gemini utilise la clé en query param, les autres en Bearer
        if (_apiMgr.LLMActif != APIProviderManager.ProviderLLM.Gemini)
            req.SetRequestHeader("Authorization", "Bearer " + _cleApi);

        var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
        req.SendWebRequest().completed += _ => tcs.TrySetResult(true);
        await tcs.Task;

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = $"[SimpleLLM] ❌ {req.error} | {req.downloadHandler.text}";
            Debug.LogError(err);
            OnErreur?.Invoke(err);
            if (conserHistory && _historique.Count > 0)
                _historique.RemoveAt(_historique.Count - 1);
            return;
        }

        try
        {
            string contenu = ExtraireContenuReponse(req.downloadHandler.text);
            if (string.IsNullOrEmpty(contenu))
            {
                OnErreur?.Invoke("[SimpleLLM] Réponse vide du LLM.");
                return;
            }

            if (conserHistory)
                _historique.Add(new MessageJSON { role = "assistant", content = contenu });

            if (debug) Debug.Log($"[SimpleLLM] Réponse → {contenu}");
            OnReponseRecue?.Invoke(contenu);
        }
        catch (Exception ex)
        {
            string err = $"[SimpleLLM] Erreur parse : {ex.Message}";
            Debug.LogError(err);
            OnErreur?.Invoke(err);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CONSTRUCTION DE LA REQUÊTE
    // ════════════════════════════════════════════════════════════════════

    private string ConstruireCorpsRequete()
    {
        var messages = new List<MessageJSON>
        {
            new MessageJSON { role = "system", content = ConstruireSystemPrompt() }
        };
        if (conserHistory) messages.AddRange(_historique);

        var corps = new CorpsRequete
        {
            model       = modeleActif,
            messages    = messages,
            temperature = temperature,
            max_tokens  = maxTokens
        };

        return JsonConvert.SerializeObject(corps);
    }

    private string ObtenirURLAvecCle()
    {
        string url = _apiMgr.URLLLMActive;
        // Gemini nécessite la clé en query parameter
        if (_apiMgr.LLMActif == APIProviderManager.ProviderLLM.Gemini)
            url += $"?key={_cleApi}";
        return url;
    }

    private string ExtraireContenuReponse(string jsonBrut)
    {
        // Format OpenAI / Groq / Unsloth / Gemini OpenAI-compat
        var reponse = JsonConvert.DeserializeObject<CorpsReponse>(jsonBrut);
        return reponse?.choices?[0]?.message?.content ?? "";
    }

    // ════════════════════════════════════════════════════════════════════
    // MODÈLES JSON
    // ════════════════════════════════════════════════════════════════════

    [Serializable] private class MessageJSON   { public string role; public string content; }
    [Serializable] private class CorpsRequete
    {
        public string            model;
        public List<MessageJSON> messages;
        public float             temperature;
        public int               max_tokens;
    }
    [Serializable] private class Choix        { public MessageJSON message; }
    [Serializable] private class CorpsReponse { public List<Choix> choices; }
}
