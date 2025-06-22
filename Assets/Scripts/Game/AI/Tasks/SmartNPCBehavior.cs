using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using Game.Abilities;
using Game.Character;
using Game.Controllers;

namespace Game.AI.Tasks
{
    public enum NPCBehaviorState
    {
        SeekingBall,
        HasBallHunting,
        Combat,
        Patrolling,
        Resting
    }

    [TaskCategory("Game/Composite")]
    public class SmartNPCBehavior : Action
    {
        [Header("Shared Variables")]
        [SharedRequired] public SharedVector2 moveDirection;
        [SharedRequired] public SharedBool shouldSprint;
        [SharedRequired] public SharedBool buttonA, buttonB, buttonD;
        
        [Header("Detection Ranges")]
        [RequiredField] public SharedFloat ballDetectionRange = 20f;
        [RequiredField] public SharedFloat enemyDetectionRange = 15f;
        [RequiredField] public SharedFloat tackleRange = 3f;
        [RequiredField] public SharedFloat throwRange = 12f;
        
        [Header("Combat Settings")]
        [RequiredField] public SharedFloat randomActionChance = 0.2f;
        [RequiredField] public SharedFloat combatCooldown = 3f;
        [RequiredField] public SharedFloat decisionCooldown = 0.5f;
        
        [Header("Patrol Settings")]
        [RequiredField] public SharedFloat patrolChangeInterval = 3f;
        [RequiredField] public SharedFloat patrolRadius = 8f;
        
        [Header("Stamina Management")]
        [RequiredField] public SharedFloat staminaThreshold = 0.3f;
        [RequiredField] public SharedFloat restThreshold = 0.8f;
        
        private NPCBehaviorState mCurrentState = NPCBehaviorState.SeekingBall;
        private Vector3 mTargetBallPosition;
        private Vector3 mTargetEnemyPosition;
        private Vector3 mPatrolTarget;
        private Vector3 mSpawnPosition;
        
        private float mLastCombatAction;
        private float mLastDecisionTime;
        private float mLastPatrolChange;
        private float mStateTimer;
        
        private bool mHasBall;
        private CharacterStats mStats;
        private PickUpAbility mPickupAbility;

        public override void OnStart()
        {
            mStats = GetComponent<CharacterStats>();
            mPickupAbility = GetComponent<PickUpAbility>();
            mSpawnPosition = transform.position;
            mPatrolTarget = GeneratePatrolTarget();
            mLastPatrolChange = Time.time;
        }

        public override TaskStatus OnUpdate()
        {
            if (Time.time - mLastDecisionTime < decisionCooldown.Value)
            {
                ContinueCurrentBehavior();
                return TaskStatus.Running;
            }
            
            mLastDecisionTime = Time.time;
            UpdateGameState();
            
            NPCBehaviorState newState = DetermineOptimalState();
            
            if (newState != mCurrentState)
            {
                OnStateChanged(mCurrentState, newState);
                mCurrentState = newState;
                mStateTimer = 0f;
            }
            
            mStateTimer += decisionCooldown.Value;
            return ExecuteCurrentState();
        }

