using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Enemies
{
    public class AttackState : IEnemyState
    {
        private HanaduraAI ai;
        private bool hasAttacked = false;
        private float rotationSpeed = 8f; 
        private float minRotationThreshold = 5f;

        public AttackState(HanaduraAI ai)
        {
            this.ai = ai;
        }

        public void Enter()
        {
            ai.StopMovement();
            ai.SetAnimatorBool("IsChasing", false);
            ai.SetAnimatorBool("IsLookingAround", false);
            hasAttacked = false;
        }

        public void Exit()
        {
            if (ai.weaponCollider != null)
            {
                ai.weaponCollider.DisableWeapon();
            }

            if (ai.modelTransform != null)
            {
                ai.modelTransform.localRotation = Quaternion.Slerp(
                    ai.modelTransform.localRotation,
                    Quaternion.identity,
                    Time.deltaTime * rotationSpeed
                );
            }
        }

        public void Tick()
        {
            if (ai.CurrentTarget == null) return;

            if (!ai.navAgent.isStopped)
            {
                ai.StopMovement();
            }

            Vector3 horizontalDir = (ai.CurrentTarget.position - ai.transform.position);
            horizontalDir.y = 0f;

            if (horizontalDir.sqrMagnitude > 0.001f)
            {
                Quaternion horizontalLook = Quaternion.LookRotation(horizontalDir);
                ai.transform.rotation = Quaternion.Slerp(
                    ai.transform.rotation,
                    horizontalLook,
                    Time.deltaTime * 10f
                );
            }

            Quaternion targetLocalRotation = Quaternion.identity;
            bool isAimedAtTarget = false;

            if (ai.modelTransform != null)
            {
                Vector3 attackOrigin = ai.RaycastOrigin.position;
                Vector3 targetCenter = ai.CurrentTarget.GetComponentInParent<PlayerCharacter>().RaycastTarget.position + Vector3.up * 1.5f;
                Vector3 worldDirection = (targetCenter - attackOrigin).normalized;
                Vector3 localDirection = ai.transform.InverseTransformDirection(worldDirection);

                if (localDirection.sqrMagnitude > 0.001f)
                {
                    targetLocalRotation = Quaternion.LookRotation(localDirection);

                    ai.modelTransform.localRotation = Quaternion.Slerp(
                        ai.modelTransform.localRotation,
                        targetLocalRotation,
                        Time.deltaTime * rotationSpeed
                    );

                    float rotationDifference = Quaternion.Angle(ai.modelTransform.localRotation, targetLocalRotation);
                    isAimedAtTarget = rotationDifference < minRotationThreshold;
                }
            }

            if (isAimedAtTarget && ai.TryPerformAttack())
            {
                if (!hasAttacked)
                {
                    ai.SetAnimatorTrigger("Attack");
                    hasAttacked = true;
                }
            }
            else
            {
                hasAttacked = false;
            }
        }
    }
}