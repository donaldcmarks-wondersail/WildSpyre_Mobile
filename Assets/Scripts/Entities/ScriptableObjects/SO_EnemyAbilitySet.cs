using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyAbilitySet", menuName = "WildSpyre/Enemy/AbilitySet")]
public class SO_EnemyAbilitySet : ScriptableObject
{
    [Header("Abilities")]
    [InlineSO] public List<SO_AbilityBase> abilities = new List<SO_AbilityBase>();
}
