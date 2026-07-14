using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════
// PanneauJeu.cs — Panneau de configuration in-game
//
// FONCTIONNALITÉS :
//   → Sélection provider LLM / STT / TTS
//   → Sélection du modèle selon le provider actif
//   → Gestion de la bibliothèque de voix TTS (ajout / suppression)
//   → Modification du prompt système de la victime active
//   → Alerte visuelle si un provider signale une erreur (tokens épuisés)
//   → URL du provider actif affichée en temps réel
//
// UTILISATION :
//   Assigner toutes les références UI dans l'Inspector Unity.
//   Ouvrir/fermer via ToggleVisibilite() (bouton ou touche clavier).
// ═══════════════════════════════════════════════════════════════════════════

public class PanneauJeu : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════
    // RÉFÉRENCES AUX MANAGERS
    // ════════════════════════════════════════════════════════════════════

    [Header("── Managers ──────────────────────────────────")]
    [SerializeField] private APIProviderManager apiMgr;
    [SerializeField] private SimpleLLM          llm;
    [SerializeField] private SimpleSTT          stt;
    [SerializeField] private SimpleTTS          tts;
    [SerializeField] private VictimManager      victimMgr;
    [SerializeField] private SimpleAIManager    aiMgr;

    // ════════════════════════════════════════════════════════════════════
    // RÉFÉRENCES UI — RACINE DU PANNEAU
    // ════════════════════════════════════════════════════════════════════

    [Header("── Panneau principal ─────────────────────────")]
    [SerializeField] private GameObject panneauRacine;
    [SerializeField] private KeyCode    toucheOuverture = KeyCode.F1;

    // ════════════════════════════════════════════════════════════════════
    // SECTION LLM
    // ════════════════════════════════════════════════════════════════════

    [Header("── Section LLM ──────────────────────────────")]
    [SerializeField] private TMP_Dropdown  dropdownProviderLLM;
    [SerializeField] private TMP_Dropdown  dropdownModeleLLM;
    [SerializeField] private TMP_Text      txtURLLLM;
    [SerializeField] private TMP_Text      txtAlerteLLM;
    [SerializeField] private Button        btnAppliquerLLM;

    // ════════════════════════════════════════════════════════════════════
    // SECTION STT
    // ════════════════════════════════════════════════════════════════════

    [Header("── Section STT ──────────────────────────────")]
    [SerializeField] private TMP_Dropdown  dropdownProviderSTT;
    [SerializeField] private TMP_Dropdown  dropdownModeleSTT;
    [SerializeField] private TMP_Text      txtURLSTT;
    [SerializeField] private TMP_Text      txtAlerteSTT;
    [SerializeField] private Button        btnAppliquerSTT;

    // ════════════════════════════════════════════════════════════════════
    // SECTION TTS
    // ════════════════════════════════════════════════════════════════════

    [Header("── Section TTS ──────────────────────────────")]
    [SerializeField] private TMP_Dropdown  dropdownProviderTTS;
    [SerializeField] private TMP_Dropdown  dropdownModeleTTS;
    [SerializeField] private TMP_Dropdown  dropdownVoixTTS;
    [SerializeField] private TMP_Text      txtURLTTS;
    [SerializeField] private TMP_Text      txtAlerteTTS;
    [SerializeField] private Button        btnAppliquerTTS;

    // Ajout / Suppression de voix
    [SerializeField] private TMP_InputField inputNomVoix;
    [SerializeField] private TMP_InputField inputIdVoix;
    [SerializeField] private TMP_InputField inputProviderVoix;
    [SerializeField] private Button         btnAjouterVoix;
    [SerializeField] private Button         btnSupprimerVoix;

    // ════════════════════════════════════════════════════════════════════
    // SECTION PROMPT / VICTIME
    // ════════════════════════════════════════════════════════════════════

    [Header("── Section Victime & Prompt ────────────────")]
    [SerializeField] private TMP_Dropdown   dropdownVictime;
    [SerializeField] private TMP_InputField inputNomVictime;
    [SerializeField] private TMP_InputField inputCategorieVictime;
    [SerializeField] private TMP_InputField inputRoleVictime;
    [SerializeField] private TMP_InputField inputContexteVictime;
    [SerializeField] private TMP_InputField inputEtatVictime;
    [SerializeField] private TMP_InputField inputVoixIdVictime;
    [SerializeField] private Button         btnSauvegarderVictime;
    [SerializeField] private Button         btnNouvelleVictime;
    [SerializeField] private TMP_Text       txtIDVictime;

    // ════════════════════════════════════════════════════════════════════
    // SECTION ÉTAT GÉNÉRAL
    // ════════════════════════════════════════════════════════════════════

    [Header("── Section État ─────────────────────────────")]
    [SerializeField] private TMP_Text txtStatut;
    [SerializeField] private Button   btnFermer;

    // ════════════════════════════════════════════════════════════════════
    // ÉTAT INTERNE
    // ════════════════════════════════════════════════════════════════════

    private readonly string[] _nomsProviderLLM = { "GroqCloud", "Unsloth", "Gemini" };
    private readonly string[] _nomsProviderSTT = { "Groq Whisper", "Unsloth", "Gemini" };
    private readonly string[] _nomsProviderTTS = { "ElevenLabs", "Speechify", "Supertone", "Gemini" };

    private bool _panneauOuvert = false;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (panneauRacine != null) panneauRacine.SetActive(false);

        ConfigurerDropdowns();
        ConfigurerBoutons();
        AbonnerEvenements();
        ActualiserAffichage();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toucheOuverture))
            ToggleVisibilite();
    }

    // ════════════════════════════════════════════════════════════════════
    // CONFIGURATION DES DROPDOWNS
    // ════════════════════════════════════════════════════════════════════

    private void ConfigurerDropdowns()
    {
        // LLM
        RemplirDropdown(dropdownProviderLLM, _nomsProviderLLM, (int)apiMgr.LLMActif, OnChangerProviderLLM);
        RemplirModelesLLM();

        // STT
        RemplirDropdown(dropdownProviderSTT, _nomsProviderSTT, (int)apiMgr.STTActif, OnChangerProviderSTT);
        RemplirModelesSTT();

        // TTS
        RemplirDropdown(dropdownProviderTTS, _nomsProviderTTS, (int)apiMgr.TTSActif, OnChangerProviderTTS);
        RemplirModelesTTS();
        RemplirVoixTTS();

        // Victimes
        RemplirDropdownVictimes();
    }

    private static void RemplirDropdown(TMP_Dropdown dropdown, string[] options, int indexActif, UnityEngine.Events.UnityAction<int> callback)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>(options));
        dropdown.SetValueWithoutNotify(indexActif);
        dropdown.onValueChanged.RemoveAllListeners();
        dropdown.onValueChanged.AddListener(callback);
    }

    private void RemplirModelesLLM()
    {
        if (dropdownModeleLLM == null || apiMgr == null) return;
        var modeles = apiMgr.ObtenirModelesLLM();
        dropdownModeleLLM.ClearOptions();
        dropdownModeleLLM.AddOptions(new List<string>(modeles));
        // Sélectionne le modèle actif
        int index = Array.IndexOf(modeles, llm?.ModeleActif ?? "");
        dropdownModeleLLM.SetValueWithoutNotify(Mathf.Max(0, index));
    }

    private void RemplirModelesSTT()
    {
        if (dropdownModeleSTT == null || apiMgr == null) return;
        var modeles = apiMgr.ObtenirModelesSTT();
        dropdownModeleSTT.ClearOptions();
        dropdownModeleSTT.AddOptions(new List<string>(modeles));
        int index = Array.IndexOf(modeles, stt?.ModeleActif ?? "");
        dropdownModeleSTT.SetValueWithoutNotify(Mathf.Max(0, index));
    }

    private void RemplirModelesTTS()
    {
        if (dropdownModeleTTS == null || apiMgr == null) return;
        var modeles = apiMgr.ObtenirModelesTTS();
        dropdownModeleTTS.ClearOptions();
        dropdownModeleTTS.AddOptions(new List<string>(modeles));
        int index = Array.IndexOf(modeles, tts?.ModeleActif ?? "");
        dropdownModeleTTS.SetValueWithoutNotify(Mathf.Max(0, index));
    }

    private void RemplirVoixTTS()
    {
        if (dropdownVoixTTS == null || tts == null) return;
        dropdownVoixTTS.ClearOptions();
        var noms = new List<string>();
        foreach (var v in tts.Voix) noms.Add(v.nom);
        dropdownVoixTTS.AddOptions(noms);

        // Sélectionne la voix active
        int idx = tts.Voix.FindIndex(v => v.voixId == tts.VoixIdActif);
        dropdownVoixTTS.SetValueWithoutNotify(Mathf.Max(0, idx));
    }

    private void RemplirDropdownVictimes()
    {
        if (dropdownVictime == null || victimMgr == null) return;
        dropdownVictime.ClearOptions();
        string[] ids = victimMgr.ObtenirIDs();
        var noms = new List<string>();
        foreach (var id in ids) noms.Add(victimMgr.ObtenirNom(id));
        dropdownVictime.AddOptions(noms);
        dropdownVictime.onValueChanged.RemoveAllListeners();
        dropdownVictime.onValueChanged.AddListener(OnChangerVictime);
        ChargerChampsVictime(victimMgr.ActiveVictim);
    }

    // ════════════════════════════════════════════════════════════════════
    // CONFIGURATION DES BOUTONS
    // ════════════════════════════════════════════════════════════════════

    private void ConfigurerBoutons()
    {
        btnAppliquerLLM?.onClick.AddListener(AppliquerChangementsLLM);
        btnAppliquerSTT?.onClick.AddListener(AppliquerChangementsSTT);
        btnAppliquerTTS?.onClick.AddListener(AppliquerChangementsTTS);
        btnAjouterVoix?.onClick.AddListener(AjouterVoix);
        btnSupprimerVoix?.onClick.AddListener(SupprimerVoixSelectionnee);
        btnSauvegarderVictime?.onClick.AddListener(SauvegarderVictime);
        btnNouvelleVictime?.onClick.AddListener(NouvelleVictime);
        btnFermer?.onClick.AddListener(() => ToggleVisibilite());
    }

    // ════════════════════════════════════════════════════════════════════
    // ABONNEMENTS
    // ════════════════════════════════════════════════════════════════════

    private void AbonnerEvenements()
    {
        if (llm  != null) llm.OnErreur += err => AfficherAlerteProvider("LLM", err);
        if (stt  != null) stt.OnErreur += err => AfficherAlerteProvider("STT", err);
        if (tts  != null) tts.OnErreur += err => AfficherAlerteProvider("TTS", err);
        if (victimMgr != null)
        {
            victimMgr.OnVictimeChargee     += v => ChargerChampsVictime(v);
            victimMgr.OnVictimesActualisees += RemplirDropdownVictimes;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CHANGEMENT DE PROVIDER
    // ════════════════════════════════════════════════════════════════════

    private void OnChangerProviderLLM(int index)
    {
        apiMgr.ChangerProviderLLM((APIProviderManager.ProviderLLM)index);
        RemplirModelesLLM();
        ActualiserURLsAffichees();
        EffacerAlerte(txtAlerteLLM);
    }

    private void OnChangerProviderSTT(int index)
    {
        apiMgr.ChangerProviderSTT((APIProviderManager.ProviderSTT)index);
        RemplirModelesSTT();
        ActualiserURLsAffichees();
        EffacerAlerte(txtAlerteSTT);
    }

    private void OnChangerProviderTTS(int index)
    {
        apiMgr.ChangerProviderTTS((APIProviderManager.ProviderTTS)index);
        RemplirModelesTTS();
        RemplirVoixTTS();
        ActualiserURLsAffichees();
        EffacerAlerte(txtAlerteTTS);
    }

    // ════════════════════════════════════════════════════════════════════
    // APPLICATION DES CHANGEMENTS
    // ════════════════════════════════════════════════════════════════════

    private void AppliquerChangementsLLM()
    {
        if (dropdownModeleLLM != null)
        {
            string modele = apiMgr.ObtenirModelesLLM()[dropdownModeleLLM.value];
            llm?.DefinirModele(modele);
        }
        AfficherStatut($"LLM : {apiMgr.LLMActif} | {(dropdownModeleLLM != null ? apiMgr.ObtenirModelesLLM()[dropdownModeleLLM.value] : "")}");
    }

    private void AppliquerChangementsSTT()
    {
        if (dropdownModeleSTT != null)
        {
            string modele = apiMgr.ObtenirModelesSTT()[dropdownModeleSTT.value];
            stt?.DefinirModele(modele);
        }
        AfficherStatut($"STT : {apiMgr.STTActif} | {(dropdownModeleSTT != null ? apiMgr.ObtenirModelesSTT()[dropdownModeleSTT.value] : "")}");
    }

    private void AppliquerChangementsTTS()
    {
        if (dropdownModeleTTS != null)
        {
            string modele = apiMgr.ObtenirModelesTTS()[dropdownModeleTTS.value];
            tts?.DefinirModele(modele);
        }
        if (dropdownVoixTTS != null && tts != null && tts.Voix.Count > 0)
        {
            string voixId = tts.Voix[dropdownVoixTTS.value].voixId;
            tts.DefinirVoix(voixId);
        }
        AfficherStatut($"TTS : {apiMgr.TTSActif}");
    }

    // ════════════════════════════════════════════════════════════════════
    // GESTION DES VOIX
    // ════════════════════════════════════════════════════════════════════

    private void AjouterVoix()
    {
        if (tts == null) return;
        string nom      = inputNomVoix?.text?.Trim();
        string voixId   = inputIdVoix?.text?.Trim();
        string provider = inputProviderVoix?.text?.Trim() ?? apiMgr.TTSActif.ToString();

        if (string.IsNullOrEmpty(nom) || string.IsNullOrEmpty(voixId))
        {
            AfficherStatut("⚠ Remplis le nom et l'ID de voix.");
            return;
        }

        tts.AjouterVoix(nom, voixId, provider);
        RemplirVoixTTS();

        if (inputNomVoix    != null) inputNomVoix.text = "";
        if (inputIdVoix     != null) inputIdVoix.text  = "";
        AfficherStatut($"Voix ajoutée : {nom}");
    }

    private void SupprimerVoixSelectionnee()
    {
        if (tts == null || tts.Voix.Count == 0 || dropdownVoixTTS == null) return;
        string voixId = tts.Voix[dropdownVoixTTS.value].voixId;
        tts.SupprimerVoix(voixId);
        RemplirVoixTTS();
        AfficherStatut($"Voix supprimée : {voixId}");
    }

    // ════════════════════════════════════════════════════════════════════
    // GESTION DES VICTIMES
    // ════════════════════════════════════════════════════════════════════

    private void OnChangerVictime(int index)
    {
        string[] ids = victimMgr.ObtenirIDs();
        if (index < ids.Length)
            victimMgr.ChargerVictime(ids[index]);
    }

    private void ChargerChampsVictime(VictimData v)
    {
        if (v == null) return;
        if (txtIDVictime        != null) txtIDVictime.text    = $"ID : {v.id}";
        if (inputNomVictime     != null) inputNomVictime.text = v.victimName;
        if (inputCategorieVictime != null) inputCategorieVictime.text = v.victimCategory;
        if (inputRoleVictime    != null) inputRoleVictime.text = v.roleDescription;
        if (inputContexteVictime != null) inputContexteVictime.text = v.accidentContext;
        if (inputEtatVictime    != null) inputEtatVictime.text = v.currentState;
        if (inputVoixIdVictime  != null) inputVoixIdVictime.text = v.voiceId;
    }

    private void SauvegarderVictime()
    {
        if (victimMgr?.ActiveVictim == null) return;

        victimMgr.MettreAJourVictimeActive(
            nom:       inputNomVictime?.text ?? "",
            categorie: inputCategorieVictime?.text ?? "",
            role:      inputRoleVictime?.text ?? "",
            contexte:  inputContexteVictime?.text ?? "",
            etat:      inputEtatVictime?.text ?? "",
            voixId:    inputVoixIdVictime?.text
        );
        llm?.RechargerSystemPrompt();
        AfficherStatut($"✓ Victime sauvegardée : {victimMgr.ActiveVictim.victimName}");
    }

    private void NouvelleVictime()
    {
        if (inputNomVictime != null) inputNomVictime.text = "Nouvelle Victime";
        if (inputCategorieVictime != null) inputCategorieVictime.text = "blesse_leger";
        if (inputRoleVictime != null) inputRoleVictime.text = "";
        if (inputContexteVictime != null) inputContexteVictime.text = "";
        if (inputEtatVictime != null) inputEtatVictime.text = "";
        if (inputVoixIdVictime != null) inputVoixIdVictime.text = "";
        if (txtIDVictime != null) txtIDVictime.text = "ID : (généré à la sauvegarde)";
    }

    // ════════════════════════════════════════════════════════════════════
    // AFFICHAGE
    // ════════════════════════════════════════════════════════════════════

    private void ActualiserAffichage()
    {
        ActualiserURLsAffichees();
        AfficherStatut("Panneau prêt.");
    }

    private void ActualiserURLsAffichees()
    {
        if (txtURLLLM != null) txtURLLLM.text = $"URL LLM : {apiMgr?.URLLLMActive}";
        if (txtURLSTT != null) txtURLSTT.text = $"URL STT : {apiMgr?.URLSTTActive}";
        if (txtURLTTS != null) txtURLTTS.text = $"URL TTS : {apiMgr?.URLTTSActive}";
    }

    private void AfficherStatut(string message)
    {
        if (txtStatut != null) txtStatut.text = message;
        Debug.Log($"[PanneauJeu] {message}");
    }

    /// <summary>Affiche une alerte colorée si un provider renvoie une erreur (quota épuisé, etc.).</summary>
    private void AfficherAlerteProvider(string pipeline, string erreur)
    {
        string msgAlerte = $"⚠ {pipeline} — Erreur : {Tronquer(erreur, 80)}\nChanger de provider ?";

        switch (pipeline)
        {
            case "LLM": if (txtAlerteLLM != null) { txtAlerteLLM.text  = msgAlerte; txtAlerteLLM.color  = Color.red; } break;
            case "STT": if (txtAlerteSTT != null) { txtAlerteSTT.text  = msgAlerte; txtAlerteSTT.color  = Color.red; } break;
            case "TTS": if (txtAlerteTTS != null) { txtAlerteTTS.text  = msgAlerte; txtAlerteTTS.color  = Color.red; } break;
        }

        // Ouvre automatiquement le panneau si fermé
        if (!_panneauOuvert) ToggleVisibilite();
    }

    private void EffacerAlerte(TMP_Text zone)
    {
        if (zone != null) { zone.text = ""; zone.color = Color.white; }
    }

    public void ToggleVisibilite()
    {
        _panneauOuvert = !_panneauOuvert;
        if (panneauRacine != null) panneauRacine.SetActive(_panneauOuvert);
        if (_panneauOuvert) ActualiserAffichage();
    }

    public void Fermer()
    {
        _panneauOuvert = false;
        if (panneauRacine != null) panneauRacine.SetActive(false);
    }

    private void OnDestroy()
    {
        if (victimMgr != null)
        {
            victimMgr.OnVictimeChargee     -= ChargerChampsVictime;
            victimMgr.OnVictimesActualisees -= RemplirDropdownVictimes;
        }
    }

    private string Tronquer(string s, int max)
        => s == null ? "" : s.Length <= max ? s : s.Substring(0, max) + "...";
}
