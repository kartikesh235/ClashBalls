using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using Game.Abilities;
using Game.Controllers;
using Game.Character;
using Game.AI;

namespace Game.AI.Tasks
{
    public enum NPCBehaviorState
    {
        SeekingBall,
        HuntingWithBall,
        DefensiveDodging,
        AggressiveTackle,
        CautiousParry,
        AvoidingDanger,
        ReturningToCenter  // New state
    }

    [TaskCategory("Game/Composite")]
    public class SmartNPCBehavior : Action
    {
        [Header("Required Components")]
        [RequiredField] public NpcAISO aiSettings;
        
        [Header("Shared Variables")]
        [SharedRequired] public SharedVector2 moveDirection;
        [SharedRequired] public SharedBool shouldSprint;
        [SharedRequired] public SharedBool buttonA, buttonB, buttonC, buttonD;
        
        [Header("World Boundaries")]
        public float worldBoundaryX = 23f;
        public float worldBoundaryZ = 23f;
        
        [Header("Movement Smoothing")]
        public float movementSmoothTime = 0.3f;
        public float directionChangeDelay = 1f;
        public float centerAttractionForce = 0.3f;
        public float cornerAvoidanceDistance = 5f;
        
        private NPCBehaviorState mCurrentState;
        private Vector3 mTargetBallPosition;
        private Vector3 mTargetEnemyPosition;
        private Vector3 mAvoidanceDirection;
        private Vector3 mCurrentMovementDirection;
        private Vector3 mTargetMovementDirection;
        private Vector3 mVelocity;
        
        private float mLastDecisionTime;
        private float mLastCombatAction;
        private float mLastDirectionChange;
        private float mStateTimer;
        private float mStuckTimer;
        private Vector3 mLastPosition;
        
        private bool mHasBall;
        private bool mEnemyHasBall;
        private bool mEnemyLookingAtMe;
        private float mDistanceToEnemy;
        private bool mIsInCorner;
        private bool mIsNearBoundary;
        
        private CharacterStats mStats;
        private CharacterTypeSO mCharacterType;
        private PickUpAbility mPickupAbility;
        private PlayerController mClosestEnemy;
        private StunSystem mStunSystem;

        public override void OnStart()
        {
            mStats = GetComponent<CharacterStats>();
            mPickupAbility = GetComponent<PickUpAbility>();
            mStunSystem = GetComponent<StunSystem>();
            
            var playerController = GetComponent<PlayerController>();
            if (playerController != null)
            {
                mCharacterType = playerController.GetCharacterTypeSO();
            }
            
            mCurrentState = NPCBehaviorState.SeekingBall;
            mCurrentMovementDirection = transform.forward;
            mLastPosition = transform.position;
        }

        public override TaskStatus OnUpdate()
        {
            if (mStunSystem != null && mStunSystem.IsStunned)
            {
                StopMovement();
                return TaskStatus.Running;
            }
            
            UpdateMovementSmoothing();
            CheckIfStuck();
            AnalyzeSpatialSituation();
            
            // Less frequent decision making for more natural behavior
            if (Time.time - mLastDecisionTime < aiSettings.decisionCooldown)
            {
                ContinueCurrentAction();
                return TaskStatus.Running;
            }
            
            mLastDecisionTime = Time.time;
            AnalyzeGameSituation();
            
            NPCBehaviorState newState = DetermineOptimalAction();
            
            if (newState != mCurrentState)
            {
                mCurrentState = newState;
                mStateTimer = 0f;
            }
            
            mStateTimer += aiSettings.decisionCooldown;
            return ExecuteCurrentAction();
        }

        private void UpdateMovementSmoothing()
        {
            // Smooth movement direction changes
            mCurrentMovementDirection = Vector3.SmoothDamp(
                mCurrentMovementDirection, 
                mTargetMovementDirection, 
                ref mVelocity, 
                movementSmoothTime
            );
        }

