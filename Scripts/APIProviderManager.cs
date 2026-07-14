using UnityEngine;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════
// APIProviderManager.cs — Centralise tous les fournisseurs API
//
// PROVIDERS LLM  : GroqCloud | Unsloth | Gemini
// PROVIDERS STT  : GroqWhisper | Unsloth | Gemini
// PROVIDERS TTS  : ElevenLabs | Speechify | Supertone | Gemini
//
// Seule l'URL du provider ACTIF est affichée dans l'Inspector.
// ═══════════════════════════════════════════════════════════════════════════

public class APIProviderManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════
    // ÉNUMÉRATIONS
    // ════════════════════════════════════════════════════════════════════

    public enum ProviderLLM  { GroqCloud, Unsloth, Gemini }
    public enum ProviderSTT  { GroqWhisper, Unsloth, Gemini }
    public enum ProviderTTS  { ElevenLabs, Speechify, Supertone, Gemini }

    public enum FormatEmotion
    {
        Aucun,
        ElevenLabsV3,   // [tag] texte
        SpeechifySSML   // <speak>...</speak>
    }

    // ════════════════════════════════════════════════════════════════════
    // PROVIDER ACTIFS
    // ════════════════════════════════════════════════════════════════════

    [Header("── Providers actifs ──────────────────────────")]
    [SerializeField] private ProviderLLM providerLLMActif = ProviderLLM.GroqCloud;
    [SerializeField] private ProviderSTT providerSTTActif = ProviderSTT.GroqWhisper;
    [SerializeField] private ProviderTTS providerTTSActif = ProviderTTS.ElevenLabs;

    // ════════════════════════════════════════════════════════════════════
    // URLs — UNE SEULE AFFICHÉE (selon le provider actif)
    // ════════════════════════════════════════════════════════════════════

    [Header("── URL LLM active ───────────────────────────")]
    [SerializeField] private string urlGroqLLM    = "https://api.groq.com/openai/v1/chat/completions";
    [SerializeField] private string urlUnslothLLM = "https://api.unsloth.ai/v1/chat/completions";
    [SerializeField] private string urlGeminiLLM  = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";

    [Header("── URL STT active ───────────────────────────")]
    [SerializeField] private string urlGroqSTT    = "https://api.groq.com/openai/v1/audio/transcriptions";
    [SerializeField] private string urlUnslothSTT = "https://api.unsloth.ai/v1/audio/transcriptions";
    [SerializeField] private string urlGeminiSTT  = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    [Header("── URL TTS active ───────────────────────────")]
    [SerializeField] private string urlElevenLabs = "https://api.elevenlabs.io/v1/text-to-speech/";
    [SerializeField] private string urlSpeechify  = "https://api.speechify.com/v1/audio/speech";
    [SerializeField] private string urlSupertone  = "https://supertoneapi.com/v1/text-to-speech";
    [SerializeField] private string urlGeminiTTS  = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

    // ════════════════════════════════════════════════════════════════════
    // MODÈLES DISPONIBLES
    // ════════════════════════════════════════════════════════════════════

    [Header("── Modèles LLM ──────────────────────────────")]
    [SerializeField] private string[] modelesGroqLLM = new[]
    {
        "llama-3.3-70b-versatile",
        "llama-3.1-8b-instant",
        "meta-llama/llama-4-scout-17b-16e-instruct",
        "meta-llama/llama-4-maverick-17b-128e-instruct",
        "gemma2-9b-it",
        "mistral-saba-24b"
    };
    [SerializeField] private string[] modelesUnslothLLM = new[]
    {
        "unsloth/Llama-3.3-70B-Instruct",
        "unsloth/Llama-3.2-3B-Instruct",
        "unsloth/mistral-7b-instruct-v0.3"
    };
    [SerializeField] private string[] modelesGeminiLLM = new[]
    {
        "gemini-2.0-flash",
        "gemini-2.5-flash-preview-05-20",
        "gemini-2.5-pro-preview-06-05",
        "gemini-1.5-flash"
    };

    [Header("── Modèles STT ──────────────────────────────")]
    [SerializeField] private string[] modelesGroqSTT = new[]
    {
        "whisper-large-v3-turbo",
        "whisper-large-v3",
        "distil-whisper-large-v3-en"
    };
    [SerializeField] private string[] modelesUnslothSTT = new[]
    {
        "unsloth/whisper-large-v3-turbo",
        "unsloth/whisper-medium"
    };
    [SerializeField] private string[] modelesGeminiSTT = new[]
    {
        "gemini-2.0-flash",
        "gemini-1.5-flash"
    };

    [Header("── Modèles TTS ──────────────────────────────")]
    [SerializeField] private string[] modelesElevenLabs = new[]
    {
        "eleven_v3",
        "eleven_turbo_v2_5",
        "eleven_multilingual_v2"
    };
    [SerializeField] private string[] modelesSpeechify = new[] { "speechify-default" };
    [SerializeField] private string[] modelesSupertone = new[]
    {
        "sona-1",
        "sona-1-turbo"
    };
    [SerializeField] private string[] modelesGeminiTTS = new[]
    {
        "gemini-2.5-flash-preview-tts",
        "gemini-2.5-pro-preview-tts"
    };

    // ════════════════════════════════════════════════════════════════════
    // VOIX TTS PAR DÉFAUT
    // ════════════════════════════════════════════════════════════════════

    [Header("── Voix par défaut ──────────────────────────")]
    [SerializeField] private string voixIdElevenLabs = "JBFqnCBsd6RMkjVDRZzb";
    [SerializeField] private string voixIdSpeechify  = "default";
    [SerializeField] private string voixIdSupertone  = "";
    [SerializeField] private string voixIdGeminiTTS  = "Aoede";  // Voix Gemini disponible

    // ════════════════════════════════════════════════════════════════════
    // NOMS DES CLÉS (à définir dans Resources/Secure/APIkeys.txt)
    // ════════════════════════════════════════════════════════════════════

    [Header("── Noms des clés API ─────────────────────────")]
    [SerializeField] private string nomCleGroq       = "Groq_API_Key";
    [SerializeField] private string nomCleUnsloth    = "Unsloth_API_Key";
    [SerializeField] private string nomCleGemini     = "Gemini_API_Key";
    [SerializeField] private string nomCleElevenLabs = "ElevenLabs_API_Key";
    [SerializeField] private string nomCleSpeechify  = "Speechify_API_Key";
    [SerializeField] private string nomCleSupertone  = "Supertone_API_Key";

    // ════════════════════════════════════════════════════════════════════
    // ÉTAT INTERNE
    // ════════════════════════════════════════════════════════════════════

    private SimpleAPIKeys _apiKeys;
    private Dictionary<string, string> _cachesCles = new();

    // ── Propriétés publiques ──────────────────────────────────────────────
    public ProviderLLM LLMActif  => providerLLMActif;
    public ProviderSTT STTActif  => providerSTTActif;
    public ProviderTTS TTSActif  => providerTTSActif;

    public FormatEmotion FormatEmotionActuel => providerTTSActif switch
    {
        ProviderTTS.ElevenLabs => FormatEmotion.ElevenLabsV3,
        ProviderTTS.Speechify  => FormatEmotion.SpeechifySSML,
        _                      => FormatEmotion.Aucun
    };

    /// <summary>Retourne uniquement l'URL du LLM actif.</summary>
    public string URLLLMActive => providerLLMActif switch
    {
        ProviderLLM.GroqCloud => urlGroqLLM,
        ProviderLLM.Unsloth   => urlUnslothLLM,
        ProviderLLM.Gemini    => urlGeminiLLM,
        _                     => urlGroqLLM
    };

    /// <summary>Retourne uniquement l'URL du STT actif.</summary>
    public string URLSTTActive => providerSTTActif switch
    {
        ProviderSTT.GroqWhisper => urlGroqSTT,
        ProviderSTT.Unsloth     => urlUnslothSTT,
        ProviderSTT.Gemini      => urlGeminiSTT,
        _                       => urlGroqSTT
    };

    /// <summary>Retourne uniquement l'URL du TTS actif.</summary>
    public string URLTTSActive => providerTTSActif switch
    {
        ProviderTTS.ElevenLabs => urlElevenLabs,
        ProviderTTS.Speechify  => urlSpeechify,
        ProviderTTS.Supertone  => urlSupertone,
        ProviderTTS.Gemini     => urlGeminiTTS,
        _                      => urlElevenLabs
    };

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    public void Init(SimpleAPIKeys apiKeys)
    {
        _apiKeys = apiKeys;
        ChargerCles();
        Debug.Log($"[APIProviderManager] LLM={providerLLMActif} ({URLLLMActive})" +
                  $" | STT={providerSTTActif} ({URLSTTActive})" +
                  $" | TTS={providerTTSActif} ({URLTTSActive})");
    }

    private void ChargerCles()
    {
        _cachesCles.Clear();
        ChargerCle(nomCleGroq);
        ChargerCle(nomCleUnsloth);
        ChargerCle(nomCleGemini);
        ChargerCle(nomCleElevenLabs);
        ChargerCle(nomCleSpeechify);
        ChargerCle(nomCleSupertone);
    }

    private void ChargerCle(string nomCle)
    {
        if (string.IsNullOrEmpty(nomCle)) return;
        string valeur = _apiKeys?.Get(nomCle);
        if (!string.IsNullOrEmpty(valeur))
            _cachesCles[nomCle] = valeur;
    }

    // ════════════════════════════════════════════════════════════════════
    // ACCÈS AUX CLÉS
    // ════════════════════════════════════════════════════════════════════

    public string ObtenirCle(string nomCle)
        => _cachesCles.TryGetValue(nomCle, out var v) ? v : null;

    /// <summary>Retourne la clé API pour le LLM actif.</summary>
    public string ObtenirCleeLLM() => providerLLMActif switch
    {
        ProviderLLM.GroqCloud => ObtenirCle(nomCleGroq),
        ProviderLLM.Unsloth   => ObtenirCle(nomCleUnsloth),
        ProviderLLM.Gemini    => ObtenirCle(nomCleGemini),
        _                     => ObtenirCle(nomCleGroq)
    };

    /// <summary>Retourne la clé API pour le STT actif.</summary>
    public string ObtenirCleSTT() => providerSTTActif switch
    {
        ProviderSTT.GroqWhisper => ObtenirCle(nomCleGroq),
        ProviderSTT.Unsloth     => ObtenirCle(nomCleUnsloth),
        ProviderSTT.Gemini      => ObtenirCle(nomCleGemini),
        _                       => ObtenirCle(nomCleGroq)
    };

    /// <summary>Retourne la clé API pour le TTS actif.</summary>
    public string ObtenirCleTTS() => providerTTSActif switch
    {
        ProviderTTS.ElevenLabs => ObtenirCle(nomCleElevenLabs),
        ProviderTTS.Speechify  => ObtenirCle(nomCleSpeechify),
        ProviderTTS.Supertone  => ObtenirCle(nomCleSupertone),
        ProviderTTS.Gemini     => ObtenirCle(nomCleGemini),
        _                      => ObtenirCle(nomCleElevenLabs)
    };

    // ════════════════════════════════════════════════════════════════════
    // MODÈLES DISPONIBLES
    // ════════════════════════════════════════════════════════════════════

    public string[] ObtenirModelesLLM() => providerLLMActif switch
    {
        ProviderLLM.GroqCloud => modelesGroqLLM,
        ProviderLLM.Unsloth   => modelesUnslothLLM,
        ProviderLLM.Gemini    => modelesGeminiLLM,
        _                     => modelesGroqLLM
    };

    public string[] ObtenirModelesSTT() => providerSTTActif switch
    {
        ProviderSTT.GroqWhisper => modelesGroqSTT,
        ProviderSTT.Unsloth     => modelesUnslothSTT,
        ProviderSTT.Gemini      => modelesGeminiSTT,
        _                       => modelesGroqSTT
    };

    public string[] ObtenirModelesTTS() => providerTTSActif switch
    {
        ProviderTTS.ElevenLabs => modelesElevenLabs,
        ProviderTTS.Speechify  => modelesSpeechify,
        ProviderTTS.Supertone  => modelesSupertone,
        ProviderTTS.Gemini     => modelesGeminiTTS,
        _                      => modelesElevenLabs
    };

    // ════════════════════════════════════════════════════════════════════
    // VOIX TTS ACTIVE
    // ════════════════════════════════════════════════════════════════════

    public string ObtenirVoixIdTTSActive() => providerTTSActif switch
    {
        ProviderTTS.ElevenLabs => voixIdElevenLabs,
        ProviderTTS.Speechify  => voixIdSpeechify,
        ProviderTTS.Supertone  => voixIdSupertone,
        ProviderTTS.Gemini     => voixIdGeminiTTS,
        _                      => voixIdElevenLabs
    };

    public void DefinirVoixId(string voixId)
    {
        switch (providerTTSActif)
        {
            case ProviderTTS.ElevenLabs: voixIdElevenLabs = voixId; break;
            case ProviderTTS.Speechify:  voixIdSpeechify  = voixId; break;
            case ProviderTTS.Supertone:  voixIdSupertone  = voixId; break;
            case ProviderTTS.Gemini:     voixIdGeminiTTS  = voixId; break;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CHANGEMENT DE PROVIDER (runtime + panneau en jeu)
    // ════════════════════════════════════════════════════════════════════

    public void ChangerProviderLLM(ProviderLLM provider)
    {
        providerLLMActif = provider;
        Debug.Log($"[APIProviderManager] LLM → {provider} | URL: {URLLLMActive}");
    }

    public void ChangerProviderSTT(ProviderSTT provider)
    {
        providerSTTActif = provider;
        Debug.Log($"[APIProviderManager] STT → {provider} | URL: {URLSTTActive}");
    }

    public void ChangerProviderTTS(ProviderTTS provider)
    {
        providerTTSActif = provider;
        Debug.Log($"[APIProviderManager] TTS → {provider} | URL: {URLTTSActive}");
    }

    // ════════════════════════════════════════════════════════════════════
    // INFORMATIONS AFFICHAGE PANNEAU
    // ════════════════════════════════════════════════════════════════════

    public string ObtenirInfoProviders()
    {
        return $"LLM actif  : {providerLLMActif}\n  → {URLLLMActive}\n\n" +
               $"STT actif  : {providerSTTActif}\n  → {URLSTTActive}\n\n" +
               $"TTS actif  : {providerTTSActif}\n  → {URLTTSActive}";
    }

    /// <summary>Vérifie que la clé du provider actif est disponible.</summary>
    public bool CleDisponible(string typePipeline)
    {
        string cle = typePipeline switch
        {
            "LLM" => ObtenirCleeLLM(),
            "STT" => ObtenirCleSTT(),
            "TTS" => ObtenirCleTTS(),
            _     => null
        };
        return !string.IsNullOrEmpty(cle);
    }
}
