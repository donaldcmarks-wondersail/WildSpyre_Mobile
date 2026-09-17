using UnityEngine;

[CreateAssetMenu(fileName = "NewDashAbility", menuName = "WildSpyre/Enemy/Ability/Dash")]
public class SO_AbilityDash : SO_AbilityBase
{
    [Header("Dash Settings")]
    public float dashDistance = 2.4f;
    public float dashDuration = 0.2f;
    public bool invulnerableDuringDash = true;
    public GameObject dashParticlesPrefab;

    [Header("Dash Easing")]
    [Tooltip("Evaluated over [0,1] across dashDuration; scales dashDistance per axis, same as the " +
             "anticipation curves scale anticipationDistance. Flat at 1 = constant speed to the full distance.")]
    public AnimationCurve dashCurveX = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    public AnimationCurve dashCurveY = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    // Anticipation is inherited from SO_AbilityBase (shared across all ability types).
    // Deliberately inherits SO_AbilityBase directly, not SO_AbilityBaseMovable — the dash
    // has its own dedicated movement (dashDistance/dashDuration/dashCurveX/Y above), so the
    // generic Ability Movement option is left off this asset entirely rather than sitting
    // there unused.
}
