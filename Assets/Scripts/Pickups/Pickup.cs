using System;
using UnityEngine;

public class Pickup : MonoBehaviour
{
    [Header("Core Pickup Variables")]
    [SerializeField] private bool _magnetizedToPlayer = false;
    [SerializeField] private float _magnetizePower = 0.5f;
    [SerializeField] private bool _ignoreWallsForMagnetize = false;
    [SerializeField] private float _lifetime = 5.0f;

    [Header("FX Variables")]
    [SerializeField] private ParticleSystem _spawnFX;
    [SerializeField] private ParticleSystem _pickupFX;
    
    [Header("Behavior Variables")]
    [SerializeField] private PickupBehaviorType _pickupBehavior;
    [Header("Movement")]
    [SerializeField] private float _movementSpeed = 5.0f;
    [SerializeField] private float _movementDamping = 5.0f;
    [Header("Bounce")]
    [SerializeField] private float _bounceAmount = 5.0f;
    
    protected virtual void Start()
    {
        // Optional: Base implementation for derived classes
    }
    
    protected virtual void Update()
    {
        // Optional: Base implementation for derived classes
    }

    protected virtual void OnTriggerEnter2D(Collider other)
    {
        
    }

    protected private enum PickupBehaviorType
    {
        Static,
        Move,
        Bounce,
        MoveAndBounce,
        Fly,
        Evade,
        Chase
    }
}