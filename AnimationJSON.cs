namespace walking_mod
{   
    public class AnimationJSON
    {
        public float duration;
        public float[] times;
        public AnimationJSONParts parts;

        public AnimationJSON(float duration, float[] times, AnimationJSONParts parts)
        {
            this.duration = duration;
            this.times = times;
            this.parts = parts;

            if (this.times.Length == 0) this.times = new float[] { duration - .02f };
        }

        public override string ToString()
        {
            return this.duration + " " + this.times.Length;
        }
    }

    public class AnimationJSONParts
    {
        public AnimationJSONPart Skater_pelvis { get; set; }
        public AnimationJSONPart LeftLegJacket { get; set; }
        public AnimationJSONPart LeftLegJacket_dist { get; set; }
        public AnimationJSONPart RightLegJacket { get; set; }
        public AnimationJSONPart RightLegJacket_dist { get; set; }
        public AnimationJSONPart Skater_Spine { get; set; }
        public AnimationJSONPart Skater_Spine1 { get; set; }
        public AnimationJSONPart Skater_Spine2 { get; set; }
        public AnimationJSONPart Skater_Neck { get; set; }
        public AnimationJSONPart Skater_Head { get; set; }
        public AnimationJSONPart Skater_eye_l { get; set; }
        public AnimationJSONPart Skater_eye_r { get; set; }
        public AnimationJSONPart Skater_Shoulder_l { get; set; }
        public AnimationJSONPart Skater_Arm_l { get; set; }
        public AnimationJSONPart Skater_Arm_twist_01_l { get; set; }
        public AnimationJSONPart Skater_ForeArm_l { get; set; }
        public AnimationJSONPart Skater_ForeArm_twist_01_l { get; set; }
        public AnimationJSONPart Skater_hand_l { get; set; }
        public AnimationJSONPart Skater_index_01_l { get; set; }
        public AnimationJSONPart Skater_index_02_l { get; set; }
        public AnimationJSONPart Skater_index_03_l { get; set; }
        public AnimationJSONPart Skater_middle_01_l { get; set; }
        public AnimationJSONPart Skater_middle_02_l { get; set; }
        public AnimationJSONPart Skater_middle_03_l { get; set; }
        public AnimationJSONPart Skater_pinky_01_l { get; set; }
        public AnimationJSONPart Skater_pinky_02_l { get; set; }
        public AnimationJSONPart Skater_pinky_03_l { get; set; }
        public AnimationJSONPart Skater_ring_01_l { get; set; }
        public AnimationJSONPart Skater_ring_02_l { get; set; }
        public AnimationJSONPart Skater_ring_03_l { get; set; }
        public AnimationJSONPart Skater_thumb_01_l { get; set; }
        public AnimationJSONPart Skater_thumb_02_l { get; set; }
        public AnimationJSONPart Skater_thumb_03_l { get; set; }
        public AnimationJSONPart Skater_Shoulder_r { get; set; }
        public AnimationJSONPart Skater_Arm_r { get; set; }
        public AnimationJSONPart Skater_Arm_twist_01_r { get; set; }
        public AnimationJSONPart Skater_ForeArm_r { get; set; }
        public AnimationJSONPart Skater_ForeArm_twist_01_r { get; set; }
        public AnimationJSONPart Skater_hand_r { get; set; }
        public AnimationJSONPart Skater_index_01_r { get; set; }
        public AnimationJSONPart Skater_index_02_r { get; set; }
        public AnimationJSONPart Skater_index_03_r { get; set; }
        public AnimationJSONPart Skater_middle_01_r { get; set; }
        public AnimationJSONPart Skater_middle_02_r { get; set; }
        public AnimationJSONPart Skater_middle_03_r { get; set; }
        public AnimationJSONPart Skater_pinky_01_r { get; set; }
        public AnimationJSONPart Skater_pinky_02_r { get; set; }
        public AnimationJSONPart Skater_pinky_03_r { get; set; }
        public AnimationJSONPart Skater_ring_01_r { get; set; }
        public AnimationJSONPart Skater_ring_02_r { get; set; }
        public AnimationJSONPart Skater_ring_03_r { get; set; }
        public AnimationJSONPart Skater_thumb_01_r { get; set; }
        public AnimationJSONPart Skater_thumb_02_r { get; set; }
        public AnimationJSONPart Skater_thumb_03_r { get; set; }
        public AnimationJSONPart Skater_UpLeg_l { get; set; }
        public AnimationJSONPart Skater_Leg_l { get; set; }
        public AnimationJSONPart Skater_foot_l { get; set; }
        public AnimationJSONPart Skater_Toe1_l { get; set; }
        public AnimationJSONPart Skater_Toe2_l { get; set; }
        public AnimationJSONPart Skater_Leg_twist_01_l { get; set; }
        public AnimationJSONPart Skater_UpLeg_twist_01_l { get; set; }
        public AnimationJSONPart Skater_UpLeg_r { get; set; }
        public AnimationJSONPart Skater_Leg_r { get; set; }
        public AnimationJSONPart Skater_foot_r { get; set; }
        public AnimationJSONPart Skater_Toe1_r { get; set; }
        public AnimationJSONPart Skater_Toe2_r { get; set; }
        public AnimationJSONPart Skater_Leg_twist_01_r { get; set; }
        public AnimationJSONPart Skater_UpLeg_twist_01_r { get; set; }
    }

    public class AnimationJSONPart
    {
        public float[][] position, quaternion;

        public AnimationJSONPart(float[][] position, float[][] quaternion)
        {
            this.position = position;
            this.quaternion = quaternion;
        }

        public override string ToString()
        {
            return this.position.Length + " " + this.quaternion.Length;
        }
    }
}