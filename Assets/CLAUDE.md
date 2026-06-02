# Soulsboss — Unity 6

Memo d'architecture pour Claude Code. Lis ce fichier en premier avant toute modification.

## Resume du projet

Jeu de combat Souls-like en 3D. Un joueur affronte un boss. Objectif final : brancher le plugin **Pluminus** (Q-learning) pour que le boss apprenne a se battre via IA vs IA (joueur artificiel vs boss).

Le projet utilise le **New Input System**. La camera est un script custom **CameraController** (Zelda-style permanent lock-on). Cinemachine a ete entierement retire.

**Orientation des modeles** : le front visuel des deux entites est sur **X+** (pas Z+ standard Unity). Toutes les rotations LookRotation appliquent un offset de **-90 degres en Y** pour compenser.

## Arborescence des scripts

```
Assets/Scripts/Combat/
├── IDamageable.cs            interface + enum Team (Player, Boss)
│                             TakeDamage(amount, hitOrigin)
│                             TakeDamageUnblockable(amount, hitOrigin)
│                             Team, IsAlive
│
├── Health.cs                 MonoBehaviour : IDamageable
│                             HP, invincible (i-frames), guarding (bouclier)
│                             Events : OnDamaged, OnBlocked, OnDeath, OnDisabled
│                             TakeDamage() respecte invincible + guarding
│                             TakeDamageUnblockable() ignore tout
│                             A la mort : OnDeath -> attente disableDelay (1.5s) -> OnDisabled -> SetActive(false)
│                             ResetHealth() remet current=maxHp, invincible=false, guarding=false
│
├── DamageHitbox.cs           detection par OverlapCapsule (PAS OnTriggerEnter)
│                             Begin()/End() pilotes par les attaques
│                             HashSet anti-double hit par swing
│                             Capsule orientee sur transform.right, longueur + radius editables
│                             Interpolation midpoint entre frames pour attraper les mouvements rapides
│                             offset Vector3 pour decaler le centre de la capsule
│
├── HealthBarUI.cs            lie un UI Slider a un Health
│                             auto-find par tag si target non assigne
│                             met a jour slider.value chaque frame
│
├── LockOnTarget.cs           marker sur le boss, registre statique All
├── LockOnController.cs       sur le joueur, Toggle() via OnLockOn, scoring angle+distance
│                             (peu utilise — la camera est en lock permanent)
│
├── CameraController.cs       camera Zelda-style permanent lock-on
│                             Derriere le joueur, regarde un point pondere entre joueur et boss
│                             distance, height, sideOffset, lookBias editables
│                             Auto-find player/boss par tags
│                             Snap au Start, smoothing en Update
│
├── PlayerController.cs       mouvement relatif a l'axe joueur->boss
│                             toBossDir = direction vers le boss (Y=0)
│                             strafeDir = perpendiculaire a toBossDir
│                             WASD : W/S = avancer/reculer vers boss, A/D = strafe
│                             Rotation : X+ pointe vers le boss, offset +90 en Y
│                             rotationSpeed = 1440 deg/s
│                             Expose StrafeRight pour le dodge circulaire
│                             SetInputLocked(bool) pour bloquer pendant attaque/dodge
│                             SphereCast ground check avec gravite
│
├── PlayerCombat.cs           attaque verticale (swordPivot anime, cooldown 2s)
│                             dodge circulaire autour du boss (arc de cercle)
│                             RequestAttack() / RequestDodge(int dir) = API publique
│                             i-frames pendant iframeDuration au debut du dodge
│                             dodgeSpeed expose dans l'inspecteur
│                             Directions dodge : A -> dodge droite, D -> dodge gauche (INVERSES)
│                             Events : OnAttackStarted, OnDodged
│
├── BossController.cs         state machine : Idle/Guarding/Attacking/Dead
│                             boucle Guard -> pick attack -> execute -> repeat
│                             PickAttack() par poids + portee (minRange/maxRange)
│                             ForceAttack() pour debug inspector
│                             InterruptForCounter() pour le shield counter
│                             canRotate : FaceTarget() en Update sauf pendant Attacking
│                             FaceTarget() : LookRotation + offset -90 Y (modele X+ forward)
│                             turnSpeed = 720 deg/s
│
│                             MOUVEMENT vers le joueur :
│                             moveSpeed (3), preferredDistance (2.5), stopDistance (2)
│                             Avance en Guarding/Idle, s'arrete a stopDistance
│                             Utilise CharacterController si present, sinon transform.position
│
├── BossShield.cs             Raise()/Lower() anime le bouclier (position + rotation lerp)
│                             Pilote health.guarding
│                             guardLocalPos/Rot + loweredLocalPos/Rot editables
│                             shieldTransform = reference au mesh du bouclier
│
├── BossAttack.cs             abstract base : Execute(BossController) coroutine
│                             minRange, maxRange, weight pour le picker
│                             IsInRange(float distance) -> bool
│
├── BossSwordSwing.cs         attaque verticale
│                             telegraph -> raise sword -> swing down (hitbox active) -> ending lag -> return
│                             swordPivot anime en position + rotation
│                             raisedPosition/raisedRotation + strikePosition/strikeRotation editables
│
├── BossDiagonalSlash.cs      frappe diagonale
│                             windupRotation -> endRotation (Vector3 complets, 3 axes editables)
│                             deux instances (L->R, R->L) pour forcer dodge directionnel
│
├── BossThrust.cs             estoc
│                             windupPosition (-0.8, 0.5, 0) -> thrustPosition (1.2, 0.5, 0)
│                             Positions sur l'axe X (le boss fait face au joueur en X+)
│                             thrustDuration = 0.25s
│                             ATTENTION : verifier que le champ hitbox pointe vers l'epee du BOSS
│
├── BossShieldCounter.cs      declanche quand joueur frappe le bouclier (Health.OnBlocked)
│                             aura inesquivable : TakeDamageUnblockable + knockback CharacterController
│                             particules generees au runtime (charge convergente + burst explosif)
│                             chargeTime, endingLag, auraRadius, auraDamage editables
│                             N'est PAS dans la liste BossController.attacks
│
├── BossLeapAttack.cs         dash aerien vers le joueur (longue portee)
│                             Ascension : arc sinusoidal vers le haut, epee levee
│                             Descente : chute acceleree, TRAQUE LE JOUEUR a chaque frame
│                             Impact : AOE OverlapSphere (slamRadius, slamDamage)
│                             Particules d'impact generees au runtime
│                             boss.canRotate = false pendant le saut
│                             Utilise CharacterController.Move() pour les deplacements
│                             Configurer avec minRange eleve (6-8) pour ne trigger que de loin
│                             raisedPosition/slamPosition + rotations editables pour l'epee
│
└── Editor/
    └── BossControllerEditor.cs   inspector custom : boutons force attack + shield + counter
```

