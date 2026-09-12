# WildSpyre AI Enemy System

## Prerequisites
- A* Pathfinding Project (Free) imported from the Asset Store
- Player GameObject must have the tag **"Player"**
- Physics layers configured: **Ground (3)**, **PlayerCol (6)**, **EnemyCol (7)**, **EnemyDmg (9)**, **PlayerDmg (10)**

---

## Step 1 — Scene: A* Pathfinding GameObject

1. **Hierarchy → Create Empty** → name it `A* Pathfinding`
2. **Add Component → AstarPath**
3. In the Inspector, under **Graphs**, click **Add New Graph → Grid Graph**
4. Configure the Grid Graph:
   - **Width / Depth** — set to cover your level (e.g. 100 × 50)
   - **Node Size** — `0.5` (half a Unity unit, matches typical tile sizes)
   - **Collision Testing** → enable, set **Diameter** to match node size, set **Mask** to **Ground** layer
   - **Height Testing** → enable (detects walkable surfaces)
   - **2D** — enable this checkbox (critical for 2D projects)
5. Click **Scan** — the graph will bake. Green nodes = walkable, red = obstacle.
6. Click **Save & Cache** in the AstarPath Inspector to persist the baked graph.

> **Flying enemies** need all air cells walkable. Either use a separate graph with collision disabled, or add a second Grid Graph with no collision mask and use Seeker tags to route each enemy type to the correct graph.

---

## Step 2 — Scene: MNGR_EnemyManager

1. **Hierarchy → Create Empty** → name it `MNGR_EnemyManager`
2. **Add Component → MNGR_EnemyManager**
3. This is a singleton — only one is needed per scene. It auto-persists across scene loads.

---

## Step 3 — Create ScriptableObject Assets

Right-click in the **Project window → Create → WildSpyre → Enemy → ...**

Create one of each for each enemy type you want:

| Asset | Menu Path | Purpose |
|---|---|---|
| SO_EnemyProfile | Enemy / Profile | Master config — links the four below |
| SO_EnemyBehavior | Enemy / Behavior | Detection radius, patrol type, attack cooldown |
| SO_EnemyMovement | Enemy / Movement | Capability flags, speeds, jump force |
| SO_EnemyStats | Enemy / Stats | Max HP, knockback, death particles |
| SO_EnemyAbilitySet | Enemy / AbilitySet | List of abilities |
| SO_AbilityMelee | Enemy / Ability / Melee | Hitbox duration, child name |
| SO_AbilityDash | Enemy / Ability / Dash | Dash force, duration |
| SO_AbilityProjectile | Enemy / Ability / Projectile | Projectile prefab, launch speed |

### Movement Capability Flags (SO_EnemyMovement)
Set these flags to define how the enemy moves:

| Enemy Type | Flags to Set |
|---|---|
| Static Turret | `None` |
| Walking Guard | `Walk \| Jump` |
| Walking Wall-Crawler | `Walk \| Jump \| ClimbWalls` |
| Flying Scout | `Fly` |
| Ceiling Crawler | `Walk \| ClimbWalls \| ClimbCeiling` |

---

## Step 4 — Build an Enemy Prefab

### Root GameObject
1. Create an empty GameObject, name it (e.g. `P_Enemy_Guard`)
2. Set **Tag** → `Enemy`
3. Set **Layer** → `EnemyCol (7)`
4. **Add Component → Rigidbody2D**
   - Freeze Rotation Z ✅
   - Collision Detection → Continuous
5. **Add Component → CapsuleCollider2D** (or BoxCollider2D) — sized to the enemy sprite. **isTrigger = OFF**
6. **Add Component → CTRL_Enemy**
   - Assign the **SO_EnemyProfile** asset you created
   - Assign **Rigidbody2D**, **Animator**, **SpriteRenderer** references
   - Assign **Patrol Waypoints** (see Step 5)

### Sprite / Animator Child
- Add a child GameObject with **SpriteRenderer** and **Animator**
- Animator requires these triggers/bools (add to your Animator Controller):
  - `isMoving` (Bool)
  - `isStunned` (Bool)
  - `Attack` (Trigger)
  - `TakeDamage` (Trigger)
  - `Death` (Trigger)

### Melee Hitbox Child (if using Ability_Melee)
1. Add a child GameObject named exactly what you set in **SO_AbilityMelee.hitboxChildName** (default: `"MeleeHitbox"`)
2. Set **Layer** → `EnemyDmg (9)`
3. **Add Component → Collider2D** (BoxCollider2D recommended) — sized to the attack reach. **isTrigger = ON**
4. **Add Component → CTRL_EnemyDamager**
5. The child starts disabled automatically — Ability_Melee enables it during attack windows

