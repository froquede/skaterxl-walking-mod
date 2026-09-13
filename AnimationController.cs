using ModIO.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    public delegate void CallBack();

    public class AnimController
    {
        public bool isPlaying = false;

        public AnimationJSON animation;
        FakeSkater fs;
        public string path = "";
        public Vector3 offset = new Vector3(0, -.73f, 0);
        public Quaternion rotation_offset = Quaternion.Euler(0, 0, 0), original_rotation = Quaternion.identity;
        CallBack callback;
        bool loop = true;
        public bool anchorRoot = false, doCrossfade = true, offsetPelvis = false;
        public float speed = 1f;
        public float timeLimit = 0f, timeLimitStart = 0f;

        float animTime = 0f;
        public int frame = 0, last_frame = 0, crossfade = 0;
        public float count = 0;
        Vector3 first_frame_pelvis;
        public bool anchorRootFade = true;
        public float anchorRootSpeed = 12f;
        public bool skate_animation = false;
        public int mag_start = -1, mag_end = -1;
        // forced closed hands, the hand holding the board closes on its own
        public bool right_hand_closed = false, left_hand_closed = false;

        public AnimController(AnimController origin)
        {
            animation = origin.animation;
            fs = origin.fs;
            path = origin.path;
            offset = origin.offset;
            rotation_offset = origin.rotation_offset;
            callback = origin.callback;
            loop = origin.loop;
            anchorRoot = origin.anchorRoot;
            doCrossfade = origin.doCrossfade;
            speed = origin.speed;
            timeLimit = origin.timeLimit;
            animTime = origin.animTime;
            frame = origin.frame;
            last_frame = origin.last_frame;
            count = origin.count;
            crossfade = origin.crossfade;
            first_frame_pelvis = origin.first_frame_pelvis;
            anchorRootFade = origin.anchorRootFade;
            anchorRootSpeed = origin.anchorRootSpeed;
            offsetPelvis = origin.offsetPelvis;
            skate_animation = origin.skate_animation;
            right_hand_closed = origin.right_hand_closed;
            left_hand_closed = origin.left_hand_closed;
        }

        public AnimController()
        {
            this.animation_name = "NotSet";
        }

        public AnimController(string path, FakeSkater fs)
        {
            this.path = path;
            this.fs = fs;

            LoadJSON();
        }

        public AnimController(string path, FakeSkater fs, Quaternion rotation_offset)
        {
            this.path = path;
            this.fs = fs;
            this.rotation_offset = rotation_offset;
            this.original_rotation = rotation_offset;

            LoadJSON();
        }

        public AnimController(string path, FakeSkater fs, bool loop)
        {
            this.path = path;
            this.fs = fs;
            this.loop = loop;

            LoadJSON();
        }

        public AnimController(string path, FakeSkater fs, bool loop, bool anchorRoot)
        {
            this.path = path;
            this.fs = fs;
            this.loop = loop;
            this.anchorRoot = anchorRoot;

            LoadJSON();
        }

        public AnimController(string path, FakeSkater fs, bool loop, int crossfade)
        {
            this.path = path;
            this.fs = fs;
            this.loop = loop;
            this.crossfade = crossfade;

            LoadJSON();
        }

        string animation_name;
        public string name
        {
            get
            {
                if (animation_name == null)
                {
                    string[] pieces = this.path.Split(Path.DirectorySeparatorChar);
                    animation_name = pieces[pieces.Length - 1].Replace(".json", String.Empty);
                }

                return animation_name;
            }
        }

        async void LoadJSON()
        {
            string json;
            if (File.Exists(path))
            {
                using (var reader = File.OpenText(path))
                {
                    json = await reader.ReadToEndAsync();
                }
            }
            else
            {
                MessageSystem.QueueMessage(MessageDisplayData.Type.Error, "Error loading animation '" + path + "', file doesn't exist", 3f);
                return;
            }

            JObject json_parsed = JObject.Parse(json);
            AnimationJSONParts parts = new AnimationJSONParts();

            foreach (string part in fs.bones)
            {
                try
                {
                    Type type = typeof(AnimationJSONParts);
                    var property = type.GetProperty(part);
                    AnimationJSONPart new_part = new AnimationJSONPart(JsonConvert.DeserializeObject<float[][]>(json_parsed["parts"][part]["position"].ToString()), JsonConvert.DeserializeObject<float[][]>(json_parsed["parts"][part]["quaternion"].ToString()));
                    property.SetValue(parts, new_part);
                }
                catch (Exception e)
                {
                    MessageSystem.QueueMessage(MessageDisplayData.Type.Error, "Error loading animation '" + name + "', file malformed | " + e.Message, 3f);
                }
            }

            if (json_parsed["skate_animation"] != null)
            {
                if ((bool)json_parsed["skate_animation"])
                {
                    string part = "Skate";
                    try
                    {
                        Type type = typeof(AnimationJSONParts);
                        var property = type.GetProperty(part);
                        AnimationJSONPart new_part = new AnimationJSONPart(JsonConvert.DeserializeObject<float[][]>(json_parsed["parts"][part]["position"].ToString()), JsonConvert.DeserializeObject<float[][]>(json_parsed["parts"][part]["quaternion"].ToString()));
                        property.SetValue(parts, new_part);
                        skate_animation = true;
                        mag_start = (int)json_parsed["mag_start"];
                        mag_end = (int)json_parsed["mag_end"];
                    } catch { }
                }
            }

            try { anchorRoot = json_parsed["anchorRoot"] == null ? anchorRoot : (bool)json_parsed["anchorRoot"]; }
            catch { }

            try { offsetPelvis = json_parsed["offset_pelvis"] == null ? offsetPelvis : (bool)json_parsed["offset_pelvis"]; }
            catch { }

            animation = new AnimationJSON((float)json_parsed["duration"], Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(json_parsed["times"].ToString()), parts);
            animation.ResolveBones(fs.bones);
            Utils.Log("Loaded animation: " + animation.ToString() + " " + name);

            AnimationJSONPart pelvis = animation.parts.Skater_pelvis;
            first_frame_pelvis = new Vector3(pelvis.position[0][0], pelvis.position[0][1], pelvis.position[0][2]);

            if (timeLimit == 0f) timeLimit = animation.duration;
            if (timeLimitStart == 0f) timeLimitStart = animation.times[0];
        }

        public void Update()
        {
            if (fs.self && isPlaying && animation != null && animation.boneParts != null)
            {
                float frameScale = Utils.FrameScale();
                bool interpolateActual = false;
                float[] times = animation.times;
                int last = times.Length - 1;

                if (Main.walking_go.last_animation == null) Main.walking_go.last_animation = new AnimController(this);

                // crossfade lengths are in 60 fps frames
                int d_crossfade = (Main.walking_go.last_animation.name != name && doCrossfade) ? 9 : doCrossfade ? crossfade : 0;
                if (Time.unscaledTime - Main.walking_go.enterBailTimestamp <= Time.deltaTime * 2f && Main.walking_go.enterFromBail)
                {
                    interpolateActual = true;
                    d_crossfade = 12;
                }
                bool crossfading = count < d_crossfade;

                // keyframes around animTime, sampled by time so the pose doesn't depend on the frame rate
                int a, b;
                float t;
                SampleKeys(times, out a, out b, out t);

                AnimationJSONPart pelvis = animation.parts.Skater_pelvis;
                if (anchorRoot && pelvis != null)
                {
                    Vector3 pelvis_pos = Vector3.Lerp(pelvis.positions[a], pelvis.positions[b], t);
                    Vector3 delta = -(pelvis_pos - pelvis.positions[0]);
                    Vector3 target_offset = new Vector3(delta.x, -(fs.collider.height / 2) + delta.y, delta.z);
                    offset = !anchorRootFade ? target_offset : Vector3.Lerp(offset, target_offset, Utils.FrameIndependentLerp((1f / 60f) * 24f));
                }

                // crossfade source: the previous animation's last shown key, or the current pose if there's none
                AnimController source = Main.walking_go.last_animation;
                AnimationJSON source_anim = crossfading && source != null ? source.animation : null;
                int source_key = 0;
                if (source_anim != null && source_anim.boneParts != null) source_key = Mathf.Clamp(source.last_frame, 0, source_anim.times.Length - 1);
                else source_anim = null;

                float blend = d_crossfade > 0 ? Mathf.Clamp01((count + frameScale) / d_crossfade) : 1f;

                Quaternion baseRotation = rotation_offset * fs.self.transform.rotation;
                Vector3 basePosition = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation);

                bool holdingBoard = Main.walking_go.magnetized && !skate_animation;
                bool closeLeft = left_hand_closed || (holdingBoard && Main.settings.left_arm);
                bool closeRight = right_hand_closed || (holdingBoard && !Main.settings.left_arm);

                for (int i = 0; i < fs.bones.Length; i++)
                {
                    Transform tpart = fs.getBone(i);
                    AnimationJSONPart apart = animation.boneParts[i];
                    if (!tpart || apart == null) continue;

                    try
                    {
                        Vector3 target_pos = TranslateWithRotation(basePosition, Vector3.Lerp(apart.positions[a], apart.positions[b], t), baseRotation);
                        Quaternion target_rot = baseRotation * Quaternion.Slerp(apart.rotations[a], apart.rotations[b], t);
                        if (!isValidMatrix(target_pos, target_rot)) continue;

                        if (interpolateActual)
                        {
                            float step = Mathf.Clamp01(Time.deltaTime * Vector3.Distance(tpart.position, target_pos) * 60f);
                            tpart.position = Vector3.Lerp(tpart.position, target_pos, step);
                            tpart.rotation = Quaternion.Slerp(tpart.rotation, target_rot, step);
                        }
                        else if (crossfading && blend < 1f)
                        {
                            Vector3 from_pos = tpart.position;
                            Quaternion from_rot = tpart.rotation;
                            AnimationJSONPart spart = source_anim != null ? source_anim.boneParts[i] : null;
                            if (spart != null && source_key < spart.positions.Length && source_key < spart.rotations.Length)
                            {
                                from_pos = TranslateWithRotation(basePosition, spart.positions[source_key], baseRotation);
                                from_rot = baseRotation * spart.rotations[source_key];
                            }

                            tpart.position = Vector3.Lerp(from_pos, target_pos, blend);
                            tpart.rotation = Quaternion.Slerp(from_rot, target_rot, blend);
                        }
                        else
                        {
                            tpart.position = target_pos;
                            tpart.rotation = target_rot;
                        }

                        // close the fingers of the hand holding the board
                        if ((closeLeft && fs.left_hand_set.Contains(fs.bones[i])) || (closeRight && fs.right_hand_set.Contains(fs.bones[i])))
                        {
                            tpart.rotation = tpart.parent.rotation * Quaternion.Euler(0, 30f, 0f);
                        }
                    }
                    catch (Exception e)
                    {
                        Utils.Log("Error playing frame " + e.Message + " " + a + " " + b);
                    }
                }

                AnimationJSONPart skate = skate_animation ? animation.parts.Skate : null;
                if (skate != null)
                {
                    Vector3 anim_position = Vector3.Lerp(skate.positions[a], skate.positions[b], t);
                    Quaternion anim_rotation = baseRotation * Quaternion.Slerp(skate.rotations[a], skate.rotations[b], t);
                    Vector3 target_pos = TranslateWithRotation(basePosition, anim_position, fs.self.transform.rotation);

                    // Lerp by dt * 48 as tuned at 60 fps
                    float skate_step = Utils.FrameIndependentLerp((1f / 60f) * 48f);

                    target_pos = Vector3.Lerp(Main.walking_go.fakeSkate.transform.position, target_pos, skate_step);
                    anim_rotation = Quaternion.Lerp(Main.walking_go.fakeSkate.transform.rotation, anim_rotation * Quaternion.Euler(90f, 0, 0), skate_step);

                    if (mag_start >= 0 || mag_end >= 0)
                    {
                        float time_start = animation.times[mag_start];
                        float time_end = animation.times[mag_end];
                        Main.walking_go.magnetized = animTime >= time_start && animTime <= time_end;
                        if (Main.walking_go.magnetized)
                        {
                            Main.walking_go.fakeSkate.transform.position = target_pos;
                            Main.walking_go.fakeSkate.transform.rotation = anim_rotation;
                        }
                    }
                    else
                    {
                        Main.walking_go.magnetized = true;
                        Main.walking_go.fakeSkate.transform.position = target_pos;
                        Main.walking_go.fakeSkate.transform.rotation = anim_rotation;
                    }
                }

                last_frame = frame = crossfading ? 0 : b;

                if (!crossfading) animTime += Time.deltaTime * speed;
                count += frameScale;

                if (animTime > timeLimit)
                {
                    if (loop)
                    {
                        // carry the overshoot into the next loop, and don't restart the crossfade:
                        // resetting count here blended every loop back from the previous pose
                        float length = timeLimit - timeLimitStart;
                        animTime = length > 0f ? timeLimitStart + Mathf.Repeat(animTime - timeLimitStart, length) : timeLimitStart;
                    }
                    else
                    {
                        isPlaying = false;
                    }

                    if (callback != null)
                    {
                        callback();
                        callback = null;
                    }

                    if (Main.walking_go.last_animation.name != name) Main.walking_go.last_animation = new AnimController(this);
                }
            }
        }

        // Finds the keys around animTime. Past the last key a looping animation blends back to its start key.
        void SampleKeys(float[] times, out int a, out int b, out float t)
        {
            int last = times.Length - 1;
            if (last <= 0)
            {
                a = b = 0;
                t = 1f;
                return;
            }

            if (animTime >= times[last])
            {
                a = last;
                if (loop && timeLimit > times[last])
                {
                    b = 0;
                    while (b < last && times[b] < timeLimitStart) b++;
                    t = (animTime - times[last]) / (timeLimit - times[last]);
                }
                else
                {
                    b = last;
                    t = 1f;
                }
            }
            else
            {
                b = 0;
                while (b < last && times[b] < animTime) b++;
                if (b == 0)
                {
                    a = 0;
                    t = 1f;
                }
                else
                {
                    a = b - 1;
                    float span = times[b] - times[a];
                    t = span > 0f ? (animTime - times[a]) / span : 1f;
                }
            }

            t = float.IsNaN(t) ? 1f : Mathf.Clamp01(t);
        }

        public static bool isValidMatrix(Vector3 position, Quaternion rotation)
        {
            if (position == null || rotation == null)
            {
                return false;
            }

            if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                Utils.Log("Transform position has infinity value(s).");
                return false;
            }

            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z))
            {
                Utils.Log("Transform position has NaN value(s).");
                return false;
            }

            if (float.IsInfinity(rotation.x) || float.IsInfinity(rotation.y) || float.IsInfinity(rotation.z) || float.IsInfinity(rotation.w))
            {
                Utils.Log("Transform rotation has infinity value(s).");
                return false;
            }

            if (float.IsNaN(rotation.x) || float.IsNaN(rotation.y) || float.IsNaN(rotation.z) || float.IsNaN(rotation.w))
            {
                Utils.Log("Transform rotation has NaN value(s).");
                return false;
            }

            return true;
        }

        public static bool HasNaNValues(Quaternion q)
        {
            return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w);
        }

        public static bool HasNaNValues(Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z);
        }

        Quaternion EnsureQuaternionContinuity(Quaternion last, Quaternion curr)
        {
            if (last.x * curr.x + last.y * curr.y + last.z * curr.z + last.w * curr.w < 0f)
            {
                return new Quaternion(-curr.x, -curr.y, -curr.z, -curr.w);
            }
            return curr;
        }
        public static float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        public Vector3 TranslateWithRotation(Vector3 input, Vector3 translation, Quaternion rotation)
        {
            Vector3 rotatedTranslation = rotation * translation;
            Vector3 output = input + rotatedTranslation;
            return output;
        }

        public void Play()
        {
            animTime = timeLimitStart;
            count = 0;
            isPlaying = true;
        }

        public void Play(CallBack call)
        {
            animTime = timeLimitStart;
            count = 0;
            callback = call;
            isPlaying = true;
        }

        public void Stop(bool ignore_callback = false)
        {
            rotation_offset = original_rotation;
            isPlaying = false;
            Main.walking_go.last_animation = new AnimController(this);
            if (callback != null && !ignore_callback)
            {
                callback();
                callback = null;
            }
        }
    }
}

