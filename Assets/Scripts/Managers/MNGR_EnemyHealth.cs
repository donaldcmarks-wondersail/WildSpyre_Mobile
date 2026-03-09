using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MNGR_EnemyHealth : MonoBehaviour
{
    [Header("Enemy Health Variables")]
    [SerializeField] private int _startingHealth = 1;
    private int _currentHealth;
    [SerializeField] private float _takeDamageCooldown = 0.25f;
    [SerializeField] private Animator _animator;
    [SerializeField] private LayerMask _damageLayerMask;
    [SerializeField] private bool _rechargeHealthOverTime = false;
    [SerializeField] private float _rechargeHealthRate = 1.0f;
    [SerializeField] private ParticleSystem _takeDamageParticles;
    [SerializeField] private ParticleSystem _deathParticles;
    private Rigidbody2D _rb;
    private Entity _entityCtrl;
    [SerializeField] private bool _hitReactForce = false;
    private Damager _damager;
    private bool _cooldownActive = false;
    [SerializeField] private float _pauseTimeOnTakeDamage = 0.5f;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CurrentHealth = _startingHealth;
        _entityCtrl = GetComponent<Entity>();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        // Check if the other collider's layer is included in the allowedLayers mask
        if (((1 << col.gameObject.layer) & _damageLayerMask) != 0 && !_cooldownActive)
        {
            Rigidbody2D colRB = col.GetComponentInParent<Rigidbody2D>();
            if (colRB != null && colRB.velocity.y < 0.05f)
            {
                Debug.Log("REMOVING LIFE FROM ENTITY");
                _damager = col.gameObject.GetComponent<Damager>();
                CurrentHealth--;
            }
        }
    }

    public int CurrentHealth
    {
        get { return _currentHealth; }
        set
        {
            Debug.Log("Setting CurrentHealth To: " + value);
            if (value != _currentHealth)
            {
                if (value < _currentHealth)
                {
                    _cooldownActive = true;
                    if (_damager.damagerReact)
                    {
                        _damager.DamagerReaction();                        
                    }

                    if (value <= 0)
                    {
                        if (_deathParticles != null)
                        {
                            Instantiate(_deathParticles, transform.position, Quaternion.identity);                            
                        }
                        Destroy(this.gameObject);
                    }
                    else
                    {
                        if (_damager.reactToDamage)
                        {
                            Debug.Log("Damage REACTION SHOULD BE PLAYING ON ENEMY");
                            Vector2 damageReactVector = Vector2.down;
                            //_rb.AddForce(damageReactVector.normalized * _damager.reactToDamageForce, ForceMode2D.Impulse);
                        }

                        if (_damager.damagerReact)
                        {
                            _damager.DamagerReaction();
                        }
                        
                        if (_animator != null)
                        {
                            _animator.SetTrigger("TakeDamage");
                        }
                        if (_takeDamageParticles)
                        {
                            Instantiate(_takeDamageParticles, transform.position, Quaternion.identity);                            
                        }
                    }
                    
                    Vector3 reactVector = Vector2.down;
                    if (_damager._dynamicReactVector)
                    {
                        reactVector = (_damager.transform.position + this.transform.position).normalized * _damager._damageReactDistance;
                    }
                    else
                    {
                        reactVector = this.transform.position + _damager._damageReactVector;
                    }
                    
                    Debug.Log("React Vector = " + reactVector);
                    StartCoroutine(_entityCtrl.TakeDamageCO(_takeDamageCooldown, reactVector));
                    StartCoroutine(DamageCooldownCO());
                }
                _currentHealth = value;
            }
        }
    }

    private IEnumerator DamageCooldownCO()
    {
        yield return new WaitForSeconds(_takeDamageCooldown);
        _cooldownActive = false;
        yield return null;
    }
}