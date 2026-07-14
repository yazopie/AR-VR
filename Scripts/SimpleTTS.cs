using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

// ═══════════════════════════════════════════════════════════════════════════
// SimpleTTS.cs — Synthèse vocale multi-providers
//
// PROVIDERS : ElevenLabs | Speechify | Supertone | Gemini
//
// Fonctionnalités :
//   → Liste de voix sélectionnables (avec ajout/suppression)
//   → Sélection du modèle TTS
//   → Chaque victime peut avoir sa propre voix via VoixVictime
// ═══════════════════════════════════════════════════════════════════════════

[Serializable]
public class EntreeVoix
{
    public string nom;     // Nom affiché dans le panneau
    public string voixId;  // ID technique (ElevenLabs ID, Supertone voice, etc.)
    public string provider;// Provider associé
}

public class SimpleTTS : MonoBehaviour
{
    [Header("Voix et modèle actifs")]
    [SerializeField] private string voixIdActif = "JBFqnCBsd6RMkjVDRZzb";
    [SerializeField] private string modeleActif = "eleven_v3";

    [Header("Bibliothèque de voix")]
    [SerializeField] private List<EntreeVoix> bibliothequeVoix = new()
    {
        new EntreeVoix { nom = "Rachel (ElevenLabs)",   voixId = "21m00Tcm4TlvDq8ikWAM", provider = "ElevenLabs" },
        new EntreeVoix { nom = "Antoni (ElevenLabs)",   voixId = "ErXwobaYiN019PkySvjV", provider = "ElevenLabs" },
        new EntreeVoix { nom = "Charlotte (ElevenLabs)",voixId = "XB0fDUnXU5powFXDhCwa", provider = "ElevenLabs" },
        new EntreeVoix { nom = "Aoede (Gemini)",         voixId = "Aoede",                provider = "Gemini"     },
        new EntreeVoix { nom = "Charon (Gemini)",        voixId = "Charon",               provider = "Gemini"     },
        new EntreeVoix { nom = "Kore (Gemini)",          voixId = "Kore",                 provider = "Gemini"     },
    };

    [Header("Options")]
    [SerializeField] private bool utiliserVoixVictime = true; // Utilise voixId de VictimData si disponible
    [SerializeField] private bool debug = false;

    // ── Événements ────────────────────────────────────────────────────────
    public event Action         OnParoleTerminee;
    public event Action<string> OnErreur;
    public event Action         OnParoleCommencee;

    // ── État ──────────────────────────────────────────────────────────────
    private APIProviderManager _apiMgr;
    private string             _cleApi;
    private AudioSource        _audioSource;
    private bool               _pret           = false;
    private bool               _annulation     = false;

    public bool EstEnTrain         { get; private set; }
    public string ModeleActif      => modeleActif;
    public string VoixIdActif      => voixIdActif;
    public List<EntreeVoix> Voix   => bibliothequeVoix;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    public void Init(APIProviderManager apiMgr, string cleApi)
    {
        _apiMgr     = apiMgr;
        _cleApi     = cleApi;
        _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
        {
            Debug.LogError("[SimpleTTS] ❌ Aucun AudioSource trouvé sur le GameObject.");
            return;
        }

        _pret = true;
        SynchroniserAvecProvider();
        Debug.Log($"[SimpleTTS] ✓ Prêt | Provider={_apiMgr.TTSActif} | Voix={voixIdActif} | Modèle={modeleActif}");
    }

    public void SynchroniserAvecProvider()
    {
        voixIdActif = _apiMgr.ObtenirVoixIdTTSActive();
        string[] modeles = _apiMgr.ObtenirModelesTTS();
        if (modeles.Length > 0) modeleActif = modeles[0];
    }

    // ════════════════════════════════════════════════════════════════════
    // GESTION DE LA BIBLIOTHÈQUE DE VOIX
    // ════════════════════════════════════════════════════════════════════

    public void DefinirVoix(string voixId)
    {
        voixIdActif = voixId;
        _apiMgr?.DefinirVoixId(voixId);
        if (debug) Debug.Log($"[SimpleTTS] Voix → {voixId}");
    }

    public void DefinirVoixParNom(string nom)
    {
        var entree = bibliothequeVoix.Find(v => v.nom == nom);
        if (entree != null) DefinirVoix(entree.voixId);
        else Debug.LogWarning($"[SimpleTTS] Voix '{nom}' introuvable.");
    }

    public void DefinirModele(string modele)
    {
        modeleActif = modele;
        if (debug) Debug.Log($"[SimpleTTS] Modèle → {modele}");
    }

