using UnityEngine;

/// <summary>
/// Pulls this pickup toward the player once they're within Magnetize Radius. Kept as its own
/// component (rather than fields baked into Pickup) so it can be flipped on/off at runtime per
/// instance — e.g. a player "magnet" ability enabling magnetize on pickups that don't normally
/// have it — without touching the pickup's idle IPickupMotion at all.
///
/// While active and in range it drives the transform directly; Pickup.Update checks
/// IsMagnetizing and skips its own motion tick that frame so the two never fight over position.
/// Optional — a Pickup with no PickupMagnetize on it just never magnetizes.
/// </summary>
public class PickupMagnetize : MonoBehaviour
{
    [Tooltip("Checked between the pickup and the player when Ignore Walls For Magnetize (on the " +
             "SO_PickupData) is off, to block pulling a pickup through a wall.")]
    [SerializeField] private LayerMask _wallLayerMask;
    [Tooltip("The wall linecast starts this far above the pickup's own position instead of " +
             "exactly on it, so a pickup resting on Ground doesn't false-positive against the " +
             "floor it's sitting on. Raise this if pickups still fail to magnetize while grounded; " +
             "lower it if a real wall right above a pickup stops registering.")]
    [SerializeField] private float _linecastSkin = 0.15f;

    private SO_PickupData _data;
    private Transform _player;

    public bool IsMagnetizing { get; private set; }

    /// <summary>Runtime on/off switch, independent of SO_PickupData.magnetizeToPlayer (which only
    /// sets the starting value — see Init).</summary>
    public bool Enabled { get; set; }

    /// <summary>Called by Pickup on every activation (including pool reuse) so state matches the
    /// data asset's default each time, before anything else toggles it at runtime.</summary>
    public void Init(SO_PickupData data)
    {
        _data = data;
        Enabled = data != null && data.magnetizeToPlayer;
        IsMagnetizing = false;

        if (_player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                _player = playerObj.transform;
        }
    }

    private void Update()
    {
        if (!Enabled || _data == null || _player == null)
        {
            IsMagnetizing = false;
            return;
        }

        float sqrDist = ((Vector2)(_player.position - transform.position)).sqrMagnitude;
        if (sqrDist > _data.magnetizeRadius * _data.magnetizeRadius)
        {
            IsMagnetizing = false;
            return;
        }

        if (!_data.ignoreWallsForMagnetize)
        {
            Vector2 castStart = (Vector2)transform.position + Vector2.up * _linecastSkin;
            if (Physics2D.Linecast(castStart, _player.position, _wallLayerMask))
            {
                IsMagnetizing = false;
                return;
            }
        }

        IsMagnetizing = true;
        transform.position = Vector2.MoveTowards(transform.position, _player.position,
            _data.magnetizePower * Time.deltaTime);
    }
}
