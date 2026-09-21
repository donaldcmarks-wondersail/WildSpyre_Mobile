using UnityEngine;

[CreateAssetMenu(fileName = "NewChargeAbility", menuName = "WildSpyre/Player/Ability/Charge")]
public class SO_PlayerAbility_Charge : ScriptableObject
{
    [Header("Charge Timing")]
    [Tooltip("How long the button must be held before release fires the ability instead of registering as a combo tap.")]
    public float chargeTimeThreshold = 0.4f;
    public float cooldown = 1.5f;

    [Header("Aim")]
    [Tooltip("Drag magnitude below this uses the default facing direction instead of the aimed direction.")]
    public float aimDeadzone = 0.2f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float launchSpeed = 8f;
    public int damage = 2;
    [Tooltip("Optional knockback / interrupt applied to enemies the projectile hits.")]
    public HitEffects hitEffects = HitEffects.Default;

    [Header("Animation")]
    [Tooltip("Seconds after the initial touch before the charge animation starts. Keep this shorter than " +
             "chargeTimeThreshold so the wind-up plays while the charge is still building; a quick tap " +
             "released before this never starts the charge animation at all.")]
    public float chargeAnimStartDelay = 0.15f;
    [Tooltip("Fired once the hold passes chargeAnimStartDelay, while still held.")]
    public string chargeAnimTrigger = "ChargeLoop";
    [Tooltip("Fired on joystick release whenever the charge animation had started, regardless of " +
             "how long the hold was — including releases before chargeTimeThreshold that don't fire the projectile.")]
    public string releaseAnimTrigger = "ChargeRelease";
}
