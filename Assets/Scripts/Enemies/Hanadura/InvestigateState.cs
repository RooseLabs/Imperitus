using UnityEngine;
using DebugManager = RooseLabs.Utils.DebugManager;

namespace RooseLabs.Enemies
{
    public class InvestigateState : IEnemyState
    {
        private readonly HanaduraAI owner;
        private float investigateTimer;
        private readonly float investigateDuration = 6f;   // How long to investigate/look around
        private readonly float stopDistance = 1.5f;        // Distance to investigated spot
        private readonly float rotationSpeed = 45f;        // Degrees per second when looking around
        private Vector3 investigatePoint;
        private bool hasReachedPoint = false;

        public InvestigateState(HanaduraAI owner)
        {
            this.owner = owner;
        }

        public void Enter()
        {
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
            // Clean up - the priority system now handles state transitions
            DebugManager.Log("[InvestigateState] Finished investigating.");
        }

        public void Tick()
        {
            // The priority system will automatically switch to chase if visual detection occurs
            // No need to manually check detection here

            // Check if reached investigation point
            if (!hasReachedPoint)
            {
                if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= stopDistance)
                {
                    hasReachedPoint = true;
                    owner.StopMovement();
                    DebugManager.Log("[InvestigateState] Reached investigation point, looking around...");
                }
            }
            else
            {
                // Look around at the investigation point
                investigateTimer -= Time.deltaTime;
                owner.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }

            // If investigation timer expires, this will be handled by detection expiry
            // The priority system will automatically transition to patrol
            if (investigateTimer <= 0f)
            {
                DebugManager.Log("[InvestigateState] Investigation timer expired.");
                // Note: The HanaduraAI's detection system will handle the transition
                // when the detection becomes stale
            }
        }
    }
}
