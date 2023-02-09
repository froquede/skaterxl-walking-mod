using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
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
        public Quaternion rotation_offset = Quaternion.Euler(0, 0, 0);
        CallBack callback;
        bool loop = true;
        GameObject copy;
        public bool anchorRoot = false;
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
                catch { }
            }

            try {
                anchorRoot = json_parsed["anchorRoot"] == null ? anchorRoot : (bool)json_parsed["anchorRoot"];
            }
            catch { }

            try
            {
                animationType = json_parsed["type"] == null ? animationType : (string)json_parsed["type"];
            }
            catch { }

            animation = new AnimationJSON((float)json_parsed["duration"], Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(json_parsed["times"].ToString()), parts);
            UnityModManager.Logger.Log("Loaded " + animation.ToString() + " " + name);

            Type type_pelvis = typeof(AnimationJSONParts);
            var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
            AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
            first_frame_pelvis = new Vector3(pelvis.position[0][0], pelvis.position[0][1], pelvis.position[0][2]);
        }

        float animTime = 0f;
        public int frame = 0, count = 0, crossfade = 12;
        Vector3 first_frame_pelvis;
        public bool anchorRootFade = true;

        public void FixedUpdate()
        {
            if (copy == null)
            {
                copy = new GameObject();
                UnityEngine.Object.DontDestroyOnLoad(copy);
            }
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

                int d_crossfade = Main.walking_go.last_animation != name ? 12 : crossfade;
                float smooth_factor = Main.walking_go.last_animation != name ? .075f : .2f;
                float step = count < d_crossfade ? Time.smoothDeltaTime * (48 / d_crossfade) : Time.smoothDeltaTime * 48f;

                Type type_pelvis = typeof(AnimationJSONParts);
                var prop_pelvis = type_pelvis.GetProperty("Skater_pelvis");
                AnimationJSONPart pelvis = (AnimationJSONPart)prop_pelvis.GetValue(animation.parts, null);
                if (anchorRoot)
                {
                    if (anchorRootFade) offset = Vector3.Lerp(offset, new Vector3(-(pelvis.position[frame][0] - first_frame_pelvis.x), -.73f - (pelvis.position[frame][1] - first_frame_pelvis.y), -(pelvis.position[frame][2] - first_frame_pelvis.z)), Time.smoothDeltaTime * 12f);
                    else offset = new Vector3(-(pelvis.position[frame][0] - first_frame_pelvis.x), -.73f - (pelvis.position[frame][1] - first_frame_pelvis.y), -(pelvis.position[frame][2] - first_frame_pelvis.z));
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

                            /*if (part[part.Length - 1] == 'l' || part[part.Length - 1] == 'r')
                            {
                                string counterpart = part.Remove(part.Length - 1) + (part[part.Length - 1] == 'l' ? 'r' : 'l');
                                var ctproperty = type.GetProperty(counterpart);
                                AnimationJSONPart ctapart = (AnimationJSONPart)ctproperty.GetValue(animation.parts, null);
                                anim_position = new Vector3(ctapart.position[index][0], ctapart.position[index][1], ctapart.position[index][2]);
                            }*/

                            copy.transform.position = translateLocal(fs.self.transform, offset);
                            copy.transform.rotation = rotateLocal(fs.self.transform, rotation_offset).rotation;

                            Vector3 target_pos = translateLocal(copy.transform, anim_position);
                            step = step * (1f - smooth_factor * Vector3.Distance(tpart.position, target_pos));

                            tpart.position = Vector3.Lerp(tpart.position, target_pos, step);
                            Transform result = rotateLocal(copy.transform, new Quaternion(apart.quaternion[index][0], apart.quaternion[index][1], apart.quaternion[index][2], apart.quaternion[index][3]));

                            if(animationType == "mixamo")
                            {
                                result.Rotate(90, 0, 0, Space.Self); // mixamo
                                //result.Rotate(0, -90, 0, Space.Self); // mixamo
                            }

                            tpart.rotation = Quaternion.Slerp(tpart.rotation, result.rotation, step);
                        }
                        catch (Exception e) {
                            UnityModManager.Logger.Log("Error playing frame " + e.Message);
                        }
                    }
                }
                animTime += Time.smoothDeltaTime * speed;
                count++;

                if (animTime > animation.duration) {
                    if (loop) animTime = 0;
                    else {
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

        public static float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        GameObject translate_local_go;
        public Vector3 translateLocal(Transform origin, Vector3 offset)
        {
            if (translate_local_go == null)
            {
                translate_local_go = new GameObject();
                UnityEngine.Object.DontDestroyOnLoad(translate_local_go);
            }

            translate_local_go.transform.position = origin.position;
            translate_local_go.transform.rotation = origin.rotation;

            translate_local_go.transform.Translate(offset, Space.Self);

            Vector3 result = translate_local_go.transform.position;
            return result;
        }

        GameObject rotate_local_go;
        Transform rotateLocal(Transform origin, Quaternion offset)
        {
            if (rotate_local_go == null)
            {
                rotate_local_go = new GameObject();
                UnityEngine.Object.DontDestroyOnLoad(rotate_local_go);
            }
            rotate_local_go.transform.position = origin.position;
            rotate_local_go.transform.rotation = origin.rotation;

            rotate_local_go.transform.Rotate(offset.eulerAngles, Space.Self);

            Transform result = rotate_local_go.transform;
            return result;
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
            this.callback = call;
            isPlaying = true;
        }

        public void Stop()
        {
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

