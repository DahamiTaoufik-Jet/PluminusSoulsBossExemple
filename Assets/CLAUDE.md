# Soulsboss — Unity 6

Memo d'architecture pour Claude Code. Lis ce fichier en premier avant toute modification.

## Resume du projet

Jeu de combat Souls-like en 3D. Un joueur affronte un boss. Objectif final : brancher le plugin **Pluminus** (Q-learning) pour que le boss apprenne a se battre via IA vs IA (joueur artificiel vs boss).

Le projet utilise le **New Input System** et **Cinemachine** pour la camera.

## Arborescence des scripts

```
Assets/Scripts/Combat/
├── IDamageable.cs            interface + enum Team (Player, Boss)
├── Health.cs                 MonoBehaviour : HP, invincible (i-frames), guarding (bouclier)
│                             Events : OnDamaged, OnBlocked, OnDeath
│                             TakeDamage() respecte invincible + guarding
│                             TakeDamageUnblockable() ignore tout
│
├── DamageHitbox.cs           trigger collider, Begin()/End() pilotes par les attaques
│                             HashSet anti-double hit par swing
│
├── LockOnTarget.cs           marker sur le boss, registre statique All
├── LockOnController.cs       sur le joueur, Toggle() via OnLockOn, scoring angle+distance
│
├── CameraController.cs       pivot de camera independant du joueur
│                             Free mode : souris pilote yaw/pitch
│                             Lock-on : auto-orient vers le boss
│
├── ThirdPersonCamera.cs      LEGACY — a supprimer, remplace par CameraController
│
├── PlayerController.cs       mouvement camera-relatif (cameraTransform.forward)
│                             Lock-on : face au boss, strafe
│                             Free : face direction de mouvement
│
├── PlayerCombat.cs           attaque verticale (swordPivot anime, cooldown 2s)
│                             dodge gauche/droite avec i-frames
│                             RequestAttack() / RequestDodge() = API publique
│
├── BossController.cs         state machine : Idle/Guarding/Attacking/Dead
│                             boucle Guard -> pick attack -> execute -> repeat
│                             PickAttack() par poids + portee
│                             ForceAttack() pour debug inspector
│                             InterruptForCounter() pour le shield counter
│
├── BossShield.cs             Raise()/Lower() anime le bouclier (position + rotation lerp)
│                             Pilote health.guarding
│                             guardLocalPos/Rot + loweredLocalPos/Rot editables
│
├── BossAttack.cs             abstract base : Execute(BossController) coroutine
│                             minRange, maxRange, weight pour le picker
│
├── BossSwordSwing.cs         attaque verticale : raise -> swing (Z axis) -> ending lag
│                             swordPivot anime, raisedRotation/restRotation editables
│
├── BossDiagonalSlash.cs      frappe diagonale : windupRotation -> endRotation
│                             deux instances (L->R, R->L) forcent dodge directionnel
│
├── BossThrust.cs             estoc : recul en position -> thrust avant
│                             windupPosition/thrustPosition editables
│                             esquivable des deux cotes
│
├── BossShieldCounter.cs      declanche quand joueur frappe le bouclier (Health.OnBlocked)
│                             aura inesquivable : TakeDamageUnblockable + knockback
│                             particules generees au runtime (charge + burst)
│
└── Editor/
    └── BossControllerEditor.cs   inspector custom : boutons force attack + shield + counter
```

## Hierarchie de scene

```
Main Camera          CinemachineBrain, tag MainCamera
TPSCam               Cinemachine Camera, Third Person Follow
                     Tracking Target = CameraPivot
                     Position Control = Third Person Follow
                     Rotation Control = None

CameraPivot          [PlayerInput] [CameraController]
                     followTarget = PlayerObject, lockOn = PlayerObject/LockOnController

PlayerObject         tag Player
  [CharacterController] [PlayerInput] [Health team=Player]
  [PlayerController] [PlayerCombat] [LockOnController]
  ├── Player         mesh corps
  ├── Head           mesh tete
  └── Sword          [BoxCollider trigger] [Rigidbody kinematic] [DamageHitbox team=Player]

BossObject
  [Health team=Boss] [BossController] [BossShield] [LockOnTarget pivot=BossHead]
  [BossSwordSwing] [BossDiagonalSlash x2] [BossThrust] [BossShieldCounter]
  ├── Boss           mesh corps
  ├── BossHead       mesh tete
  ├── BossSword      [BoxCollider trigger] [Rigidbody kinematic] [DamageHitbox team=Boss]
  └── Shield         mesh bouclier (pas de composant, pilote par BossShield.shieldTransform)

Ground               sol avec collider
Directional Light
Global Volume
```

