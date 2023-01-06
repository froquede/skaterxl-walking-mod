using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    public delegate void CallBack();

    public class AnimController : MonoBehaviour
    {
        public bool isPlaying = false;

        AnimationJSON animation;
        FakeSkater fs;
        public string path = "";
        public Vector3 offset = new Vector3(0, -.73f, 0);
        CallBack callback;
        bool loop = true;
        GameObject copy;

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

        public AnimController(string path, FakeSkater fs, bool loop)
        {
            this.path = path;
            this.fs = fs;
            this.loop = loop;

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

            animation = new AnimationJSON((float)json_parsed["duration"], Newtonsoft.Json.JsonConvert.DeserializeObject<float[]>(json_parsed["times"].ToString()), parts);
            UnityModManager.Logger.Log(animation.ToString());
        }

        float animTime = 0f;
        public int frame = 0, count = 0, crossfade = 16;

        public void FixedUpdate()
        {
            if (copy == null) copy = new GameObject();
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

                            /*if (count == 0 && frame == 0 && crossfade != 1)
                            {
                                crossfade = (int)Vector3.Distance(tpart.transform.position, anim_position);
                                crossfade = crossfade > 16 ? 16 : crossfade;
                            }*/

                            copy.transform.position = translateLocal(fs.self.transform, offset);
                            copy.transform.rotation = fs.self.transform.rotation;

                            float step = count < crossfade ? Time.smoothDeltaTime * (48 / (crossfade - count)) : Time.smoothDeltaTime * 48f;
                            tpart.position = Vector3.Lerp(tpart.position, translateLocal(copy.transform, anim_position), step);
                            Transform result = rotateLocal(copy.transform, new Quaternion(apart.quaternion[index][0], apart.quaternion[index][1], apart.quaternion[index][2], apart.quaternion[index][3]));

                            /*result.Rotate(90, 0, 0, Space.Self); // mixamo
                            result.Rotate(0, -90, 0, Space.Self);*/ // mixamo

                            tpart.rotation = Quaternion.Slerp(tpart.rotation, result.rotation, step);
                        }
                        catch {
                            UnityModManager.Logger.Log("Catch error");
                        }
                    }
                }
                animTime += Time.smoothDeltaTime;
                count++;

                if (animTime > animation.duration) {
                    if (loop) animTime = 0;
                    else isPlaying = false;

                    if (callback != null)
                    {
                        callback();
                        callback = null;
                    } 
                } 
            }
        }

        public static float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        public Vector3 translateLocal(Transform origin, Vector3 offset)
        {
            GameObject copy = new GameObject();
            copy.transform.position = origin.position;
            copy.transform.rotation = origin.rotation;

            copy.transform.Translate(offset, Space.Self);

            Vector3 result = copy.transform.position;

            Destroy(copy);

            return result;
        }

        Transform rotateLocal(Transform origin, Quaternion offset)
        {
            GameObject copy = new GameObject();
            copy.transform.position = origin.position;
            copy.transform.rotation = origin.rotation;

            copy.transform.Rotate(offset.eulerAngles, Space.Self);

            Transform result = copy.transform;

            Destroy(copy);

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
            if (callback != null)
            {
                callback();
                callback = null;
            }
        }
    }
}

