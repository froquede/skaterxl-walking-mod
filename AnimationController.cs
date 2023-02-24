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
        public string animationType = "xl";

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

        void LoadJSON()
        {
            string json;
            if (File.Exists(path))
            {
                json = File.ReadAllText(path);
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

            try
            {
                animationType = json_parsed["type"] == null ? animationType : (string)json_parsed["type"];
            }
            catch { }

            animation = new AnimationJSON((float)json_parsed["duration"], Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(json_parsed["times"].ToString()), parts);
            UnityModManager.Logger.Log("[walking-mod] Loaded animation: " + animation.ToString() + " " + name);

            Type type_pelvis = typeof(AnimationJSONParts);
            var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
            AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
            first_frame_pelvis = new Vector3(pelvis.position[0][0], pelvis.position[0][1], pelvis.position[0][2]);
        }

        float animTime = 0f;
        public int frame = 0, count = 0, crossfade = 12;
        Vector3 first_frame_pelvis;
        public bool anchorRootFade = true;
        public float anchorRootSpeed = 12f;
        IDictionary<string, float> Distances = new Dictionary<string, float>();

        public void FixedUpdate()
        {
            if (fs.self && isPlaying)
            {
                int index = 0;

                for (int i = 0; i < animation.times.Length; i++)
                {
                    index = i;
                    if (animation.times[i] >= animTime) break;
                }

                if (count < crossfade) index = 0;
                frame = index;

                if (Main.walking_go.last_animation == "") Main.walking_go.last_animation = name;

                int d_crossfade = Main.walking_go.last_animation != name && doCrossfade ? 12 : crossfade;
                float smooth_factor = Main.walking_go.last_animation != name ? .033f : .4f;
                float step = count < d_crossfade ? Time.smoothDeltaTime * (36 / d_crossfade) : Time.smoothDeltaTime * 24f;

                Type type_pelvis = typeof(AnimationJSONParts);
                var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
                AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
                if (anchorRoot)
                {
                    if (anchorRootFade) offset = Vector3.Lerp(offset, new Vector3(-(pelvis.position[frame][0] - first_frame_pelvis.x), -.73f - (pelvis.position[frame][1] - first_frame_pelvis.y), -(pelvis.position[frame][2] - first_frame_pelvis.z)), Time.smoothDeltaTime * 12f);
                    else offset = new Vector3(-(pelvis.position[frame][0] - first_frame_pelvis.x), -(fs.collider.height / 2f) - (pelvis.position[frame][1] - first_frame_pelvis.y), -(pelvis.position[frame][2] - first_frame_pelvis.z));
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
                            Vector3 anim_position = new Vector3(apart.position[index][0], apart.position[index][1], apart.position[index][2]);

                            Vector3 position = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation);
                            Quaternion rotation = rotation_offset * fs.self.transform.rotation;

                            Vector3 target_pos = TranslateWithRotation(position, anim_position, rotation);
                            step = step * (1f - smooth_factor * Vector3.Distance(tpart.position, target_pos));

                            tpart.position = Vector3.Lerp(tpart.position, target_pos, step);

                            rotation = rotation * new Quaternion(apart.quaternion[index][0], apart.quaternion[index][1], apart.quaternion[index][2], apart.quaternion[index][3]);

                            tpart.rotation = Quaternion.Slerp(tpart.rotation, rotation, step * 2f);
                        }
                        catch (Exception e)
                        {
                            UnityModManager.Logger.Log("Error playing frame " + e.Message);
                        }
                    }
                }
                if (count >= d_crossfade || Main.walking_go.last_animation == name) animTime += Time.smoothDeltaTime * speed;
                count++;

                if (animTime > animation.duration)
                {
                    if (loop) animTime = 0;
                    else
                    {
                        isPlaying = false;
                    }

                    if (callback != null)
                    {
                        callback();
                        callback = null;
                    }

                    Main.walking_go.last_animation = name;
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
            animTime = 0f;
            count = 0;
            isPlaying = true;
        }

        public void Play(CallBack call)
        {
            animTime = 0f;
            count = 0;
            callback = call;
            isPlaying = true;
        }

        public void Stop()
        {
            rotation_offset = original_rotation;
            isPlaying = false;
            Main.walking_go.last_animation = name;
            if (callback != null)
            {
                callback();
                callback = null;
            }
        }
    }
}

