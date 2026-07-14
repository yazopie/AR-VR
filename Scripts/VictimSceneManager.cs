using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// ═══════════════════════════════════════════════════════════════════════════
// VictimSceneManager.cs — Orchestre les animations de groupe
//
// Commandes supportées (déclenchées par LLM ou STT direct) :
//   StandUp, WalkLeft, WalkRight, WalkRandom,
//   StandUpWalkLeft/Right/Random, Wave, StopWave, ResetAll
//
// Points de ralliement assignables dans l'Inspector.
// Chaque victime reçoit un offset aléatoire pour éviter les superpositions.
// ═══════════════════════════════════════════════════════════════════════════

public enum CommandeScene
{
    Aucune,
    Lever,
    MarcherGauche,
    MarcherDroite,
    MarcherAleatoire,
    LeverMarcherGauche,
    LeverMarcherDroite,
    LeverMarcherAleatoire,
    FaireSigne,
    ArretSigne,
    ToutReinitialiser
}

public class VictimSceneManager : MonoBehaviour
{
    [Header("── Victimes ─────────────────────────────────")]
    [Tooltip("Laisser vide = auto-découverte dans la scène")]
    [SerializeField] private List<VictimAnimController> toutesVictimes = new();

    [Header("── Points de ralliement ─────────────────────")]
    [Tooltip("Créer un GameObject vide dans la scène et l'assigner ici")]
    [SerializeField] private Transform pointRallimentGauche;
    [SerializeField] private Transform pointRallimentDroite;

    [Tooltip("Rayon de dispersion (évite les superpositions)")]
    [SerializeField] private float rayonGroupeRalliment = 1.2f;

    [Header("── Pipeline vocal ───────────────────────────")]
    [SerializeField] private SimpleSTT stt;
    [SerializeField] private SimpleLLM llm;

    [Header("── Tags LLM ─────────────────────────────────")]
    [SerializeField] private string tagLever         = "[ACTION:STAND_UP]";
    [SerializeField] private string tagMarcherG      = "[ACTION:WALK_LEFT]";
    [SerializeField] private string tagMarcherD      = "[ACTION:WALK_RIGHT]";
    [SerializeField] private string tagMarcherRnd    = "[ACTION:WALK_RANDOM]";
    [SerializeField] private string tagLeverMarcherG = "[ACTION:STAND_WALK_LEFT]";
    [SerializeField] private string tagLeverMarcherD = "[ACTION:STAND_WALK_RIGHT]";
    [SerializeField] private string tagLeverMarcherR = "[ACTION:STAND_WALK_RANDOM]";
    [SerializeField] private string tagReset         = "[ACTION:RESET]";
    [SerializeField] private string tagFaireSigne    = "[ACTION:WAVE]";
    [SerializeField] private string tagArretSigne    = "[ACTION:STOP_WAVE]";

    [Header("── Debug ─────────────────────────────────────")]
    [SerializeField] private bool debug = false;

    // ════════════════════════════════════════════════════════════════════
    // UNITY
    // ════════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (toutesVictimes.Count == 0)
            toutesVictimes.AddRange(FindObjectsOfType<VictimAnimController>());

        if (llm != null) llm.OnReponseRecue += OnReponseLLM;
        if (stt != null) stt.OnTranscriptionRecue += OnTranscriptionSTT;

        if (pointRallimentGauche == null)
            Debug.LogWarning("[VictimSceneManager] ⚠ Point de ralliement GAUCHE non assigné !");
        if (pointRallimentDroite == null)
            Debug.LogWarning("[VictimSceneManager] ⚠ Point de ralliement DROIT non assigné !");

