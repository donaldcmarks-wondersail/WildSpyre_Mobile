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

    [Header("Animation")]
    [Tooltip("Fired once hold time crosses chargeTimeThreshold, while still held.")]
    public string chargeAnimTrigger = "ChargeLoop";
    [Tooltip("Fired the instant the charge ability actually releases.")]
    public string releaseAnimTrigger = "ChargeRelease";
}
