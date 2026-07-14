using UnityEngine;
using UnityEngine.XR;
using System;
using System.Collections.Generic;
using System.Reflection;

// ═══════════════════════════════════════════════════════════════════════════
// RaycastPTT.cs — Pointe & Parle (Quest 2 / PC)
//
// DEUX MODES :
//   MODE Q&A   : le rayon pointe une victime → question à CETTE victime
//   MODE ORDRE : le rayon pointe ailleurs    → commande pour TOUTES
//
// PC    : clic gauche maintenu = PTT | clic droit = ANNULER
// Quest : bouton X maintenu   = PTT | bouton Y   = ANNULER
// ═══════════════════════════════════════════════════════════════════════════

public class RaycastPTT : MonoBehaviour
{
    [Header("── Références ────────────────────────────────")]
    [SerializeField] private SimpleSTT          stt;
    [SerializeField] private SimpleLLM          llm;
    [SerializeField] private SimpleAIManager    aiMgr;
    [SerializeField] private VictimManager      victimMgr;
    [SerializeField] private VictimSceneManager sceneManager;

    [Header("── Source du rayon ──────────────────────────")]
    [Tooltip("Vide = souris. Assigné = Transform du contrôleur gauche Quest 2")]
    [SerializeField] private Transform raySource;

    [Header("── Paramètres ────────────────────────────────")]
    [SerializeField] private float     distanceMax  = 10f;
    [SerializeField] private LayerMask masqueCalque = Physics.DefaultRaycastLayers;

    [Header("── Debug ─────────────────────────────────────")]
    [SerializeField] private bool debug = false;

    // ── Événements publics ────────────────────────────────────────────────
    /// <summary>
    /// true  = mode Q&A (victime ciblée)  — string = nom affiché de la victime
    /// false = mode ORDRE                 — string = null
    /// </summary>
    public event Action<bool, string> OnModeChange;

    // ── Privé ─────────────────────────────────────────────────────────────
    private bool                 _enregistrement = false;
    private VictimAnimController _victimeCiblee  = null;
    private InputDevice          _controleurGauche;

    // ── OVR (Meta Quest via Reflection) ───────────────────────────────────
    private MethodInfo _ovrGet;
    private object     _ovrBouton1, _ovrBouton2, _ovrLTouch;
    private bool       _ovrDisponible = false;

    // ════════════════════════════════════════════════════════════════════
    // DÉMARRAGE
    // ════════════════════════════════════════════════════════════════════

    private void Start()
    {
        if (stt == null)       Debug.LogError("[RaycastPTT] ❌ SimpleSTT non assigné !");
        if (llm == null)       Debug.LogWarning("[RaycastPTT] SimpleLLM non assigné.");
        if (victimMgr == null) Debug.LogWarning("[RaycastPTT] VictimManager non assigné.");
        if (sceneManager == null) Debug.LogWarning("[RaycastPTT] VictimSceneManager non assigné.");

        InitOVR();
        InitXR();

        if (stt != null)
            stt.OnTranscriptionRecue += OnTranscriptionRecue;
    }

    private void OnDestroy()
    {
        if (stt != null)
            stt.OnTranscriptionRecue -= OnTranscriptionRecue;
    }

    // ════════════════════════════════════════════════════════════════════
    // BOUCLE UPDATE
    // ════════════════════════════════════════════════════════════════════

    private void Update()
    {
        // Annulation prioritaire
        if (BoutonAnnulation())
        {
            stt?.Annuler();
            _enregistrement = false;
            _victimeCiblee  = null;
            if (debug) Debug.Log("[RaycastPTT] ANNULÉ.");
            return;
        }

        if (raySource == null) GererSouris();
        else                   GererVR();
    }

    // ════════════════════════════════════════════════════════════════════
    // MODE SOURIS (PC)
    // ════════════════════════════════════════════════════════════════════

