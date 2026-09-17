using UnityEngine;

[CreateAssetMenu(fileName = "NewComboAbility", menuName = "WildSpyre/Player/Ability/Combo")]
public class SO_PlayerAbility_Combo : ScriptableObject
{
    [System.Serializable]
    public class ComboHit
    {
        public string animTrigger = "Combo1";
        public int damage = 1;
        public float hitboxActiveDuration = 0.15f;
    }

    [Header("Combo Hits (in order — index 0 fires on the first tap)")]
    public ComboHit[] hits = new ComboHit[3];

    [Header("Timing")]
    [Tooltip("Max time allowed between taps before the combo drops back to hit 1.")]
    public float comboWindowDuration = 0.5f;
    [Tooltip("Lockout after the final hit before a new combo sequence can begin.")]
    public float comboRecoveryTime = 0.2f;
}
