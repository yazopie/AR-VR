using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

// ═══════════════════════════════════════════════════════════════════════════
// VictimManager.cs — Gestion complète des victimes
//
// FONCTIONNALITÉS :
//   → ID auto-généré à la création
//   → Champs Inspector simplifiés (Role, Contexte, État, Nom fichier)
//   → Bouton Save
//   → Charge depuis Resources (JSON) + depuis persistentDataPath
//   → Charge configuration depuis JSON externe (gaia_config.json)
// ═══════════════════════════════════════════════════════════════════════════

public class VictimManager : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════
    // CHAMPS INSPECTOR — Affichage simplifié
    // ════════════════════════════════════════════════════════════════════

    [Header("── Nouvelle victime ──────────────────────────")]
    [SerializeField] private string              nomFichier        = "victime_001";
    [SerializeField] private string              nomVictime        = "Nouvelle Victime";
    [SerializeField] private string              categorieVictime  = "blesse_leger";

    [TextArea(2, 4)]
    [SerializeField] private string              descriptionRole   = "";

    [TextArea(3, 6)]
    [SerializeField] private string              contexteAccident  = "";

    [TextArea(2, 4)]
    [SerializeField] private string              etatActuel        = "";

    [Header("── Victime active ───────────────────────────")]
    [SerializeField] private string              idVictimeActive   = "";

    [Header("Debug")]
    [SerializeField] private bool                debug             = false;

    // ── Données internes ──────────────────────────────────────────────────
    private Dictionary<string, VictimData> _victimes  = new();
    private VictimData                     _victimeActive;

    // ── Événements publics ────────────────────────────────────────────────
    public event Action<VictimData> OnVictimeChargee;
    public event Action             OnVictimesActualisees;

    // ── Propriétés ────────────────────────────────────────────────────────
    public VictimData                             ActiveVictim    => _victimeActive;
    public IReadOnlyDictionary<string, VictimData> ToutesVictimes => _victimes;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    public void Init()
    {
        ChargerToutesVictimes();

        if (!string.IsNullOrEmpty(idVictimeActive) && _victimes.ContainsKey(idVictimeActive))
            ChargerVictime(idVictimeActive);
        else if (_victimes.Count > 0)
            ChargerVictime(_victimes.Keys.First());
    }

    // ════════════════════════════════════════════════════════════════════
    // CHARGEMENT
    // ════════════════════════════════════════════════════════════════════

    public void ChargerToutesVictimes()
    {
        _victimes.Clear();

        // 1) Resources (assets embarqués dans le build)
        ChargerDepuisResources();

        // 2) persistentDataPath (victimes sauvegardées en jeu)
        ChargerDepuisDisque();

        OnVictimesActualisees?.Invoke();
        Debug.Log($"[VictimManager] {_victimes.Count} victime(s) chargée(s).");
    }

    private void ChargerDepuisResources()
    {
        TextAsset[] assets = Resources.LoadAll<TextAsset>(CheminsDonnees.ResourcesVictimes);
        foreach (var asset in assets)
            TenterChargerJSON(asset.text, $"Resources/{asset.name}");
    }

    private void ChargerDepuisDisque()
    {
        string dossier = CheminsDonnees.ObtenirDossierVictimes();
        foreach (string fichier in Directory.GetFiles(dossier, "*.json"))
            TenterChargerJSON(File.ReadAllText(fichier), fichier);
    }

    private void TenterChargerJSON(string json, string source)
    {
        try
        {
            VictimData data = JsonConvert.DeserializeObject<VictimData>(json);
            if (data != null && !string.IsNullOrEmpty(data.id))
            {
                _victimes[data.id] = data;
                if (debug) Debug.Log($"[VictimManager] ✓ Chargé : {data.id} ({data.victimName}) ← {source}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[VictimManager] ⚠ Parse échoué ({source}) : {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CHARGEMENT DEPUIS UN JSON EXTERNE (configuration)
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Charge les paramètres de configuration depuis gaia_config.json.
    /// Le JSON peut contenir des victimes pré-configurées.
    /// </summary>
    public void ChargerDepuisConfigJSON()
    {
        string chemin = CheminsDonnees.ObtenirCheminConfig();
        if (!File.Exists(chemin))
        {
            Debug.LogWarning($"[VictimManager] Config JSON introuvable : {chemin}");
            return;
        }

        try
        {
            string json        = File.ReadAllText(chemin);
            var    config      = JsonConvert.DeserializeObject<ConfigGAIA>(json);

            if (config?.victimes == null) return;

            foreach (var v in config.victimes)
            {
                if (!string.IsNullOrEmpty(v.id))
                {
                    _victimes[v.id] = v;
                    if (debug) Debug.Log($"[VictimManager] Config JSON → {v.victimName}");
                }
            }

            OnVictimesActualisees?.Invoke();
            Debug.Log($"[VictimManager] {config.victimes.Count} victime(s) chargée(s) depuis config JSON.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VictimManager] ❌ Erreur lecture config : {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CHARGER UNE VICTIME ACTIVE
    // ════════════════════════════════════════════════════════════════════

    public void ChargerVictime(string id)
    {
        if (!_victimes.TryGetValue(id, out var data))
        {
            Debug.LogError($"[VictimManager] ❌ Victime '{id}' introuvable.");
            return;
        }

        _victimeActive  = data;
        idVictimeActive = id;
        AppliquerAuxChamps(data);
        OnVictimeChargee?.Invoke(data);

        if (debug) Debug.Log($"[VictimManager] ✓ Victime active : {data.victimName} ({id})");
    }

    // ════════════════════════════════════════════════════════════════════
    // SAUVEGARDER UNE NOUVELLE VICTIME
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Crée une nouvelle VictimData avec un ID auto-généré et la sauvegarde sur disque.
    /// Appelé depuis le bouton "Sauvegarder" de l'Inspector ou du Panneau.
    /// </summary>
    [ContextMenu("Sauvegarder la victime")]
    public void SauvegarderNouvelleVictime()
    {
        var data = new VictimData
        {
            id               = GenererID(),
            victimName       = nomVictime,
            victimCategory   = categorieVictime,
            roleDescription  = descriptionRole,
            accidentContext  = contexteAccident,
            currentState     = etatActuel,
            dateCreation     = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            dateModification = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
        };

        string nomFichierFinal = string.IsNullOrWhiteSpace(nomFichier) ? $"victime_{data.id}" : nomFichier;
        EcrireSurDisque(data, nomFichierFinal);

        _victimes[data.id] = data;
        ChargerVictime(data.id);
        OnVictimesActualisees?.Invoke();

        Debug.Log($"[VictimManager] ✓ Victime sauvegardée : {nomFichierFinal}.json (ID={data.id})");
    }

    // ════════════════════════════════════════════════════════════════════
    // MODIFIER LA VICTIME ACTIVE
    // ════════════════════════════════════════════════════════════════════

    public void MettreAJourVictimeActive(
        string nom, string categorie, string role,
        string contexte, string etat, string voixId = null)
    {
        if (_victimeActive == null)
        {
            Debug.LogWarning("[VictimManager] Aucune victime active à mettre à jour.");
            return;
        }

        _victimeActive.victimName      = nom;
        _victimeActive.victimCategory  = categorie;
        _victimeActive.roleDescription = role;
        _victimeActive.accidentContext = contexte;
        _victimeActive.currentState    = etat;
        _victimeActive.dateModification = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

        if (!string.IsNullOrEmpty(voixId))
            _victimeActive.voiceId = voixId;

        EcrireSurDisque(_victimeActive, _victimeActive.id);
        OnVictimeChargee?.Invoke(_victimeActive);

        Debug.Log($"[VictimManager] ✓ Victime mise à jour : {_victimeActive.victimName}");
    }

    // ════════════════════════════════════════════════════════════════════
    // UTILITAIRES
    // ════════════════════════════════════════════════════════════════════

    private void AppliquerAuxChamps(VictimData data)
    {
        nomVictime       = data.victimName;
        categorieVictime = data.victimCategory;
        descriptionRole  = data.roleDescription;
        contexteAccident = data.accidentContext;
        etatActuel       = data.currentState;
    }

    private void EcrireSurDisque(VictimData data, string nomFich)
    {
        string chemin = Path.Combine(CheminsDonnees.ObtenirDossierVictimes(), $"{nomFich}.json");
        string json   = JsonConvert.SerializeObject(data, Formatting.Indented);
        try
        {
            File.WriteAllText(chemin, json);
            if (debug) Debug.Log($"[VictimManager] JSON écrit : {chemin}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VictimManager] ❌ Écriture impossible : {ex.Message}");
        }
    }

    private static string GenererID()
        => Guid.NewGuid().ToString("N").Substring(0, 8);

    // ── API publique complémentaire ────────────────────────────────────────
    public string[] ObtenirIDs()           => _victimes.Keys.ToArray();
    public string   ObtenirNom(string id)  => _victimes.TryGetValue(id, out var d) ? d.victimName : id;

    // ════════════════════════════════════════════════════════════════════
    // MODÈLE CONFIG JSON
    // ════════════════════════════════════════════════════════════════════

    [Serializable]
    private class ConfigGAIA
    {
        public List<VictimData> victimes;
    }
}