        private void CheckIfStuck()
        {
            float distanceMoved = Vector3.Distance(transform.position, mLastPosition);
            
            if (distanceMoved < 0.1f)
            {
                mStuckTimer += Time.deltaTime;
            }
            else
            {
                mStuckTimer = 0f;
                mLastPosition = transform.position;
            }
            
            // If stuck for too long, force return to center
            if (mStuckTimer > 3f)
            {
                mCurrentState = NPCBehaviorState.ReturningToCenter;
                mStuckTimer = 0f;
            }
        }

        private void AnalyzeSpatialSituation()
        {
            Vector3 pos = transform.position;
            
            // Check if in corner
            mIsInCorner = (Mathf.Abs(pos.x) > worldBoundaryX - cornerAvoidanceDistance && 
                          Mathf.Abs(pos.z) > worldBoundaryZ - cornerAvoidanceDistance);
            
            // Check if near boundary
            mIsNearBoundary = (Mathf.Abs(pos.x) > worldBoundaryX - cornerAvoidanceDistance || 
                              Mathf.Abs(pos.z) > worldBoundaryZ - cornerAvoidanceDistance);
        }

        private void AnalyzeGameSituation()
        {
            mHasBall = mPickupAbility != null && mPickupAbility.HasBall;
            
            if (!mHasBall)
            {
                mTargetBallPosition = FindClosestBall();
            }
            else
            {
                mTargetBallPosition = Vector3.zero;
            }
            
            AnalyzeEnemyThreats();
        }

        private void AnalyzeEnemyThreats()
        {
            mClosestEnemy = FindClosestEnemy();
            
            if (mClosestEnemy != null)
            {
                mTargetEnemyPosition = mClosestEnemy.transform.position;
                mDistanceToEnemy = Vector3.Distance(transform.position, mTargetEnemyPosition);
                
                var enemyPickup = mClosestEnemy.GetComponent<PickUpAbility>();
                mEnemyHasBall = enemyPickup != null && enemyPickup.HasBall;
                
                Vector3 enemyToUs = (transform.position - mTargetEnemyPosition).normalized;
                Vector3 enemyForward = mClosestEnemy.transform.forward;
                float facingDot = Vector3.Dot(enemyForward, enemyToUs);
                mEnemyLookingAtMe = facingDot > 0.5f;
            }
            else
            {
                mTargetEnemyPosition = Vector3.zero;
                mEnemyHasBall = false;
                mEnemyLookingAtMe = false;
                mDistanceToEnemy = float.MaxValue;
            }
        }

        private NPCBehaviorState DetermineOptimalAction()
        {
            float staminaRatio = GetStaminaRatio();
            float distanceFromCenter = Vector3.Distance(transform.position, Vector3.zero);
            
            // Priority 1: Escape corner if trapped
            if (mIsInCorner && mStuckTimer > 1f)
            {
                return NPCBehaviorState.ReturningToCenter;
            }
            
            // Priority 2: Return to center if too far out and no clear objective
            if (distanceFromCenter > worldBoundaryX * 0.8f && !mHasBall && mTargetBallPosition == Vector3.zero)
            {
                return NPCBehaviorState.ReturningToCenter;
            }
            
            // Priority 3: Defensive dodge
            if (mEnemyHasBall && mDistanceToEnemy <= aiSettings.dangerDetectionRange && 
                mEnemyLookingAtMe && staminaRatio > aiSettings.lowStaminaThreshold)
            {
                if (Random.value < aiSettings.dodgeChance)
                    return NPCBehaviorState.DefensiveDodging;
            }
            
            // Priority 4: Aggressive tackle (only if not near boundary to avoid corner traps)
            if (mDistanceToEnemy <= aiSettings.tackleRange && !mIsNearBoundary && 
                staminaRatio > aiSettings.aggressiveStaminaThreshold)
            {
                if (Random.value < aiSettings.tackleChance)
                    return NPCBehaviorState.AggressiveTackle;
            }
            
            // Priority 5: Cautious parry
            if (mDistanceToEnemy <= aiSettings.parryRange && mEnemyLookingAtMe && 
                staminaRatio > aiSettings.lowStaminaThreshold)
            {
                if (Random.value < aiSettings.parryChance)
                    return NPCBehaviorState.CautiousParry;
            }
            
            // Priority 6: Hunt with ball
            if (mHasBall && mTargetEnemyPosition != Vector3.zero)
            {
                return NPCBehaviorState.HuntingWithBall;
            }
            
            // Priority 7: Seek ball
            if (!mHasBall && mTargetBallPosition != Vector3.zero)
            {
                if (IsEnemyBlockingPath(mTargetBallPosition))
                    return NPCBehaviorState.AvoidingDanger;
                else
                    return NPCBehaviorState.SeekingBall;
            }
            
            // Fallback: Return to center for better positioning
            return NPCBehaviorState.ReturningToCenter;
        }

