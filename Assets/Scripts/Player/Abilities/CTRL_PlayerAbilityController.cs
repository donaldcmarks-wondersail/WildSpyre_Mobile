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

    private int _comboIndex;
    private float _comboWindowTimer;
    private float _comboRecoveryTimer;

    private float _chargeCooldownTimer;
    private bool _chargeLoopFired;

    /// <summary>Held duration at/above this is a charge release, not a combo tap. Used by INPT_AbilityJoystick.</summary>
    public float ChargeTimeThreshold => _abilitySet != null && _abilitySet.chargeAbility != null
        ? _abilitySet.chargeAbility.chargeTimeThreshold
        : float.MaxValue;

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
    }

    private bool AbilitiesAllowed()
    {
        return _playerCtrl != null
            && _playerCtrl.PlayerState == CTRL_PlayerPlatformer.playerControlState.Platformer;
    }

    // ── Combo ────────────────────────────────────────────────────────────────
    public void RegisterTap()
    {
        if (!AbilitiesAllowed()) return;
        if (_abilitySet == null || _abilitySet.comboAbility == null) return;
        if (_comboRecoveryTimer > 0f) return;

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
    /// <summary>Called every frame by INPT_AbilityJoystick while the button is held.</summary>
    public void OnHoldTick(float heldDuration)
    {
        if (_chargeLoopFired) return;
        if (_abilitySet == null || _abilitySet.chargeAbility == null) return;
        if (heldDuration < _abilitySet.chargeAbility.chargeTimeThreshold) return;
        if (!AbilitiesAllowed()) return;

        _chargeLoopFired = true;
        if (!string.IsNullOrEmpty(_abilitySet.chargeAbility.chargeAnimTrigger))
            _playerCtrl.animatorChar?.SetTrigger(_abilitySet.chargeAbility.chargeAnimTrigger);
    }

    public void OnChargeReleased(float heldDuration, Vector2 aimDir)
    {
        _chargeLoopFired = false;

        if (!AbilitiesAllowed()) return;
        if (_abilitySet == null || _abilitySet.chargeAbility == null) return;

        SO_PlayerAbility_Charge charge = _abilitySet.chargeAbility;
        if (heldDuration < charge.chargeTimeThreshold) return;
        if (_chargeCooldownTimer > 0f) return;
        if (charge.projectilePrefab == null) return;

        // Default aim ties to facing (Flipped) when the drag is inside the deadzone —
        // decided in this turn's plan, not left ambiguous.
        Vector2 launchDir = aimDir.magnitude >= charge.aimDeadzone
            ? aimDir.normalized
            : (_playerCtrl.Flipped ? Vector2.left : Vector2.right);

        if (!string.IsNullOrEmpty(charge.releaseAnimTrigger))
            _playerCtrl.animatorChar?.SetTrigger(charge.releaseAnimTrigger);

        GameObject proj = Instantiate(charge.projectilePrefab, _playerCtrl.rb.position, Quaternion.identity);
        PlayerProjectile projCtrl = proj.GetComponent<PlayerProjectile>();
        if (projCtrl != null)
            projCtrl.Launch(launchDir, charge.launchSpeed, charge.damage);

        _chargeCooldownTimer = charge.cooldown;
    }

    /// <summary>Called by INPT_AbilityJoystick if a press is abandoned (e.g. dragged off the control).</summary>
    public void OnHoldCancelled()
    {
        _chargeLoopFired = false;
    }
}
