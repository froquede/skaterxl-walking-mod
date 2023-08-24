using Rewired;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace walking_mod
{
    public class GolfController : MonoBehaviour
    {
        private Rigidbody rb;
        float LX, LY, RX, RY;
        void UpdateSticks()
        {
            LX = PlayerController.Instance.inputController.player.GetAxis(19);
            LY = PlayerController.Instance.inputController.player.GetAxis(20);
            RX = PlayerController.Instance.inputController.player.GetAxis(21);
            RY = PlayerController.Instance.inputController.player.GetAxis(22);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 0.04593f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            PhysicMaterial ball = new PhysicMaterial();
            ball.bounciness = 1f;
            ball.dynamicFriction = ball.staticFriction = .6f;
            gameObject.AddComponent<LineRenderer>();
            gameObject.GetComponent<MeshRenderer>().material = new Material(Shader.Find("HDRP/Lit"));

            gameObject.AddComponent<GolfTrailController>();
        }

        public float hitForceMagnitude = 1f;
        public float upwardForceMagnitude = .5f;
        bool hasAppliedForce = false;
        public void Update()
        {
            UpdateSticks();

            if (Main.walking_go.GetButtonDown("A"))
            {
                if (!hasAppliedForce)
                {
                    Vector3 hitDirection = Main.walking_go.fallbackCamera.transform.forward.normalized;
                    Vector3 upwardComponent = Main.walking_go.fallbackCamera.transform.up.normalized;
                    hitDirection += upwardComponent * upwardForceMagnitude;
                    hitDirection.Normalize();
                    rb.AddForce(hitDirection * hitForceMagnitude, ForceMode.Impulse);
                    hasAppliedForce = true;
                }
            }
            else hasAppliedForce = false;
        }
    }
}