        if (debug) LogVictimes();
    }

    private void OnDestroy()
    {
        if (llm != null) llm.OnReponseRecue -= OnReponseLLM;
        if (stt != null) stt.OnTranscriptionRecue -= OnTranscriptionSTT;
    }

    // ════════════════════════════════════════════════════════════════════
    // PARSING DES TAGS LLM
    // ════════════════════════════════════════════════════════════════════

    private void OnReponseLLM(string reponse)
    {
        CommandeScene cmd = ParserTags(reponse);
        if (debug) Debug.Log($"[VictimSceneManager] LLM → '{reponse}' → {cmd}");
        if (cmd == CommandeScene.Aucune) return;
        Executer(cmd);
    }

    private CommandeScene ParserTags(string r)
    {
        if (r.Contains(tagLever))         return CommandeScene.Lever;
        if (r.Contains(tagLeverMarcherG)) return CommandeScene.LeverMarcherGauche;
        if (r.Contains(tagLeverMarcherD)) return CommandeScene.LeverMarcherDroite;
        if (r.Contains(tagLeverMarcherR)) return CommandeScene.LeverMarcherAleatoire;
        if (r.Contains(tagMarcherG))      return CommandeScene.MarcherGauche;
        if (r.Contains(tagMarcherD))      return CommandeScene.MarcherDroite;
        if (r.Contains(tagMarcherRnd))    return CommandeScene.MarcherAleatoire;
        if (r.Contains(tagReset))         return CommandeScene.ToutReinitialiser;
        if (r.Contains(tagFaireSigne))    return CommandeScene.FaireSigne;
        if (r.Contains(tagArretSigne))    return CommandeScene.ArretSigne;
        return CommandeScene.Aucune;
    }

    // ── Fallback STT direct (sans LLM) ────────────────────────────────────
    private void OnTranscriptionSTT(string texte)
    {
        if (llm != null) return; // LLM gère les commandes

        string min    = texte.ToLower();
        bool marcher  = new[] { "marcher", "marchez", "marche", "walk" }.Any(k => min.Contains(k));
        bool lever    = new[] { "lever", "levez", "debout", "stand" }.Any(k => min.Contains(k));
        bool gauche   = new[] { "gauche", "left" }.Any(k => min.Contains(k));
        bool droite   = new[] { "droite", "right" }.Any(k => min.Contains(k));

        CommandeScene cmd = CommandeScene.Aucune;

        if (lever && marcher)
            cmd = gauche ? CommandeScene.LeverMarcherGauche
                : droite ? CommandeScene.LeverMarcherDroite
                : CommandeScene.LeverMarcherAleatoire;
        else if (marcher)
            cmd = gauche ? CommandeScene.MarcherGauche
                : droite ? CommandeScene.MarcherDroite
                : CommandeScene.MarcherAleatoire;

        if (cmd != CommandeScene.Aucune) Executer(cmd);
    }

    // ════════════════════════════════════════════════════════════════════
    // EXÉCUTION DES COMMANDES
    // ════════════════════════════════════════════════════════════════════

    public void Executer(CommandeScene commande)
    {
        if (debug) Debug.Log($"[VictimSceneManager] Exécuter → {commande}");

        switch (commande)
        {
            case CommandeScene.Lever:
                foreach (var v in toutesVictimes)
                    if (!v.CanWave) v.StandUp();
                break;

            case CommandeScene.MarcherGauche:
                EnvoyerVersRalliment(versGauche: true, leverDabord: false);
                break;

            case CommandeScene.MarcherDroite:
                EnvoyerVersRalliment(versGauche: false, leverDabord: false);
                break;

            case CommandeScene.MarcherAleatoire:
                EnvoyerVersRalliment(versGauche: Random.value > 0.5f, leverDabord: false);
                break;

            case CommandeScene.LeverMarcherGauche:
                EnvoyerVersRalliment(versGauche: true, leverDabord: true);
                break;

            case CommandeScene.LeverMarcherDroite:
                EnvoyerVersRalliment(versGauche: false, leverDabord: true);
                break;

            case CommandeScene.LeverMarcherAleatoire:
                EnvoyerVersRalliment(versGauche: Random.value > 0.5f, leverDabord: true);
                break;

            case CommandeScene.ToutReinitialiser:
                foreach (var v in toutesVictimes) v.ResetToIdle();
                break;

            case CommandeScene.FaireSigne:
                if (debug) Debug.Log($"[VictimSceneManager] Wave → {toutesVictimes.Count} victime(s)");
                foreach (var v in toutesVictimes)
                {
                    if (!v.CanWave) continue;
                    if (v.IsInPartialIdle) v.PlayFullIdle();
                    v.StartWave();
                }
                break;

            case CommandeScene.ArretSigne:
                foreach (var v in toutesVictimes) v.StopWave();
                break;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ENVOI VERS LE POINT DE RALLIEMENT
    // ════════════════════════════════════════════════════════════════════

    private void EnvoyerVersRalliment(bool versGauche, bool leverDabord)
    {
        Transform point = versGauche ? pointRallimentGauche : pointRallimentDroite;

        if (point == null)
        {
            Debug.LogWarning($"[VictimSceneManager] ⚠ Point {(versGauche ? "GAUCHE" : "DROIT")} non assigné !");
            return;
        }

        Vector3 centre = point.position;

        foreach (var victime in toutesVictimes)
        {
            // Les victimes "signe" ne bougent pas
            if (victime.CanWave && !victime.CanWalk) continue;
            if (!victime.CanWalk) continue;


            // Offset aléatoire pour dispersion naturelle du groupe
            Vector2 rand2D = Random.insideUnitCircle * rayonGroupeRalliment;
            Vector3 cible  = centre + new Vector3(rand2D.x, 0, rand2D.y);

            victime.WalkToRallyPoint(cible, versGauche);

            if (debug) Debug.Log($"[VictimSceneManager] {victime.name} → {cible}");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // API PUBLIQUE (boutons Inspector / UnityEvents)
    // ════════════════════════════════════════════════════════════════════

    public void CMD_MarcherGauche()         => Executer(CommandeScene.MarcherGauche);
    public void CMD_MarcherDroite()         => Executer(CommandeScene.MarcherDroite);
    public void CMD_MarcherAleatoire()      => Executer(CommandeScene.MarcherAleatoire);
    public void CMD_LeverMarcherGauche()    => Executer(CommandeScene.LeverMarcherGauche);
    public void CMD_LeverMarcherDroite()    => Executer(CommandeScene.LeverMarcherDroite);
    public void CMD_LeverMarcherAleatoire() => Executer(CommandeScene.LeverMarcherAleatoire);
    public void CMD_ToutReinitialiser()     => Executer(CommandeScene.ToutReinitialiser);
    public void CMD_FaireSigne()            => Executer(CommandeScene.FaireSigne);
    public void CMD_ArretSigne()            => Executer(CommandeScene.ArretSigne);

    // ════════════════════════════════════════════════════════════════════
    // DEBUG
    // ════════════════════════════════════════════════════════════════════

    private void LogVictimes()
    {
        Debug.Log($"[VictimSceneManager] {toutesVictimes.Count} victime(s) enregistrée(s) :");
        foreach (var v in toutesVictimes)
            Debug.Log($"  └─ {v.name} | Marche={v.CanWalk} | Signe={v.CanWave}");
    }
}
