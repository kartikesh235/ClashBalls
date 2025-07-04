using Fusion;
using Fusion.Addons.Physics;
using Game.Abilities;
using UnityEngine;
using Game.Controllers;
using Game.Character;
using Game.Managers;

namespace Game.Ball
{
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : NetworkBehaviour
    {
        private NetworkRigidbody3D _rb;
        private Collider _collider;
        private TrailRenderer mTrail;

        [Networked] public bool IsHeld { get; private set; }
        [Networked] private TickTimer PickupCooldown { get; set; }

        private GameObject _carrier;
        public CharacterTypeSO typeData;
        
        [Header("Ball Damage")]
        public float ballDamage = 25f;
        public bool hasHitGround = false;
        public float ballHitStunDuration = 0.5f; // Add this field
        

        public override void Spawned()
        {
            _rb = GetComponent<NetworkRigidbody3D>();
            _collider = GetComponent<Collider>();
            mTrail = GetComponent<TrailRenderer>();
        }

        public bool CanPickUp() => !IsHeld && PickupCooldown.ExpiredOrNotRunning(Runner);

        public void PickUp(GameObject carrier)
        {
            if (!HasStateAuthority || !CanPickUp()) return;

            IsHeld = true;
            _carrier = carrier;
            typeData = carrier.GetComponent<PlayerController>().GetCharacterTypeSO();

            _rb.Rigidbody.isKinematic = true;
            _collider.enabled = false;
            if (mTrail != null) mTrail.enabled = false;
            
            carrier.GetComponent<PlayerController>().ball = this;
            transform.SetParent(carrier.GetComponent<PlayerController>().ballTransformHolder);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (carrier.TryGetComponent(out ThrowAbility throwAbility))
            {
                throwAbility.SetHeldBall(this);
            }
            PickupCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
        }
        

        public override void FixedUpdateNetwork()
        {
            if (IsHeld && _carrier != null)
            {
                var holder = _carrier.GetComponent<PlayerController>()?.ballTransformHolder;
                if (holder != null)
                {
                    transform.position = holder.position;
                    transform.rotation = holder.rotation;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!HasStateAuthority || IsHeld) return;

            if (collision.gameObject.CompareTag("Wall"))
            {
                HandleWallBounce(collision);
            }
            else if (collision.gameObject.CompareTag("Ground"))
            {
                hasHitGround = true;
            }
            else if (collision.gameObject.CompareTag("Player"))
            {
                HandlePlayerHit(collision.gameObject);
            }
        }

        private void HandlePlayerHit(GameObject player)
        {
            if (player == _carrier) return;
    
            // Only do damage if ball hasn't hit ground yet
            if (!hasHitGround && _carrier != null)
            {
                var healthSystem = player.GetComponent<PlayerHealthSystem>();
                var thrower = _carrier.GetComponent<PlayerController>();
        
                if (healthSystem != null && healthSystem.CanTakeDamage())
                {
                    // Apply damage
                    healthSystem.TakeDamage(ballDamage, thrower);
                    
                    // Apply stun (only on StateAuthority)
                    if (HasStateAuthority)
                    {
                        var stunSystem = player.GetComponent<StunSystem>();
                        if (stunSystem != null)
                        {
                            stunSystem.ApplyStun(ballHitStunDuration);
                        }
                    }
            
                    // Add score to thrower
                    if (thrower != null)
                    {
                        ScoreManager.Instance.AddScore(thrower, (int)ballDamage, "Ball Hit");
                    }
                }
            }
    
            // Reset ball state
            hasHitGround = true;
        }

// Add to your Throw method:
        public void Throw(Vector3 dir, float force, GameObject carrier)
        {
            if (!HasStateAuthority || !IsHeld) return;

            IsHeld = false;
            _carrier = carrier;
            hasHitGround = false; // Reset ground hit status

            transform.SetParent(null);
            _rb.Rigidbody.isKinematic = false;
            _rb.Rigidbody.linearVelocity = dir.normalized * force;
            _collider.enabled = true;
            if (mTrail != null) mTrail.enabled = true;

            PickupCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
        }

        private void HandleWallBounce(Collision collision)
        {
            Vector3 normal = collision.contacts[0].normal;
            Vector3 reflected = Vector3.Reflect(_rb.Rigidbody.linearVelocity, normal);

            float originalForce = _rb.Rigidbody.linearVelocity.magnitude;
            if (originalForce > typeData.baseThrowForce * typeData.maxChargeThrowMultiplier)
                reflected *= 1.5f;

            _rb.Rigidbody.linearVelocity = reflected;
        }
    }
}
