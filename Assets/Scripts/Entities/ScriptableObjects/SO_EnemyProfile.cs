using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyProfile", menuName = "WildSpyre/Enemy/Profile")]
public class SO_EnemyProfile : ScriptableObject
{
    [Header("Sub-Profiles")]
    [InlineSO] public SO_EnemyBehavior behavior;
    [InlineSO] public SO_EnemyMovement movement;
    [InlineSO] public SO_EnemyStats stats;
    [InlineSO] public SO_EnemyAbilitySet abilitySet;
}
