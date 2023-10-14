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
        public int frame = 0, last_frame = 0, count = 0, crossfade = 0;
        Vector3 first_frame_pelvis;
        public bool anchorRootFade = true;
        public float anchorRootSpeed = 12f;
        public bool skate_animation = false;
        public int mag_start = -1, mag_end = -1;

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
                var reader = File.OpenText(path);
                json = await reader.ReadToEndAsync();
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
            Utils.Log("[walking-mod] Loaded animation: " + animation.ToString() + " " + name);

            Type type_pelvis = typeof(AnimationJSONParts);
            var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
            AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
            first_frame_pelvis = new Vector3(pelvis.position[0][0], pelvis.position[0][1], pelvis.position[0][2]);

            if (timeLimit == 0f) timeLimit = animation.duration;
            if (timeLimitStart == 0f) timeLimitStart = animation.times[0];
        }

        float stiffness = 200f, damping = 40f;
        Dictionary<string, Vector3> velocity = new Dictionary<string, Vector3>();
        public void Update()
        {
            if (fs.self && isPlaying)
            {
                bool interpolateActual = false;

                int index = 0;

                for (int i = 0; i < animation.times.Length; i++)
                {
                    index = i;
                    if (animation.times[i] >= animTime) break;
                }

                if (Main.walking_go.last_animation == null) Main.walking_go.last_animation = new AnimController(this);

                int d_crossfade = (Main.walking_go.last_animation.name != name && doCrossfade) ? 9 : doCrossfade ? crossfade : 0;
                if (Time.fixedUnscaledTime - Main.walking_go.enterBailTimestamp <= Time.deltaTime * 2f && Main.walking_go.enterFromBail)
                {
                    interpolateActual = true;
                    d_crossfade = 12;
                }

                float step = 0;

                if (count < d_crossfade) index = 0;
                last_frame = index;
                frame = index;

                int interpolation_index = index - 1;

                if (animation.times.Length == 1)
                {
                    frame = index = interpolation_index = 0;
                }

                if (interpolation_index < 0) interpolation_index = animation.times.Length - 1;
                AnimationJSON i_animation = animation;
                if (count < d_crossfade)
                {
                    interpolation_index = Main.walking_go.last_animation.last_frame;
                    i_animation = Main.walking_go.last_animation.animation;
                }

                Type type_pelvis = typeof(AnimationJSONParts);
                var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
                AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
                if (anchorRoot)
                {
                    float x = -(pelvis.position[index][0] - pelvis.position[0][0]);
                    float y = -(pelvis.position[index][1] - pelvis.position[0][1]);
                    float z = -(pelvis.position[index][2] - pelvis.position[0][2]);
                    offset = Vector3.Lerp(offset, new Vector3(x, -(fs.collider.height / 2) + y, z), !anchorRootFade ? 1f : Time.deltaTime * 24f);
                }

                foreach (string part in fs.bones)
                {
                    Transform tpart = fs.getPart(part);
                    if (tpart)
                    {
                        try
                        {
                            Type type = typeof(AnimationJSONParts);
                            var property = type.GetProperty(part);
                            AnimationJSONPart apart = (AnimationJSONPart)property.GetValue(animation.parts, null);
                            AnimationJSONPart iapart = (AnimationJSONPart)property.GetValue(i_animation.parts, null);
                            float[] times = animation.times;
                            float[] itimes = i_animation.times;

                            float i_time = itimes.Length - 1 >= interpolation_index ? itimes[interpolation_index] : itimes[0];

                            float istep = times[index] - i_time;
                            float diff = animTime - i_time;
                            step = 1 - ((istep - diff) / istep);

                            if (count < d_crossfade && Main.walking_go.last_animation.name != name)
                            {
                                step = ((float)count + 1f) / (float)d_crossfade;
                            }

                            Vector3 anim_position = new Vector3(apart.position[index][0], apart.position[index][1], apart.position[index][2]);
                            Vector3 i_anim_position = new Vector3(iapart.position[interpolation_index][0], iapart.position[interpolation_index][1], iapart.position[interpolation_index][2]);

                            Quaternion rotation = rotation_offset * fs.self.transform.rotation;
                            Vector3 position = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation);

                            Vector3 target_pos = TranslateWithRotation(position, anim_position, rotation);
                            Vector3 i_target_pos = TranslateWithRotation(position, i_anim_position, rotation);

                            Quaternion i_rotation = rotation * new Quaternion(iapart.quaternion[interpolation_index][0], iapart.quaternion[interpolation_index][1], iapart.quaternion[interpolation_index][2], iapart.quaternion[interpolation_index][3]);
                            rotation = rotation * new Quaternion(apart.quaternion[index][0], apart.quaternion[index][1], apart.quaternion[index][2], apart.quaternion[index][3]);


                            if (times.Length > 1)
                            {
                                bool valid = isValidMatrix(i_target_pos, i_rotation);
                                Vector3 target_i = valid ? i_target_pos : tpart.position;

                                if (interpolateActual)
                                {
                                    i_target_pos = tpart.position;
                                    i_rotation = tpart.rotation;
                                    float d = Vector3.Distance(target_i, target_pos) * 60f;
                                    step = Time.deltaTime * d;
                                }

                                tpart.position = Vector3.Lerp(target_i, target_pos, step);
                                tpart.rotation = Quaternion.Lerp(valid ? i_rotation : tpart.rotation, rotation, step);

                                if(!valid) { Utils.Log(step + " " + index + " " + interpolation_index + " " + itimes.Length); }
                            }
                            else
                            {
                                tpart.position = target_pos;
                                tpart.rotation = rotation;
                            }
                        }
                        catch (Exception e)
                        {
                            Utils.Log("Error playing frame " + e.Message + " " + index + " " + interpolation_index);
                        }
                    }
                }

                if (skate_animation)
                {
                    string part = "Skate";
                    Type type = typeof(AnimationJSONParts);
                    var property = type.GetProperty(part);
                    AnimationJSONPart apart = (AnimationJSONPart)property.GetValue(animation.parts, null);
                    Vector3 anim_position = new Vector3(apart.position[index][0], apart.position[index][1], apart.position[index][2]);
                    Quaternion anim_rotation = rotation_offset * fs.self.transform.rotation;
                    anim_rotation = anim_rotation * new Quaternion(apart.quaternion[index][0], apart.quaternion[index][1], apart.quaternion[index][2], apart.quaternion[index][3]);

                    Vector3 position = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation);
                    Vector3 target_pos = TranslateWithRotation(position, anim_position, fs.self.transform.rotation);

                    float skate_step = Time.deltaTime * 48f;

                    target_pos = Vector3.Lerp(Main.walking_go.fakeSkate.transform.position, target_pos, skate_step);
                    anim_rotation = Quaternion.Lerp(Main.walking_go.fakeSkate.transform.rotation, anim_rotation * Quaternion.Euler(90f, 0, 0), skate_step);

                    if (mag_start >= 0 || mag_end >= 0)
                    {
                        float time_start = animation.times[mag_start];
                        float time_end = animation.times[mag_end];
                        Main.walking_go.magnetized = animTime >= time_start && animTime <= time_end;
                        if(Main.walking_go.magnetized)
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

                if (count >= d_crossfade) animTime += Time.deltaTime * speed;

                count++;

                if (animTime > timeLimit)
                {
                    if (loop)
                    {
                        count = 0;
                        animTime = timeLimitStart;
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

        public static bool isValidMatrix(Vector3 position, Quaternion rotation)
        {
            if (position == null || rotation == null)
            {
                Utils.Log("Transform is null.");
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

