using System;
using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════
// VictimData.cs — Données complètes d'une victime (sérialisable JSON)
// Champs ajoutés : voiceId, victimCategory
// ═══════════════════════════════════════════════════════════════════════════

[Serializable]
public class VictimData
{
    // ── Identité ──────────────────────────────────────────────────────────
    public string id;                  // Identifiant unique auto-généré (ex: "a3f8b12c")
    public string victimName;          // Prénom/Nom de la victime

    // ── Classification ────────────────────────────────────────────────────
    /// <summary>
    /// Catégorie de la victime :
    /// blesse_leger, blesse_grave, inconscient, psychologique, valide
    /// </summary>
    public string victimCategory;

    // ── Voix TTS ──────────────────────────────────────────────────────────
    public string voiceId;             // ID de voix TTS (ElevenLabs, Supertone, etc.)
    public string voiceProvider;       // Provider de la voix (ex: "ElevenLabs", "Supertone")

    // ── Contexte narratif ─────────────────────────────────────────────────
    public string roleDescription;     // Description du rôle joué
    public string accidentContext;     // Contexte de l'accident
    public string behaviorRules;       // Règles de comportement pour le LLM
    public string currentState;        // État actuel (blessures, position, etc.)
    public string emotionProfile;      // Profil émotionnel (anxieux, calme, en choc...)

    // ── Métadonnées ───────────────────────────────────────────────────────
    public string dateCreation;        // Date de création (ISO 8601)
    public string dateModification;    // Dernière modification
}
