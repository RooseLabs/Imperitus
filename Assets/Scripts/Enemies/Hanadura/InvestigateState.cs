using UnityEngine;
using DebugManager = RooseLabs.Utils.DebugManager;

namespace RooseLabs.Enemies
{
    public class InvestigateState : IEnemyState
    {
        private readonly HanaduraAI owner;
        private float investigateTimer;
        private readonly float investigateDuration = 6f;
        private readonly float stopDistance = 1.5f;
        private readonly float rotationSpeed = 45f;
        private Vector3 investigatePoint;
        private bool hasReachedPoint = false;
        private bool investigationComplete = false;

        public bool IsInvestigationComplete => investigationComplete;

        public InvestigateState(HanaduraAI owner)
        {
            this.owner = owner;
        }

        public void Enter()
        {
            owner.SetAnimatorBool("IsChasing", false);
            owner.SetAnimatorBool("IsLookingAround", false);
            investigationComplete = false;

            // Get the investigation point
            if (owner.LastKnownTargetPosition.HasValue)
                investigatePoint = owner.LastKnownTargetPosition.Value;
            else
                investigatePoint = owner.transform.position;

            investigateTimer = investigateDuration;
            hasReachedPoint = false;

            // Start moving to the investigation point
            owner.navAgent.isStopped = false;
            owner.navAgent.SetDestination(investigatePoint);
            DebugManager.Log($"[InvestigateState] Starting investigation at {investigatePoint}");
        }

        public void Exit()
        {
            owner.SetAnimatorBool("IsLookingAround", false);
            DebugManager.Log("[InvestigateState] Finished investigating.");
        }

        public void Tick()
        {
            // Check if reached investigation point
            if (!hasReachedPoint)
            {
                if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= stopDistance)
                {
                    hasReachedPoint = true;
                    owner.StopMovement();
                    owner.SetAnimatorBool("IsLookingAround", true);
                    DebugManager.Log("[InvestigateState] Reached investigation point, looking around...");
                }
            }
            else
            {
                // Look around at the investigation point
                investigateTimer -= Time.deltaTime;
                owner.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

                if (investigateTimer <= 0f && !investigationComplete)
                {
                    investigationComplete = true;
                    DebugManager.Log("[InvestigateState] Investigation timer expired - ready to transition.");
                }
            }
        }
    }
}