using System.Collections;
using UnityEngine;

/// <summary>
/// Drives the player's data-driven ability kit (a 3-hit tap combo + a hold-to-charge
/// aimed projectile), fed by INPT_AbilityJoystick. Entirely separate from
/// CTRL_PlayerPlatformer's movement/jump/sling — only ever reads its public state
/// (PlayerState, Flipped, rb, animatorChar), never calls into its movement methods.
///
/// Tap vs. hold is decided by INPT_AbilityJoystick comparing held duration against
/// ChargeTimeThreshold at release time; this controller just executes whichever one
/// it's told happened.
/// </summary>
public class CTRL_PlayerAbilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;
    [SerializeField] private SO_PlayerAbilitySet _abilitySet;

    [Header("Combo Hitbox")]
    [Tooltip("Child hitbox with a PlayerAbilityDamager — place it under the character art " +
             "so it mirrors automatically with Flipped. Leave it disabled (Start Active off) " +
             "in the scene; this script enables it only for each hit's active window.")]
    [SerializeField] private PlayerAbilityDamager _comboHitbox;

    [Header("Charge Aim Reticle")]
    [Tooltip("Object whose rotation follows the charge joystick's aim while it's held — its local +X " +
             "axis points where the projectile will launch. Put the reticle sprite as a child of this " +
             "object, and keep it out of the Flipped-scaled art node so the flip doesn't mirror the rotation.")]
    [SerializeField] private Transform _aimReticle;

    [Header("Projectile Impact FX")]
    [Tooltip("Handed to every projectile this controller spawns; only projectiles with Spawn Fire " +
             "Trail On Impact enabled on their prefab actually use it. Auto-found on this GameObject " +
             "(the player root, where FireTrailSlingEmitter lives) if left empty.")]
    [SerializeField] private FireTrailSlingEmitter _fireTrailStamper;

    private int _comboIndex;
    private float _comboWindowTimer;
    private float _comboRecoveryTimer;

    private float _chargeCooldownTimer;
    private float _aimCooldownTimer;
    private bool _chargeLoopFired;
    private bool _aimAnimActive;

    /// <summary>Held duration at/above this is a charge release, not a combo tap. Used by INPT_AbilityJoystick.</summary>
    public float ChargeTimeThreshold => _abilitySet != null && _abilitySet.chargeAbility != null
        ? _abilitySet.chargeAbility.chargeTimeThreshold
        : float.MaxValue;

    /// <summary>
    /// Joystick drag magnitude at/above which a quick (sub-charge) release is an aimed release
    /// rather than a melee tap. float.MaxValue when no aim ability is equipped, so the joystick
    /// falls back to plain taps. Used by INPT_AbilityJoystick.
    /// </summary>
    public float AimThreshold => _abilitySet != null && _abilitySet.aimAbility != null
        ? _abilitySet.aimAbility.aimThreshold
        : float.MaxValue;

    private void Awake()
    {
        if (_fireTrailStamper == null)
            _fireTrailStamper = GetComponent<FireTrailSlingEmitter>();
    }

    private void Update()
    {
        if (_comboWindowTimer > 0f)
        {
            _comboWindowTimer -= Time.deltaTime;
            if (_comboWindowTimer <= 0f)
                _comboIndex = 0; // dropped the combo — took too long between taps
        }

        if (_comboRecoveryTimer > 0f)
            _comboRecoveryTimer -= Time.deltaTime;

        if (_chargeCooldownTimer > 0f)
            _chargeCooldownTimer -= Time.deltaTime;

        if (_aimCooldownTimer > 0f)
            _aimCooldownTimer -= Time.deltaTime;
    }

    private bool AbilitiesAllowed()
    {
        return _playerCtrl != null
            && _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer;
    }

    // ── Combo ────────────────────────────────────────────────────────────────
    /// <summary>True while the lockout after the final combo hit is running.</summary>
    private bool ComboRecoveryActive => _comboRecoveryTimer > 0f;

    public void RegisterTap()
    {
        // Combo recovery: no new animation triggers of any kind while it runs — that includes the
        // charge-release trigger EndChargeAnim would otherwise fire below, not just the combo hit's
        // own. OnHoldTick holds the charge-start trigger back during recovery as well, so there's
        // never a charge animation left running here to release; just clear the state silently.
        if (ComboRecoveryActive)
        {
            _chargeLoopFired = false;
            SetAimAnim(false);
            return;
        }

        // A release between chargeAnimStartDelay and chargeTimeThreshold lands here as a tap
        // with the charge animation already started — end it (fires the release trigger,
        // clears the flag) before the combo hit.
        EndChargeAnim();

        if (!AbilitiesAllowed()) return;
        if (_abilitySet == null || _abilitySet.comboAbility == null) return;

        SO_PlayerAbility_Combo combo = _abilitySet.comboAbility;
        if (combo.hits == null || _comboIndex >= combo.hits.Length) return;

        SO_PlayerAbility_Combo.ComboHit hit = combo.hits[_comboIndex];
        if (!string.IsNullOrEmpty(hit.animTrigger))
            _playerCtrl.animatorChar?.SetTrigger(hit.animTrigger);

        StartCoroutine(ComboHitCO(hit));

        _comboIndex++;
        if (_comboIndex >= combo.hits.Length)
        {
            _comboIndex = 0;
            _comboWindowTimer = 0f;
            _comboRecoveryTimer = combo.comboRecoveryTime;
        }
        else
        {
            _comboWindowTimer = combo.comboWindowDuration;
        }
    }

    private IEnumerator ComboHitCO(SO_PlayerAbility_Combo.ComboHit hit)
    {
        if (_comboHitbox == null) yield break;

        _comboHitbox.damage = hit.damage;
        _comboHitbox.SetActive(true);

        float startTime = Time.time;
        while (Time.time < startTime + hit.hitboxActiveDuration)
            yield return null;

        _comboHitbox.SetActive(false);
    }

    // ── Charge ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Where a charge release would launch: the joystick's drag direction, or the facing
    /// direction (Flipped) while the drag is inside the aim deadzone. Shared by the aim
    /// reticle and the actual launch so the reticle always shows where the shot will go.
    /// </summary>
    private Vector2 ResolveAimDirection(SO_PlayerAbility_Charge charge, Vector2 aimDir)
    {
        if (aimDir.magnitude >= charge.aimDeadzone)
            return aimDir.normalized;

        bool facingLeft = _playerCtrl != null && _playerCtrl.Flipped;
        return facingLeft ? Vector2.left : Vector2.right;
    }

    private void UpdateAimReticle(Vector2 aimDir)
    {
        if (_aimReticle == null || _abilitySet == null || _abilitySet.chargeAbility == null) return;

        Vector2 dir = ResolveAimDirection(_abilitySet.chargeAbility, aimDir);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        _aimReticle.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>Called every frame by INPT_AbilityJoystick while the button is held.</summary>
    public void OnHoldTick(float heldDuration, Vector2 aimDir)
    {
        UpdateAimReticle(aimDir);
        UpdateAimAnim(aimDir);

        if (_chargeLoopFired) return;

        // No charge-start trigger while combo recovery runs. Not latched — if the hold outlasts
        // the recovery, this passes on a later frame and the charge animation starts then.
        if (ComboRecoveryActive) return;
        if (_abilitySet == null || _abilitySet.chargeAbility == null) return;
        if (heldDuration < _abilitySet.chargeAbility.chargeAnimStartDelay) return;
        if (!AbilitiesAllowed()) return;

        _chargeLoopFired = true;
        if (!string.IsNullOrEmpty(_abilitySet.chargeAbility.chargeAnimTrigger))
            _playerCtrl.animatorChar?.SetTrigger(_abilitySet.chargeAbility.chargeAnimTrigger);
    }

    /// <summary>
    /// Drives the Aim animator bool live: true while the joystick drag is at/past the aim
    /// threshold, false otherwise. Only touches the Animator when the value changes.
    /// </summary>
    private void UpdateAimAnim(Vector2 aimDir)
    {
        SetAimAnim(AbilitiesAllowed() && aimDir.magnitude >= AimThreshold);
    }

    private void SetAimAnim(bool active)
    {
        if (_aimAnimActive == active) return;
        _aimAnimActive = active;

        SO_PlayerAbility_Aim aim = _abilitySet != null ? _abilitySet.aimAbility : null;
        if (aim != null && _playerCtrl != null && !string.IsNullOrEmpty(aim.aimAnimBool))
            _playerCtrl.animatorChar?.SetBool(aim.aimAnimBool, active);
    }

    /// <summary>
    /// Ends the hold animations on any joystick release: clears the Aim bool, and fires the
    /// charge release trigger whenever the charge animation had started, regardless of whether
    /// the hold reached chargeTimeThreshold (so an early release still exits the charge pose),
    /// then clears the flag.
    /// </summary>
    private void EndChargeAnim()
    {
        SetAimAnim(false);

        bool wasCharging = _chargeLoopFired;
        _chargeLoopFired = false;
        if (!wasCharging) return;

        SO_PlayerAbility_Charge charge = _abilitySet != null ? _abilitySet.chargeAbility : null;
        if (charge != null && _playerCtrl != null && !string.IsNullOrEmpty(charge.releaseAnimTrigger))
            _playerCtrl.animatorChar?.SetTrigger(charge.releaseAnimTrigger);
    }

    public void OnChargeReleased(float heldDuration, Vector2 aimDir)
    {
        EndChargeAnim();

        if (!AbilitiesAllowed()) return;
        if (_abilitySet == null || _abilitySet.chargeAbility == null) return;

        SO_PlayerAbility_Charge charge = _abilitySet.chargeAbility;
        if (heldDuration < charge.chargeTimeThreshold) return;
        if (_chargeCooldownTimer > 0f) return;
        if (charge.projectilePrefab == null) return;

        Vector2 launchDir = ResolveAimDirection(charge, aimDir);

        SpawnProjectile(charge.projectilePrefab, launchDir, charge.launchSpeed, charge.damage);

        _chargeCooldownTimer = charge.cooldown;
    }

    // ── Aim ──────────────────────────────────────────────────────────────────
    /// <summary>
    /// Quick touch-aim-release: the joystick was released before the charge threshold with a drag
    /// past AimThreshold. Fires the aim ability along the drag direction.
    /// </summary>
    public void OnAimReleased(Vector2 aimDir)
    {
        // The charge animation can already have started (past chargeAnimStartDelay) — end it.
        EndChargeAnim();

        if (!AbilitiesAllowed()) return;
        if (_abilitySet == null || _abilitySet.aimAbility == null) return;

        SO_PlayerAbility_Aim aim = _abilitySet.aimAbility;
        if (_aimCooldownTimer > 0f) return;
        if (aim.projectilePrefab == null) return;

        SpawnProjectile(aim.projectilePrefab, aimDir.normalized, aim.launchSpeed, aim.damage);

        _aimCooldownTimer = aim.cooldown;
    }

    private void SpawnProjectile(GameObject prefab, Vector2 direction, float speed, int damage)
    {
        GameObject proj = Instantiate(prefab, _playerCtrl.rb.position, Quaternion.identity);
        PlayerProjectile projCtrl = proj.GetComponent<PlayerProjectile>();
        if (projCtrl != null)
            projCtrl.Launch(direction, speed, damage, _fireTrailStamper);
    }

    /// <summary>Called by INPT_AbilityJoystick if a press is abandoned (e.g. dragged off the control).</summary>
    public void OnHoldCancelled()
    {
        EndChargeAnim();
    }
}
