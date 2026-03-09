using UnityEngine;

public class PickupCoin : Pickup
{
    [Header("Coin Pickup Variables")]
    [SerializeField] private int _coinAmount;
    
    protected override void Start()
    {
        base.Start(); // Call the base class implementation (optional)
    }

    protected override void Update()
    {
        base.Update(); // Call the base class implementation (optional)
    }
}