    private void GererSouris()
    {
        bool appui = Input.GetMouseButton(0);

        if (appui && !_enregistrement)
        {
            _victimeCiblee  = DetecterVictime();
            _enregistrement = true;
            NotifierModeChange();
            stt?.DemarrerEnregistrement();
            if (debug) Debug.Log($"[RaycastPTT][Souris] DÉBUT → {EtiquetteMode()}");
        }
        else if (!appui && _enregistrement)
        {
            _enregistrement = false;
            stt?.ArreterEnregistrement();
            if (debug) Debug.Log("[RaycastPTT][Souris] FIN");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // MODE VR (Meta Quest)
    // ════════════════════════════════════════════════════════════════════

    private void GererVR()
    {
        bool xAppuye = BoutonX();

        if (xAppuye && !_enregistrement)
        {
            _victimeCiblee  = DetecterVictime();
            _enregistrement = true;
            NotifierModeChange();
            stt?.DemarrerEnregistrement();
            if (debug) Debug.Log($"[RaycastPTT][VR] DÉBUT → {EtiquetteMode()}");
        }

        if (_enregistrement && !xAppuye)
        {
            _enregistrement = false;
            stt?.ArreterEnregistrement();
            if (debug) Debug.Log("[RaycastPTT][VR] FIN");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // TRAITEMENT DE LA TRANSCRIPTION
    // ════════════════════════════════════════════════════════════════════

    private void OnTranscriptionRecue(string transcription)
    {
        if (llm == null) return;

        if (_victimeCiblee != null)
        {
            // ── MODE Q&A ─────────────────────────────────────────────
            if (victimMgr != null && !string.IsNullOrEmpty(_victimeCiblee.VictimId))
            {
                victimMgr.ChargerVictime(_victimeCiblee.VictimId);
                llm.RechargerSystemPrompt();
            }

            // Transmet la question au SimpleAIManager (pour le feedback)
            aiMgr?.DefinirDerniereQuestion(transcription);

            if (debug) Debug.Log($"[RaycastPTT] Q&A → {_victimeCiblee.DisplayName} | \"{transcription}\"");
            _ = llm.Ask($"[QA] {transcription}");
        }
        else
        {
            // ── MODE ORDRE ────────────────────────────────────────────
            if (debug) Debug.Log($"[RaycastPTT] ORDRE → \"{transcription}\"");
            _ = llm.Ask($"[ORDRE] {transcription}");
        }

        _victimeCiblee = null;
    }

    // ════════════════════════════════════════════════════════════════════
    // DÉTECTION DE LA VICTIME POINTÉE
    // ════════════════════════════════════════════════════════════════════

    private VictimAnimController DetecterVictime()
    {
        Ray rayon = ConstruireRayon();
        RaycastHit[] touches = Physics.RaycastAll(rayon, distanceMax, masqueCalque);

        foreach (RaycastHit touch in touches)
        {
            VictimAnimController victime =
                touch.collider.GetComponent<VictimAnimController>() ??
                touch.collider.GetComponentInParent<VictimAnimController>();

            if (victime != null)
            {
                if (debug) Debug.Log($"[RaycastPTT] Victime détectée : {victime.DisplayName} (id={victime.VictimId})");
                return victime;
            }
        }

        if (debug) Debug.Log("[RaycastPTT] Aucune victime → mode ORDRE");
        return null;
    }

    // ════════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════════

    private void NotifierModeChange()
    {
        bool   estQA       = _victimeCiblee != null;
        string nomVictime  = estQA ? _victimeCiblee.DisplayName : null;
        OnModeChange?.Invoke(estQA, nomVictime);
    }

    private string EtiquetteMode() =>
        _victimeCiblee != null ? $"Q&A ({_victimeCiblee.DisplayName})" : "ORDRE";

    private Ray ConstruireRayon()
    {
        if (raySource != null)
            return new Ray(raySource.position, raySource.forward);
        if (Camera.main != null)
            return Camera.main.ScreenPointToRay(Input.mousePosition);
        return new Ray(Vector3.zero, Vector3.forward);
    }

    // ════════════════════════════════════════════════════════════════════
    // BOUTONS
    // ════════════════════════════════════════════════════════════════════

    private bool BoutonX()
    {
        if (_ovrDisponible && _ovrGet != null)
        {
            try { return (bool)_ovrGet.Invoke(null, new[] { _ovrBouton1, _ovrLTouch }); }
            catch { }
        }
        if (!_controleurGauche.isValid) InitXR();
        return _controleurGauche.isValid
            && _controleurGauche.TryGetFeatureValue(CommonUsages.primaryButton, out bool appuye)
            && appuye;
    }

    private bool BoutonAnnulation()
    {
        if (_ovrDisponible && _ovrGet != null)
        {
            try
            {
                bool y = (bool)_ovrGet.Invoke(null, new[] { _ovrBouton2, _ovrLTouch });
                if (y) return true;
            }
            catch { }
        }
        if (!_controleurGauche.isValid) InitXR();
        if (_controleurGauche.isValid
            && _controleurGauche.TryGetFeatureValue(CommonUsages.secondaryButton, out bool yXR)
            && yXR) return true;

        return Input.GetMouseButtonDown(1);
    }

    // ════════════════════════════════════════════════════════════════════
    // INITIALISATION CONTROLLERS
    // ════════════════════════════════════════════════════════════════════

    private void InitOVR()
    {
        try
        {
            Type typeOVR = Type.GetType("OVRInput, Assembly-CSharp-firstpass")
                        ?? Type.GetType("OVRInput, Assembly-CSharp")
                        ?? Type.GetType("OVRInput, OVR");
            if (typeOVR == null) return;

            Type typeBouton     = typeOVR.GetNestedType("Button");
            Type typeController = typeOVR.GetNestedType("Controller");
            if (typeBouton == null || typeController == null) return;

            _ovrGet        = typeOVR.GetMethod("Get", new[] { typeBouton, typeController });
            _ovrBouton1    = Enum.Parse(typeBouton, "One");
            _ovrBouton2    = Enum.Parse(typeBouton, "Two");
            _ovrLTouch     = Enum.Parse(typeController, "LTouch");
            _ovrDisponible = true;

            if (debug) Debug.Log("[RaycastPTT] ✓ OVRInput détecté.");
        }
        catch { _ovrDisponible = false; }
    }

    private void InitXR()
    {
        var appareils = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, appareils);
        if (appareils.Count > 0)
        {
            _controleurGauche = appareils[0];
            if (debug) Debug.Log("[RaycastPTT] ✓ Contrôleur XR gauche → " + _controleurGauche.name);
        }
    }

    private void OnDisable()
    {
        if (_enregistrement)
        {
            _enregistrement = false;
            stt?.Annuler();
        }
    }
}
