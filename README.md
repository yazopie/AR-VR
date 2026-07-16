# AR-VR — Simulation VR de Formation aux Premiers Secours

**AR-VR** est une plateforme de formation en réalité virtuelle (Unity, PC & Meta Quest 2) qui simule des scénarios d'urgence médicale/accidentelle. Le user est au milieu de victimes virtuelles animées, leur parle à voix haute, et reçoit des réponses vocales générées par une IA conversationnelle — le tout piloté par un pipeline **Voix → Texte → IA → Voix** (STT → LLM → TTS) entièrement configurable.

>  Ce dépôt contient le **code source** (scripts C#) et les **ressources de configuration** (fiches victimes, clés API) du projet. Les scènes Unity, prefabs, modèles 3D et animations sont distribués séparément via le fichier `.unitypackage` — voir Installation.

---

##  Télécharger le projet Unity complet

**[Télécharger le `.unitypackage` (Google Drive)](https://drive.google.com/file/d/1C3bmwGeu7_s9d9b5N7R_hlvcpzw2ycqp/view?usp=drive_link)**

---

##  Vue d'ensemble du fonctionnement

```
  Micro (SimpleSTT)
        │  audio → WAV
        ▼
  Transcription (Groq Whisper / Unsloth / Gemini)
        │  texte
        ▼
 LLM (SimpleLLM) — "joue" la victime ciblée ou interprète un ordre de groupe
        │  réponse texte (+ tag [ACTION:...] si commande)
        ▼
  TTS (SimpleTTS) — voix propre à chaque victime
        │
        ▼
  Animation (VictimAnimController / VictimSceneManager) si un ordre est détecté
```

Deux **modes d'interaction** cohabitent, gérés par `RaycastPTT` :

| Mode | Déclenché quand… | Effet |
|---|---|---|
| **Q&A** | Le rayon pointe une victime précise | Le LLM répond **en tant que cette victime** (voix, personnalité, état définis dans son JSON) |
| **ORDRE** | Le rayon ne pointe personne | Le LLM interprète une commande de groupe (« lève-toi », « marche à droite », « fais signe », « réinitialise »...) et déclenche l'animation correspondante sur toutes les victimes concernées |

Tous les providers (LLM, STT, TTS) sont interchangeables à chaud via un **panneau de configuration in-game** (touche `F1`).

---

## Structure du dépôt

```
AR-VR/
├── Scripts/
│   ├── SimpleAIManager.cs        # Orchestrateur du pipeline STT → LLM → TTS
│   ├── APIProviderManager.cs     # Centralise fournisseurs, URLs, modèles, clés
│   ├── SimpleAPIKeys.cs          # Lecture du fichier de clés API
│   ├── SimpleSTT.cs              # Reconnaissance vocale (Groq / Unsloth / Gemini)
│   ├── SimpleLLM.cs              # Appel LLM + prompt système dynamique
│   ├── SimpleTTS.cs              # Synthèse vocale multi-providers + bibliothèque de voix
│   ├── AI_WAV.cs                 # Conversion AudioClip → WAV
│   ├── RaycastPTT.cs             # Pointer-et-parler (souris PC / manette Quest)
│   ├── VictimManager.cs          # Chargement/sauvegarde des fiches victimes (JSON)
│   ├── VictimData.cs             # Modèle de données d'une victime
│   ├── VictimAnimController.cs   # Animations individuelles (idle, marche, signe)
│   ├── VictimSceneManager.cs     # Orchestration des ordres de groupe
│   ├── FeedbackManager.cs        # Historique des échanges (session, Q/R, audio)
│   ├── LatencyTracker.cs         # Mesure et export CSV des latences STT/LLM/TTS
│   ├── DebugPanel.cs             # Affichage en jeu de la question/réponse en cours
│   ├── PanneauJeu.cs             # Panneau de configuration in-game (F1)
│   ├── CheminsDonnees.cs         # Centralise tous les chemins de sauvegarde
│   ├── PlayerController.cs / PlayerControls.cs  # Input System (générés automatiquement)
│   ├── GameStartUI.cs / StartPoint.cs           # Démarrage / positionnement du joueur
│   ├── GarrotAttach.cs / ItemSpawner.cs / RegenerableItem.cs  # Objets XR interactifs (ex. garrot)
│   └── Vision Follower.cs
│
├── Ressources/
│   ├── APIKeys.txt                # Modèle vide du fichier de clés à compléter
│   └── Victime-Store/             # Fiches JSON des victimes pré-configurées
│       ├── victime_debout.json
│       ├── victime_assis.json
│       ├── victime_couche_dos.json
│       ├── victime_couche_ventre.json
│       ├── victime_chevilles.json
│       └── victime_cuisse.json
│
└── README.md
```

---

## Fonctionnalités principales

- **Multi-providers, interchangeables à chaud** : LLM (Groq Cloud, Unsloth, Gemini), STT (Groq Whisper, Unsloth, Gemini), TTS (ElevenLabs, Speechify, Supertone, Gemini).
- **Victimes configurables sans coder** : chaque victime est une fiche JSON définissant nom, voix, état, contexte d'accident, profil émotionnel et règles de comportement — injectés automatiquement dans le prompt système du LLM.
- **Commandes de groupe pilotées par la voix** : « levez-vous », « allez à droite », « faites signe » sont détectées par le LLM, traduites en tags `[ACTION:...]`, puis exécutées par `VictimSceneManager` sur les victimes capables de les effectuer.
- **Compatible PC et Meta Quest 2** : `RaycastPTT` bascule entre souris (clic gauche = parler, clic droit = annuler) et manette VR (bouton X = parler, bouton Y = annuler).
- **Journal de session complet** : `FeedbackManager` enregistre chaque échange (question, réponse, audio) et `LatencyTracker` exporte un CSV des latences par étape (ASR/LLM/TTS).
- **Panneau de configuration in-game (`F1`)** : changer de provider/modèle, gérer la bibliothèque de voix, éditer le prompt de la victime active, sans recompiler.

---

## Prérequis

- **Unity** (recommandé : 2022 LTS ou plus récent).
- Packages : `Input System`, `XR Interaction Toolkit`, `TextMeshPro`, `Newtonsoft Json` (`com.unity.nuget.newtonsoft-json`).
- Pour le Quest 2 : SDK Meta/Oculus (OVRInput) ou XR Plugin Management + OpenXR.
- Des clés API valides pour au moins un provider LLM, un STT et un TTS.

---

## Installation

**Option recommandée — importer le package complet**
1. Créez/ouvrez un projet Unity 3D et installez les packages ci-dessus.
2. Téléchargez le `.unitypackage` : [lien Google Drive](https://drive.google.com/file/d/1C3bmwGeu7_s9d9b5N7R_hlvcpzw2ycqp/view?usp=drive_link).
3. `Assets → Import Package → Custom Package…` puis sélectionnez le fichier.
4. Configurez vos clés API (voir plus bas) puis lancez la scène principale.

**Option alternative — depuis ce dépôt seul**
Copiez `Scripts/` dans `Assets/Scripts/` et `Ressources/` dans `Assets/Resources/`, puis assignez manuellement les composants sur vos GameObjects (voir Architecture des composants).

---

## Configuration des clés API

`SimpleAPIKeys.cs` lit `Assets/Resources/Secure/APIkeys.txt`. Format (une clé par ligne) :

```
Groq_API_Key:votre_clé_groq
ElevenLabs_API_Key:votre_clé_elevenlabs
Gemini_API_Key:votre_clé_gemini
Unsloth_API_Key:votre_clé_unsloth
Speechify_API_Key:votre_clé_speechify
Supertone_API_Key:votre_clé_supertone
HF_API_Key:votre_clé_huggingface
```

> Seules les clés des providers réellement sélectionnés dans `APIProviderManager` sont nécessaires. `SimpleAIManager` refuse de démarrer le pipeline concerné si la clé manque.

---

## Créer ou modifier une victime

Exemple (`victime_assis.json`) :

```json
{
  "id": "victime_assis",
  "victimName": "Moussa Diallo",
  "victimCategory": "physique",
  "voiceId": "acw2cWc6cub0KWkFpIlv",
  "voiceProvider": "ElevenLabs",
  "roleDescription": "Victime souffrant d'une blessure douloureuse au pied mais capable de marcher avec difficulté.",
  "accidentContext": "Son pied a été blessé pendant l'accident...",
  "behaviorRules": "Marche avec beaucoup d'effort et une forte boiterie...",
  "currentState": "Debout et capable de marcher lentement avec difficulté...",
  "emotionProfile": "Déterminé mais souffrant, stressé et inquiet..."
}
```

Ajoutez un fichier `.json` dans `Victime-Store/` (même schéma, `id` unique), ou utilisez le bouton **Sauvegarder** de `VictimManager` en jeu. Les capacités physiques (`PeutMarcher`, `PeutFaireSigne`, aucune) déterminent la réaction de la victime aux ordres de groupe.

---

## Contrôles

| Action | PC | Meta Quest 2 |
|---|---|---|
| Parler (PTT) | Clic gauche maintenu | Bouton X maintenu |
| Annuler | Clic droit | Bouton Y |
| Panneau de config | `F1` | — |
| Cibler une victime | Pointer avec la souris | Rayon du contrôleur gauche |

---

## Données de sortie

Sous `Application.persistentDataPath/` (nom de dossier défini en dur dans `CheminsDonnees.cs`) : `Feedback/` (sessions Q/R), `Audio/`, `Latence/latence_log.csv`, `Victimes/`, `Config/*.json`.

---

## Notes

- `AI_WAV.cs` provient du projet *DigitalPlusPlus*, sous licence **GPL 3.0**.
- Le `.unitypackage` reste la source de vérité pour les assets non versionnés ici (scènes, modèles, animations).