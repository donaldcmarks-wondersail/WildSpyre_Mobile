using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Quick touch-aim-release ability. Fires when the Ability Joystick is released with a drag
/// past aimThreshold, before the hold reaches the charge ability's chargeTimeThreshold
/// (a longer hold always becomes the charge ability; an un-aimed release is the melee combo).
/// </summary>
[CreateAssetMenu(fileName = "NewAimAbility", menuName = "WildSpyre/Player/Ability/Aim")]
public class SO_PlayerAbility_Aim : ScriptableObject
{
    [Header("Aim Timing")]
    [Tooltip("Joystick drag magnitude (0-1) at release needed to count as an aimed release. Below this, " +
             "a quick release is a normal melee tap instead.")]
    [Range(0f, 1f)] public float aimThreshold = 0.4f;
    public float cooldown = 0.5f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float launchSpeed = 10f;
    public int damage = 1;
    [Tooltip("Optional knockback / interrupt applied to enemies the projectile hits.")]
    public HitEffects hitEffects = HitEffects.Default;

    [Header("Animation")]
    [Tooltip("Animator Bool parameter driven live while the joystick is held: true while the drag is at/past " +
             "aimThreshold, false otherwise (and always false once released). Needs a matching Bool " +
             "parameter on the player Animator.")]
    [FormerlySerializedAs("aimAnimTrigger")]
    public string aimAnimBool = "Aim";
}