        private void UpdateGameState()
        {
            mHasBall = mPickupAbility != null && mPickupAbility.HasBall;
    
            // Only look for balls if we don't have one
            if (!mHasBall)
            {
                mTargetBallPosition = FindClosestBall();
            }
            else
            {
                mTargetBallPosition = Vector3.zero; // Clear target since we have a ball
            }
    
            mTargetEnemyPosition = FindClosestEnemy();
        }
        private NPCBehaviorState DetermineOptimalState()
        {
            float currentStaminaRatio = GetStaminaRatio();
    
            // Priority 1: Rest if exhausted
            if (currentStaminaRatio < staminaThreshold.Value)
            {
                return NPCBehaviorState.Resting;
            }
    
            // Priority 2: If has ball and enemy in range, hunt
            if (mHasBall && mTargetEnemyPosition != Vector3.zero)
            {
                return NPCBehaviorState.HasBallHunting;
            }
    
            // Priority 3: Seek ball ONLY if we don't have one and ball exists
            if (!mHasBall && mTargetBallPosition != Vector3.zero && currentStaminaRatio > 0.5f)
            {
                return NPCBehaviorState.SeekingBall;
            }
    
            // Priority 4: Combat if enemy nearby and we don't have a ball to use
            if (!mHasBall && mTargetEnemyPosition != Vector3.zero && 
                Vector3.Distance(transform.position, mTargetEnemyPosition) <= enemyDetectionRange.Value * 0.7f)
            {
                return NPCBehaviorState.Combat;
            }
    
            // Fallback: Patrol
            return NPCBehaviorState.Patrolling;
        }
        private TaskStatus ExecuteCurrentState()
        {
            switch (mCurrentState)
            {
                case NPCBehaviorState.SeekingBall:
                    return HandleSeekingBall();
                case NPCBehaviorState.HasBallHunting:
                    return HandleBallHunting();
                case NPCBehaviorState.Combat:
                    return HandleCombat();
                case NPCBehaviorState.Resting:
                    return HandleResting();
                case NPCBehaviorState.Patrolling:
                    return HandlePatrolling();
                default:
                    return TaskStatus.Running;
            }
        }

        private TaskStatus HandleSeekingBall()
        {
            if (mTargetBallPosition == Vector3.zero)
                return TaskStatus.Failure;
                
            float distance = Vector3.Distance(transform.position, mTargetBallPosition);
            
            if (distance <= 4f && CanConsumeStamina(0.5f))
            {
                buttonA.Value = true;
                return TaskStatus.Success;
            }
            
            MoveTowardsTarget(mTargetBallPosition, true);
            return TaskStatus.Running;
        }

        private TaskStatus HandleBallHunting()
        {
            if (mTargetEnemyPosition == Vector3.zero)
                return TaskStatus.Failure;
                
            Vector3 direction = (mTargetEnemyPosition - transform.position).normalized;
            transform.forward = direction;
            
            float distance = Vector3.Distance(transform.position, mTargetEnemyPosition);
            
            if (distance <= throwRange.Value)
            {
                buttonA.Value = true;
                return TaskStatus.Success;
            }
            
            MoveTowardsTarget(mTargetEnemyPosition, distance > 8f);
            TryRandomCombatAction();
            return TaskStatus.Running;
        }

        private TaskStatus HandleCombat()
        {
            if (mTargetEnemyPosition == Vector3.zero)
                return TaskStatus.Failure;
                
            float distance = Vector3.Distance(transform.position, mTargetEnemyPosition);
            
            if (distance <= tackleRange.Value && CanConsumeStamina(1f))
            {
                Vector3 direction = (mTargetEnemyPosition - transform.position).normalized;
                transform.forward = direction;
                buttonD.Value = true;
                return TaskStatus.Success;
            }
            
            MoveTowardsTarget(mTargetEnemyPosition, distance > 5f);
            TryRandomCombatAction();
            return TaskStatus.Running;
        }

        private TaskStatus HandleResting()
        {
            StopMovement();
            
            if (GetStaminaRatio() >= restThreshold.Value)
            {
                return TaskStatus.Success;
            }
            
            return TaskStatus.Running;
        }

        private TaskStatus HandlePatrolling()
        {
            if (Time.time - mLastPatrolChange >= patrolChangeInterval.Value)
            {
                float distanceToTarget = Vector3.Distance(transform.position, mPatrolTarget);
                
                if (distanceToTarget <= 2f || mStateTimer > patrolChangeInterval.Value * 2f)
                {
                    mPatrolTarget = GeneratePatrolTarget();
                    mLastPatrolChange = Time.time;
                }
            }
            
            MoveTowardsTarget(mPatrolTarget, false);
            TryRandomCombatAction();
            return TaskStatus.Running;
        }