## Hierarchie de scene

```
Main Camera          tag MainCamera, [CameraController]

PlayerObject         tag Player
  [CharacterController] [PlayerInput] [Health team=Player]
  [PlayerController] [PlayerCombat] [LockOnController]
  ├── Player         mesh corps
  ├── Head           mesh tete (collider, pas de IDamageable)
  └── Sword          [DamageHitbox team=Player]

BossObject           tag Boss
  [CharacterController] [Health team=Boss] [BossController] [BossShield]
  [BossSwordSwing] [BossDiagonalSlash x2] [BossThrust] [BossLeapAttack]
  [BossShieldCounter] [LockOnTarget]
  ├── Boss           mesh corps
  ├── BossHead       mesh tete
  ├── BossSword      [DamageHitbox team=Boss]
  └── Shield         mesh bouclier (pilote par BossShield.shieldTransform)

Canvas
  ├── PlayerHPSlider [Slider] [HealthBarUI targetTag=Player]
  └── BossHPSlider   [Slider] [HealthBarUI targetTag=Boss]

Ground               sol avec collider
Directional Light
```

## Systeme de camera

Camera Zelda-style permanent lock-on. Un seul script **CameraController** sur la Main Camera.

- Se positionne derriere le joueur par rapport au boss
- Regarde un point pondere entre la tete du joueur et la tete du boss (lookBias)
- sideOffset pour le cadrage
- Snap au Start, smoothing en Update
- Aucun Cinemachine, zero feedback loop

## Systeme de combat

### Joueur
- Attaque verticale, cooldown 2s, animation via swordPivot rotation
- Dodge circulaire autour du boss (arc de cercle, pas ligne droite)
- i-frames pendant le debut du dodge (iframeDuration)
- Mouvement et input bloques pendant attaque/dodge (inputLocked)

