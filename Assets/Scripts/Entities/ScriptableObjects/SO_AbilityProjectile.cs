using UnityEngine;

[CreateAssetMenu(fileName = "NewProjectileAbility", menuName = "WildSpyre/Enemy/Ability/Projectile")]
public class SO_AbilityProjectile : SO_AbilityBase
{
    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public float launchSpeed = 8f;
    public bool arcedShot = false;
    public float arcAngleDeg = 30f;
}
