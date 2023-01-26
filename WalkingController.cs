using Cinemachine;
using Dreamteck.Splines;
using GameManagement;
using HarmonyLib;
using ModIO.UI;
using Photon.Pun;
using ReplayEditor;
using RootMotion.Dynamics;
using SkaterXL.Core;
using SkaterXL.Data;
using SkaterXL.Multiplayer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace walking_mod
{
    public class WalkingController : MonoBehaviour
    {
        FakeSkater fs = new FakeSkater();
        AnimController walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown, falling;
        public AnimController emote1, emote2, emote3, emote4;
        AnimController[] animations;
        AudioClip[] sounds;
        public AnimController actual_anim;
        public bool inState = false;
        GameObject fallbackCamera;
        GameObject fakeSkate;
        Transform[] fakeTrucks = new Transform[2];
        CinemachineVirtualCamera main_cam, fall_cam;
        PlayTime playtimeobj;
        public List<string> emotes;
        IDictionary<string, AnimController> cache = new Dictionary<string, AnimController>();
        public string last_animation = "";

        void Start()
        {
            try
            {
                playtimeobj = GameObject.Find("PlayTime").GetComponent<PlayTime>();
            }
            catch
            {
                Log("Error getting PlayTime component");
            }

            InitAnimations();
            animations = new AnimController[21] { walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown, falling, emote1, emote2, emote3, emote4 };

            InitSounds();
            sounds = new AudioClip[4] { step, step2, step3, step4 };

            fallbackCamera = PlayerController.Instance.skaterController.transform.parent.parent.Find("Fallback Camera").gameObject;
            fall_cam = fallbackCamera.GetComponent<CinemachineVirtualCamera>();
            main_cam = MonoBehaviourSingleton<PlayerController>.Instance.cameraController._actualCam.GetComponent<CinemachineVirtualCamera>();

            actual_anim = new AnimController();

            SceneManager.sceneLoaded += OnSceneLoaded;

            string[] temp_emotes = Directory.GetFiles(Path.Combine(Main.modEntry.Path, "animations"), "*.json");
            emotes = new List<string>();
            for (int i = 0; i < temp_emotes.Length; i++)
            {
                string[] pieces = temp_emotes[i].Split(Path.DirectorySeparatorChar);
                string name = pieces[pieces.Length - 1].Replace(".json", String.Empty);
                bool add = true;
                for (int j = 0; j < animations.Length - 4; j++)
                {
                    if (name == animations[j].name) add = false;
                }

                if (add) emotes.Add(name);
            }
        }

        int walking_crossfade = 1;
        void InitAnimations()
        {
            walking = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking.json"), fs, true, walking_crossfade));
            walking.anchorRoot = true;
            walking_backwards = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking_backwards.json"), fs, true, walking_crossfade));
            walking_backwards.anchorRoot = true;
            walking_left = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking_left.json"), fs, true, walking_crossfade));
            walking_left.anchorRoot = true;
            walking_right = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking_right.json"), fs, true, walking_crossfade));
            walking_right.anchorRoot = true;
            running = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running.json"), fs, true, walking_crossfade));
            running.anchorRoot = true;
            running_backwards = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_backwards.json"), fs, true, walking_crossfade));
            running_backwards.anchorRoot = true;
            running_left = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_left.json"), fs, true, walking_crossfade));
            running_left.anchorRoot = true;
            running_right = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_right.json"), fs, true, walking_crossfade));
            running_right.anchorRoot = true;
            idle = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\idle.json"), fs));
            jump = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\jumping.json"), fs, false));
            running_jump = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_jump.json"), fs, false, true));
            running_jump.crossfade = 1;
            running_jump.anchorRootFade = false;

            left_turn = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\left_turn.json"), fs));
            right_turn = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\right_turn.json"), fs));

            front_flip = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\front_flip.json"), fs, false, true));
            back_flip = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\back_flip.json"), fs, false, true));

            throwdown = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\throwdown.json"), fs, false, true));
            throwdown.speed = 1.1f;
            falling = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\falling.json"), fs, Quaternion.Euler(-37.5f, 0, 0)));
            falling.crossfade = 6;
            falling.anchorRoot = true;

            emote1 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote1 + ".json"), fs, false, 1));
            //emote1.anchorRoot = true;
            emote2 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote2 + ".json"), fs, false, 1));
            //emote2.anchorRoot = true;
            emote3 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote3 + ".json"), fs, false, 1));
            //emote3.anchorRoot = true;
            emote4 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote4 + ".json"), fs, false, 1));
            //emote4.anchorRoot = true;
        }

        AnimController LoadAnim(AnimController anim)
        {
            if (!cache.ContainsKey(anim.name)) cache.Add(anim.name, anim);
            return anim;
        }

        AudioClip step, step2, step3, step4;
        void InitSounds()
        {
            step = GetClip(Path.Combine(Main.modEntry.Path, "sounds\\footstep_a.wav"));
            step2 = GetClip(Path.Combine(Main.modEntry.Path, "sounds\\footstep_b.wav"));
            step3 = GetClip(Path.Combine(Main.modEntry.Path, "sounds\\footstep_c.wav"));
            step4 = GetClip(Path.Combine(Main.modEntry.Path, "sounds\\footstep_d.wav"));
        }

        public float speed = 10f;
        public float jumpForce = 10.0f;
        public Vector3 velocity;
        bool jumping = false, throwdown_state = false;
        int press_count = 0;
        string actual_state = "";
        float max_speed = 50f;
        float running_speed = 3.9f;
        bool emoting = false;
        bool respawnSwitch = false;
        float limit_idle = .3f;
        float decay = .95f;

        Vector3 last_pos = Vector3.zero;

        void FixedUpdate()
        {
            if (!should_run) return;

            if (inState == true) inStateLogic();
            else inPlayStateLogic();
        }

        bool should_run = false, throwed = false;
        void Update()
        {
            if (GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState) && GameStateMachine.Instance.CurrentState.GetType() != typeof(PauseState))
            {
                should_run = false;

                if (inState)
                {
                    inState = false;
                    ReplaceBones(true);
                    DestroyFS();
                }

                if (!PlayerController.Instance.inputController.enabled) PlayerController.Instance.inputController.enabled = true;
            }
            else should_run = true;

            if (PlayerController.Instance.respawn.respawning)
            {
                Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(false);
                Traverse.Create(ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1]).Field("didRespawn").SetValue(false);
            }
            else if (GameStateMachine.Instance.CurrentState.GetType() == typeof(PlayState)) Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(true);

            if (!inState) Throwdown();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log("Scene loaded: " + scene.name + " " + mode);
            resetStates();
            DestroyFS();
            for (int i = 0; i < animations.Length; i++)
            {
                if (animations[i] == null) Log(i + " is null");
            }
            // if (gameplay_disabled) EnableGameplay();
        }

        void resetStates()
        {
            throwdown_state = false;
            jumping = false;
            actual_state = "idle";
            last_nonplaytime = (float)Traverse.Create(playtimeobj).Field("nonPlayTime").GetValue();
            inState = false;
        }

        void Log(object log)
        {
            UnityModManager.Logger.Log(log.ToString());
        }

        void DestroyFS()
        {
            try
            {
                fs.rb = null;
                if (fs.self != null) Destroy(fs.self);
                fs.self = null;
                if (fakeSkate != null) Destroy(fakeSkate);
                fakeSkate = null;
            }
            catch
            {
                Log("Error destroying fs");
                fs.rb = null;
                fs.self = null;
                fakeSkate = null;
            }

            original_bones = null;
        }

        AudioSource audioSource_left, audioSource_right;
        SphereCollider slide_collider;
        void createFS()
        {
            Log("Creating new FS");

            try
            {
                fs.Create();

                GameObject left_source = new GameObject("Left Audio Source");
                audioSource_left = left_source.AddComponent<AudioSource>();
                //left_source.AddComponent<AudioSourceTracker>();
                left_source.transform.parent = fs.self.transform;

                GameObject right_source = new GameObject("Left Audio Source");
                audioSource_right = right_source.AddComponent<AudioSource>();
                //right_source.AddComponent<AudioSourceTracker>();
                right_source.transform.parent = fs.self.transform;

                //fs.self.AddComponent<TransformTracker>();
                fakeSkate = Instantiate(PlayerController.Instance.boardController.boardTransform.gameObject);
                fakeSkate.GetComponent<Rigidbody>().isKinematic = magnetized;
                fakeTrucks[0] = fakeSkate.transform.FindChildRecursively("Back Truck");
                fakeTrucks[1] = fakeSkate.transform.FindChildRecursively("Front Truck");
                fakeSkate.transform.position = PlayerController.Instance.boardController.boardTransform.position;
                fakeSkate.transform.rotation = PlayerController.Instance.boardController.boardTransform.rotation;
                //fakeSkate.AddComponent<TransformTracker>();

                /*slide_collider = GameObject.CreatePrimitive(PrimitiveType.Sphere).GetComponent<SphereCollider>();
                slide_collider.gameObject.AddComponent<FootTrigger>();
                slide_collider.gameObject.transform.localScale = new Vector3(.5f, .5f, .5f);
                slide_collider.isTrigger = true;*/

                Log("FS Loaded");
            }
            catch
            {
                Log("Error creating FS");
            }
        }

        bool gameplay_disabled = false;
        float last_nonplaytime = 0;
        void DisableGameplay()
        {
            Log("Disabling gameplay");
            //SoundManager.Instance.StopBearingSound();
            SoundManager.Instance.StopGrindSound(0f);
            SoundManager.Instance.StopPowerslideSound(0, 0);
            SoundManager.Instance.StopPowerslideSound(1, 0);
            //DeckSounds.Instance.StopBearingSounds();
            DeckSounds.Instance.powerslideLoopSource.Stop();
            DeckSounds.Instance.powerslideLoopSource2.Stop();
            Traverse.Create(DeckSounds.Instance).Field("_isMuted").SetValue(true);
            //PlayerController.Instance.DisableGameplay();
            SoundManager.Instance.deckSounds.MuteAll();
            GameStateMachine.Instance.PinObject.SetActive(false);
            /*MonoBehaviourSingleton<PlayerController>.Instance.respawn.DoRespawn();*/
            TogglePlayObject(false);
            ReplaceBones(false);
            gameplay_disabled = true;
            last_nonplaytime = (float)Traverse.Create(playtimeobj).Field("nonPlayTime").GetValue();
            PlayerController.Instance.inputController.enabled = false;

            if (MultiplayerManager.Instance.InRoom) PlayerController.Instance.ForceBail();
        }

        void EnableGameplay()
        {
            inState = false;
            DestroyFS();
            if (GameStateMachine.Instance.CurrentState.GetType() == typeof(PlayState))
            {
                //SoundManager.Instance.StopBearingSound();
                SoundManager.Instance.StopGrindSound(0f);
                SoundManager.Instance.StopPowerslideSound(0, 0);
                SoundManager.Instance.StopPowerslideSound(1, 0);
                DeckSounds.Instance.powerslideLoopSource.Stop();
                DeckSounds.Instance.powerslideLoopSource2.Stop();
                Traverse.Create(DeckSounds.Instance).Field("_isMuted").SetValue(false);
                Log("Enabling gameplay");
                PlayerController.Instance.EnableGameplay();
                TogglePlayObject(true);
                PlayerController.Instance.inputController.enabled = true;
                gameplay_disabled = false;
            }
            else ReplaceBones(true);

            if (last_nonplaytime != 0) Traverse.Create(playtimeobj).Field("nonPlayTime").SetValue(last_nonplaytime);
        }

        Quaternion EnsureQuaternionContinuity(Quaternion last, Quaternion curr)
        {
            if ((throwdown_state || updating || check_velocity) && last.x * curr.x + last.y * curr.y + last.z * curr.z + last.w * curr.w < 0f)
            {
                return new Quaternion(-curr.x, -curr.y, -curr.z, -curr.w);
            }
            return curr;
        }

        bool Sideway()
        {
            return actual_anim.name == running_left.name || actual_anim.name == running_right.name || actual_anim.name == walking_left.name || actual_anim.name == walking_right.name;
        }

        bool Rotating()
        {
            return actual_anim.name == left_turn.name || actual_anim.name == right_turn.name;
        }

        float record_delta = 0;
        int instate_count = 0;
        Vector3 relativeVelocity;
        Quaternion last_rotation_offset = Quaternion.Euler(0, 0, 0);
        Vector3 last_velocity;
        float rotation_speed = 8f;
        void inStateLogic()
        {
            if (!fs.self || !fs.rb || !fakeSkate)
            {
                Log(fs.self + " " + fs.rb + " " + fakeSkate);
                return;
            }

            if (GameStateMachine.Instance.CurrentState.GetType() == typeof(PauseState)) return;

            if (relativeVelocity != Vector3.zero && Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) > limit_idle && instate_count >= 24)
            {
                if (!Sideway() && !Rotating() && !jumping && actual_state != "falling" && actual_state != "idle")
                {
                    actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.LookRotation(backwards ? relativeVelocity : -relativeVelocity), Time.smoothDeltaTime * rotation_speed);
                }
                else
                {
                    if (Sideway())
                    {
                        Quaternion lr = Quaternion.LookRotation(relativeVelocity);
                        actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.Euler(lr.eulerAngles.x, lr.eulerAngles.y + (relativeVelocity.x > 0 ? -90 : 90), lr.eulerAngles.z), Time.smoothDeltaTime * rotation_speed);
                    }
                    else
                    {
                        if (jumping)
                        {
                            if (actual_state != "idle")
                            {
                                Quaternion lr = Quaternion.LookRotation(backwards ? relativeVelocity : -relativeVelocity);
                                actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.Euler(0, lr.eulerAngles.y, 0), Time.smoothDeltaTime * rotation_speed);
                            }
                        }
                        else
                        {
                            if (actual_anim.name != falling.name) actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.identity, Time.smoothDeltaTime * rotation_speed);
                        }
                    }
                }
            }
            else actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.identity, Time.smoothDeltaTime * rotation_speed);
            last_rotation_offset = actual_anim.rotation_offset;

            try { actual_anim.FixedUpdate(); } catch { UnityModManager.Logger.Log("Error updating animation " + inState); }

            Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(true);

            UpdateSticks();
            RaycastPelvis();
            RaycastFloor();
            RaycastFeet();
            RaycastStairs();
            emoteInput();

            /*slide_collider.gameObject.transform.position = translateLocal(fs.self.transform, new Vector3(0, -.73f, 1f));
            slide_collider.gameObject.transform.rotation = fs.self.transform.rotation;

            if(grinding)
            {
                slide_collider.gameObject.transform.rotation = Quaternion.LookRotation(last_result.normal);
            }*/

            if (MultiplayerManager.Instance.InRoom) UpdateRagdoll();

            PlayerController.Instance.boardController.boardRigidbody.isKinematic = magnetized;

            UpdateGameplay();

            if (!jumping)
            {
                if (Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) >= running_speed) actual_state = "running";
                else
                {
                    if (Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) <= limit_idle) actual_state = "idle";
                    else actual_state = "walking";
                }
            }

            if (relativeVelocity.y > 0.045f && !grounded && !jumping) actual_state = "falling";

            relativeVelocity = fs.rb.transform.InverseTransformDirection(last_pos - fs.self.transform.position);
            last_pos = fs.self.transform.position;

            if (!jumping)
            {
                HandleAnimations();
                JumpInput();
            }
            else JumpingOffset();

            Board();

            if (!emoting) ThrowdownInput();

            AddReplayFrame();

            if (PlayerController.Instance.inputController.player.GetButtonDown("Y")) EnableGameplay();
            if (PlayerController.Instance.inputController.player.GetButtonShortPressDown("X")) magnetized = !magnetized;

            respawning = false;

            if (!GetButtonDown("LB") && !GetButtonDown("RB") && !Main.ui.emote_config)
            {
                if (PlayerController.Instance.inputController.player.GetButtonDown(68) || PlayerController.Instance.inputController.player.GetButton(68)) SetRespawn();
                if (PlayerController.Instance.inputController.player.GetButtonDown(67) || PlayerController.Instance.inputController.player.GetButton(67)) DoRespawn();
            }

            instate_count++;

            last_velocity = fs.rb.velocity;
        }

        void UpdateRagdoll()
        {
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscleWeight = 0;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.internalCollisions = false;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.position = fs.self.transform.position;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.rotation = fs.self.transform.rotation;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.puppetMaster.pinWeight = 1f;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.puppetMaster.muscleWeight = 1f;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.behaviourPuppet.defaults.minMappingWeight = 0f;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.behaviourPuppet.masterProps.normalMode = BehaviourPuppet.NormalMode.Kinematic;

            for (int i = 0; i < PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles.Length; i++)
            {
                Transform part = fs.getPart(PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].name);
                if (part != null)
                {
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.isKinematic = true;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.useGravity = false;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].transform.position = part.position;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].transform.rotation = part.rotation;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].transform.gameObject.layer = LayerUtility.RagdollNoInternalCollision;
                }
            }
        }

        bool respawning = false;
        void DoRespawn()
        {
            respawning = true;
            last_nr = (RespawnInfo)Traverse.Create(MonoBehaviourSingleton<PlayerController>.Instance.respawn).Field("markerRespawnInfos").GetValue();
            fs.self.transform.position = new Vector3(last_nr.position.x, last_nr.position.y + .76f, last_nr.position.z);
            fs.self.transform.rotation = last_nr.rotation;
            fs.rb.velocity = Vector3.zero;
            fs.rb.angularVelocity = Vector3.zero;
            PlayerController.Instance.respawn.puppetMaster.Teleport(fs.self.transform.position + fs.self.transform.rotation * PlayerController.Instance.respawn.GetOffsetPositions(false)[1] + (Vector3)Traverse.Create(PlayerController.Instance.respawn).Field("_playerOffset").GetValue(), fs.self.transform.rotation, false);
        }

        void SetRespawn()
        {
            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, .715f, 0),
                IsBoardBackwards = false,
                rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(fs.rb.transform.forward) : Quaternion.identity,
                isSwitch = false
            };
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.SetSpawnPoint(respawnInfo);
        }

        void AddReplayFrame()
        {
            float time = PlayTime.deltaTime;
            //Log(time + " " + ReplayRecorder.Instance.LocalPlayerFrames.Count + " " + PlayTime.deltaTime);
            if (ReplayRecorder.Instance.LocalPlayerFrames == null) Traverse.Create(ReplayRecorder.Instance).Field("LocalPlayerFrames").SetValue(new List<ReplayPlayerFrameHalf>());

            if (ReplayRecorder.Instance.LocalPlayerFrames.Count > 0 && ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1] != null) time += ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1].time;


            ReplayPlayerFrameHalf replayPlayerFrameHalf = new ReplayPlayerFrameHalf
            {
                time = time,
                serverTime = MultiplayerManager.Instance.InRoom ? PhotonNetwork.Time : double.MinValue,
                playingClips = new PlayingClipData[0],
                oneShotEvents = new OneShotEventData[0],
                paramChangeEvents = new AudioParamEventData[0],
                controllerState = ReplayRecorder.Instance.RecordControllerState()
            };

            PlayerTransformStateHalf transformState = default(PlayerTransformStateHalf);
            transformState.boardPosition = fakeSkate.transform.position;
            if (ReplayRecorder.Instance.transformReference.lastState != null) transformState.boardRotation = EnsureQuaternionContinuity(ReplayRecorder.Instance.transformReference.lastState.Value.boardRotation, fakeSkate.transform.rotation);
            else transformState.boardRotation = fakeSkate.transform.rotation;
            transformState.skaterRootPosition = fs.self.transform.position;

            transformState.boardWheelSpeeds.SetAll(0);
            for (int i = 0; i < ReplayRecorder.Instance.transformReference.boardTruckTransforms.Length; i++)
            {
                transformState.boardTruckLocalPositions[i] = fakeTrucks[i].localPosition;

                if (ReplayRecorder.Instance.transformReference.lastState != null)
                {
                    int i2 = i;
                    PlayerTransformStateHalf value = ReplayRecorder.Instance.transformReference.lastState.Value;
                    transformState.boardTruckLocalRotations[i2] = EnsureQuaternionContinuity(value.boardTruckLocalRotations[i], fakeTrucks[i].localRotation);
                }
                else
                {
                    transformState.boardTruckLocalRotations[i] = fakeTrucks[i].localRotation;
                }
            }

            for (int j = 0; j < ReplayRecorder.Instance.transformReference.skaterMainBones.Length; j++)
            {
                Quaternion rot = fs.getPart(ReplayRecorder.Instance.transformReference.skaterMainBones[j].name).localRotation;
                if (ReplayRecorder.Instance.transformReference.lastState != null) transformState.skaterBoneLocalRotations[j] = EnsureQuaternionContinuity(ReplayRecorder.Instance.transformReference.lastState.Value.skaterBoneLocalRotations[j], rot);
                else transformState.skaterBoneLocalRotations[j] = rot;
            }

            for (int k = 0; k < fingers.Length; k++)
            {
                float num2;
                Vector3 a;
                (PlayerTransformStateHalf.fingerRestRotationInverse[k] * this.fingers[k].localRotation).ToAngleAxis(out num2, out a);
                num2 = Mathf.Repeat(num2 + 180f, 360f) - 180f;
                float value2 = Vector3.Dot(num2 * a, PlayerTransformStateHalf.fingerRotationDeltaNormalized[k]) / PlayerTransformStateHalf.fingerRotationDeltaMagnitude[k];
                transformState.fingerLerpValues[k] = Mathf.Clamp01(value2);
            }

            transformState.skaterRootRotation = fs.self.transform.rotation;
            transformState.skaterPelvisLocalPosition = fs.getPart("Skater_pelvis").localPosition;
            transformState.camera.position = fallbackCamera.transform.position;
            transformState.camera.rotation = fallbackCamera.transform.rotation;
            replayPlayerFrameHalf.transformState = transformState;

            ReplayRecorder.Instance.LocalPlayerFrames.Add(replayPlayerFrameHalf);

            try
            {
                Traverse.Create(ReplayRecorder.Instance.transformReference).Field("lastState").SetValue(transformState);
                Traverse.Create(ReplayEditorController.Instance.playbackController.transformReference).Field("lastState").SetValue(transformState);
            }
            catch (Exception e) { Log(e); }
        }

        Vector3 move;
        float LX, LY, RX;

        void UpdateSticks()
        {
            LX = PlayerController.Instance.inputController.player.GetAxis(19);
            LY = PlayerController.Instance.inputController.player.GetAxis(20);
            RX = PlayerController.Instance.inputController.player.GetAxis(21);
        }

        Quaternion cam_rotation = Quaternion.Euler(0, 0, 0);
        Quaternion last_rotation;
        void Movement()
        {
            if (fs.rb)
            {
                move = transform.right * LX + transform.forward * LY;
                Physics.SyncTransforms();
                fs.rb.AddForce(-fs.rb.velocity * decay);
                if (!emoting) fs.rb.MoveRotation(Quaternion.Euler(fs.rb.rotation.eulerAngles.x, fs.rb.rotation.eulerAngles.y + (actual_state == "idle" ? RX : RX / 1.5F), fs.rb.rotation.eulerAngles.z));

                if (!emoting && (LX != 0 || LY != 0)) cam_rotation = Quaternion.Euler(0, 0, 0);

                if (fs.rb.velocity.magnitude <= max_speed && !grinding)
                {
                    fs.rb.AddRelativeForce(move * (speed * (actual_state == "running" ? 1.1f : 1f)));
                }

                last_rotation = fs.self.transform.rotation;
                /*if(grinding)*/
            }
        }

        public int last_dpad = 0;
        void emoteInput()
        {
            if (!Main.ui.emote_config)
            {
                if (GetButtonDown("LB"))
                {
                    for (int i = 67; i <= 70; i++)
                    {
                        if (GetButtonDown(i))
                        {
                            Log(i + " is pressed, A is: " + GetButtonDown("A"));
                            if (GetButtonDown("A"))
                            {
                                setSelectedEmote(getEmote(i).name);
                                Main.ui.emote_config = true;
                                UISounds.Instance.PlayOneShotSelectMajor();
                            }
                            PlayEmote(getEmote(i));
                            last_dpad = i;
                        }
                    }
                }
            }
        }

        void setSelectedEmote(string name)
        {
            for (int i = 0; i < emotes.Count; i++)
            {
                if (emotes[i] == name) Main.ui.selected = i;
            }
        }

        public void changeEmote(string key)
        {
            AnimController new_emote;
            if (!cache.ContainsKey(key))
            {
                new_emote = new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + key + ".json"), fs, false, 1);
                cache.Add(key, new_emote);
            }
            else { new_emote = cache[key]; }

            if (last_dpad == 70)
            {
                emote1 = new_emote;
                //emote1.anchorRoot = true;
                Main.settings.emote1 = key;
            }
            if (last_dpad == 68)
            {
                emote2 = new_emote;
                //emote2.anchorRoot = true;
                Main.settings.emote2 = key;
            }
            if (last_dpad == 69)
            {
                emote3 = new_emote;
                //emote3.anchorRoot = true;
                Main.settings.emote3 = key;
            }
            if (last_dpad == 67)
            {
                emote4 = new_emote;
                //emote4.anchorRoot = true;
                Main.settings.emote4 = key;
            }

            Main.settings.Save(Main.modEntry);
        }

        AnimController getEmote(int i)
        {
            return i == 70 ? emote1 : i == 68 ? emote2 : i == 69 ? emote3 : emote4;
        }

        bool GetButtonDown(string button)
        {
            return PlayerController.Instance.inputController.player.GetButtonDown(button) || PlayerController.Instance.inputController.player.GetButton(button) || PlayerController.Instance.inputController.player.GetButtonShortPressDown(button) || PlayerController.Instance.inputController.player.GetButtonLongPressDown(button);
        }

        bool GetButtonDown(int button)
        {
            return PlayerController.Instance.inputController.player.GetButtonDown(button) || PlayerController.Instance.inputController.player.GetButton(button) || PlayerController.Instance.inputController.player.GetButtonShortPressDown(button) || PlayerController.Instance.inputController.player.GetButtonLongPressDown(button);
        }

        bool backwards = false;
        void HandleAnimations()
        {
            float forward_velocity = relativeVelocity.z;
            forward_velocity = forward_velocity < 0 ? -forward_velocity : forward_velocity;
            float side_velocity = relativeVelocity.x;
            side_velocity = side_velocity < 0 ? -side_velocity : side_velocity;
            backwards = relativeVelocity.z > 0;

            if (actual_state == "idle")
            {
                if (RX != 0)
                {
                    if (RX < 0 && left_turn.name != actual_anim.name) Play(left_turn);
                    if (RX > 0 && right_turn.name != actual_anim.name) Play(right_turn);
                }
                else if (idle.name != actual_anim.name) Play(idle);
            }
            else
            {
                if (actual_state == "running")
                {
                    if (forward_velocity >= side_velocity)
                    {
                        Play(backwards ? running_backwards : running);
                    }
                    else
                    {
                        Play(relativeVelocity.x > 0 ? running_left : running_right);
                    }
                }
                else
                {
                    if (actual_state == "falling") Play(falling);
                    else
                    {
                        if (forward_velocity >= side_velocity)
                        {
                            Play(backwards ? walking_backwards : walking);
                        }
                        else
                        {
                            Play(relativeVelocity.x > 0 ? walking_left : walking_right);
                        }
                    }
                }
            }


        }

        void JumpInput()
        {
            if (PlayerController.Instance.inputController.player.GetButtonSinglePressHold("B")) normalJump();
            else if (PlayerController.Instance.inputController.player.GetButtonDoublePressDown("B"))
            {
                jumping = true;
                CallBack call = OnJumpEnd;
                fs.rb.AddRelativeForce(-move * (speed / 2f));
                Play(backwards ? back_flip : front_flip, call);
            }
        }

        void normalJump()
        {
            jumping = true;
            CallBack call = OnJumpEnd;
            fs.rb.AddRelativeForce(-move * (speed / 3f));
            StopAll();
            Play(actual_state == "idle" ? jump : running_jump, call);
        }

        void JumpingOffset()
        {
            if (actual_anim.name == front_flip.name && front_flip.frame == 16) fs.rb.AddRelativeForce(0, 3f, 0, ForceMode.Impulse);

            if (actual_anim.name == back_flip.name && back_flip.frame == 9) fs.rb.AddRelativeForce(0, 3.2f, 0, ForceMode.Impulse);

            if (actual_anim.name == running_jump.name || actual_anim.name == running_jump.name)
            {
                if (running_jump.frame == 1) fs.rb.AddRelativeForce(0, 2.5f, 0, ForceMode.Impulse);
            }

            if (actual_anim.name == jump.name || actual_anim.name == jump.name)
            {
                if (jump.frame == 22) fs.rb.AddRelativeForce(0, 1f, 0, ForceMode.Impulse);
            }
        }

        GameObject deck_target;
        bool magnetized = true;
        Rigidbody skate_rb;
        bool set_bail = false;

        void Board()
        {
            if (fs.self && fakeSkate)
            {
                if (!skate_rb) skate_rb = fakeSkate.GetComponent<Rigidbody>();
                if (deck_target == null)
                {
                    deck_target = new GameObject();
                    DontDestroyOnLoad(deck_target);
                }

                if (magnetized)
                {
                    bool throwdown_anim = throwdown_state && throwdown.frame > 8;
                    try
                    {
                        if (!skate_rb.isKinematic) skate_rb.isKinematic = true;
                        if (skate_rb.useGravity) skate_rb.useGravity = false;

                        deck_target.transform.position = throwdown_anim ? fs.getPart("Skater_hand_l").transform.position : fs.getPart("Skater_ForeArm_r").transform.position;
                        deck_target.transform.rotation = throwdown_anim ? fs.getPart("Skater_hand_l").transform.rotation : fs.getPart("Skater_ForeArm_r").transform.rotation;

                        if (throwdown_anim && throwdown.frame >= 36)
                        {
                            //deck_target.transform.rotation = Quaternion.identity;
                        }

                        deck_target.transform.Rotate(90f, 0, 0, Space.Self);

                        if (throwdown_anim)
                        {
                            deck_target.transform.Rotate(90f, 0, 0, Space.Self);
                            deck_target.transform.Rotate(0, 0, -90f, Space.Self);
                            deck_target.transform.Rotate(0, -45f, 0, Space.Self);
                            deck_target.transform.Rotate(0, respawnSwitch ? 0f : 180f, 0, Space.Self);

                            if (throwdown.frame >= 36) deck_target.transform.Rotate(respawnSwitch ? -60 : 60, 0, 0, Space.Self);
                        }
                        else
                        {
                            deck_target.transform.Rotate(20f, -10f, -5, Space.Self);
                        }
                        if (throwdown_anim)
                        {
                            if (throwdown.frame < 36) deck_target.transform.Translate(0, -.1f, respawnSwitch ? .4f : -.4f, Space.Self);
                            else deck_target.transform.Translate(0, -.6f, 0, Space.Self);
                        }
                        else deck_target.transform.Translate(-.225f, .035f, -.1f, Space.Self);

                        float multiplier = throwdown_anim && throwdown.frame <= 18 ? .3f : throwdown_anim && throwdown.frame >= 36 ? .16f : 1;
                        fakeSkate.transform.position = Vector3.Lerp(fakeSkate.transform.position, deck_target.transform.position, Time.smoothDeltaTime * 60f * multiplier);
                        fakeSkate.transform.rotation = Quaternion.Slerp(fakeSkate.transform.rotation, deck_target.transform.rotation, Time.smoothDeltaTime * 40f * multiplier);
                    }
                    catch
                    {
                        Log((fs.getPart("Skater_hand_l") == null) + " " + (fs.getPart("Skater_ForeArm_r") == null));
                    }
                }
                else
                {
                    if (skate_rb.isKinematic) skate_rb.isKinematic = false;
                    if (!skate_rb.useGravity) skate_rb.useGravity = true;

                    if (set_bail)
                    {
                        fakeSkate.transform.position = PlayerController.Instance.boardController.boardTransform.position;
                        fakeSkate.transform.rotation = PlayerController.Instance.boardController.boardTransform.rotation;
                        set_bail = false;
                    }
                }
            }
        }

        void ThrowdownInput()
        {
            if (PlayerController.Instance.inputController.player.GetButtonDown("RT") || PlayerController.Instance.inputController.player.GetButtonDown("LT"))
            {
                CallBack call = OnThrowdownEnd;
                Play(throwdown, call);
                throwdown_state = true;
                actual_state = "throwdown";
                respawnSwitch = PlayerController.Instance.inputController.player.GetButtonDown("LT");
                magnetized = true;
                throwed = true;
            }
        }

        float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        public Transform[] fingers;
        void inPlayStateLogic()
        {
            fallbackCamera.transform.position = main_cam.transform.position;
            fallbackCamera.transform.rotation = main_cam.transform.rotation;

            if (PlayerController.Instance.inputController.player.GetButton("A") && PlayerController.Instance.inputController.player.GetButton("X"))
            {
                press_count++;
                if (press_count >= 12)
                {

                    Vector3 old_pos = PlayerController.Instance.skaterController.animBoardTargetTransform.position;
                    Quaternion old_rot = PlayerController.Instance.skaterController.skaterTransform.rotation;

                    DisableGameplay();
                    DestroyFS();
                    createFS();
                    inState = true;

                    cam_rotation = Quaternion.Euler(0, 0, 0);
                    fingers = (from t in fs.getPart("Skater_hand_l").GetComponentsInChildren<Transform>()
                               where !t.name.Contains("hand")
                               select t).Union(from t in fs.getPart("Skater_hand_r").GetComponentsInChildren<Transform>()
                                               where !t.name.Contains("hand")
                                               select t).ToArray();

                    press_count = 0;
                    fs.self.transform.position = old_pos + new Vector3(0, .73f, 0);
                    fs.self.transform.rotation = old_rot;

                    if (PlayerController.Instance.IsSwitch) fs.self.transform.Rotate(0, 180, 0, Space.Self);

                    StopAll();
                    throwdown_state = false;
                    emoting = false;
                    jumping = false;
                    magnetized = true;
                    actual_state = "idle";
                    last_animation = "idle";
                    Play(idle);
                    set_bail = false;
                    last_pos = fs.self.transform.position;
                    instate_count = 0;

                    MonoBehaviourSingleton<PlayerController>.Instance.respawn.DoRespawn();
                }
            }
            else
            {
                press_count = 0;
            }           

            //Throwdown();
        }

        void PlayEmote(AnimController target)
        {
            CallBack call = OnEmoteEnd;
            Play(target, call);
            emoting = true;
            actual_state = "emoting";
        }

        float pelvis_distance = 0f;
        void RaycastPelvis()
        {
            Transform origin = fs.getPart("Skater_pelvis");
            RaycastHit hit;

            if (Physics.Raycast(origin.position, transform.TransformDirection(-Vector3.up), out hit, 3f, LayerUtility.GroundMask))
            {
                pelvis_distance = hit.distance;
            }
        }

        public Vector3 last_offset = Vector3.zero;
        RaycastHit hit_body;
        bool grounded = true;
        int raycastCount = 6;
        float groundRaycastDistance = 3f;
        float pelvis_offset = .26f;
        void RaycastFloor()
        {
            Vector3 center_origin = fs.rb.transform.position + fs.collider.center;
            int mask = LayerUtility.GroundMask;

            Vector3 averageNormal = Vector3.zero, averagePoint = Vector3.zero;
            float averageDistance = 0;
            int hitCount = 0;

            for (int i = 0; i < raycastCount; i++)
            {
                float angle = 360f / raycastCount * i;
                Vector3 direction = -fs.rb.transform.up;
                Vector3 raycastOrigin = center_origin + Quaternion.Euler(0, angle, 0) * fs.rb.transform.right * fs.collider.radius;
                Ray groundRay = new Ray(raycastOrigin, direction);
                RaycastHit groundHit;

                if (Physics.Raycast(groundRay, out groundHit, groundRaycastDistance, mask))
                {
                    averageNormal += groundHit.normal;
                    averagePoint += groundHit.point;
                    averageDistance += groundHit.distance;
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                averageNormal /= hitCount;
                averagePoint /= hitCount;
                averageDistance /= hitCount;

                if (averageDistance <= 1.25f && !respawning)
                {
                    Quaternion rotation = Quaternion.FromToRotation(fs.self.transform.up, averageNormal);
                    fs.collider.transform.rotation = Quaternion.Slerp(fs.collider.transform.rotation, rotation * fs.collider.transform.rotation, Time.smoothDeltaTime * 3f);
                }

                if (averageDistance <= fs.collider.height / 1.5f) grounded = true;
                else grounded = false;
            }
            else
            {
                grounded = false;
            }

            //last_offset = actual_anim.offset;
        }

        bool left_grounded = false, right_grounded = false, last_l_grounded = false, last_r_grounded = false;
        RaycastHit hit_l, hit_r;
        Ray ray_l, ray_r;
        void RaycastFeet()
        {
            Transform left_origin = fs.getPart("Skater_Toe1_l");
            Transform right_origin = fs.getPart("Skater_Toe1_r");

            ray_l = new Ray(left_origin.position, -left_origin.up);
            ray_r = new Ray(right_origin.position, -right_origin.up);

            if (Physics.Raycast(ray_l, out hit_l, .075f, LayerUtility.GroundMask)) left_grounded = true;
            else left_grounded = false;

            if (Physics.Raycast(ray_r, out hit_r, .075f, LayerUtility.GroundMask)) right_grounded = true;
            else right_grounded = false;

            if (!last_l_grounded && left_grounded) PlayRandomOneShotFromArray(sounds, audioSource_left, Main.settings.volume);
            if (!last_r_grounded && right_grounded) PlayRandomOneShotFromArray(sounds, audioSource_right, Main.settings.volume);

            if (left_grounded || right_grounded)
            {
                Movement();
                /*fs.rb.MoveRotation(Quaternion.Slerp(fs.rb.transform.rotation, Quaternion.Euler(0, fs.rb.transform.rotation.eulerAngles.y, 0), Time.smoothDeltaTime * 24F));*/
            }

            if (emoting) cam_rotation = Quaternion.Euler(0, cam_rotation.eulerAngles.y + RX, 0);

            last_l_grounded = left_grounded;
            last_r_grounded = right_grounded;
        }

        RaycastHit hit_stairs;
        Ray stairs;
        void RaycastStairs()
        {
            stairs = new Ray(new Vector3(fs.rb.transform.position.x, fs.rb.transform.position.y - .65f, fs.rb.transform.position.z), fs.rb.transform.forward);
            if (Physics.Raycast(stairs, out hit_stairs, fs.collider.radius * 4f, LayerUtility.GroundMask))
            {
                if (grounded && !jumping) { }
            }
        }

        GameObject target;
        public void UpdateCamera(bool pos, bool rot)
        {
            if (inState)
            {
                if (target == null)
                {
                    target = new GameObject();
                    DontDestroyOnLoad(target);
                }
                target.transform.position = fs.self.transform.position;
                target.transform.rotation = Quaternion.Euler(0, fs.self.transform.rotation.eulerAngles.y + cam_rotation.eulerAngles.y, 0);
                target.transform.Translate(Main.settings.camera_offset, Space.Self);

                if (pos) fallbackCamera.transform.position = Vector3.Lerp(fallbackCamera.transform.position, target.transform.position, Time.smoothDeltaTime * 10f);
                if (rot) fallbackCamera.transform.rotation = Quaternion.Slerp(fallbackCamera.transform.rotation, target.transform.rotation, Time.smoothDeltaTime * 4f);

                main_cam.transform.position = fallbackCamera.transform.position;
                main_cam.transform.rotation = fallbackCamera.transform.rotation;

                fall_cam.m_Lens = main_cam.m_Lens;
            }
        }

        bool updating = false;
        bool check_velocity = false;
        void LateUpdate()
        {
            if (inState)
            {
                if (fs.self != null && fs.rb == null)
                {
                    try
                    {
                        fs.rb = fs.self.AddComponent<Rigidbody>();
                        fs.rb.constraints = RigidbodyConstraints.FreezeRotation;
                        fs.rb.freezeRotation = true;
                        fs.rb.interpolation = RigidbodyInterpolation.Interpolate;
                        fs.rb.angularVelocity = Vector3.zero;
                        fs.rb.velocity = PlayerController.Instance.skaterController.skaterRigidbody.velocity;
                        fs.rb.maxDepenetrationVelocity = 10f;
                        fs.rb.mass = 1.25f;
                        UnityModManager.Logger.Log("Created RB");
                    }
                    catch
                    {
                        UnityModManager.Logger.Log("Error creating RB " + (fs.rb == null));
                    }
                }

                if (!fs.visible && fs.self)
                {
                    fs.show();
                    fakeSkate.SetActive(true);
                }

                UpdateCamera(true, true);
            }
        }

        bool aborted = false;
        int respawn_delay = 0;
        bool pushed = false;
        IEnumerator pushCoroutine;

        public void AddPushForce(float p_value)
        {
            pushCoroutine = PushOverTime(p_value);
            base.StartCoroutine(pushCoroutine);
        }

        IEnumerator PushOverTime(float p_value)
        {
            MonoBehaviourSingleton<PlayerController>.Instance.BoardFreezedAfterRespawn = false;

            p_value /= 10f;
            int _frame = 0;
            while (_frame < 10)
            {
                int num = _frame;
                _frame = num + 1;
                if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude < MonoBehaviourSingleton<PlayerController>.Instance.topSpeed)
                {
                    if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude < 0.15f)
                    {
                        if (Vector3.Angle(MonoBehaviourSingleton<PlayerController>.Instance.PlayerForward(), MonoBehaviourSingleton<PlayerController>.Instance.cameraController._actualCam.forward) < 90f)
                        {
                            PlayerController.Instance.boardController.boardRigidbody.AddForce(MonoBehaviourSingleton<PlayerController>.Instance.PlayerForward() * p_value * 1.4f, ForceMode.Impulse);
                        }
                        else
                        {
                            PlayerController.Instance.boardController.boardRigidbody.AddForce(-MonoBehaviourSingleton<PlayerController>.Instance.PlayerForward() * p_value * 1.4f, ForceMode.Impulse);
                        }
                    }
                    else
                    {
                        PlayerController.Instance.boardController.boardRigidbody.AddForce(PlayerController.Instance.boardController.boardRigidbody.velocity.normalized * p_value, ForceMode.Impulse);
                    }
                }
                yield return new WaitForFixedUpdate();
            }
            yield break;
        }

        void Throwdown()
        {
            if (check_velocity && respawn_delay >= 1)
            {
                //AddPushForce(PlayerController.Instance.GetPushForce() * (.4f + (-relativeVelocity.z * Main.settings.throwdown_force)));
                if (PlayerController.Instance.boardController.GroundCheck()) check_velocity = false;
                PlayerController.Instance.inputController.enabled = true;
                PlayerController.Instance.respawn.SetSpawnPoint(last_nr, Respawn.SpawnPointChangeMethod.Auto);
                AddPushForce(PlayerController.Instance.GetPushForce() * (.35f + (-relativeVelocity.z * Main.settings.throwdown_force)));

                /*Log("Checking " + PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude + " " + (limit_idle * 1.25f));
                if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude <= (limit_idle * 1.25f))
                {
                    PlayerController.Instance.skaterController.skaterTargetTransform.position = MonoBehaviourSingleton<PlayerController>.Instance.skaterController.animBoardTargetTransform.position;
                    PlayerController.Instance.skaterController.skaterTargetTransform.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;

                    float force = PlayerController.Instance.GetPushForce() * (.4f + (-relativeVelocity.z * Main.settings.throwdown_force));
                    //PlayerController.Instance.boardController.boardRigidbody.AddForce(force * PlayerController.Instance.boardController.boardRigidbody.transform.forward, ForceMode.VelocityChange);

                    AddPushForce(PlayerController.Instance.GetPushForce() * (.4f + (-relativeVelocity.z * Main.settings.throwdown_force)));
                    pushed = true;
                }
                else
                {
                    check_velocity = false;
                    PlayerController.Instance.inputController.enabled = true;
                    PlayerController.Instance.respawn.SetSpawnPoint(last_nr, Respawn.SpawnPointChangeMethod.Auto);
                    SoundManager.Instance.deckSounds.UnMuteAll();
                    S
                oundManager.Instance.ragdollSounds.MuteRagdollSounds(false);
                }*/
            }

            if (updating && respawn_delay >= 0)
            {
                updating = false;
                check_velocity = true;
                aborted = false;
                pushed = false;

                //SoundManager.Instance.StopBearingSound();
                SoundManager.Instance.StopGrindSound(0f);
                SoundManager.Instance.StopPowerslideSound(0, 0);
                SoundManager.Instance.StopPowerslideSound(1, 0);
                //DeckSounds.Instance.StopBearingSounds();
                DeckSounds.Instance.powerslideLoopSource.Stop();
                DeckSounds.Instance.powerslideLoopSource2.Stop();

                EventManager.Instance.EnterAir(respawnSwitch ? PopType.Switch : PopType.Ollie);
            }


            if (updating || check_velocity)
            {
                respawn_delay++;
                Traverse.Create(ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1]).Field("didRespawn").SetValue(false);
            }
            else respawn_delay = 0;
        }

        void OnJumpEnd()
        {
            jumping = false;
        }

        RespawnInfo last_nr;
        void OnThrowdownEnd()
        {
            Vector3 forward = fs.rb.transform.forward;

            last_nr = (RespawnInfo)Traverse.Create(MonoBehaviourSingleton<PlayerController>.Instance.respawn).Field("markerRespawnInfos").GetValue();

            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, .7f, 0),
                IsBoardBackwards = false,
                rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(forward) : Quaternion.identity,
                isSwitch = respawnSwitch
            };

            throwdown_state = false;
            updating = true;

            UpdateGameplay();
            EnableGameplay();
            PlayerController.Instance.inputController.enabled = false;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.SetSpawnPoint(respawnInfo);
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.DoRespawn();
            Traverse.Create(ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1]).Field("didRespawn").SetValue(false);
            SoundManager.Instance.deckSounds.UnMuteAll();
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(false);
            PlayerController.Instance.boardController.boardRigidbody.isKinematic = false;
            PlayerController.Instance.boardController.boardRigidbody.useGravity = true;

            PlayerController.Instance.respawn.behaviourPuppet.BoostImmunity(1000f);
            PlayerController.Instance.boardController.ResetCenterOfMass();
            PlayerController.Instance.boardController.boardRigidbody.angularVelocity = Vector3.zero;
            PlayerController.Instance.boardController.boardRigidbody.AddForce((respawnSwitch ? -PlayerController.Instance.PlayerForward() : PlayerController.Instance.PlayerForward()) * (15f + (20 * -relativeVelocity.z * Main.settings.throwdown_force)), ForceMode.Impulse);
        }


        void UpdateGameplay()
        {
            PlayerController.Instance.skaterController.skaterTransform.position = fs.self.transform.position;
            PlayerController.Instance.skaterController.skaterTransform.rotation = fs.self.transform.rotation;
            PlayerController.Instance.boardController.boardTransform.position = fakeSkate.transform.position;
            PlayerController.Instance.boardController.boardTransform.rotation = fakeSkate.transform.rotation;
            PlayerController.Instance.comController.transform.position = fs.self.transform.position;
            PlayerController.Instance.comController.transform.rotation = fs.self.transform.rotation;
            PlayerController.Instance.comController.COMRigidbody.position = fs.self.transform.position;
            PlayerController.Instance.comController.COMRigidbody.transform.position = fs.self.transform.position;
            PlayerController.Instance.skaterController.leanProxy.position = fs.self.transform.position;
            PlayerController.Instance.skaterController.leanProxy.transform.position = fs.self.transform.position;
            PlayerController.Instance.skaterController.leanProxy.rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(fs.rb.transform.forward) : Quaternion.identity;
            PlayerController.Instance.skaterController.leanProxy.transform.rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(fs.rb.transform.forward) : Quaternion.identity;

            PlayerController.Instance.skaterController.skaterTargetTransform.position = MonoBehaviourSingleton<PlayerController>.Instance.skaterController.animBoardTargetTransform.position;
            PlayerController.Instance.skaterController.skaterTargetTransform.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;
        }

        Transform[] original_bones;
        void TogglePlayObject(bool enabled)
        {
            if (MultiplayerManager.Instance.InRoom)
            {
                PlayerController.Instance.animationController.ikAnim.enabled = enabled;
                PlayerController.Instance.animationController.skaterAnim.enabled = enabled;
            }
            else
            {
                GameStateMachine.Instance.PlayObject.SetActive(enabled);
            }

            if (enabled)
            {
                for (int i = 0; i < PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles.Length; i++)
                {
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.isKinematic = false;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.useGravity = true;
                }
            }
            else PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.DisableImmediately();

            Log("Toggled play object " + enabled);

            /*GameStateMachine.Instance.PlayObject.transform.Find("GameplayUI").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("Behaviours").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("IK").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("Skateboard").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("References").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("Input Thread").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("Lean Proxy").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("CenterOfMass").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("CenterOfMassPlayer").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("SkaterTarget").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("TransitionDetection").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("Camera Rig").gameObject.SetActive(enabled);
            GameStateMachine.Instance.PlayObject.transform.Find("PuppetMaster").gameObject.SetActive(enabled);
            //GameStateMachine.Instance.PlayObject.transform.Find("NewSkater").gameObject.SetActive(enabled);

            GameStateMachine.Instance.PlayObject.GetComponent<PlayerController>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<InputController>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<SettingsManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<SoundManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<PlayerPrefsManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<EventManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<TrickManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<RagdollSounds>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<CharacterCustomizer>().enabled = enabled;*/
        }

        void ReplaceBones(bool enabled)
        {
            Transform[] bones = GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>().skaterMainBones;

            if (original_bones == null)
            {
                original_bones = new Transform[32];
                Array.Copy(bones, original_bones, 32);
            }

            Log("Replacing bones");

            if (enabled)
            {
                if (original_bones != null)
                {
                    GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>().skaterMainBones = original_bones;
                }
            }
            else
            {
                if (fs.self)
                {
                    PlayerTransformReference ptr = GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>();
                    ptr.skaterMainBones = new Transform[]
                    {
                        fs.getPart("Skater_pelvis"),
                        fs.getPart("Skater_Spine"),
                        fs.getPart("Skater_Spine1"),
                        fs.getPart("Skater_Spine2"),
                        fs.getPart("Skater_Neck"),
                        fs.getPart("Skater_Head"),
                        fs.getPart("Skater_Shoulder_l"),
                        fs.getPart("Skater_Arm_l"),
                        fs.getPart("Skater_Arm_twist_01_l"),
                        fs.getPart("Skater_ForeArm_l"),
                        fs.getPart("Skater_ForeArm_twist_01_l"),
                        fs.getPart("Skater_hand_l"),
                        fs.getPart("Skater_Shoulder_r"),
                        fs.getPart("Skater_Arm_r"),
                        fs.getPart("Skater_Arm_twist_01_r"),
                        fs.getPart("Skater_ForeArm_r"),
                        fs.getPart("Skater_ForeArm_twist_01_r"),
                        fs.getPart("Skater_hand_r"),
                        fs.getPart("Skater_UpLeg_l"),
                        fs.getPart("Skater_Leg_l"),
                        fs.getPart("Skater_foot_l"),
                        fs.getPart("Skater_Toe1_l"),
                        fs.getPart("Skater_Toe2_l"),
                        fs.getPart("Skater_Leg_twist_01_l"),
                        fs.getPart("Skater_UpLeg_twist_01_l"),
                        fs.getPart("Skater_UpLeg_r"),
                        fs.getPart("Skater_Leg_r"),
                        fs.getPart("Skater_foot_r"),
                        fs.getPart("Skater_Toe1_r"),
                        fs.getPart("Skater_Toe2_r"),
                        fs.getPart("Skater_Leg_twist_01_r"),
                        fs.getPart("Skater_UpLeg_twist_01_r")
                    };

                    MultiplayerManager.Instance.localPlayer.transformSyncer.transformReference.skaterMainBones = ptr.skaterMainBones;
                }
            }
        }

        void OnEmoteEnd()
        {
            UnityModManager.Logger.Log("OnEmoteEnd");
            emoting = false;
        }

        void Play(AnimController target)
        {
            if (actual_anim.name == target.name && target.isPlaying) return;
            if ((throwdown_state && actual_anim.name == throwdown.name) || emoting) return;
            //Log(target.name + " normal");
            actual_anim.Stop();
            actual_anim = target;
            target.Play();
        }

        void Play(AnimController target, CallBack call)
        {
            if (actual_anim.name == target.name && target.isPlaying) return;
            if ((throwdown_state && actual_anim.name == throwdown.name) || emoting) return;
            //Log(target.name + " callback");
            actual_anim.Stop();
            actual_anim = target;
            target.Play(call);
        }

        void StopAll()
        {
            for (int i = 0; i < animations.Length; i++)
            {
                try
                {
                    animations[i].Stop();
                }
                catch { }
            }
        }

        GameObject copy;
        Vector3 translateLocal(Transform origin, Vector3 offset)
        {
            if (copy == null)
            {
                copy = new GameObject();
                DontDestroyOnLoad(copy);
            }
            copy.transform.position = origin.position;
            copy.transform.rotation = origin.rotation;

            copy.transform.Translate(offset, Space.Self);

            Vector3 result = copy.transform.position;
            return result;
        }

        private AudioClip GetClip(string path)
        {
            WWW audioLoader = new WWW(path);
            while (!audioLoader.isDone) System.Threading.Thread.Sleep(1);
            return audioLoader.GetAudioClip();
        }

        private void PlayRandomOneShotFromArray(AudioClip[] array, AudioSource source, float vol)
        {
            if (!source.isPlaying)
            {
                if (array == null || array.Length == 0)
                {
                    return;
                }
                int num = UnityEngine.Random.Range(0, array.Length);
                source.clip = array[num];
                source.volume = vol;
                source.Play();
            }
        }

        bool grinding = false;
        SplineResult last_result;
        public void doGrind(SplineComputer spline)
        {
            grinding = true;
            double percent = spline.Project(fs.self.transform.position, 3, 0.0, 1.0);
            SplineResult p_splineResult = spline.Evaluate(percent);
            last_result = p_splineResult;
            fs.rb.centerOfMass = fs.self.transform.InverseTransformPoint(p_splineResult.position);
            fs.rb.ResetInertiaTensor();
        }
    }
}