    /// <summary>Applique la voix de la victime si disponible et activé.</summary>
    public void AppliquerVoixVictime(VictimData victime)
    {
        if (!utiliserVoixVictime) return;
        if (victime == null || string.IsNullOrEmpty(victime.voiceId)) return;
        DefinirVoix(victime.voiceId);
        if (debug) Debug.Log($"[SimpleTTS] Voix victime appliquée : {victime.voiceId}");
    }

    public void AjouterVoix(string nom, string voixId, string provider)
    {
        if (bibliothequeVoix.Exists(v => v.voixId == voixId))
        {
            Debug.LogWarning($"[SimpleTTS] Voix '{voixId}' déjà présente.");
            return;
        }
        bibliothequeVoix.Add(new EntreeVoix { nom = nom, voixId = voixId, provider = provider });
        Debug.Log($"[SimpleTTS] Voix ajoutée : {nom} ({voixId})");
    }

    public void SupprimerVoix(string voixId)
    {
        int enleve = bibliothequeVoix.RemoveAll(v => v.voixId == voixId);
        if (enleve > 0) Debug.Log($"[SimpleTTS] Voix supprimée : {voixId}");
    }

    public string[] ObtenirModelesDisponibles()
        => _apiMgr?.ObtenirModelesTTS() ?? new[] { modeleActif };

    // ════════════════════════════════════════════════════════════════════
    // SYNTHÈSE VOCALE PRINCIPALE
    // ════════════════════════════════════════════════════════════════════

    public async Task Dire(string texte)
    {
        if (!_pret) { Debug.LogWarning("[SimpleTTS] Non initialisé."); return; }
        if (string.IsNullOrWhiteSpace(texte)) { Debug.LogWarning("[SimpleTTS] Texte vide."); return; }

        _annulation = false;
        OnParoleCommencee?.Invoke();
        string textePropre = NettoyerTexte(texte);

        switch (_apiMgr.TTSActif)
        {
            case APIProviderManager.ProviderTTS.ElevenLabs:
                await DireElevenLabs(textePropre); break;

            case APIProviderManager.ProviderTTS.Speechify:
                await DireSpeechify(textePropre); break;

            case APIProviderManager.ProviderTTS.Supertone:
                await DireSupertone(textePropre); break;

            case APIProviderManager.ProviderTTS.Gemini:
                await DireGemini(textePropre); break;
        }
    }

    public void Annuler()
    {
        _annulation = true;
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
            EstEnTrain = false;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ELEVEN LABS
    // ════════════════════════════════════════════════════════════════════

    private async Task DireElevenLabs(string texte)
    {
        string corps = JsonConvert.SerializeObject(new
        {
            text     = texte,
            model_id = modeleActif
        });
        byte[] bytes = Encoding.UTF8.GetBytes(corps);
        string url   = $"{_apiMgr.URLTTSActive}{voixIdActif}?output_format=mp3_44100_128";

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
        req.SetRequestHeader("xi-api-key", _cleApi);
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "audio/mpeg");

        await EnvoyerRequete(req);
        if (_annulation) return;

        if (req.result != UnityWebRequest.Result.Success)
        { GererErreur("[ElevenLabs]", req); return; }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        await LireClip(clip);
    }

    // ════════════════════════════════════════════════════════════════════
    // SPEECHIFY
    // ════════════════════════════════════════════════════════════════════

