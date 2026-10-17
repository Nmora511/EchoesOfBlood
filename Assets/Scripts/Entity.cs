using System.Collections;
using UnityEngine;

public abstract class Entity : MonoBehaviour
{
    [Header("Combat Settings")]
    public float HealthPoints = 20f;
    public float InvulnerabilityDuration = 1f;
    
    [Header("Knockback Settings")]
    public float KnockbackForce = 15f;
    public float KnockbackFriction = 5f;
    
    protected Vector3 KnockbackVelocity;

    private float _hitTimer;
    private bool _isInvulnerable;

    private Renderer[] _entityRenderers;
    private bool _hitFlashIsExecuting = false;
    
    protected virtual void Start()
    {
        _entityRenderers = GetComponentsInChildren<Renderer>();
    }

    protected virtual void Update()
    {
        if (_hitTimer > 0f)
        {
            _hitTimer -= Time.deltaTime;
        }
        else
        {
            _isInvulnerable = false;
        }
        UpdateKnockback();
    }

    public virtual bool OnHit(float damage, Vector3 attackerPosition)
    {
        if (_isInvulnerable) return false;

        if (!_hitFlashIsExecuting)
        {
            StartCoroutine(HitFlash());
        }
        
        // Knockback handler
        var pushDirection = (transform.position - attackerPosition).normalized;
        pushDirection.y = 0f;
        
        KnockbackVelocity = pushDirection * KnockbackForce;
        
        //Damage Handler
        HealthPoints -= damage;
        if (HealthPoints <= 0)
        {
            Death();
        }
        
        _isInvulnerable = true;
        _hitTimer = InvulnerabilityDuration;
        return true;
    }
    
    private void UpdateKnockback()
    {
        KnockbackVelocity = Vector3.Lerp(KnockbackVelocity, Vector3.zero, Time.deltaTime * KnockbackFriction);
    }

    private IEnumerator HitFlash()
    {
        _hitFlashIsExecuting = true;
        var originalColors = new Color[_entityRenderers.Length];
        for (var i = 0; i < _entityRenderers.Length; i++)
        {
            originalColors[i] = _entityRenderers[i].material.color;
        }

        ChangeEntityColor(Color.red);
        yield return new WaitForSeconds(0.1f);
        ChangeEntityColor(Color.white);
        yield return new WaitForSeconds(0.1f);
        ChangeEntityColor(Color.red);
        yield return new WaitForSeconds(0.1f);

        for (var i = 0; i < _entityRenderers.Length; i++)
        {
            _entityRenderers[i].material.color = originalColors[i];
        }
        _hitFlashIsExecuting = false;
    }

    private void ChangeEntityColor(Color newColor)
    {
        foreach (var renderer in _entityRenderers)
        {
            renderer.material.color = newColor;
        }
    }

    public virtual void Death()
    {
        
    }

}
