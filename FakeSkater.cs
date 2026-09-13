using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RealisticEyeMovements;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    public class FakeSkater : MonoBehaviour
    {
        public string[] bones = new string[] { "Skater_pelvis", "LeftLegJacket", "LeftLegJacket_dist", "RightLegJacket", "RightLegJacket_dist", "Skater_Spine", "Skater_Spine1", "Skater_Spine2", "Skater_Neck", "Skater_Head", "Skater_eye_l", "Skater_eye_r", "Skater_Shoulder_l", "Skater_Arm_l", "Skater_Arm_twist_01_l", "Skater_ForeArm_l", "Skater_ForeArm_twist_01_l", "Skater_hand_l", "Skater_index_01_l", "Skater_index_02_l", "Skater_index_03_l", "Skater_middle_01_l", "Skater_middle_02_l", "Skater_middle_03_l", "Skater_pinky_01_l", "Skater_pinky_02_l", "Skater_pinky_03_l", "Skater_ring_01_l", "Skater_ring_02_l", "Skater_ring_03_l", "Skater_thumb_01_l", "Skater_thumb_02_l", "Skater_thumb_03_l", "Skater_Shoulder_r", "Skater_Arm_r", "Skater_Arm_twist_01_r", "Skater_ForeArm_r", "Skater_ForeArm_twist_01_r", "Skater_hand_r", "Skater_index_01_r", "Skater_index_02_r", "Skater_index_03_r", "Skater_middle_01_r", "Skater_middle_02_r", "Skater_middle_03_r", "Skater_pinky_01_r", "Skater_pinky_02_r", "Skater_pinky_03_r", "Skater_ring_01_r", "Skater_ring_02_r", "Skater_ring_03_r", "Skater_thumb_01_r", "Skater_thumb_02_r", "Skater_thumb_03_r", "Skater_UpLeg_l", "Skater_Leg_l", "Skater_foot_l", "Skater_Toe1_l", "Skater_Toe2_l", "Skater_Leg_twist_01_l", "Skater_UpLeg_twist_01_l", "Skater_UpLeg_r", "Skater_Leg_r", "Skater_foot_r", "Skater_Toe1_r", "Skater_Toe2_r", "Skater_Leg_twist_01_r", "Skater_UpLeg_twist_01_r" };
        public string[] left_hand = new string[] { "Skater_index_01_l", "Skater_index_02_l", "Skater_index_03_l", "Skater_middle_01_l", "Skater_middle_02_l", "Skater_middle_03_l", "Skater_pinky_01_l", "Skater_pinky_02_l", "Skater_pinky_03_l", "Skater_ring_01_l", "Skater_ring_02_l", "Skater_ring_03_l" };
        public string[] right_hand = new string[] { "Skater_index_01_r", "Skater_index_02_r", "Skater_index_03_r", "Skater_middle_01_r", "Skater_middle_02_r", "Skater_middle_03_r", "Skater_pinky_01_r", "Skater_pinky_02_r", "Skater_pinky_03_r", "Skater_ring_01_r", "Skater_ring_02_r", "Skater_ring_03_r" };
        public GameObject self;
        public bool visible = false;
        public Rigidbody rb;
        public CapsuleCollider collider;

        public void Create(Vector3 pos)
        {
            if (PlayerController.Instance.skaterController.skaterTransform.gameObject != null)
            {
                self = Instantiate(PlayerController.Instance.skaterController.skaterTransform.gameObject);
                self.transform.position = pos;
                self.name = "FakeSkater";

                Destroy(self.GetComponent<Animator>());
                Destroy(self.GetComponent<Rigidbody>());
                Destroy(self.GetComponent<SkaterController>());
                Destroy(self.GetComponent<AnimationController>());
                Destroy(self.GetComponent<CoMDisplacement>());
                Destroy(self.GetComponent<Respawn>());
                Destroy(self.GetComponent<IKController>());
                Destroy(self.GetComponent<Bail>());
                Destroy(self.GetComponent<HeadIK>());
                Destroy(self.GetComponent<GestureAnimationController>());
                Destroy(self.GetComponent<FullBodyBipedIK>());
                Destroy(self.GetComponent<LookAtIK>());
                Destroy(self.GetComponent<EyeAndHeadAnimator>());
                Destroy(self.GetComponent<LookTargetController>());
                Destroy(self.GetComponent<CapsuleCollider>());

                Destroy(self.transform.Find("NewSteezeIK").gameObject);
                Destroy(self.transform.Find("Armature").gameObject);
                Destroy(self.transform.Find("Deck").gameObject);
                Destroy(self.transform.Find("Board Control").gameObject);
                Destroy(self.transform.Find("Front Wheels").gameObject);
                Destroy(self.transform.Find("Back Wheels").gameObject);
                Destroy(self.transform.Find("Displacement Offset").gameObject);
                Destroy(self.transform.Find("Left Knee Target").gameObject);
                Destroy(self.transform.Find("Right Knee Target").gameObject);
                Destroy(self.transform.Find("LookTargets").gameObject);
                Destroy(self.transform.Find("TutorialStickArrows").gameObject);
                Destroy(self.transform.Find("Original Camera Position").gameObject);
                Destroy(self.transform.Find("SecondaryDeformers").gameObject);
                Destroy(self.transform.Find("ColliderActivationTrigger").gameObject);
                Destroy(self.transform.Find("Original Camera Position").gameObject);

                // any collider left on the cloned bones would move with the animation inside the body's rigidbody and shove it around
                foreach (Collider child in self.GetComponentsInChildren<Collider>(true)) child.enabled = false;

                collider = self.AddComponent<CapsuleCollider>();
                collider.height = 1.4404f;
                collider.radius = .2f;

                cache = new Dictionary<string, Transform>();
                joints = null;
                boneTransforms = null;
                left_hand_set = new HashSet<string>(left_hand);
                right_hand_set = new HashSet<string>(right_hand);
            }
        }

        public IDictionary<string, Transform> cache;
        Transform joints;
        Transform[] boneTransforms;
        public HashSet<string> left_hand_set, right_hand_set;

        public Transform getPart(string id)
        {
            Transform joint;
            if (!cache.TryGetValue(id, out joint))
            {
                if (joints == null) joints = self.transform.Find("Skater_Joints");
                joint = joints.FindChildRecursively(id);
                //joint.gameObject.AddComponent<TransformTracker>();
                cache.Add(id, joint);
            }
            return joint;
        }

        // Transforms in bones order, resolved once per fake skater instance
        public Transform getBone(int index)
        {
            if (boneTransforms == null)
            {
                boneTransforms = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++) boneTransforms[i] = getPart(bones[i]);
            }
            return boneTransforms[index];
        }

        public void show()
        {
            visible = true;
            self.SetActive(true);
        }

        public void hide()
        {
            visible = false;
            self.SetActive(false);
        }
    }
}
