using UnityEngine;

namespace RooseLabs.Enemies
{
    public class InvestigateState : IEnemyState
    {
        private readonly HanaduraAI owner;
        private float timer;
        private readonly float investigateDuration = 5f;   // how long to stay alert
        private readonly float stopDistance = 1.5f;        // distance to the investigated spot
        private Vector3 investigatePoint;

        public InvestigateState(HanaduraAI owner)
        {
            this.owner = owner;
        }

        public void Enter()
        {
            // pick the point we were told about
            if (owner.LastKnownTargetPosition.HasValue)
                investigatePoint = owner.LastKnownTargetPosition.Value;
            else
                investigatePoint = owner.transform.position;

            timer = investigateDuration;
            owner.navAgent.isStopped = false;
            owner.navAgent.SetDestination(investigatePoint);

            Debug.Log($"[HanaduraAI] Investigating area: {investigatePoint}");
        }

        public void Exit()
        {
            owner.SetIsInvestigatingFlag();
        }

        public void Tick()
        {
            // if see target -> chase
            if (owner.detection.DetectedTarget != null)
            {
                owner.SetCurrentTarget(owner.detection.DetectedTarget);
                owner.EnterState(owner.ChaseState);
                return;
            }

            // move to investigation point
            if (!owner.navAgent.pathPending && owner.navAgent.remainingDistance <= stopDistance)
            {
                // look around for a while
                timer -= Time.deltaTime;
                owner.transform.Rotate(Vector3.up, 45f * Time.deltaTime);
            }

            // done investigating -> go back to patrol
            if (timer <= 0f)
            {
                owner.EnterState(owner.PatrolState);
            }
        }
    }
}
