using UnityEngine;

/// <summary>
/// SO_AbilityBase plus "Ability Movement" — optional motion during the ability's own
/// active window (e.g. a melee enemy stepping toward the target as it swings). Split out
/// from SO_AbilityBase rather than living there, because it doesn't apply to every ability:
/// Ability_Dash has its own dedicated movement (dashDistance/dashDuration/dashCurveX/Y) and
/// deliberately doesn't use this, so SO_AbilityDash inherits SO_AbilityBase directly instead
/// of this class, to keep an inert, do-nothing option off the Dash asset in the Inspector.
/// </summary>
public abstract class SO_AbilityBaseMovable : SO_AbilityBase
{
    [Header("Ability Movement (optional motion during the ability's own active window)")]
    [Tooltip("E.g. a melee enemy stepping slightly toward the target as it swings. Runs alongside the " +
             "ability's own effect (concurrently), not before it — anticipation happens first, this doesn't.")]
    public bool useAbilityMovement = false;
    public float abilityMovementDistance = 0.3f;
    public float abilityMovementDuration = 0.2f;
    [Tooltip("Evaluated over [0,1] across abilityMovementDuration; scales abilityMovementDistance per axis.")]
    public AnimationCurve abilityMovementCurveX = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    public AnimationCurve abilityMovementCurveY = AnimationCurve.Linear(0f, 0f, 1f, 1f);
}
