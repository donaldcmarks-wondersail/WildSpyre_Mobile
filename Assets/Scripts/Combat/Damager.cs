using System;
using UnityEngine;

public class Damager : MonoBehaviour
{
    [Header("Damager Variables")] 
    public int damage;
    public bool reactToDamage = false;
    public float reactToDamageForce = 1.0f;
    public float damagerReactForce = 1.0f;
    public bool damagerReact = false;
    [SerializeField] Rigidbody2D reactorTransformRB;
    [SerializeField] Animator _animator;
    [SerializeField] private CTRL_PlayerPlatformer _playerCtrl;   
    [SerializeField] private float _damagerReactTime = 0.35f;   
    [SerializeField] private bool _requireDownwardMovement = false;
    public float _damageReactDistance = 0.25f;
    public bool _dynamicReactVector = false;
    public Vector3 _damageReactVector;
    
    private void Start()
    {
        if (reactorTransformRB == null)
        {
            reactorTransformRB = GetComponentInParent<Rigidbody2D>();            
        }
    }

    public void DamagerReaction()
    {
        if (_requireDownwardMovement)
        {
            if (reactorTransformRB.linearVelocity.y < 0)
            {
                _playerCtrl.GiveDamageReaction(Vector3.up*damagerReactForce, _damagerReactTime, damagerReactForce);
                reactorTransformRB.AddForce(transform.up * damagerReactForce);
                _animator.SetTrigger("Jump");
            }
        }
        else
        {
            _playerCtrl.GiveDamageReaction(Vector3.up*damagerReactForce, _damagerReactTime, damagerReactForce);
            reactorTransformRB.AddForce(transform.up * damagerReactForce);
            _animator.SetTrigger("Jump");
        }
//        _playerCtrl.resetPlayerPhysics();

    }
}