## Systeme de camera (approche pro)

Deux modes :
- **Free (unlocked)** : la souris pilote yaw/pitch du CameraPivot. Le mouvement WASD est relatif a cameraTransform.forward. Le joueur fait face a sa direction de mouvement.
- **Lock-on** : le CameraPivot auto-oriente vers le boss. Le joueur fait face au boss. WASD = strafe/avancer/reculer relatif a l'axe joueur-boss.

Le CameraPivot est un GO separe qui suit la position du joueur mais dont la rotation est independante. Cinemachine suit passivement le pivot — zero feedback loop.

Deux PlayerInput dans la scene :
- PlayerObject : recoit OnMove, OnAttack, OnDodge, OnLockOn
- CameraPivot : recoit OnLook

## Systeme de combat

### Joueur
- Attaque verticale, cooldown 2s, animation via swordPivot rotation
- Dodge gauche/droite (Space + A/D), i-frames pendant iframeDuration
- Mouvement et input bloques pendant attaque/dodge (inputLocked)

### Boss
- Boucle : Guard (bouclier leve) -> attaque -> ending lag (fenetre de punish) -> repeat
- Bouclier bloque toutes les attaques joueur (health.guarding = true)
- Le bouclier baisse quand le boss attaque (tout le Execute() + ending lag)
- Shield Counter : si le joueur frappe le bouclier, aura inesquivable + knockback

### Attaques du boss
- **BossSwordSwing** : vertical, anime en Z. Esquivable L ou R.
- **BossDiagonalSlash** : diagonal, force dodge dans une direction specifique.
- **BossThrust** : estoc, recul + thrust en position. Esquivable L ou R.
- Toutes heritent de BossAttack (abstract). Pour ajouter une attaque custom : heriter, implementer Execute(), ajouter dans BossController.attacks.

## Input Actions

Map **Player** :
- Move (Value, Vector2) : WASD + arrows + gamepad leftStick
- Look (Value, Vector2) : mouse delta + gamepad rightStick
- Attack (Button) : LMB + gamepad West + Enter
- Dodge (Button) : Space + gamepad South
- LockOn (Button) : Tab + gamepad rightStickPress
- Sprint, Interact, Crouch, Previous, Next : existants mais pas utilises actuellement

## Integration Pluminus (a venir)

Le plugin Pluminus est installe via Package Manager (git URL). A terme :
1. Ajouter un PluminusBrain sur le joueur IA et sur le boss
2. Les Brains appellent PlayerCombat.RequestAttack()/RequestDodge() et BossController.ForceAttack()
3. Systeme de reset d'episode (cf. memoire) : UnityEvent OnEpisodeEnd sur le Brain, interface IPluminusResettable, composants PluminusResetTransform/Value, pooling projectiles
4. Synchronisation 2 Brains : reward loser -> reward winner -> EndEpisode les deux -> reset

## Conventions

- Namespace : Soulsboss.Combat
- Un script par fichier
- Pas de LINQ dans les hot paths
- Encodage UTF-8 sans BOM, pas d'accents dans les chaines affichees
- Les API publiques (RequestAttack, RequestDodge, ForceAttack) sont prevues pour etre appelees par Pluminus plus tard

## Code legacy a supprimer

- `ThirdPersonCamera.cs` : remplace par CameraController + Cinemachine

## Pieges a eviter

- Ne pas mettre CameraController sur le joueur — il doit etre sur un GO separe (CameraPivot) sinon feedback loop
- Deux PlayerInput necessaires (un sur PlayerObject, un sur CameraPivot)
- Le BossShieldCounter se branche via Health.OnBlocked, PAS dans la liste BossController.attacks
- DamageHitbox a besoin d'un Rigidbody kinematic sur le meme GO pour que OnTriggerEnter fonctionne
- CharacterController Center Y = moitie du Height, sinon le joueur est coince dans le sol
