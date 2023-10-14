using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    public static class Utils
    {
        public static float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        public static float map(float value, float leftMin, float leftMax, float rightMin, float rightMax)
        {
            return rightMin + (value - leftMin) * (rightMax - rightMin) / (leftMax - leftMin);
        }

        public static Quaternion GetLocalRotationRelativeToRootParent(Transform transform)
        {
            if (transform.parent == null)
            {
                return transform.localRotation;
            }
            else
            {
                Quaternion rootParentRotation = Quaternion.identity;
                Transform currentParent = transform.parent;
                while (currentParent.parent != null)
                {
                    rootParentRotation = currentParent.rotation * rootParentRotation;
                    currentParent = currentParent.parent;
                }
                return Quaternion.Inverse(rootParentRotation) * transform.rotation;
            }
        }

        public static float WrapAngle(float angle)
        {
            angle %= 360;
            if (angle > 180)
                return angle - 360;

            return angle;
        }

        public static float UnwrapAngle(float angle)
        {
            if (angle >= 0)
                return angle;

            angle = -angle % 360;

            return 360 - angle;
        }

        public static Vector3 TranslateWithRotation(Vector3 input, Vector3 translation, Quaternion rotation)
        {
            Vector3 rotatedTranslation = rotation * translation;
            Vector3 output = input + rotatedTranslation;
            return output;
        }

        public static Quaternion SmoothDampQuaternion(Quaternion current, Quaternion target, ref Vector3 currentVelocity, float smoothTime)
        {
            Vector3 c = current.eulerAngles;
            Vector3 t = target.eulerAngles;
            return Quaternion.Euler(
              Mathf.SmoothDampAngle(c.x, t.x, ref currentVelocity.x, smoothTime),
              Mathf.SmoothDampAngle(c.y, t.y, ref currentVelocity.y, smoothTime),
              Mathf.SmoothDampAngle(c.z, t.z, ref currentVelocity.z, smoothTime)
            );
        }

        public static Quaternion EnsureQuaternionContinuity(Quaternion last, Quaternion curr)
        {
            if (last.x * curr.x + last.y * curr.y + last.z * curr.z + last.w * curr.w < 0f)
            {
                return new Quaternion(-curr.x, -curr.y, -curr.z, -curr.w);
            }
            return curr;
        }

        public static string[] hands_parts = new string[] {
            "Skater_index_01_l",
            "Skater_index_02_l",
            "Skater_index_03_l",
            "Skater_middle_01_l",
            "Skater_middle_02_l",
            "Skater_middle_03_l",
            "Skater_pinky_01_l",
            "Skater_pinky_02_l",
            "Skater_pinky_03_l",
            "Skater_ring_01_l",
            "Skater_ring_02_l",
            "Skater_ring_03_l",
            "Skater_thumb_01_l",
            "Skater_thumb_02_l",
            "Skater_thumb_03_l",
            "Skater_index_01_r",
            "Skater_index_02_r",
            "Skater_index_03_r",
            "Skater_middle_01_r",
            "Skater_middle_02_r",
            "Skater_middle_03_r",
            "Skater_pinky_01_r",
            "Skater_pinky_02_r",
            "Skater_pinky_03_r",
            "Skater_ring_01_r",
            "Skater_ring_02_r",
            "Skater_ring_03_r",
            "Skater_thumb_01_r",
            "Skater_thumb_02_r",
            "Skater_thumb_03_r"
        };

        public static void Log(object log)
        {
            UnityModManager.Logger.Log("[walking-mod] " + log.ToString());
        }
    }
}
