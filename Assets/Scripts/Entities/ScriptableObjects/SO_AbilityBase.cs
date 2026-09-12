using UnityEngine;

public abstract class SO_AbilityBase : ScriptableObject
{
    [Header("Ability Base")]
    public string abilityName = "Ability";
    public float cooldown = 2f;
    public float range = 2f;
}