        private TaskStatus ExecuteCurrentAction()
        {
            switch (mCurrentState)
            {
                case NPCBehaviorState.SeekingBall:
                    return HandleSeekingBall();
                case NPCBehaviorState.HuntingWithBall:
                    return HandleHuntingWithBall();
                case NPCBehaviorState.DefensiveDodging:
                    return HandleDefensiveDodging();
                case NPCBehaviorState.AggressiveTackle:
                    return HandleAggressiveTackle();
                case NPCBehaviorState.CautiousParry:
                    return HandleCautiousParry();
                case NPCBehaviorState.AvoidingDanger:
                    return HandleAvoidingDanger();
                case NPCBehaviorState.ReturningToCenter:
                    return HandleReturningToCenter();
                default:
                    return TaskStatus.Running;
            }
        }

        private TaskStatus HandleSeekingBall()
        {
            if (mTargetBallPosition == Vector3.zero)
                return TaskStatus.Failure;
                
            float distance = Vector3.Distance(transform.position, mTargetBallPosition);
            
            if (distance <= 4f)
            {
                buttonA.Value = true;
                return TaskStatus.Success;
            }
            
            MoveTowardsTargetSmooth(mTargetBallPosition, true);
            return TaskStatus.Running;
        }

        private TaskStatus HandleHuntingWithBall()
        {
            if (mTargetEnemyPosition == Vector3.zero)
                return TaskStatus.Failure;
                
            // Smooth rotation toward enemy
            Vector3 direction = (mTargetEnemyPosition - transform.position).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 2f);
            
            if (mDistanceToEnemy <= GetThrowRange())
            {
                buttonA.Value = true;
                return TaskStatus.Success;
            }
            
            MoveTowardsTargetSmooth(mTargetEnemyPosition, mDistanceToEnemy > 8f);
            return TaskStatus.Running;
        }

        private TaskStatus HandleReturningToCenter()
        {
            Vector3 centerDirection = (Vector3.zero - transform.position).normalized;
            float distanceToCenter = Vector3.Distance(transform.position, Vector3.zero);
            
            // Add some randomness to avoid predictable movement
            Vector3 randomOffset = new Vector3(
                Random.Range(-2f, 2f), 
                0, 
                Random.Range(-2f, 2f)
            );
            
            Vector3 targetPosition = Vector3.zero + randomOffset;
            
            if (distanceToCenter < 5f)
            {
                return TaskStatus.Success; // Close enough to center
            }
            
            MoveTowardsTargetSmooth(targetPosition, false);
            return TaskStatus.Running;
        }

        private TaskStatus HandleDefensiveDodging()
        {
            if (CanConsumeStamina(GetDodgeStaminaCost()))
            {
                buttonB.Value = true;
                
                Vector3 enemyToUs = (transform.position - mTargetEnemyPosition).normalized;
                Vector3 dodgeDirection = Vector3.Cross(enemyToUs, Vector3.up).normalized;
                if (Random.value < 0.5f) dodgeDirection = -dodgeDirection;
                
                // Bias dodge toward center if near boundary
                if (mIsNearBoundary)
                {
                    Vector3 centerDirection = (Vector3.zero - transform.position).normalized;
                    dodgeDirection = Vector3.Lerp(dodgeDirection, centerDirection, 0.5f).normalized;
                }
                
                mAvoidanceDirection = dodgeDirection;
                return TaskStatus.Success;
            }
            
            return TaskStatus.Failure;
        }

