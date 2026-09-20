using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerAbilitySet", menuName = "WildSpyre/Player/Ability/Ability Set")]
public class SO_PlayerAbilitySet : ScriptableObject
{
    public SO_PlayerAbility_Combo comboAbility;
    public SO_PlayerAbility_Charge chargeAbility;
    public SO_PlayerAbility_Aim aimAbility;
}
