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
        // normalized time the left foot plants in a looping cycle, -1 when unknown; lets walk, run and stairs switch in step
        public float footPhase = -1f;

        // Crossfades start from the pose actually on screen (kept relative to the body), while the new animation already runs.
        // Blending from the old clip's last key with the new one frozen on its first key popped on quick changes and hitched.
        static Vector3[] fadePositions;
        static Quaternion[] fadeRotations;
        static bool[] fadeValid;
        bool fading;
        float fadeElapsed, fadeDuration;
        // the animation being faded out keeps playing under the blend, so transitions mix two motions instead of morphing
        // out of a frozen pose
        AnimController fadeSource;

        // a second cycle mixed in by weight and kept in step with this one (walk with run, stairs with running stairs),
        // so speed changes blend the stride continuously instead of switching clips
        public AnimController layer;
        public float layerWeight;
        int keyA, keyB, layerA, layerB;
        float keyT, layerT;
        bool useLayer;

        // A one-shot animation (jump, roll, emote) blends into this animation over its last moments, so it ends already in
        // the pose it hands over to instead of fading out of its frozen last frame afterwards.
        public AnimController outTo;
        public float blendOutDuration = .3f;
        float outWeight;
        public float blendOutWeight { get { return outWeight; } }
        public bool isLoop { get { return loop; } }
        // real seconds left until the animation reaches its end
        public float remainingTime { get { return (timeLimit - animTime) / Mathf.Max(speed, .01f); } }

        // another cycle (with its own layer) mixed in by weight and kept in step with this one: stairs over the stride
        public AnimController blendTarget;
        public float blendWeight;
        bool useBlend;

        // a standing animation mixed over everything by weight (idle under the stride), running on its own clock
        public AnimController idleLayer;
        public float idleWeight;
        int idleA, idleB;
        float idleT;
        bool useIdle;

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
            footPhase = origin.footPhase;
            timeLimitStart = origin.timeLimitStart;
            fading = origin.fading;
            layer = origin.layer;
            layerWeight = origin.layerWeight;
            idleLayer = origin.idleLayer;
            idleWeight = origin.idleWeight;
            blendTarget = origin.blendTarget;
            blendWeight = origin.blendWeight;
            outTo = origin.outTo;
            outWeight = origin.outWeight;
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
            footPhase = FindFootPhase();
        }

        float FindFootPhase()
        {
            AnimationJSONPart toe = animation.parts.Skater_Toe1_l;
            float length = timeLimit - timeLimitStart;
            if (!loop || toe == null || toe.positions == null || toe.positions.Length != animation.times.Length || length <= 0f) return -1f;

            int lowest = 0;
            for (int i = 1; i < toe.positions.Length; i++)
            {
                if (toe.positions[i].y < toe.positions[lowest].y) lowest = i;
            }
            return Mathf.Clamp01((animation.times[lowest] - timeLimitStart) / length);
        }

        public float normalizedTime
        {
            get
            {
                float length = timeLimit - timeLimitStart;
                return length > 0f ? Mathf.Repeat((animTime - timeLimitStart) / length, 1f) : 0f;
            }
            set
            {
                animTime = timeLimitStart + Mathf.Repeat(value, 1f) * (timeLimit - timeLimitStart);
            }
        }

        // keys of this animation (and its layer, synced to the same point of the stride) for the current time
        void PrepareKeys()
        {
            SampleKeys(animation.times, out keyA, out keyB, out keyT);

            useLayer = layer != null && layer != this && layerWeight > .001f && layer.animation != null && layer.animation.boneParts != null;
            if (useLayer)
            {
                float phase = normalizedTime;
                if (footPhase >= 0f && layer.footPhase >= 0f) phase = phase - footPhase + layer.footPhase;
                layer.normalizedTime = phase;
                layer.SampleKeys(layer.animation.times, out layerA, out layerB, out layerT);
            }

            useBlend = blendTarget != null && blendTarget != this && blendTarget.blendTarget != this && blendWeight > .001f && blendTarget.animation != null && blendTarget.animation.boneParts != null;
            if (useBlend)
            {
                float phase = normalizedTime;
                if (footPhase >= 0f && blendTarget.footPhase >= 0f) phase = phase - footPhase + blendTarget.footPhase;
                blendTarget.normalizedTime = phase;
                blendTarget.PrepareKeys();
            }

            useIdle = idleLayer != null && idleLayer != this && idleWeight > .001f && idleLayer.animation != null && idleLayer.animation.boneParts != null;
            if (useIdle) idleLayer.SampleKeys(idleLayer.animation.times, out idleA, out idleB, out idleT);
        }

        // advances an animation that isn't the one playing (a layer, or the locomotion mix under an action), looping it
        public void AdvanceLayerTime(float dt)
        {
            animTime += dt * speed;
            float length = timeLimit - timeLimitStart;
            if (animTime > timeLimit) animTime = loop && length > 0f ? timeLimitStart + Mathf.Repeat(animTime - timeLimitStart, length) : timeLimit;
            if (animTime < timeLimitStart) animTime = timeLimitStart;
        }

        // pose of one bone relative to the animation root
        bool SampleBone(int i, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            AnimationJSONPart part = i < animation.boneParts.Length ? animation.boneParts[i] : null;
            if (part == null || keyB >= part.positions.Length || keyB >= part.rotations.Length || keyA >= part.positions.Length) return false;

            position = Vector3.Lerp(part.positions[keyA], part.positions[keyB], keyT);
            rotation = Quaternion.Slerp(part.rotations[keyA], part.rotations[keyB], keyT);

            if (useLayer)
            {
                AnimationJSONPart lpart = i < layer.animation.boneParts.Length ? layer.animation.boneParts[i] : null;
                if (lpart != null && layerB < lpart.positions.Length && layerB < lpart.rotations.Length && layerA < lpart.positions.Length)
                {
                    Vector3 lpos = Vector3.Lerp(lpart.positions[layerA], lpart.positions[layerB], layerT);
                    Quaternion lrot = Quaternion.Slerp(lpart.rotations[layerA], lpart.rotations[layerB], layerT);
                    position = Vector3.Lerp(position, lpos, layerWeight);
                    rotation = Quaternion.Slerp(rotation, lrot, layerWeight);
                }
            }

            if (useBlend)
            {
                Vector3 bpos;
                Quaternion brot;
                if (blendTarget.SampleBone(i, out bpos, out brot))
                {
                    position = Vector3.Lerp(position, bpos, blendWeight);
                    rotation = Quaternion.Slerp(rotation, brot, blendWeight);
                }
            }

            if (useIdle)
            {
                AnimationJSONPart ipart = i < idleLayer.animation.boneParts.Length ? idleLayer.animation.boneParts[i] : null;
                if (ipart != null && idleB < ipart.positions.Length && idleB < ipart.rotations.Length && idleA < ipart.positions.Length)
                {
                    Vector3 ipos = Vector3.Lerp(ipart.positions[idleA], ipart.positions[idleB], idleT);
                    Quaternion irot = Quaternion.Slerp(ipart.rotations[idleA], ipart.rotations[idleB], idleT);
                    position = Vector3.Lerp(position, ipos, idleWeight);
                    rotation = Quaternion.Slerp(rotation, irot, idleWeight);
                }
            }
            return true;
        }

        void CaptureFade()
        {
            fading = false;
            fadeSource = null;
            if (!doCrossfade || fs == null || !fs.self || fs.bones == null) return;

            AnimController last = Main.walking_go.last_animation;
            bool sameAnimation = last != null && last.name == name;
            if (sameAnimation && crossfade <= 0) return;

            int n = fs.bones.Length;
            if (fadePositions == null || fadePositions.Length != n)
            {
                fadePositions = new Vector3[n];
                fadeRotations = new Quaternion[n];
                fadeValid = new bool[n];
            }

            Transform body = fs.self.transform;
            Quaternion inverse = Quaternion.Inverse(body.rotation);
            for (int i = 0; i < n; i++)
            {
                Transform bone = fs.getBone(i);
                fadeValid[i] = bone != null;
                if (!bone) continue;
                fadePositions[i] = inverse * (bone.position - body.position);
                fadeRotations[i] = inverse * bone.rotation;
            }

            // Keep the previous animation running under the blend. If it was itself still fading in, the pose on screen
            // is a mix, so blend from the captured pose instead or the unfinished part would pop.
            if (!sameAnimation && last != null && last.animation != null && last.animation.boneParts != null && !last.fading && last.outWeight < .01f) fadeSource = last;

            // restarting the same animation keeps its own crossfade length (60 fps frames); moving between two cycles
            // (idle, walk, run, stairs) gets the longest blend
            if (sameAnimation) fadeDuration = crossfade / 60f;
            else fadeDuration = loop && last != null && last.loop ? .4f : .28f;
            fadeElapsed = 0f;
            fading = true;
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

                if (Time.unscaledTime - Main.walking_go.enterBailTimestamp <= Time.deltaTime * 2f && Main.walking_go.enterFromBail)
                {
                    interpolateActual = true;
                }

                float blend = 1f;
                if (fading)
                {
                    fadeElapsed += Time.deltaTime;
                    float x = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
                    blend = x * x * (3f - 2f * x);
                    if (x >= 1f)
                    {
                        fading = false;
                        fadeSource = null;
                    }
                }

                // the faded out animation advances as it would have kept playing
                Vector3 sourceBasePosition = Vector3.zero;
                Quaternion sourceBaseRotation = Quaternion.identity;
                AnimController src = fading ? fadeSource : null;
                if (src != null)
                {
                    src.animTime += Time.deltaTime * src.speed;
                    float srcLength = src.timeLimit - src.timeLimitStart;
                    if (src.animTime > src.timeLimit) src.animTime = src.loop && srcLength > 0f ? src.timeLimitStart + Mathf.Repeat(src.animTime - src.timeLimitStart, srcLength) : src.timeLimit;
                    src.PrepareKeys();
                    sourceBaseRotation = src.rotation_offset * fs.self.transform.rotation;
                    sourceBasePosition = TranslateWithRotation(fs.self.transform.position, src.offset, fs.self.transform.rotation) + Vector3.up * Main.walking_go.visualOffsetY;
                }

                // the idle layer keeps its own clock, advanced once here (a fade source may share it)
                if (idleLayer != null && idleLayer != this && idleLayer.animation != null) idleLayer.AdvanceLayerTime(Time.deltaTime);

                // keyframes around animTime, sampled by time so the pose doesn't depend on the frame rate
                PrepareKeys();
                int a = keyA, b = keyB;
                float t = keyT;

                AnimationJSONPart pelvis = animation.parts.Skater_pelvis;
                if (anchorRoot && pelvis != null)
                {
                    Vector3 pelvis_pos = Vector3.Lerp(pelvis.positions[a], pelvis.positions[b], t);
                    Vector3 delta = -(pelvis_pos - pelvis.positions[0]);
                    Vector3 target_offset = new Vector3(delta.x, -(fs.collider.height / 2) + delta.y, delta.z);
                    offset = !anchorRootFade ? target_offset : Vector3.Lerp(offset, target_offset, Utils.FrameIndependentLerp((1f / 60f) * 24f));
                }

                Transform bodyTransform = fs.self.transform;
                Vector3 bodyPosition = bodyTransform.position;
                Quaternion bodyRotation = bodyTransform.rotation;

                Quaternion baseRotation = rotation_offset * fs.self.transform.rotation;
                // visualOffsetY eases the body over steps the physics capsule takes instantly
                Vector3 basePosition = TranslateWithRotation(fs.self.transform.position, offset, fs.self.transform.rotation) + Vector3.up * Main.walking_go.visualOffsetY;

                // one-shot animations blend into the animation they hand over to (kept running by the walking controller) near their end
                outWeight = 0f;
                AnimController outAnim = null;
                Vector3 outBasePosition = Vector3.zero;
                Quaternion outBaseRotation = Quaternion.identity;
                if (!loop && outTo != null && outTo != this && outTo.animation != null && outTo.animation.boneParts != null && blendOutDuration > 0f)
                {
                    float remaining = (timeLimit - animTime) / Mathf.Max(speed, .01f);
                    float x = Mathf.Clamp01(1f - remaining / blendOutDuration);
                    outWeight = x * x * (3f - 2f * x);
                    if (outWeight > .001f)
                    {
                        outAnim = outTo;
                        outAnim.PrepareKeys();
                        outBaseRotation = outAnim.rotation_offset * bodyRotation;
                        outBasePosition = TranslateWithRotation(bodyPosition, outAnim.offset, bodyRotation) + Vector3.up * Main.walking_go.visualOffsetY;
                    }
                }

                bool holdingBoard = Main.walking_go.magnetized && !skate_animation;
                bool closeLeft = left_hand_closed || (holdingBoard && Main.settings.left_arm);
                bool closeRight = right_hand_closed || (holdingBoard && !Main.settings.left_arm);

                for (int i = 0; i < fs.bones.Length; i++)
                {
                    Transform tpart = fs.getBone(i);
                    if (!tpart) continue;

                    try
                    {
                        Vector3 local_pos;
                        Quaternion local_rot;
                        if (!SampleBone(i, out local_pos, out local_rot)) continue;
                        Vector3 target_pos = TranslateWithRotation(basePosition, local_pos, baseRotation);
                        Quaternion target_rot = baseRotation * local_rot;
                        if (outAnim != null)
                        {
                            Vector3 out_pos;
                            Quaternion out_rot;
                            if (outAnim.SampleBone(i, out out_pos, out out_rot))
                            {
                                target_pos = Vector3.Lerp(target_pos, TranslateWithRotation(outBasePosition, out_pos, outBaseRotation), outWeight);
                                target_rot = Quaternion.Slerp(target_rot, outBaseRotation * out_rot, outWeight);
                            }
                        }
                        if (!isValidMatrix(target_pos, target_rot)) continue;

                        if (interpolateActual)
                        {
                            float step = Mathf.Clamp01(Time.deltaTime * Vector3.Distance(tpart.position, target_pos) * 60f);
                            tpart.position = Vector3.Lerp(tpart.position, target_pos, step);
                            tpart.rotation = Quaternion.Slerp(tpart.rotation, target_rot, step);
                        }
                        else if (blend < 1f && fadeValid != null && i < fadeValid.Length && fadeValid[i])
                        {
                            Vector3 from_pos;
                            Quaternion from_rot;
                            Vector3 source_pos;
                            Quaternion source_rot;
                            if (src != null && src.SampleBone(i, out source_pos, out source_rot))
                            {
                                from_pos = TranslateWithRotation(sourceBasePosition, source_pos, sourceBaseRotation);
                                from_rot = sourceBaseRotation * source_rot;
                            }
                            else
                            {
                                from_pos = bodyPosition + bodyRotation * fadePositions[i];
                                from_rot = bodyRotation * fadeRotations[i];
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

                last_frame = frame = b;

                animTime += Time.deltaTime * speed;
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

        // plays on from the current time (an animation whose clock kept running in the background)
        public void Resume(bool crossfade)
        {
            count = 0;
            isPlaying = true;
            outWeight = 0f;
            if (crossfade) CaptureFade();
            else
            {
                fading = false;
                fadeSource = null;
            }
        }

        public void Play()
        {
            animTime = timeLimitStart;
            count = 0;
            isPlaying = true;
            CaptureFade();
        }

        public void Play(CallBack call)
        {
            animTime = timeLimitStart;
            count = 0;
            callback = call;
            isPlaying = true;
            CaptureFade();
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

