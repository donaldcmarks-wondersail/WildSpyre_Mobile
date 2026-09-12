using UnityEngine;

/// <summary>
/// Instantiates a projectile prefab and launches it toward the player.
/// Set arcedShot = true and arcAngleDeg to add a lofted arc to the trajectory.
/// The projectile prefab must have a CTRL_EnemyProjectile component.
/// </summary>
public class Ability_ThrowProjectile : IEnemyAbility
{
    private readonly SO_AbilityProjectile _config;
    private float _cooldownTimer;

    public string AbilityName => _config.abilityName;
    public bool   IsReady     => _cooldownTimer <= 0f;
    public float  Range       => _config.range;

    public Ability_ThrowProjectile(SO_AbilityProjectile config)
    {
        _config = config;
    }

    public void Execute(EnemyBlackboard board)
    {
        if (!IsReady || _config.projectilePrefab == null || board.target == null) return;

        _cooldownTimer = _config.cooldown;

        Vector2 toTarget = ((Vector2)board.target.position - board.rb.position).normalized;

        if (_config.arcedShot)
        {
            float rad = _config.arcAngleDeg * Mathf.Deg2Rad;
            toTarget = new Vector2(
                toTarget.x * Mathf.Cos(rad) - toTarget.y * Mathf.Sin(rad),
                toTarget.x * Mathf.Sin(rad) + toTarget.y * Mathf.Cos(rad)
            ).normalized;
        }

        GameObject proj = Object.Instantiate(
            _config.projectilePrefab,
            board.rb.position,
            Quaternion.identity
        );

        CTRL_EnemyProjectile projCtrl = proj.GetComponent<CTRL_EnemyProjectile>();
        if (projCtrl != null)
            projCtrl.Launch(toTarget, _config.launchSpeed);
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= deltaTime;
    }

    public void ResetCooldown() => _cooldownTimer = 0f;
}
