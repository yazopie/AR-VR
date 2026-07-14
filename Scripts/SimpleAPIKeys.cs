using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

// ═══════════════════════════════════════════════════════════════════════════
// SimpleAPIKeys.cs — Lit les clés API depuis Resources/Secure/APIkeys.txt
//
// Format du fichier (une clé par ligne) :
//   # Commentaire
//   Groq_API_Key:gsk_xxxxxxxx
//   Unsloth_API_Key:sk_xxxxxxxx
//   Gemini_API_Key:AIzaxxxxxxxxxx
//   ElevenLabs_API_Key:xxxxxxxx
//   Speechify_API_Key:xxxxxxxx
//   Supertone_API_Key:xxxxxxxx
// ═══════════════════════════════════════════════════════════════════════════

public class SimpleAPIKeys : MonoBehaviour
{
    [Header("Chemin dans Resources (sans extension)")]
    [SerializeField] private string cheminFichier = "Secure/APIkeys";

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private Dictionary<string, string> _cles = new();

    // ════════════════════════════════════════════════════════════════════
    // CHARGEMENT
    // ════════════════════════════════════════════════════════════════════

    public async Task Init()
    {
        _cles.Clear();

        TextAsset fichier = Resources.Load<TextAsset>(cheminFichier);
        if (fichier == null)
        {
            Debug.LogError($"[SimpleAPIKeys] ❌ Fichier introuvable : Resources/{cheminFichier}.txt\n" +
                           "Créez le fichier avec une clé par ligne au format : NomCle:Valeur");
            return;
        }

        foreach (string ligne in fichier.text.Split('\n'))
        {
            string nette = ligne.Trim();
            if (string.IsNullOrEmpty(nette) || nette.StartsWith("#")) continue;

            int premierDoublePoint = nette.IndexOf(':');
            if (premierDoublePoint <= 0)
            {
                if (debug) Debug.LogWarning($"[SimpleAPIKeys] Ligne ignorée (format invalide) : {nette}");
                continue;
            }

            string nomCle = nette.Substring(0, premierDoublePoint).Trim();
            string valeur = nette.Substring(premierDoublePoint + 1).Trim();

            if (!string.IsNullOrEmpty(nomCle) && !string.IsNullOrEmpty(valeur))
            {
                _cles[nomCle] = valeur;
                if (debug) Debug.Log($"[SimpleAPIKeys] ✓ Clé chargée : {nomCle}");
            }
        }

        Debug.Log($"[SimpleAPIKeys] {_cles.Count} clé(s) chargée(s).");
        await Task.Delay(0);
    }

    // ════════════════════════════════════════════════════════════════════
    // ACCÈS
    // ════════════════════════════════════════════════════════════════════

    public string Get(string nomCle)
    {
        if (_cles.TryGetValue(nomCle, out string valeur))
            return valeur;

        Debug.LogError($"[SimpleAPIKeys] ❌ Clé manquante : '{nomCle}'\n" +
                       $"Vérifiez Resources/{cheminFichier}.txt");
        return null;
    }

    public bool ContientCle(string nomCle)
        => _cles.ContainsKey(nomCle);

    public string[] ObtenirNomsCles()
    {
        string[] noms = new string[_cles.Count];
        _cles.Keys.CopyTo(noms, 0);
        return noms;
    }
}
