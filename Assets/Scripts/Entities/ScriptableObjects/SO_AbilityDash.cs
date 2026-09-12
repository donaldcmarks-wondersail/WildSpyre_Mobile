using UnityEngine;

[CreateAssetMenu(fileName = "NewDashAbility", menuName = "WildSpyre/Enemy/Ability/Dash")]
public class SO_AbilityDash : SO_AbilityBase
{
    [Header("Dash Settings")]
    public float dashForce = 12f;
    public float dashDuration = 0.2f;
    public bool invulnerableDuringDash = true;
    public GameObject dashParticlesPrefab;
}