       private void ContinueCurrentBehavior()
{
    switch (mCurrentState)
    {
        case NPCBehaviorState.SeekingBall:
            if (!mHasBall && mTargetBallPosition != Vector3.zero)
                MoveTowardsTarget(mTargetBallPosition, true);
            break;
        case NPCBehaviorState.HasBallHunting:
            if (mHasBall && mTargetEnemyPosition != Vector3.zero)
                MoveTowardsTarget(mTargetEnemyPosition, Vector3.Distance(transform.position, mTargetEnemyPosition) > 8f);
            break;
        case NPCBehaviorState.Combat:
            if (!mHasBall && mTargetEnemyPosition != Vector3.zero)
                MoveTowardsTarget(mTargetEnemyPosition, Vector3.Distance(transform.position, mTargetEnemyPosition) > 5f);
            break;
        case NPCBehaviorState.Resting:
            StopMovement();
            break;
        case NPCBehaviorState.Patrolling:
            MoveTowardsTarget(mPatrolTarget, false);
            break;
    }
}

        private void OnStateChanged(NPCBehaviorState oldState, NPCBehaviorState newState)
        {
            if (newState == NPCBehaviorState.Patrolling && oldState != NPCBehaviorState.Patrolling)
            {
                mPatrolTarget = GeneratePatrolTarget();
                mLastPatrolChange = Time.time;
            }
        }

        private void MoveTowardsTarget(Vector3 target, bool shouldRunFast)
        {
            Vector3 direction = (target - transform.position).normalized;
            float distance = Vector3.Distance(transform.position, target);
            
            bool canSprint = shouldRunFast && CanConsumeStamina(0.1f) && distance > 3f;
            
            moveDirection.Value = new Vector2(direction.x, direction.z) * (canSprint ? 7f : 4f);
            shouldSprint.Value = canSprint;
        }

        private void StopMovement()
        {
            moveDirection.Value = Vector2.zero;
            shouldSprint.Value = false;
        }

        private Vector3 GeneratePatrolTarget()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius.Value;
            Vector3 patrolPoint = mSpawnPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
            patrolPoint.y = transform.position.y;
            return patrolPoint;
        }

        private bool CanConsumeStamina(float amount)
        {
            if (mStats == null) return true;
            return mStats.HasStamina(amount);
        }

        private float GetStaminaRatio()
        {
            if (mStats == null) return 1f;
            return mStats.StaminaRatio;
        }

        private void TryRandomCombatAction()
        {
            if (Time.time - mLastCombatAction < combatCooldown.Value)
                return;
                
            if (Random.value < randomActionChance.Value && CanConsumeStamina(1f))
            {
                mLastCombatAction = Time.time;
                
                if (Random.value < 0.5f)
                    buttonB.Value = true;
                else
                    buttonD.Value = true;
            }
        }

        private Vector3 FindClosestBall()
        {
#if UNITY_2022_2_OR_NEWER
            var balls = UnityEngine.Object.FindObjectsByType<Game.Ball.BallController>(FindObjectsSortMode.None);
#else
            var balls = UnityEngine.Object.FindObjectsOfType<Game.Ball.BallController>();
#endif
            
            Vector3 closestPos = Vector3.zero;
            float closestDistance = ballDetectionRange.Value;
            
            foreach (var ball in balls)
            {
                if (ball == null || ball.IsHeld) continue;
                
                float distance = Vector3.Distance(transform.position, ball.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPos = ball.transform.position;
                }
            }
            
            return closestPos;
        }

        private Vector3 FindClosestEnemy()
        {
#if UNITY_2022_2_OR_NEWER
            var players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
#else
            var players = UnityEngine.Object.FindObjectsOfType<PlayerController>();
#endif
            
            Vector3 closestPos = Vector3.zero;
            float closestDistance = enemyDetectionRange.Value;
            
            foreach (var player in players)
            {
                if (player == null || player.gameObject == gameObject) continue;
                
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPos = player.transform.position;
                }
            }
            
            return closestPos;
        }
    }
}