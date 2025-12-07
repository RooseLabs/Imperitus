using UnityEngine;

namespace RooseLabs.Player
{
    public class Bodypart
    {
        public HumanBodyBones Type { get; private set; }
        public Transform Transform { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public Collider Collider { get; private set; }

        public Bodypart(HumanBodyBones type, Transform transform)
        {
            Type = type;
            Transform = transform;
            Rigidbody = transform.GetComponent<Rigidbody>();
            Collider = transform.GetComponent<Collider>();
        }

        public Vector3 Position => Transform.position;
    }
}
