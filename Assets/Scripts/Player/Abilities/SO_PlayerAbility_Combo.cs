using UnityEngine;

[CreateAssetMenu(fileName = "NewComboAbility", menuName = "WildSpyre/Player/Ability/Combo")]
public class SO_PlayerAbility_Combo : ScriptableObject
{
    [System.Serializable]
    public class ComboHit
    {
        public string animTrigger = "Combo1";
        public int damage = 1;
        [Tooltip("Optional knockback / interrupt for this hit — e.g. only the finisher knocks back.")]
        public HitEffects hitEffects = HitEffects.Default;
    }

    [Header("Combo Hits (in order — index 0 fires on the first tap)")]
    public ComboHit[] hits = new ComboHit[3];

    [Header("Movement Assist (each combo hit)")]
    [Tooltip("Seconds each combo hit eases the player's fall while sliding down a wall or airborne. " +
             "0 turns the whole assist off.")]
    public float assistDuration = 0.35f;
    [Tooltip("Wall slide speed while a hit plays, as a fraction of the normal wall slide speed. " +
             "1 = no change, 0.25 = a quarter as fast, 0 = holds in place.")]
    [Range(0f, 1f)] public float wallSlideSpeedMultiplier = 0.25f;
    [Tooltip("Airborne gravity while a hit plays, as a fraction of normal. 1 = no change, 0 = none.")]
    [Range(0f, 1f)] public float airGravityMultiplier = 0.2f;
    [Tooltip("Airborne fall speed never exceeds this while a hit plays. 0 hovers; above 0 gives a slight lift " +
             "(only if the player is falling — a rising jump is left alone); a negative value just caps the fall.")]
    public float airMinVerticalVelocity = 0.5f;
    [Tooltip("One-off upward velocity given as an airborne hit starts — a small hop so the player can land the " +
             "combo mid-air. Only applies if the player is moving upward slower than this (a rising jump is " +
             "left alone). 0 = no hop.")]
    public float airUpwardVelocity = 2f;
    [Tooltip("Extra horizontal air damping per 0.1s while an airborne hit plays, on top of the normal air " +
             "damping. 0 = none; 0.8 = the run speed is almost gone within ~0.2s; 1 = stops dead.")]
    [Range(0f, 1f)] public float airHorizontalDamping = 0.8f;
    [Tooltip("How much of the player's left/right steering still applies in the air while an airborne hit plays. " +
             "1 = full control, 0 = none.")]
    [Range(0f, 1f)] public float airMoveInputMultiplier = 0.25f;
    [Tooltip("How many combo hits get the AIR assist per trip off the ground (resets on landing or grabbing a " +
             "wall), so a player can't hover forever by chaining combos. The wall assist has no limit. " +
             "Set to the number of hits in the combo to allow one full combo mid-air.")]
    public int maxAirAssistHits = 3;

    [Header("Timing")]
    [Tooltip("Max time allowed between taps before the combo drops back to hit 1.")]
    public float comboWindowDuration = 0.5f;
    [Tooltip("Lockout after the final hit before a new combo sequence can begin.")]
    public float comboRecoveryTime = 0.2f;
}
