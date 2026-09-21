using System.Collections;
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
    public SO_AbilityBase Config => _config;

    public Ability_ThrowProjectile(SO_AbilityProjectile config)
    {
        _config = config;
    }

    public void Execute(EnemyBlackboard board)
    {
        if (!IsReady || _config.projectilePrefab == null || board.target == null || board.owner == null) return;

        _cooldownTimer = _config.cooldown;

        Vector2 toTarget = ((Vector2)board.target.position - board.rb.position).normalized;

        board.owner.StartCoroutine(ThrowSequenceCO(board, toTarget));
    }

    private IEnumerator ThrowSequenceCO(EnemyBlackboard board, Vector2 toTarget)
    {
        if (_config.useAnticipation)
            yield return AnticipationCO(board, toTarget);

        if (_config.useAbilityMovement)
            board.owner.StartCoroutine(AbilityMotion.MoveCO(board, toTarget, _config.abilityMovementDistance,
                _config.abilityMovementDuration, _config.abilityMovementCurveX, _config.abilityMovementCurveY));

        Vector2 launchDir = toTarget;
        if (_config.arcedShot)
        {
            float rad = _config.arcAngleDeg * Mathf.Deg2Rad;
            launchDir = new Vector2(
                launchDir.x * Mathf.Cos(rad) - launchDir.y * Mathf.Sin(rad),
                launchDir.x * Mathf.Sin(rad) + launchDir.y * Mathf.Cos(rad)
            ).normalized;
        }

        GameObject proj = Object.Instantiate(
            _config.projectilePrefab,
            board.rb.position,
            Quaternion.identity
        );

        CTRL_EnemyProjectile projCtrl = proj.GetComponent<CTRL_EnemyProjectile>();
        if (projCtrl != null)
            projCtrl.Launch(launchDir, _config.launchSpeed);
    }

    private IEnumerator AnticipationCO(EnemyBlackboard board, Vector2 dir)
    {
        if (!string.IsNullOrEmpty(_config.anticipationTrigger))
            board.anim?.SetTrigger(_config.anticipationTrigger);

        if (_config.anticipationDistance > 0f)
        {
            Vector2 antDir = -dir;
            yield return AbilityMotion.MoveCO(board, antDir, _config.anticipationDistance,
                _config.anticipationDuration, _config.anticipationCurveX, _config.anticipationCurveY);
        }
        else
        {
            yield return AbilityMotion.WaitCO(_config.anticipationDuration);
        }
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= deltaTime;
    }

    public void ResetCooldown() => _cooldownTimer = 0f;
}
