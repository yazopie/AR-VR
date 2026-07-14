using UnityEngine;
using TMPro;

// ═══════════════════════════════════════════════════════════════════════════
// DebugPanel.cs — Affiche Question / Réponse en jeu
//
// Utilise FeedbackManager.SupprimerTousTags pour nettoyer tous les [tag] et <tag>
// avant l'affichage — cohérence avec le reste du pipeline.
// ═══════════════════════════════════════════════════════════════════════════

public class DebugPanel : MonoBehaviour
{
    [Header("── Références UI ────────────────────────────")]
    [SerializeField] private TextMeshProUGUI champQuestion;
    [SerializeField] private TextMeshProUGUI champReponse;
    [SerializeField] private TextMeshProUGUI champVictime;   // Nom + catégorie de la victime active
    [SerializeField] private TextMeshProUGUI champProvider;  // Providers actifs
    [SerializeField] private GameObject      racinePanel;

    [Header("── Options ──────────────────────────────────")]
    [SerializeField] private int  longueurMaxReponse  = 500;
    [SerializeField] private bool afficherAutomatique = true;

    [Header("── Références Managers (optionnel) ──────────")]
    [SerializeField] private APIProviderManager apiMgr;
    [SerializeField] private VictimManager      victimMgr;

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (racinePanel != null) racinePanel.SetActive(false);
        Effacer();

        // Abonnements optionnels pour mise à jour automatique
        if (victimMgr != null)
            victimMgr.OnVictimeChargee += v => AfficherInfoVictime(v);
    }

    private void OnDestroy()
    {
        if (victimMgr != null)
            victimMgr.OnVictimeChargee -= AfficherInfoVictime;
    }

    // ════════════════════════════════════════════════════════════════════
    // AFFICHAGE QUESTION
    // ════════════════════════════════════════════════════════════════════

    public void SetQuestion(string question)
    {
        if (champQuestion != null)
            champQuestion.text = $"<b>Q :</b> {question}";

        if (afficherAutomatique && racinePanel != null)
            racinePanel.SetActive(true);
    }

    // ════════════════════════════════════════════════════════════════════
    // AFFICHAGE RÉPONSE
    // ════════════════════════════════════════════════════════════════════

    public void SetResponse(string reponseBrute)
    {
        // Supprime TOUS les tags [] et <> (cohérence avec FeedbackManager)
        string propre = FeedbackManager.SupprimerTousTags(reponseBrute);

        if (champReponse != null)
        {
            string affichage = propre.Length > longueurMaxReponse
                ? propre.Substring(0, longueurMaxReponse) + "..."
                : propre;
            champReponse.text = $"<b>R :</b> {affichage}";
        }

        if (afficherAutomatique && racinePanel != null)
            racinePanel.SetActive(true);
    }

    // ════════════════════════════════════════════════════════════════════
    // INFORMATIONS VICTIME
    // ════════════════════════════════════════════════════════════════════

    public void AfficherInfoVictime(VictimData victime)
    {
        if (champVictime == null || victime == null) return;
        champVictime.text = $"<b>Victime :</b> {victime.victimName} [{victime.victimCategory}]";
    }

    // ════════════════════════════════════════════════════════════════════
    // INFORMATIONS PROVIDERS
    // ════════════════════════════════════════════════════════════════════

    public void ActualiserProviders()
    {
        if (champProvider == null || apiMgr == null) return;
        champProvider.text =
            $"<b>LLM :</b> {apiMgr.LLMActif}  " +
            $"<b>STT :</b> {apiMgr.STTActif}  " +
            $"<b>TTS :</b> {apiMgr.TTSActif}";
    }

    // ════════════════════════════════════════════════════════════════════
    // CONTRÔLES
    // ════════════════════════════════════════════════════════════════════

    public void Effacer()
    {
        if (champQuestion != null) champQuestion.text = "<b>Q :</b> ---";
        if (champReponse  != null) champReponse.text  = "<b>R :</b> ---";
    }

    public void ToggleVisibilite()
    {
        if (racinePanel != null)
            racinePanel.SetActive(!racinePanel.activeSelf);
    }

    public void Afficher()
    {
        if (racinePanel != null) racinePanel.SetActive(true);
    }

    public void Masquer()
    {
        if (racinePanel != null) racinePanel.SetActive(false);
    }
}
