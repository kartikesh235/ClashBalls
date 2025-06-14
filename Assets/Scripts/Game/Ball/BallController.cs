using Fusion;
using Fusion.Addons.Physics;
using Game.Abilities;
using UnityEngine;
using Game.Controllers;
using Game.Character;

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

            _rb.Rigidbody.isKinematic = true;
            _collider.enabled = false;
            if (mTrail != null) mTrail.enabled = false;
            
            transform.SetParent(carrier.GetComponent<PlayerController>().ballTransformHolder);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (carrier.TryGetComponent(out ThrowAbility throwAbility))
            {
                throwAbility.SetHeldBall(this);
            }
            PickupCooldown = TickTimer.CreateFromSeconds(Runner, 0.5f);
        }

        public void Throw(Vector3 dir, float force, GameObject carrier)
        {
            if (!HasStateAuthority || !IsHeld) return;

            IsHeld = false;
            _carrier = carrier;

            transform.SetParent(null);
            _rb.Rigidbody.isKinematic = false;
            _rb.Rigidbody.linearVelocity = dir.normalized * force;
            Debug.LogError("Force Throw" + force);
            _collider.enabled = true;
            if (mTrail != null) mTrail.enabled = true;

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
                HandleWallBounce(collision);
            else if (collision.gameObject.CompareTag("Player"))
                HandlePlayerHit(collision.gameObject);
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

        private void HandlePlayerHit(GameObject player)
        {
            if (player == _carrier) return;
            KnockoutPlayer(player);
            DestroyBall();
        }

        private void KnockoutPlayer(GameObject player)
        {
            // Implementation to handle player knockout
        }

        private void DestroyBall()
        {
            // Runner.Despawn(Object);
        }
    }
}
