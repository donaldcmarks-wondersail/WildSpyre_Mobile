using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all abilities on an enemy. Ticks cooldowns each frame and
/// exposes TryUseAbility() for the state machine to call.
/// Abilities are instantiated from SO_EnemyAbilitySet at Initialize time.
/// </summary>
public class CTRL_AbilityController : MonoBehaviour
{
    private List<IEnemyAbility> _abilities = new List<IEnemyAbility>();

    // ── Initialization ───────────────────────────────────────────────────────
    public void Initialize(SO_EnemyAbilitySet abilitySet, EnemyBlackboard board)
    {
        if (abilitySet == null) return;

        foreach (SO_AbilityBase abilitySO in abilitySet.abilities)
        {
            IEnemyAbility ability = CreateAbility(abilitySO);
            if (ability != null)
                _abilities.Add(ability);
        }
    }

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Update()
    {
        foreach (IEnemyAbility ability in _abilities)
            ability.UpdateCooldown(Time.deltaTime);
    }

    // ── Public API ───────────────────────────────────────────────────────────
    /// <summary>
    /// Attempts to execute an ability by name. If name is empty, executes the
    /// first ready ability whose range covers the current target. Returns true
    /// on success.
    /// </summary>
    public bool TryUseAbility(string abilityName, EnemyBlackboard board)
    {
        float dist = TargetDistance(board);

        if (string.IsNullOrEmpty(abilityName))
        {
            foreach (IEnemyAbility ability in _abilities)
            {
                if (ability.IsReady && dist <= EffectiveRange(ability, board))
                {
                    ability.Execute(board);
                    return true;
                }
            }
            return false;
        }

        foreach (IEnemyAbility ability in _abilities)
        {
            if (ability.AbilityName == abilityName && ability.IsReady
                && dist <= EffectiveRange(ability, board))
            {
                ability.Execute(board);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Distance at which the enemy should consider itself "in attack range" —
    /// the preferred ability's range if a preferred name is set, otherwise the
    /// longest range among all abilities. Falls back to <paramref name="fallback"/>
    /// (SO_EnemyBehavior.attackRadius) when no ability specifies a range.
    /// </summary>
    public float AttackRange(string preferredName, float fallback)
    {
        bool hasPreferred = !string.IsNullOrEmpty(preferredName);
        float best = 0f;

        foreach (IEnemyAbility ability in _abilities)
        {
            if (hasPreferred && ability.AbilityName == preferredName)
                return ability.Range > 0f ? ability.Range : fallback;

            best = Mathf.Max(best, ability.Range);
        }

        return best > 0f ? best : fallback;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static float TargetDistance(EnemyBlackboard board)
    {
        return board.target != null
            ? Vector2.Distance(board.rb.position, board.target.position)
            : Mathf.Infinity;
    }

    private static float EffectiveRange(IEnemyAbility ability, EnemyBlackboard board)
    {
        return ability.Range > 0f ? ability.Range : board.profile.behavior.attackRadius;
    }

    // ── Factory ───────────────────────────────────────────────────────────────
    private IEnemyAbility CreateAbility(SO_AbilityBase so)
    {
        if (so is SO_AbilityDash dashSO)
            return new Ability_Dash(dashSO);

        if (so is SO_AbilityProjectile projSO)
            return new Ability_ThrowProjectile(projSO);

        if (so is SO_AbilityMelee meleeSO)
            return new Ability_Melee(meleeSO);

        Debug.LogWarning($"[CTRL_AbilityController] Unknown ability SO type: {so.GetType().Name}");
        return null;
    }
}
