using UnityEngine;

[CreateAssetMenu(fileName = "NewMeleeAbility", menuName = "WildSpyre/Enemy/Ability/Melee")]
public class SO_AbilityMelee : SO_AbilityBase
{
    [Header("Melee Settings")]
    public float hitboxActiveDuration = 0.3f;
    public string hitboxChildName = "MeleeHitbox";
    public GameObject hitParticlesPrefab;
}
