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

        AnimationJSON animation;
        FakeSkater fs;
        public string path = "";
        public Vector3 offset = new Vector3(0, -.73f, 0);
        public Quaternion rotation_offset = Quaternion.Euler(0, 0, 0), original_rotation = Quaternion.identity;
        CallBack callback;
        bool loop = true;
        public bool anchorRoot = false, doCrossfade = true;
        public float speed = 1f;
        public float timeLimit = 0f;

        float animTime = 0f;
        public int frame = 0, last_frame = 0, count = 0, crossfade = 0;
        Vector3 first_frame_pelvis;
        public bool anchorRootFade = true;
        public float anchorRootSpeed = 12f;

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
                MessageSystem.QueueMessage(MessageDisplayData.Type.Error, "Error loading animation '" + name + "', file doesn't exists", 3f);
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

            try
            {
                anchorRoot = json_parsed["anchorRoot"] == null ? anchorRoot : (bool)json_parsed["anchorRoot"];
            }
            catch { }

            animation = new AnimationJSON((float)json_parsed["duration"], Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(json_parsed["times"].ToString()), parts);
            UnityModManager.Logger.Log("[walking-mod] Loaded animation: " + animation.ToString() + " " + name);

            Type type_pelvis = typeof(AnimationJSONParts);
            var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
            AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
            first_frame_pelvis = new Vector3(pelvis.position[0][0], pelvis.position[0][1], pelvis.position[0][2]);

            if (timeLimit == 0f) timeLimit = animation.duration;
        }

        public void Update()
        {
            if (fs.self && isPlaying)
            {
                int index = 0;

                for (int i = 0; i < animation.times.Length; i++)
                {
                    index = i;
                    if (animation.times[i] >= animTime) break;
                }

                if (Main.walking_go.last_animation == null) Main.walking_go.last_animation = new AnimController(this);

                int d_crossfade = Main.walking_go.last_animation.name != name && doCrossfade ? 10 : doCrossfade ? crossfade : 0;
                float step = 0;

                if (count < d_crossfade) index = 0;
                last_frame = index;
                frame = index;

                int interpolation_index = index - 1;
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
                    if (anchorRootFade) offset = Vector3.Lerp(offset, new Vector3(x, -(fs.collider.height / 2) + y, z), Time.fixedDeltaTime * 12f);
                    else offset = new Vector3(x, -(fs.collider.height / 2) + y, z);
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

                            float i_time = i_animation.times[interpolation_index];
                            if (i_time > animation.times[index]) i_time = 0;

                            float istep = animation.times[index] - i_time;
                            float diff = animTime - i_time;
                            step = 1 - ((istep - diff) / istep);

                            if (count < d_crossfade && Main.walking_go.last_animation.name != name)
                            {
                                step = (((float)count + 1) / (float)d_crossfade);
                            }

                            Vector3 anim_position = new Vector3(apart.position[index][0], apart.position[index][1], apart.position[index][2]);
                            Vector3 i_anim_position = new Vector3(iapart.position[interpolation_index][0], iapart.position[interpolation_index][1], iapart.position[interpolation_index][2]);

                            Vector3 position = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation);
                            Vector3 iposition = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation);
                            Quaternion rotation = rotation_offset * fs.self.transform.rotation;

                            Vector3 target_pos = TranslateWithRotation(position, anim_position, rotation);
                            Vector3 i_target_pos = TranslateWithRotation(iposition, i_anim_position, rotation);

                            tpart.position = Vector3.Lerp(i_target_pos, target_pos, step);

                            Quaternion i_rotation = rotation * new Quaternion(iapart.quaternion[interpolation_index][0], iapart.quaternion[interpolation_index][1], iapart.quaternion[interpolation_index][2], iapart.quaternion[interpolation_index][3]);

                            rotation = rotation * new Quaternion(apart.quaternion[index][0], apart.quaternion[index][1], apart.quaternion[index][2], apart.quaternion[index][3]);

                            tpart.rotation = Quaternion.Lerp(i_rotation, rotation, step);
                        }
                        catch (Exception e)
                        {
                            UnityModManager.Logger.Log("Error playing frame " + e.Message + " " + index + " ");
                        }
                    }
                }

                if (count >= d_crossfade) animTime += Time.smoothDeltaTime * speed;

                count++;

                if (animTime > timeLimit)
                {
                    if (loop)
                    {
                        count = 0;
                        animTime = 0;
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
            animTime = 0;
            if (Main.walking_go.last_animation.name != name) animTime = animation.times[0];
            count = 0;
            isPlaying = true;
        }

        public void Play(CallBack call)
        {
            animTime = 0;
            if (Main.walking_go.last_animation.name != name) animTime = animation.times[0];
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

