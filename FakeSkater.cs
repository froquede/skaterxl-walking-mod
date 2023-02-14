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
        public GameObject self;
        public bool visible = false;
        public Rigidbody rb;
        public CapsuleCollider collider;

        public void Create()
        {
            if (PlayerController.Instance.skaterController.skaterTransform.gameObject != null)
            {
                self = Instantiate(PlayerController.Instance.skaterController.skaterTransform.gameObject);
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

                collider = self.AddComponent<CapsuleCollider>();
                collider.height = 1.4404f;
                collider.radius = .2f;
                collider.material.dynamicFriction = .225f;
                collider.material.staticFriction = .6f;

                cache = new Dictionary<string, Transform>();
            }
        }

        Transform pelvis, spine, spine1, spine2, neck, head, left_arm, left_forearm, left_hand, right_arm, right_forearm, right_hand, left_upleg, left_leg, left_foot, right_upleg, right_leg, right_foot;
        Transform right_shoulder, left_shoulder, left_toe_1, left_toe_2, right_toe_1, right_toe_2;
        void getParts()
        {
            Transform joints = self.transform.Find("Skater_Joints");

            pelvis = joints.FindChildRecursively("Skater_pelvis");
            spine = joints.FindChildRecursively("Skater_Spine");
            spine1 = joints.FindChildRecursively("Skater_Spine1");
            spine2 = joints.FindChildRecursively("Skater_Spine2");
            neck = joints.FindChildRecursively("Skater_Neck");
            head = joints.FindChildRecursively("Skater_Head");
            left_arm = joints.FindChildRecursively("Skater_Arm_l");
            left_forearm = joints.FindChildRecursively("Skater_ForeArm_l");
            left_hand = joints.FindChildRecursively("Skater_hand_l");
            right_arm = joints.FindChildRecursively("Skater_Arm_r");
            right_forearm = joints.FindChildRecursively("Skater_ForeArm_r");
            right_hand = joints.FindChildRecursively("Skater_hand_r");
            left_upleg = joints.FindChildRecursively("Skater_UpLeg_l");
            left_leg = joints.FindChildRecursively("Skater_Leg_l");
            left_foot = joints.FindChildRecursively("Skater_foot_l");
            right_upleg = joints.FindChildRecursively("Skater_UpLeg_r");
            right_leg = joints.FindChildRecursively("Skater_Leg_r");
            right_foot = joints.FindChildRecursively("Skater_foot_r");
            right_shoulder = joints.FindChildRecursively("Skater_Shoulder_r");
            left_shoulder = joints.FindChildRecursively("Skater_Shoulder_l");
            left_toe_1 = joints.FindChildRecursively("Skater_Toe1_l");
            left_toe_2 = joints.FindChildRecursively("Skater_Toe2_l");
            right_toe_1 = joints.FindChildRecursively("Skater_Toe1_r");
            right_toe_2 = joints.FindChildRecursively("Skater_Toe2_r");
        }

        public IDictionary<string, Transform> cache;
        public Transform getPart(string id)
        {
            Transform joints = self.transform.Find("Skater_Joints");
            if (!cache.ContainsKey(id))
            {
                Transform joint = joints.FindChildRecursively(id);
                //joint.gameObject.AddComponent<TransformTracker>();
                cache.Add(id, joint);
            }
            return cache[id];
        }

        /*public Transform getPart(string id)
        {
            //if (id == "Hips") return pelvis;
            if (id == "Spine") return pelvis;
            if (id == "Spine1") return spine1;
            if (id == "Spine2") return spine2;
            if (id == "Head") return neck;
            if (id == "HeadTop_End") return head;
            if (id == "LeftArm") return left_arm;
            if (id == "LeftForeArm") return left_forearm;
            if (id == "LeftHand") return left_hand;
            if (id == "RightArm") return right_arm;
            if (id == "RightForeArm") return right_forearm;
            if (id == "RightHand") return right_hand;
            if (id == "LeftUpLeg") return left_upleg;
            if (id == "LeftLeg") return left_leg;
            //if (id == "LeftFoot") return left_foot;
            if (id == "RightUpLeg") return right_upleg;
            if (id == "RightLeg") return right_leg;
            //if (id == "RightFoot") return right_foot;

            if (id == "RightShoulder") return right_shoulder;
            if (id == "LeftShoulder") return left_shoulder;

            if (id == "LeftToeBase") return left_foot;
            if (id == "LeftToe_End") return left_toe_1;
            if (id == "RightToeBase") return right_foot;
            if (id == "RightToe_End") return right_toe_1;

            return null;
        }*/

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