---

## Step 5 — Patrol Waypoints

1. Create empty GameObjects in the scene at the desired patrol positions
2. Name them (e.g. `Waypoint_A`, `Waypoint_B`)
3. Drag them into the **Patrol Waypoints** array on CTRL_Enemy in the Inspector

**Patrol Types** (set in SO_EnemyBehavior):
- `Static` — never patrols, stays idle until player detected
- `LoopWaypoints` — A → B → C → A → ...
- `PingPong` — A → B → C → B → A → ...
- `RandomWaypoints` — picks a random waypoint each time

---

## Step 6 — Jump Links (Walk + Jump Enemies Only)

Jump links tell the pathfinder which platform gaps an enemy can cross by jumping.

1. At each platform **take-off edge**, create an empty GameObject
2. **Add Component → NodeLink2** (from A* Pathfinding Project)
   - Set **End** to the GameObject at the **landing** position
   - Set **Cost Factor** to `1` (increase to make AI prefer other routes)
3. **Add Component → PlatformJumpLink**
   - Set **Required Jump Force** to match the enemy's `SO_EnemyMovement.jumpForce`
4. Click **Scan** in the AstarPath Inspector to rebake the graph with the new links
5. Repeat for every jumpable gap in the level

> Tip: Group all NodeLink2 objects under an empty `JumpLinks` parent for organisation.

---

## Step 7 — Projectile Prefab (if using Ability_ThrowProjectile)

1. Create a new Prefab (e.g. `P_EnemyProjectile`)
2. Set **Layer** → `EnemyDmg (9)`
3. **Add Component → Rigidbody2D** (gravity scale = 0, set by script automatically)
4. **Add Component → CircleCollider2D** — **isTrigger = ON**
5. **Add Component → CTRL_EnemyProjectile**
   - Set **Lifetime Seconds** and optionally a **Hit Particles Prefab**
6. Assign this prefab to **SO_AbilityProjectile.projectilePrefab**

---

## Step 8 — Physics Collision Matrix

In **Project Settings → Physics 2D → Layer Collision Matrix**, ensure:

| | Ground (3) | PlayerCol (6) | EnemyCol (7) | EnemyDmg (9) | PlayerDmg (10) |
|---|---|---|---|---|---|
| **PlayerCol (6)** | ✅ | | ✅ | ✅ | |
| **EnemyCol (7)** | ✅ | ✅ | | | ✅ |
| **EnemyDmg (9)** | | ✅ | | | |
| **PlayerDmg (10)** | | | ✅ | | |

---

## Quick-Reference: Example Enemy Configs

### Static Turret
- **SO_EnemyMovement**: Capabilities = `None`
- **SO_EnemyBehavior**: PatrolType = `Static`, AttackRadius = `4`, PreferredAbility = `"Throw"`
- **SO_EnemyAbilitySet**: Add a `SO_AbilityProjectile`

### Walking Guard
- **SO_EnemyMovement**: Capabilities = `Walk | Jump`, WalkSpeed = `2`, ChaseSpeed = `4`, JumpForce = `8`
- **SO_EnemyBehavior**: PatrolType = `PingPong`, DetectionRadius = `7`, AttackRadius = `1.5`
- **SO_EnemyAbilitySet**: Add a `SO_AbilityMelee`

### Walking Wall-Crawler
- Same as Walking Guard but Capabilities = `Walk | Jump | ClimbWalls`
- ClimbSpeed = `3`

### Flying Scout
- **SO_EnemyMovement**: Capabilities = `Fly`, FlySpeed = `5`
- **SO_EnemyBehavior**: RequireLineOfSight = `false`, DetectionRadius = `10`
- **SO_EnemyAbilitySet**: Add a `SO_AbilityDash` or `SO_AbilityProjectile`

---

## How Damage Works

| Scenario | How it's handled |
|---|---|
| Player stomps enemy | `CTRL_EnemyHealth.OnTriggerEnter2D` detects PlayerDmg layer → `TakeDamage()` → `Damager.DamagerReaction()` gives player bounce-back |
| Enemy body touches player | Enemy has tag `"Enemy"` → `MNGR_PlayerLife.OnCollisionEnter2D` fires automatically |
| Enemy melee/dash hitbox hits player | Hitbox tagged `"DamagePlayer"` → `MNGR_PlayerLife.OnTriggerEnter2D` fires automatically |
| Enemy projectile hits player | Projectile tagged `"DamagePlayer"` → `MNGR_PlayerLife.OnTriggerEnter2D` fires automatically |

No code changes needed to `MNGR_PlayerLife` — the existing tag-based system handles all cases.
