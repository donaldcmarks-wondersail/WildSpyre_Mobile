using System.Collections;
using UnityEngine;

/// <summary>
/// Activates a child hitbox GameObject for a brief window.
/// The hitbox must have a CTRL_EnemyDamager component and be tagged "DamagePlayer".
/// Uses board.owner (the CTRL_Enemy MonoBehaviour) to run the enable/disable coroutine.
/// </summary>
public class Ability_Melee : IEnemyAbility
{
    private readonly SO_AbilityMelee _config;
    private float _cooldownTimer;

    public string AbilityName => _config.abilityName;
    public bool   IsReady     => _cooldownTimer <= 0f;
    public float  Range       => _config.range;

    public Ability_Melee(SO_AbilityMelee config)
    {
        _config = config;
    }

    public void Execute(EnemyBlackboard board)
    {
        if (!IsReady || board.owner == null) return;

        _cooldownTimer = _config.cooldown;
        board.anim?.SetTrigger("Attack");
        board.owner.StartCoroutine(MeleeHitboxCO(board));
    }

    private IEnumerator MeleeHitboxCO(EnemyBlackboard board)
    {
        // Find the hitbox child by name
        Transform hitboxTransform = board.owner.transform.Find(_config.hitboxChildName);
        CTRL_EnemyDamager hitbox = hitboxTransform?.GetComponent<CTRL_EnemyDamager>();

        hitbox?.SetActive(true);

        float startTime = Time.time;
        while (Time.time < startTime + _config.hitboxActiveDuration)
            yield return null;

        hitbox?.SetActive(false);

        if (_config.hitParticlesPrefab != null)
            Object.Instantiate(_config.hitParticlesPrefab, board.rb.position, Quaternion.identity);
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= deltaTime;
    }

    public void ResetCooldown() => _cooldownTimer = 0f;
}