    private async Task DireSpeechify(string texte)
    {
        string corps = JsonConvert.SerializeObject(new
        {
            input        = texte,
            voice_id     = voixIdActif,
            audio_format = "mp3"
        });
        byte[] bytes = Encoding.UTF8.GetBytes(corps);

        using UnityWebRequest req = new UnityWebRequest(_apiMgr.URLTTSActive, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerAudioClip(_apiMgr.URLTTSActive, AudioType.MPEG);
        req.SetRequestHeader("Authorization", "Bearer " + _cleApi);
        req.SetRequestHeader("Content-Type", "application/json");

        await EnvoyerRequete(req);
        if (_annulation) return;

        if (req.result != UnityWebRequest.Result.Success)
        { GererErreur("[Speechify]", req); return; }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        await LireClip(clip);
    }

    // ════════════════════════════════════════════════════════════════════
    // SUPERTONE
    // ════════════════════════════════════════════════════════════════════

    private async Task DireSupertone(string texte)
    {
        string url = $"{_apiMgr.URLTTSActive}/{voixIdActif}";
        string corps = JsonConvert.SerializeObject(new
        {
            text      = texte,
            model     = modeleActif,
            language  = "fr"
        });
        byte[] bytes = Encoding.UTF8.GetBytes(corps);

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
        req.SetRequestHeader("x-sup-api-key", _cleApi);
        req.SetRequestHeader("Content-Type", "application/json");

        await EnvoyerRequete(req);
        if (_annulation) return;

        if (req.result != UnityWebRequest.Result.Success)
        { GererErreur("[Supertone]", req); return; }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        await LireClip(clip);
    }

    // ════════════════════════════════════════════════════════════════════
    // GEMINI TTS — Retourne du PCM audio en base64
    // ════════════════════════════════════════════════════════════════════

    private async Task DireGemini(string texte)
    {
        string url = _apiMgr.URLTTSActive
            .Replace("{model}", modeleActif) + $"?key={_cleApi}";

        string corps = JsonConvert.SerializeObject(new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = texte } } }
            },
            generationConfig = new
            {
                responseModalities = new[] { "AUDIO" },
                speechConfig = new
                {
                    voiceConfig = new
                    {
                        prebuiltVoiceConfig = new { voiceName = voixIdActif }
                    }
                }
            }
        });

        byte[] bytes = Encoding.UTF8.GetBytes(corps);

        using UnityWebRequest req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(bytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        await EnvoyerRequete(req);
        if (_annulation) return;

        if (req.result != UnityWebRequest.Result.Success)
        { GererErreur("[Gemini TTS]", req); return; }

        try
        {
            // Extrait le PCM base64 de la réponse Gemini
            var reponse = JsonConvert.DeserializeObject<ReponseGeminiTTS>(req.downloadHandler.text);
            string audioBase64 = reponse?.candidates?[0]?.content?.parts?[0]?.inlineData?.data;

            if (string.IsNullOrEmpty(audioBase64))
            { OnErreur?.Invoke("[SimpleTTS][Gemini] Données audio vides."); return; }

            byte[] pcmBytes = Convert.FromBase64String(audioBase64);
            AudioClip clip  = PCMversAudioClip(pcmBytes, 24000, 1);
            await LireClip(clip);
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke($"[SimpleTTS][Gemini] Erreur parse : {ex.Message}");
        }
    }

    /// <summary>Convertit des bytes PCM 16-bit en AudioClip Unity.</summary>
    private AudioClip PCMversAudioClip(byte[] pcm, int sampleRate, int canaux)
    {
        int nbSamples = pcm.Length / 2;
        float[] samples = new float[nbSamples];
        for (int i = 0; i < nbSamples; i++)
        {
            short valeur = BitConverter.ToInt16(pcm, i * 2);
            samples[i] = valeur / 32768f;
        }
        AudioClip clip = AudioClip.Create("GeminiTTS", nbSamples, canaux, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ════════════════════════════════════════════════════════════════════
    // LECTURE AUDIO
    // ════════════════════════════════════════════════════════════════════

    private async Task LireClip(AudioClip clip)
    {
        if (clip == null) { OnErreur?.Invoke("[SimpleTTS] AudioClip null."); return; }

        EstEnTrain = true;
        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.loop = false;
        _audioSource.Play();

        while (_audioSource.isPlaying && !_annulation)
            await Task.Yield();

        _audioSource.Stop();
        _audioSource.clip = null;
        EstEnTrain = false;

        if (!_annulation)
        {
            OnParoleTerminee?.Invoke();
            if (debug) Debug.Log("[SimpleTTS] ✓ Lecture terminée.");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private static async Task EnvoyerRequete(UnityWebRequest req)
    {
        var tcs = new TaskCompletionSource<bool>();
        req.SendWebRequest().completed += _ => tcs.TrySetResult(true);
        await tcs.Task;
    }

    private void GererErreur(string prefixe, UnityWebRequest req)
    {
        string msg = $"[SimpleTTS]{prefixe} {req.error} (HTTP {req.responseCode})";
        Debug.LogError(msg);
        OnErreur?.Invoke(msg);
    }

    private static string NettoyerTexte(string entree)
    {
        return (entree ?? string.Empty)
            .Replace("+",  " plus ")
            .Replace("=",  " égal ")
            .Replace("*",  " ")
            .Replace(":",  ", ")
            .Replace("#",  " ")
            .Replace("&",  " et ")
            .Replace("\n", " ")
            .Trim();
    }

    // ── Classes JSON Gemini TTS ───────────────────────────────────────────
    [Serializable] private class ReponseGeminiTTS { public CandidatTTS[] candidates; }
    [Serializable] private class CandidatTTS      { public ContenuTTS content; }
    [Serializable] private class ContenuTTS       { public PartieTTS[] parts; }
    [Serializable] private class PartieTTS        { public DonneesInline inlineData; }
    [Serializable] private class DonneesInline    { public string mimeType; public string data; }
}
