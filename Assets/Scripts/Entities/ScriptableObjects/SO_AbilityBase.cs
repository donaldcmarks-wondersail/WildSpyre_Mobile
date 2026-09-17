using UnityEngine;

public abstract class SO_AbilityBase : ScriptableObject
{
    [Header("Ability Base")]
    public string abilityName = "Ability";
    public float cooldown = 2f;
    public float range = 2f;

    [Header("Anticipation (optional wind-up pullback)")]
    [Tooltip("Pull back opposite the ability's direction before it fires.")]
    public bool useAnticipation = false;
    public float anticipationDistance = 0.5f;
    public float anticipationDuration = 0.15f;
    [Tooltip("Evaluated over [0,1] across anticipationDuration; scales anticipationDistance per axis.")]
    public AnimationCurve anticipationCurveX = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve anticipationCurveY = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Animator trigger fired the instant the anticipation pullback begins. Leave empty for none.")]
    public string anticipationTrigger;
}