        private TaskStatus HandleAggressiveTackle()
        {
            if (CanConsumeStamina(GetTackleStaminaCost()))
            {
                Vector3 direction = (mTargetEnemyPosition - transform.position).normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 3f);
                buttonD.Value = true;
                return TaskStatus.Success;
            }
            
            return TaskStatus.Failure;
        }

        private TaskStatus HandleCautiousParry()
        {
            if (CanConsumeStamina(GetParryStaminaCost()))
            {
                buttonC.Value = true;
                return TaskStatus.Success;
            }
            
            return TaskStatus.Failure;
        }

        private TaskStatus HandleAvoidingDanger()
        {
            Vector3 ballDirection = (mTargetBallPosition - transform.position).normalized;
            Vector3 enemyDirection = (mTargetEnemyPosition - transform.position).normalized;
            
            Vector3 avoidanceVector = Vector3.Cross(enemyDirection, Vector3.up).normalized;
            Vector3 finalDirection = (ballDirection + avoidanceVector * 0.5f).normalized;
            
            // Add center bias
            Vector3 centerDirection = (Vector3.zero - transform.position).normalized;
            finalDirection = Vector3.Lerp(finalDirection, centerDirection, centerAttractionForce).normalized;
            
            MoveInDirectionSmooth(finalDirection, true);
            
            if (Vector3.Distance(transform.position, mTargetBallPosition) <= 4f)
            {
                buttonA.Value = true;
                return TaskStatus.Success;
            }
            
            return TaskStatus.Running;
        }

        private void ContinueCurrentAction()
        {
            switch (mCurrentState)
            {
                case NPCBehaviorState.SeekingBall:
                    if (mTargetBallPosition != Vector3.zero)
                        MoveTowardsTargetSmooth(mTargetBallPosition, true);
                    break;
                case NPCBehaviorState.HuntingWithBall:
                    if (mTargetEnemyPosition != Vector3.zero)
                        MoveTowardsTargetSmooth(mTargetEnemyPosition, mDistanceToEnemy > 8f);
                    break;
                case NPCBehaviorState.ReturningToCenter:
                    MoveTowardsTargetSmooth(Vector3.zero, false);
                    break;
                case NPCBehaviorState.AvoidingDanger:
                    if (mTargetBallPosition != Vector3.zero)
                    {
                        Vector3 ballDirection = (mTargetBallPosition - transform.position).normalized;
                        Vector3 enemyDirection = (mTargetEnemyPosition - transform.position).normalized;
                        Vector3 avoidanceVector = Vector3.Cross(enemyDirection, Vector3.up).normalized;
                        Vector3 finalDirection = (ballDirection + avoidanceVector * 0.5f).normalized;
                        
                        Vector3 centerDirection = (Vector3.zero - transform.position).normalized;
                        finalDirection = Vector3.Lerp(finalDirection, centerDirection, centerAttractionForce).normalized;
                        
                        MoveInDirectionSmooth(finalDirection, true);
                    }
                    break;
            }
        }

        private void MoveTowardsTargetSmooth(Vector3 target, bool sprint)
        {
            Vector3 direction = (target - transform.position).normalized;
            
            // Apply boundary avoidance
            direction = ApplyBoundaryAvoidance(direction);
            
            // Add center attraction if far from center
            float distanceFromCenter = Vector3.Distance(transform.position, Vector3.zero);
            if (distanceFromCenter > 15f)
            {
                Vector3 centerDirection = (Vector3.zero - transform.position).normalized;
                direction = Vector3.Lerp(direction, centerDirection, centerAttractionForce).normalized;
            }
            
            MoveInDirectionSmooth(direction, sprint);
        }

        private void MoveInDirectionSmooth(Vector3 direction, bool sprint)
        {
            // Only change direction if enough time has passed or direction change is significant
            float directionChange = Vector3.Angle(mTargetMovementDirection, direction);
            
            if (Time.time - mLastDirectionChange > directionChangeDelay || directionChange > 45f)
            {
                mTargetMovementDirection = direction;
                mLastDirectionChange = Time.time;
            }
            
            bool canSprint = sprint && CanConsumeStamina(0.1f) && mDistanceToEnemy > 3f;
            float speed = canSprint ? GetSprintSpeed() : GetMoveSpeed();
            
            // Use smoothed direction
            moveDirection.Value = new Vector2(mCurrentMovementDirection.x, mCurrentMovementDirection.z) * speed;
            shouldSprint.Value = canSprint;
        }

        private Vector3 ApplyBoundaryAvoidance(Vector3 direction)
        {
            Vector3 pos = transform.position;
            Vector3 avoidanceForce = Vector3.zero;
            
            // Calculate avoidance forces for each boundary
            if (pos.x > worldBoundaryX - cornerAvoidanceDistance)
                avoidanceForce.x = -(pos.x - (worldBoundaryX - cornerAvoidanceDistance));
            else if (pos.x < -(worldBoundaryX - cornerAvoidanceDistance))
                avoidanceForce.x = -(pos.x + (worldBoundaryX - cornerAvoidanceDistance));
                
            if (pos.z > worldBoundaryZ - cornerAvoidanceDistance)
                avoidanceForce.z = -(pos.z - (worldBoundaryZ - cornerAvoidanceDistance));
            else if (pos.z < -(worldBoundaryZ - cornerAvoidanceDistance))
                avoidanceForce.z = -(pos.z + (worldBoundaryZ - cornerAvoidanceDistance));
            
            // Blend original direction with avoidance
            if (avoidanceForce.magnitude > 0.1f)
            {
                avoidanceForce = avoidanceForce.normalized;
                direction = Vector3.Lerp(direction, avoidanceForce, 0.7f).normalized;
            }
            
            return direction;
        }

        private void StopMovement()
        {
            moveDirection.Value = Vector2.zero;
            shouldSprint.Value = false;
        }

        private bool IsEnemyBlockingPath(Vector3 target)
        {
            if (mTargetEnemyPosition == Vector3.zero) return false;
            
            Vector3 pathDirection = (target - transform.position).normalized;
            Vector3 enemyDirection = (mTargetEnemyPosition - transform.position).normalized;
            
            float pathSimilarity = Vector3.Dot(pathDirection, enemyDirection);
            return pathSimilarity > 0.7f && mDistanceToEnemy < Vector3.Distance(transform.position, target);
        }

        #region Character Type Properties
        private float GetMoveSpeed()
        {
            return mCharacterType != null ? mCharacterType.moveSpeed : 5f;
        }

        private float GetSprintSpeed()
        {
            return mCharacterType != null ? mCharacterType.sprintSpeed : 7f;
        }

        private float GetTackleRange()
        {
            return mCharacterType != null ? mCharacterType.tackeTravelDistance : 3f;
        }

        private float GetThrowRange()
        {
            return aiSettings.throwRange;
        }

        private float GetDodgeStaminaCost()
        {
            return 1f;
        }

        private float GetTackleStaminaCost()
        {
            return 1f;
        }

        private float GetParryStaminaCost()
        {
            return 0.5f;
        }
        #endregion

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

        private Vector3 FindClosestBall()
        {
#if UNITY_2022_2_OR_NEWER
            var balls = UnityEngine.Object.FindObjectsByType<Game.Ball.BallController>(FindObjectsSortMode.None);
#else
            var balls = UnityEngine.Object.FindObjectsOfType<Game.Ball.BallController>();
#endif
            
            Vector3 closestPos = Vector3.zero;
            float closestDistance = aiSettings.ballDetectionRange;
            
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

        private PlayerController FindClosestEnemy()
        {
#if UNITY_2022_2_OR_NEWER
            var players = UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
#else
            var players = UnityEngine.Object.FindObjectsOfType<PlayerController>();
#endif
            
            PlayerController closestEnemy = null;
            float closestDistance = aiSettings.enemyDetectionRange;
            
            foreach (var player in players)
            {
                if (player == null || player.gameObject == gameObject) continue;
                
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = player;
                }
            }
            
            return closestEnemy;
        }
    }
}