### Boss
- Boucle : Guard (bouclier leve, avance vers joueur) -> attaque -> ending lag -> repeat
- Le bouclier bloque toutes les attaques joueur (health.guarding = true)
- Le bouclier baisse quand le boss attaque
- Shield Counter : si le joueur frappe le bouclier, aura inesquivable + knockback
- Progression vers le joueur en Guarding/Idle, s'arrete a stopDistance

### Attaques du boss
| Attaque | Type | Portee typique | Particularite |
|---------|------|----------------|---------------|
| BossSwordSwing | vertical | courte | esquivable L ou R |
| BossDiagonalSlash | diagonal | courte | force dodge directionnel |
| BossThrust | estoc | courte | positions sur axe X |
| BossLeapAttack | dash aerien | longue (minRange=6+) | traque pendant descente, AOE |
| BossShieldCounter | aura AOE | auto | declenche par OnBlocked, pas dans attacks list |

Pour ajouter une attaque : heriter BossAttack, implementer Execute(), ajouter dans BossController.attacks.

### Mort et desactivation
A la mort (HP <= 0) :
1. **OnDeath** se declenche immediatement
2. Attente de **disableDelay** (1.5s par defaut)
3. **OnDisabled** se declenche
4. Le GameObject est desactive (SetActive false)

OnDisabled est l'event a utiliser pour brancher la logique post-mort (reset episode, ecran victoire, etc.)

## Detection de degats (DamageHitbox)

**Pas de OnTriggerEnter**. Le systeme utilise **Physics.OverlapCapsuleNonAlloc** :
- Capsule orientee sur transform.right
- Longueur et radius editables
- Interpolation midpoint entre la frame precedente et actuelle pour couvrir les mouvements rapides
- HashSet empeche les double-hits dans un meme swing
- Filtrage par Team, IsAlive, et anti-doublon

## Input Actions

Map **Player** :
- Move (Value, Vector2) : WASD + arrows + gamepad leftStick
- Look (Value, Vector2) : mouse delta + gamepad rightStick
- Attack (Button) : LMB + gamepad West + Enter
- Dodge (Button) : Space + gamepad South
- LockOn (Button) : Tab + gamepad rightStickPress

## Integration Pluminus (a venir)

Le plugin Pluminus est installe via Package Manager (git URL). A terme :
1. Ajouter un PluminusBrain sur le joueur IA et sur le boss
2. Les Brains appellent PlayerCombat.RequestAttack()/RequestDodge() et BossController.ForceAttack()
3. Systeme de reset d'episode (cf. memoire) : UnityEvent OnEpisodeEnd, interface IPluminusResettable
4. Utiliser Health.OnDisabled pour detecter la fin d'un episode
5. Synchronisation 2 Brains : reward loser -> reward winner -> EndEpisode les deux -> reset

## Conventions

- Namespace : Soulsboss.Combat
- Un script par fichier
- Pas de LINQ dans les hot paths
- Encodage UTF-8 sans BOM, pas d'accents dans les chaines affichees
- Modeles orientes X+ forward (offset -90 Y dans LookRotation)
- Les API publiques (RequestAttack, RequestDodge, ForceAttack) sont prevues pour Pluminus

## Pieges a eviter

- **Orientation X+** : tous les LookRotation utilisent `* Quaternion.Euler(0, -90, 0)` (boss) ou `* Quaternion.Euler(0, 90, 0)` (joueur) pour compenser
- **DamageHitbox** : Ne PAS utiliser OnTriggerEnter, le systeme est base sur OverlapCapsule
- **BossThrust hitbox** : verifier que le champ hitbox du BossThrust pointe vers BossSword (team=Boss), PAS Sword (team=Player). Erreur deja commise une fois
- **BossShieldCounter** : se branche via Health.OnBlocked, PAS dans la liste BossController.attacks
- **BossLeapAttack** : configurer minRange eleve (6+) sinon le boss saute meme au corps-a-corps
- **CharacterController Center Y** = moitie du Height, sinon le personnage est coince dans le sol
- **Camera** : CameraController est sur Main Camera directement, pas sur un pivot separe
- **L'ancien LevelGenerator / ThirdPersonCamera** : supprimes, ne pas recreer
