using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections;
using Newtonsoft.Json;

// ═══════════════════════════════════════════════════════════════════════════
// SimpleSTT.cs — Reconnaissance vocale multi-providers
//
// PROVIDERS : Groq Whisper | Unsloth | Gemini (multimodal audio)
//   → Groq & Unsloth  : envoi WAV via form multipart
//   → Gemini           : envoi audio en base64 dans le corps JSON
//
// Expose les derniers bytes WAV pour le FeedbackManager.
// ═══════════════════════════════════════════════════════════════════════════

public class SimpleSTT : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Code langue (fr, en, ar...)")]
    [SerializeField] private string langue = "fr";

    [Header("Modèle actif")]
    [SerializeField] private string modeleActif = "whisper-large-v3-turbo";

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    // ── Événements ────────────────────────────────────────────────────────
    public event Action         OnEnregistrementCommence;
    public event Action<string> OnTranscriptionRecue;
    public event Action<string> OnErreur;

    // ── État ──────────────────────────────────────────────────────────────
    private APIProviderManager _apiMgr;
    private string             _cleApi;
    private AudioClip          _clip;
    private bool               _enregistrement = false;
    private bool               _traitement     = false;
    private bool               _pret           = false;
    private bool               _annulation     = false;

    // ── Expose les derniers bytes WAV (pour FeedbackManager) ─────────────
    public byte[] DerniersOctetsWAV { get; private set; }

    // ── Propriété publique ────────────────────────────────────────────────
    public string ModeleActif => modeleActif;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    public void Init(APIProviderManager apiMgr, string cleApi)
    {
        _apiMgr = apiMgr;
        _cleApi = cleApi;
        _pret   = true;
        Debug.Log($"[SimpleSTT] ✓ Prêt | Provider={_apiMgr.STTActif} | Modèle={modeleActif}");
    }

    public void DefinirModele(string modele)
    {
        modeleActif = modele;
        Debug.Log($"[SimpleSTT] Modèle changé → {modele}");
    }

    public string[] ObtenirModelesDisponibles()
        => _apiMgr?.ObtenirModelesSTT() ?? new[] { modeleActif };

    // ════════════════════════════════════════════════════════════════════
    // CONTRÔLE ENREGISTREMENT (PTT)
    // ════════════════════════════════════════════════════════════════════

    public void DemarrerEnregistrement()
    {
        if (!_pret)        { Debug.LogWarning("[SimpleSTT] Non initialisé."); return; }
        if (_traitement)   { Debug.LogWarning("[SimpleSTT] Traitement en cours."); return; }

        _enregistrement = true;
        _clip           = null;
        _annulation     = false;
        DerniersOctetsWAV = null;

        OnEnregistrementCommence?.Invoke();
        if (debug) Debug.Log("[SimpleSTT] ▶ Enregistrement démarré.");
    }

    public void ArreterEnregistrement()
    {
        if (!_enregistrement) return;
        _enregistrement = false;
        if (debug) Debug.Log("[SimpleSTT] ■ Enregistrement arrêté.");
    }

    public void Annuler()
    {
        _annulation     = true;
        _enregistrement = false;
        Microphone.End(null);
        if (_clip != null) { Destroy(_clip); _clip = null; }
        DerniersOctetsWAV = null;
        if (debug) Debug.Log("[SimpleSTT] ✕ Annulé.");
    }

    // ════════════════════════════════════════════════════════════════════
    // BOUCLE UNITY
    // ════════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (!_pret) return;

        if (_enregistrement && _clip == null)
        {
            Microphone.End(null);
            _clip = Microphone.Start("", false, 30, 16000);
            if (_clip == null)
            {
                Debug.LogError("[SimpleSTT] ❌ Microphone indisponible.");
                _enregistrement = false;
            }
        }

        if (!_enregistrement && _clip != null && !_traitement)
        {
            _traitement = true;
            AudioClip clipCapture = _clip;
            _clip = null;
            Microphone.End(null);
            StartCoroutine(TraiterAudio(clipCapture));
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // TRAITEMENT AUDIO
    // ════════════════════════════════════════════════════════════════════

    private IEnumerator TraiterAudio(AudioClip clip)
    {
        if (_annulation) { _traitement = false; yield break; }

        // Conversion en WAV
        AI_WAV wav = gameObject.AddComponent<AI_WAV>();
        wav.ConvertClipToWav(clip);
        byte[] octetsWAV = wav.stream.ToArray();
        DerniersOctetsWAV = octetsWAV;
        Destroy(wav);
        Destroy(clip);

        if (_annulation) { _traitement = false; yield break; }
        if (debug) Debug.Log($"[SimpleSTT] Envoi {octetsWAV.Length} bytes vers {_apiMgr.STTActif}...");

        switch (_apiMgr.STTActif)
        {
            case APIProviderManager.ProviderSTT.GroqWhisper:
            case APIProviderManager.ProviderSTT.Unsloth:
                yield return StartCoroutine(TranscrireWhisper(octetsWAV));
                break;

            case APIProviderManager.ProviderSTT.Gemini:
                yield return StartCoroutine(TranscrireGemini(octetsWAV));
                break;
        }

        _traitement = false;
    }

    // ════════════════════════════════════════════════════════════════════
    // GROQ / UNSLOTH — Format Whisper OpenAI
    // ════════════════════════════════════════════════════════════════════

    private IEnumerator TranscrireWhisper(byte[] octetsWAV)
    {
        WWWForm formulaire = new WWWForm();
        formulaire.AddField("model", modeleActif);
        formulaire.AddField("language", langue);
        formulaire.AddBinaryData("file", octetsWAV, "audio.wav", "audio/wav");

        using UnityWebRequest req = UnityWebRequest.Post(_apiMgr.URLSTTActive, formulaire);
        req.SetRequestHeader("Authorization", "Bearer " + _cleApi);
        req.downloadHandler = new DownloadHandlerBuffer();

        yield return req.SendWebRequest();

        if (_annulation) yield break;

        if (req.result == UnityWebRequest.Result.Success)
        {
            var reponse = JsonUtility.FromJson<ReponseTranscription>(req.downloadHandler.text);
            if (!string.IsNullOrWhiteSpace(reponse.text))
            {
                if (debug) Debug.Log($"[SimpleSTT] ✓ Transcription → {reponse.text}");
                OnTranscriptionRecue?.Invoke(reponse.text.Trim());
            }
            else
                Debug.LogWarning("[SimpleSTT] Transcription vide.");
        }
        else
        {
            string err = $"[SimpleSTT] ❌ {req.error} | {req.downloadHandler.text}";
            Debug.LogError(err);
            OnErreur?.Invoke(err);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // GEMINI — Audio multimodal en base64
    // ════════════════════════════════════════════════════════════════════

    private IEnumerator TranscrireGemini(byte[] octetsWAV)
    {
        string audioBase64 = Convert.ToBase64String(octetsWAV);
        string url = _apiMgr.URLSTTActive
            .Replace("{model}", modeleActif) + $"?key={_cleApi}";

        string prompt = $"Transcris exactement ce qui est dit dans ce fichier audio en {ObtenirNomLangue(langue)}. " +
                        "Retourne uniquement la transcription, sans explication ni ponctuation ajoutée.";

        string corps = JsonConvert.SerializeObject(new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = "audio/wav",
                                data      = audioBase64
                            }
                        }
                    }
                }
            }
        });

        byte[] bytes = Encoding.UTF8.GetBytes(corps);
        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (_annulation) yield break;

        if (req.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var reponse = JsonConvert.DeserializeObject<ReponseGemini>(req.downloadHandler.text);
                string texte = reponse?.candidates?[0]?.content?.parts?[0]?.text?.Trim();
                if (!string.IsNullOrWhiteSpace(texte))
                {
                    if (debug) Debug.Log($"[SimpleSTT][Gemini] ✓ → {texte}");
                    OnTranscriptionRecue?.Invoke(texte);
                }
                else
                    Debug.LogWarning("[SimpleSTT][Gemini] Transcription vide.");
            }
            catch (Exception ex)
            {
                string err = $"[SimpleSTT][Gemini] Parse error : {ex.Message}";
                Debug.LogError(err);
                OnErreur?.Invoke(err);
            }
        }
        else
        {
            string err = $"[SimpleSTT][Gemini] ❌ {req.error} | {req.downloadHandler.text}";
            Debug.LogError(err);
            OnErreur?.Invoke(err);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private string ObtenirNomLangue(string code) => code switch
    {
        "fr" => "français",
        "en" => "anglais",
        "ar" => "arabe",
        "es" => "espagnol",
        _    => code
    };

    // ── Classes JSON ──────────────────────────────────────────────────────
    [Serializable] private class ReponseTranscription { public string text; }

    [Serializable] private class ReponseGemini
    {
        public CandidatGemini[] candidates;
    }
    [Serializable] private class CandidatGemini { public ContenuGemini content; }
    [Serializable] private class ContenuGemini  { public PartieGemini[] parts; }
    [Serializable] private class PartieGemini   { public string text; }
}
