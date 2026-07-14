using UnityEngine;
using System.Collections;

// ═══════════════════════════════════════════════════════════════════════════
// VictimAnimController.cs — Contrôleur d'animation des victimes
//
// STRUCTURE CONDITIONNELLE PAR TYPE DE VICTIME :
//   → CapaciteVictime.PeutMarcher  : affiche les champs de marche
//   → CapaciteVictime.PeutFaireSigne : affiche les champs de wave
//   → CapaciteVictime.Aucune       : victime immobile (couchée, inconsciente)
//
// Organisation :
//   [IDENTITÉ] → [TYPE & CAPACITÉ] → [ANIMATION IDLE] → [MARCHE*] → [WAVE*] → [DEBUG]
//   *Affiché uniquement si la capacité correspondante est activée
// ═══════════════════════════════════════════════════════════════════════════

public enum TypeVictime { Debout, Assis, CoucheDos, CoucheVentre }

[System.Flags]
public enum CapaciteVictime
{
    Aucune = 0,
    PeutMarcher = 1 << 0,   // Peut se déplacer vers un point de ralliement
    PeutFaireSigne = 1 << 1,  // Peut lever le bras / faire signe
}

[RequireComponent(typeof(Animator))]
public class VictimAnimController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════
    // ① IDENTITÉ
    // ════════════════════════════════════════════════════════════════════

    [Header("── Identité ────────────────────────────────────")]
    [SerializeField] private string victimId = "";
    [SerializeField] private string displayName = "Victime";

    // ════════════════════════════════════════════════════════════════════
    // ② TYPE & CAPACITÉ
    // ════════════════════════════════════════════════════════════════════

    [Header("── Type & Capacités ──────────────────────────────")]
    [SerializeField] private TypeVictime typeVictime = TypeVictime.Debout;
    [SerializeField] private CapaciteVictime capacites = CapaciteVictime.PeutMarcher | CapaciteVictime.PeutFaireSigne;

    // ════════════════════════════════════════════════════════════════════
    // ③ ÉTATS ANIMATOR (communs à toutes les victimes)
    // ════════════════════════════════════════════════════════════════════

    [Header("── États Animator ────────────────────────────────")]
    [SerializeField] private string etatIdle = "Idle";
    [SerializeField] private string etatIdleComplet = "Breathing-Idle";
    [Header("Séquence d'arrivée au ralliement")]
    [SerializeField] private string stateArrival1 = "Retourne";       // 1er état après arrivée
    [SerializeField] private string stateArrival2 = "Breathing-Idle"; // 2ème état (final)
                                                                      // Ajoute autant de champs que tu veux
    [SerializeField] private float dureePartialIdle = 3f;

    // ════════════════════════════════════════════════════════════════════
    // ④ MARCHE — affiché uniquement si PeutMarcher est activé
    //    (Si CapaciteVictime.PeutMarcher n'est PAS dans `capacites`,
    //     ces champs n'ont aucun effet et peuvent être ignorés)
    // ════════════════════════════════════════════════════════════════════

    [Header("── Marche (actif si PeutMarcher) ─────────────────")]
    [SerializeField] private string etatMarcheGauche = "Tourner-Gauche";
    [SerializeField] private string etatMarcheDroite = "Tourner-Droite";
    [SerializeField] private string etatMarche = "Marcher";
    [SerializeField] private string paramMarche = "IsWalking";
    [SerializeField] private string paramTournerGauche = "TurnLeft";
    [SerializeField] private string paramTournerDroite = "TurnRight";

    [SerializeField] private float vitesseMarche = 1.5f;
    [SerializeField] private float rayonArret = 0.4f;
    [SerializeField] private float vitesseRotation = 200f;
    [SerializeField] private int nombreBouclesMarche = 3;
    [SerializeField] private float dureeLever = 3f;
    [SerializeField] private Transform cibleCamera;  // Pour orientations après arrivée

    // ════════════════════════════════════════════════════════════════════
    // ⑤ SIGNE / WAVE — affiché uniquement si PeutFaireSigne est activé
    // ════════════════════════════════════════════════════════════════════

    [Header("── Wave / Signe (actif si PeutFaireSigne) ─────────")]
    [SerializeField] private string paramWave = "IsWaving";
    [SerializeField] private float dureeWave = 2f;     // Durée d'un geste
    [SerializeField] private float pauseWave = 3f;     // Pause entre deux gestes
    [SerializeField] private int repetitionsWave = 0;      // 0 = infini, N = N fois

    // ════════════════════════════════════════════════════════════════════
    // ⑥ DEBUG
    // ════════════════════════════════════════════════════════════════════

    [Header("── Debug ────────────────────────────────────────")]
    [SerializeField] private bool debug = false;

    // ════════════════════════════════════════════════════════════════════
    // PROPRIÉTÉS PUBLIQUES
    // ════════════════════════════════════════════════════════════════════

    public string VictimId => victimId;
    public string DisplayName => displayName;
    public TypeVictime VType => typeVictime;
    public bool CanWave => (capacites & CapaciteVictime.PeutFaireSigne) != 0;
    public bool CanWalk => (capacites & CapaciteVictime.PeutMarcher) != 0;
    public bool NeedsToStandFirst => typeVictime != TypeVictime.Debout;
    public bool IsInPartialIdle => _enPartialIdle;
    public bool IsWalking => _enMarche;

    // ════════════════════════════════════════════════════════════════════
    // ÉTAT INTERNE
    // ════════════════════════════════════════════════════════════════════

    private Animator _anim;
    private bool _enPartialIdle = false;
    private bool _enMarche = false;
    private Coroutine _routineActive;
    private Coroutine _routineWave;

    // ════════════════════════════════════════════════════════════════════
    // UNITY
    // ════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        if (_anim == null)
            Debug.LogError($"[VictimAnimController] ❌ Pas d'Animator sur {name} !");
    }

    private void Start() => JouerPartialIdle();

    // ════════════════════════════════════════════════════════════════════
    // IDLE PARTIEL — Première frame figée (victime en attente)
    // ════════════════════════════════════════════════════════════════════

    public void JouerPartialIdle()
    {
        StopperRoutineActive();
        ReinitialiserParams();
        _enPartialIdle = true;
        _routineActive = StartCoroutine(BouclePartialIdle());
        Log("→ PartialIdle");
    }

    private IEnumerator BouclePartialIdle()
    {
        _anim.Play(etatIdle, 0, 0f);
        _anim.speed = 0f;
        while (_enPartialIdle) yield return null;
        _anim.speed = 1f;
    }


    // ════════════════════════════════════════════════════════════════════
    // IDLE COMPLET — Respiration normale
    // ════════════════════════════════════════════════════════════════════

    public void PlayFullIdle()
    {
        StopperRoutineActive();
        ReinitialiserParams();
        _anim.CrossFade(etatIdleComplet, 0.25f, 0);
        Log($"→ FullIdle ({etatIdleComplet})");
    }

    // ════════════════════════════════════════════════════════════════════
    // SE LEVER
    // ════════════════════════════════════════════════════════════════════

    public void StandUp()
    {
        if (NeedsToStandFirst)
        {
            StopperRoutineActive();
            _routineActive = StartCoroutine(RoutineLever());
        }
        else
            PlayFullIdle();
    }

    private IEnumerator RoutineLever()
    {
        ReinitialiserParams();
        Log("→ Lever...");
        yield return new WaitForSeconds(dureeLever);
        PlayFullIdle();
        Log("→ Debout");
    }

    // ════════════════════════════════════════════════════════════════════
    // MARCHE VERS POINT DE RALLIEMENT
    // (Disponible uniquement si CanWalk == true)
    // ════════════════════════════════════════════════════════════════════

    public void WalkToRallyPoint(Vector3 cible, bool versGauche)
    {
        if (!CanWalk)
        {
            Log("⚠ Cette victime ne peut pas marcher (PeutMarcher non activé).");
            return;
        }

        StopperRoutineActive();

        _routineActive = NeedsToStandFirst
            ? StartCoroutine(RoutineLeverPuisMarcher(cible, versGauche))
            : StartCoroutine(RoutineMarcher(cible, versGauche));
    }

    private IEnumerator RoutineLeverPuisMarcher(Vector3 cible, bool versGauche)
    {
        ReinitialiserParams();
        Log("→ Lever avant marche...");
        yield return new WaitForSeconds(dureeLever);
        yield return StartCoroutine(RoutineMarcher(cible, versGauche));
    }

    private IEnumerator RoutineMarcher(Vector3 cible, bool versGauche)
    {
        _enMarche = true;
        ReinitialiserParams();

        // Phase 1 : Rotation animée
        string etatTour = versGauche ? etatMarcheGauche : etatMarcheDroite;
        _anim.Play(etatTour, 0, 0f);
        _anim.SetBool(versGauche ? paramTournerGauche : paramTournerDroite, true);

        Vector3 direction = cible - transform.position;
        direction.y = 0f;

        // Phase 2 : Marche vers la cible
        _anim.SetBool(paramTournerGauche, false);
        _anim.SetBool(paramTournerDroite, false);
        _anim.applyRootMotion = true;
        _anim.SetBool(paramMarche, true);
        _anim.Play(etatMarche, 0, 0f);

        while (direction.sqrMagnitude > 0.001f)
        {
            Quaternion rotCible = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, rotCible, vitesseRotation * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, rotCible) < 2f) break;

            direction = cible - transform.position;
            direction.y = 0f;
            yield return null;
        }

        Log("→ Rotation terminée → Marche !");

        int boucles = 0;
        while (boucles < nombreBouclesMarche)
        {
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(cible.x, 0, cible.z));

            if (dist <= rayonArret) { Log($"→ Arrivé après {boucles} boucle(s)"); break; }

            var info = _anim.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(etatMarche) && info.normalizedTime >= 0.95f)
            {
                boucles++;
                if (boucles < nombreBouclesMarche) _anim.Play(etatMarche, 0, 0f);
                else { Log("→ Nombre max de boucles atteint"); break; }
            }
            yield return null;
        }

        // Phase 3 : Arrêt + orientation caméra
        _anim.applyRootMotion = false;
        ReinitialiserParams();
        _enMarche = false;

        Transform cible_ = cibleCamera != null ? cibleCamera : Camera.main?.transform;
        if (cible_ != null)
            yield return StartCoroutine(RoterVers(cible_.position));

        _routineActive = null;
        //StartCoroutine(RoutineRespiration());
        StartCoroutine(RoutineSequenceArrivee());
    }

    private IEnumerator RoutineSequenceArrivee()
    {
        yield return null; // 1 frame de stabilisation

        // ── État 1 : Retourne ──────────────────────────────────────────
        _anim.speed = 1f;
        _anim.Play(stateArrival1, 0, 0f);
        yield return null; // laisse l'Animator prendre en compte le Play

        // Attend la fin de l'animation (non-looping)
        while (true)
        {
            var info = _anim.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(stateArrival1) && info.normalizedTime >= 0.95f)
                break;
            yield return null;
        }

        Log($"→ {stateArrival1} terminé");

        // ── État 2 : état final (Breathing-Idle, etc.) ─────────────────
        _anim.Play(stateArrival2, 0, 0f);
        Log($"→ {stateArrival2}");
    }
    private IEnumerator RoutineRespiration()
    {
        yield return null;
        _anim.speed = 1f;
        _anim.Play(etatIdleComplet, 0, 0f);
    }

    private IEnumerator RoterVers(Vector3 positionCible)
    {
        Vector3 dir = positionCible - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) yield break;

        Quaternion rotCible = Quaternion.LookRotation(dir.normalized);
        while (Quaternion.Angle(transform.rotation, rotCible) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, rotCible, vitesseRotation * Time.deltaTime);
            yield return null;
        }
        transform.rotation = rotCible;
    }

    // ════════════════════════════════════════════════════════════════════
    // WAVE / SIGNE
    // (Disponible uniquement si CanWave == true)
    // ════════════════════════════════════════════════════════════════════

    public void StartWave()
    {
        if (!CanWave)
        {
            Log("⚠ Cette victime ne peut pas faire signe (PeutFaireSigne non activé).");
            return;
        }

        StopWave();
        _routineWave = StartCoroutine(BoucleWave());
        Log($"→ Wave START | répétitions={repetitionsWave} | pause={pauseWave}s");
    }

    public void StopWave()
    {
        if (_routineWave != null)
        {
            StopCoroutine(_routineWave);
            _routineWave = null;
        }
        if (_anim != null) _anim.SetBool(paramWave, false);
        Log("→ Wave STOP");
    }

    private IEnumerator BoucleWave()
    {
        int compteur = 0;

        while (true)
        {
            _anim.SetBool(paramWave, true);
            yield return new WaitForSeconds(dureeWave);

            _anim.SetBool(paramWave, false);
            compteur++;

            Log($"→ Wave {compteur}/{(repetitionsWave == 0 ? "∞" : repetitionsWave.ToString())}");

            if (repetitionsWave > 0 && compteur >= repetitionsWave)
            {
                Log("→ Wave terminé.");
                _routineWave = null;
                yield break;
            }

            yield return new WaitForSeconds(pauseWave);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // RESET
    // ════════════════════════════════════════════════════════════════════

    public void ResetToIdle()
    {
        StopWave();
        JouerPartialIdle();
        Log("→ Reset → PartialIdle");
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS INTERNES
    // ════════════════════════════════════════════════════════════════════

    private void StopperRoutineActive()
    {
        _enPartialIdle = false;
        _enMarche = false;
        _anim.speed = 1f;
        _anim.applyRootMotion = false;

        if (_routineActive != null)
        {
            StopCoroutine(_routineActive);
            _routineActive = null;
        }
    }

    private void ReinitialiserParams()
    {
        _anim.SetBool(paramMarche, false);
        _anim.SetBool(paramTournerGauche, false);
        _anim.SetBool(paramTournerDroite, false);
    }

    private void Log(string msg)
    {
        if (debug) Debug.Log($"[{name}] {msg}");
    }
}
