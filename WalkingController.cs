using Cinemachine;
using Dreamteck.Splines;
using ExitGames.Client.Photon;
using GameManagement;
using HarmonyLib;
using ModIO.UI;
using Photon.Pun;
using Photon.Realtime;
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
    public class WalkingController : MonoBehaviour, IOnEventCallback
    {
        FakeSkater fs = new FakeSkater();
        AnimController walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown, falling, impact_roll, stumble, stairs_up;
        public AnimController emote1, emote2, emote3, emote4;
        public AudioClip semote1, semote2, semote3, semote4;
        AnimController[] animations;
        AudioClip[] sounds;
        public AnimController actual_anim;
        public bool inState = false;
        GameObject fallbackCamera;
        GameObject fakeSkate;
        Transform[] fakeTrucks = new Transform[2];
        CinemachineVirtualCamera main_cam, fall_cam;
        PlayTime playtimeobj;
        public List<string> emotes, soundEmotes;
        IDictionary<string, AudioClip> audioCache = new Dictionary<string, AudioClip>();
        IDictionary<string, AnimController> cache = new Dictionary<string, AnimController>();
        public string last_animation = "";
        TrickUIController trickUIController;

        bool init = false;

        void Init()
        {
            init = true;
            try
            {
                playtimeobj = GameObject.Find("PlayTime").GetComponent<PlayTime>();
            }
            catch
            {
                Log("Error getting PlayTime component");
            }

            InitAnimations();
            animations = new AnimController[] { walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown, falling, impact_roll, stumble, stairs_up };

            fallbackCamera = PlayerController.Instance.skaterController.transform.parent.parent.Find("Fallback Camera").gameObject;
            fall_cam = fallbackCamera.GetComponent<CinemachineVirtualCamera>();
            main_cam = PlayerController.Instance.cameraController._actualCam.GetComponent<CinemachineVirtualCamera>();

            actual_anim = new AnimController();
            //SceneManager.activeSceneChanged += OnSceneLoaded;

            LoadEmotes();

            string[] sound_emotes = Directory.GetFiles(Path.Combine(Main.modEntry.Path, "sounds"), "*.wav");
            soundEmotes = new List<string>();
            for (int i = 0; i < sound_emotes.Length; i++)
            {
                string[] pieces = sound_emotes[i].Split(Path.DirectorySeparatorChar);
                string name = pieces[pieces.Length - 1].Replace(".wav", String.Empty);
                bool add = true;

                for (int j = 0; j < animations.Length - 4; j++)
                {
                    if (name.Contains("footstep_")) add = false;
                }

                if (add)
                {
                    AudioClip clip = GetClip(Path.Combine(Main.modEntry.Path, "sounds\\" + name + ".wav"));
                    soundEmotes.Add(name);
                    audioCache.Add(name, clip);
                }
            }

            InitSounds();
            sounds = new AudioClip[4] { step, step2, step3, step4 };
        }

        public void LoadEmotes()
        {
            string[] temp_emotes = Directory.GetFiles(Path.Combine(Main.modEntry.Path, "animations"), "*.json");
            emotes = new List<string>();
            for (int i = 0; i < temp_emotes.Length; i++)
            {
                string[] pieces = temp_emotes[i].Split(Path.DirectorySeparatorChar);
                string name = pieces[pieces.Length - 1].Replace(".json", String.Empty);
                bool add = true;
                for (int j = 0; j < animations.Length; j++)
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
            walking_backwards = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking_backwards.json"), fs, true, walking_crossfade));
            walking_left = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking_left.json"), fs, true, walking_crossfade));
            walking_right = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking_right.json"), fs, true, walking_crossfade));

            running = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running.json"), fs, true, walking_crossfade));
            running.speed = 1.05f;

            running_backwards = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_backwards.json"), fs, true, walking_crossfade));
            running_left = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_left.json"), fs, true, walking_crossfade));
            running_right = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_right.json"), fs, true, walking_crossfade));
            idle = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\idle.json"), fs));
            jump = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\jumping.json"), fs, false));

            running_jump = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\running_jump.json"), fs, false, true));
            running_jump.crossfade = 1;
            running_jump.anchorRootFade = false;

            left_turn = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\left_turn.json"), fs));
            right_turn = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\right_turn.json"), fs));
            front_flip = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\front_flip.json"), fs, false, true));

            back_flip = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\back_flip.json"), fs, false, true));
            back_flip.speed = 1.1f;

            throwdown = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\throwdown.json"), fs, false, true));
            throwdown.speed = 1.1f;

            falling = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\falling.json"), fs, Quaternion.Euler(0, 0, 0)));
            falling.crossfade = 6;
            falling.anchorRoot = true;
            falling.anchorRootFade = false;

            impact_roll = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\impact_roll.json"), fs, false));
            impact_roll.crossfade = 1;
            impact_roll.speed = 1.25f;

            stumble = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\stumble.json"), fs, false));
            stumble.crossfade = 1;
            stumble.speed = 1.25f;

            stairs_up = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\stairs.json"), fs));
            stairs_up.anchorRoot = true;
            stairs_up.anchorRootFade = false;

            emote1 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote1 + ".json"), fs, false, 1));
            emote2 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote2 + ".json"), fs, false, 1));
            emote3 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote3 + ".json"), fs, false, 1));
            emote4 = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\" + Main.settings.emote4 + ".json"), fs, false, 1));
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

            semote1 = audioCache[Main.settings.semote1];
            semote2 = audioCache[Main.settings.semote2];
            semote3 = audioCache[Main.settings.semote3];
            semote4 = audioCache[Main.settings.semote4];
        }

        public float speed = 10f;
        public float jumpForce = 10.0f;
        public Vector3 velocity;
        bool jumping = false, throwdown_state = false;
        int press_count = 0;
        string actual_state = "";
        float max_speed = 50f;
        float running_speed = 4.2f;
        bool emoting = false;
        bool respawnSwitch = false;
        float limit_idle = .3f;
        float decay = .95f;

        Vector3 last_pos = Vector3.zero;
        string last_restore_state = "";

        void FixedUpdate()
        {
            if (!init) return;

            if (inState == true) inStateLogic();
            else inPlayStateLogic();

            if (push_frame < 4) PushOverTime(last_force);
        }

        bool should_run = false, throwed = false;
        void Update()
        {
            if (PlayerController.Instance.inputController.controlsActive && !init) Init();

            if (!init) return;

            if (!inState && (updating || check_velocity || delay_input)) Throwdown();

            if (GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState) && GameStateMachine.Instance.CurrentState.GetType() != typeof(PauseState) && inState)
            {
                if (GameStateMachine.Instance.CurrentState.GetType() == typeof(GearSelectionState))
                {
                }
                else RestoreGameplay(false, false);

                last_restore_state = GameStateMachine.Instance.CurrentState.GetType().ToString();
            }
            else
            {
                if (last_restore_state == typeof(GearSelectionState).ToString() && !GameStateMachine.Instance.loadingScreenController.LoadingCanvas.activeSelf)
                {
                    RestoreGameplay(true, false);
                    last_restore_state = "";
                }
            }
        }

        void Log(object log)
        {
            UnityModManager.Logger.Log("[walking-mod] " + log.ToString());
        }

        void DestroyFS()
        {
            try
            {
                audioSource_left.Stop();
                audioSource_right.Stop();
                soundEmoteSource.Stop();
            }
            catch { }

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
                trickUIController = null;
            }

            original_bones = null;
        }

        AudioSource audioSource_left, audioSource_right, soundEmoteSource;
        SphereCollider slide_collider;
        GameObject trickListCopy;
        void createFS()
        {
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

                GameObject emote_source = new GameObject("Emote Audio Source");
                soundEmoteSource = emote_source.AddComponent<AudioSource>();
                //right_source.AddComponent<AudioSourceTracker>();
                emote_source.transform.parent = fs.self.transform;

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

                /*trickListCopy = Instantiate(PlayerController.Instance.gameplayUI);
                trickListCopy.transform.parent = fs.self.transform;
                trickUIController = trickListCopy.transform.Find("Tricks UI").gameObject.GetComponent<TrickUIController>();
                Log(trickUIController);*/

                if (MultiplayerManager.Instance.InRoom)
                {
                    PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(false);
                    PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(false);
                }
            }
            catch
            {
                Log("Error creating FS");
            }
        }

        float last_nonplaytime = 0;
        void DisableGameplay()
        {
            GameStateMachine.Instance.PinObject.SetActive(false);
            EventManager.Instance.EndTrickCombo(true, false);
            //EventManager.Instance.EndTrickCombo(false, true);
            TogglePlayObject(false);
            ReplaceBones(false);
            last_nonplaytime = (float)Traverse.Create(playtimeobj).Field("nonPlayTime").GetValue();
            PlayerController.Instance.inputController.enabled = false;

            SoundManager.Instance.deckSounds.MuteAll();
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(true);
        }

        public void EnableGameplay(bool playObject = true)
        {
            inState = false;
            DestroyFS();
            PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(true);
            //EventManager.Instance.EndTrickCombo(false, true);
            Log("Enabling gameplay " + playObject);
            if (playObject)
            {
                EventManager.Instance.EndTrickCombo(true, false);
                PlayerController.Instance.EnableGameplay();
                TogglePlayObject(true);
            }

            PlayerController.Instance.animationController.ikAnim.enabled = true;
            PlayerController.Instance.animationController.skaterAnim.enabled = true;

            PlayerController.Instance.inputController.enabled = true;
            ReplaceBones(true);

            SoundManager.Instance.deckSounds.UnMuteAll();
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(false);

            PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(true);
            if (last_nonplaytime != 0) Traverse.Create(playtimeobj).Field("nonPlayTime").SetValue(last_nonplaytime);
        }

        Quaternion EnsureQuaternionContinuity(Quaternion last, Quaternion curr)
        {
            if (last.x * curr.x + last.y * curr.y + last.z * curr.z + last.w * curr.w < 0f)
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

        int instate_count = 0;
        Vector3 relativeVelocity;
        Quaternion last_rotation_offset = Quaternion.Euler(0, 0, 0);
        Vector3 last_velocity;
        float rotation_speed = 12f;
        void inStateLogic()
        {
            if (!fs.self || !fs.rb || !fakeSkate || GameStateMachine.Instance.CurrentState.GetType() == typeof(PauseState)) return;

            if (!MultiplayerManager.Instance.InRoom) AddReplayFrame();
            else CheckAudioSources();


            RotationOffset();

            try { actual_anim.FixedUpdate(); } catch { Log("Error updating animation " + inState); }

            Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(true);

            UpdateSticks();
            RaycastPelvis();
            RaycastFloor();
            RaycastFeet();
            RaycastStairs();
            emoteInput();
            soundEmoteInput();

            if (MultiplayerManager.Instance.InRoom && !respawning) UpdateRagdoll();

            PlayerController.Instance.boardController.boardRigidbody.isKinematic = magnetized;

            UpdateGameplay();

            if (!jumping && actual_state != "impact" && !climbingStairs && actual_state != "stumble")
            {
                if (Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) >= running_speed) actual_state = "running";
                else
                {
                    if (Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) <= limit_idle) actual_state = "idle";
                    else actual_state = "walking";
                }
            }

            if (climbingStairs)
            {
                if (actual_anim.name != stairs_up.name) Play(stairs_up);
                if (shouldMoveStairs) fs.rb.MovePosition(Vector3.Lerp(fs.rb.position, new Vector3(up_hit.point.x, up_hit.point.y + (fs.collider.height / 2f), up_hit.point.z), Time.deltaTime * 6f));
            }

            if (relativeVelocity.y > 0.04f && !grounded && !jumping && actual_state != "impact" && !climbingStairs && actual_state != "stumble") actual_state = "falling";

            relativeVelocity = fs.rb.transform.InverseTransformDirection(last_pos - fs.self.transform.position);
            last_pos = fs.self.transform.position;

            if (!jumping)
            {
                if (!emoting && actual_state != "impact" && !climbingStairs && actual_state != "stumble")
                {
                    HandleAnimations();
                    JumpInput();
                }
            }
            else JumpingOffset();

            Board();

            if (!emoting) ThrowdownInput();

            if (PlayerController.Instance.inputController.player.GetButtonDown("Y"))
            {
                RestoreGameplay(true, false);
                PlayerController.Instance.respawn.DPadUp();
                Traverse.Create(ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1]).Field("didRespawn").SetValue(false);
                //Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(false);
            }
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

        public void RestoreGameplay(bool originalRespawn = false, bool playObj = true)
        {
            try
            {
                PlayerController.Instance.inputController.enabled = true;
                PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(true);
                PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(true);

                if (fs.rb != null && !originalRespawn)
                {
                    UpdateGameplay();
                    RespawnInfo respawnInfo = new RespawnInfo
                    {
                        position = fs.self.transform.position - new Vector3(0, .7f, 0),
                        IsBoardBackwards = false,
                        rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(fs.rb.transform.forward) : Quaternion.identity,
                        isSwitch = false
                    };

                    EnableGameplay(playObj);
                    RespawnRoutine(respawnInfo);
                }
                else
                {
                    EnableGameplay(playObj);
                    if (!originalRespawn)
                    {
                        RespawnRoutine((RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue());
                    }
                    else PlayerController.Instance.respawn.DoRespawn();
                }

                if (!originalRespawn) RespawnRoutineCoroutines();

                Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(GameStateMachine.Instance.CurrentState.GetType() == typeof(PlayState));
            }
            catch (Exception e)
            {
                Log("Error restoring gameplay");
                Log(e.Message);
            }
        }

        void UpdateRagdoll()
        {
            PlayerController.Instance.respawn.recentlyRespawned = false;
            PlayerController.Instance.respawn.behaviourPuppet.StopAllCoroutines();
            PlayerController.Instance.respawn.behaviourPuppet.unpinnedMuscleKnockout = false;
            PlayerController.Instance.respawn.behaviourPuppet.SetState(BehaviourPuppet.State.Puppet);

            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.internalCollisions = false;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.position = fs.self.transform.position;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.rotation = fs.self.transform.rotation;

            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscleWeight = 0;
            PlayerController.Instance.respawn.puppetMaster.pinWeight = 0f;

            PlayerController.Instance.animationController.skaterAnim.enabled = false;
            PlayerController.Instance.animationController.ikAnim.enabled = false;

            PlayerController.Instance.respawn.behaviourPuppet.defaults.minMappingWeight = 1f;
            PlayerController.Instance.respawn.behaviourPuppet.masterProps.normalMode = BehaviourPuppet.NormalMode.Active;

            PlayerController.Instance.EnablePuppetMaster(false, true);

            for (int i = 0; i < PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles.Length; i++)
            {
                Transform part = fs.getPart(PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].name);
                if (part != null)
                {
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.isKinematic = true;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.useGravity = false;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].transform.position = part.position;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].transform.rotation = part.rotation;
                }
            }
        }

        bool respawning = false;
        void DoRespawn()
        {
            if (Time.unscaledTime - enterBailTimestamp >= Main.settings.bailLimit)
            {
                respawning = true;
                last_nr = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();
                fs.self.transform.position = new Vector3(last_nr.position.x, last_nr.position.y + .76f, last_nr.position.z);
                fs.self.transform.rotation = last_nr.rotation;
                fs.rb.velocity = Vector3.zero;
                fs.rb.angularVelocity = Vector3.zero;
                PlayerController.Instance.respawn.puppetMaster.Teleport(fs.self.transform.position + fs.self.transform.rotation * PlayerController.Instance.respawn.GetOffsetPositions(false)[1] + (Vector3)Traverse.Create(PlayerController.Instance.respawn).Field("_playerOffset").GetValue(), fs.self.transform.rotation, false);
            }
            else
            {
                RestoreGameplay(true);
                PlayerController.Instance.respawn.DPadUp();
            }
        }

        void SetRespawn()
        {
            DPadDown();
            /*RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, .715f, 0),
                IsBoardBackwards = false,
                rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(fs.rb.velocity) : Quaternion.identity,
                isSwitch = false
            };
            PlayerController.Instance.respawn.SetSpawnPoint(respawnInfo);*/
        }

        float last_dpad_down = 0f;
        void DPadDown()
        {
            if (Time.unscaledTime - last_dpad_down >= .4f)
            {
                last_dpad_down = Time.unscaledTime;
                PlayerController.Instance.respawn.SetSpawnPos(fs.self.transform.position - new Vector3(0, .73f, 0), PlayerController.Instance.skaterController.skaterTransform.rotation * last_rotation_offset, false);
                UISounds.Instance.PlayOneShotSelectMajor();
                Animator componentInChildren = PlayerController.Instance.respawn.pin.GetComponentInChildren<Animator>();
                PlayerController.Instance.respawn.pin.gameObject.SetActive(true);
                if (componentInChildren != null)
                {
                    componentInChildren.SetTrigger("Placed");
                }
            }
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
            if (ReplayRecorder.Instance.transformReference.lastState != null && throwdown_state && !respawnSwitch) transformState.boardRotation = EnsureQuaternionContinuity(ReplayRecorder.Instance.transformReference.lastState.Value.boardRotation, fakeSkate.transform.rotation);
            else transformState.boardRotation = fakeSkate.transform.rotation;

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

            transformState.skaterRootPosition = fs.self.transform.position;
            if (ReplayRecorder.Instance.transformReference.lastState != null)
            {
                transformState.skaterRootRotation = this.EnsureQuaternionContinuity(ReplayRecorder.Instance.transformReference.lastState.Value.skaterRootRotation, fs.self.transform.rotation * Quaternion.identity);
            }
            else
            {
                transformState.skaterRootRotation = fs.self.transform.rotation * Quaternion.identity;
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

            transformState.secondaryDeformerLeft = fs.getPart("LeftLegJacket").localPosition.x;
            transformState.secondaryDeformerRight = fs.getPart("RightLegJacket").localPosition.x;

            transformState.skaterPelvisLocalPosition = fs.getPart("Skater_pelvis").localPosition;
            transformState.camera.position = fallbackCamera.transform.position;
            transformState.camera.rotation = fallbackCamera.transform.rotation;
            transformState.didRespawn = false;
            replayPlayerFrameHalf.transformState = transformState;

            ReplayRecorder.Instance.LocalPlayerFrames.Add(replayPlayerFrameHalf);

            try
            {
                Traverse.Create(ReplayRecorder.Instance.transformReference).Field("lastState").SetValue(transformState);
                Traverse.Create(ReplayEditorController.Instance.playbackController.transformReference).Field("lastState").SetValue(transformState);
                Traverse.Create(ReplayRecorder.Instance).Field("nextRecordTime").SetValue(time + (1f / ReplaySettings.Instance.FPS));
            }
            catch (Exception e) { Log(e); }
        }


        void RotationOffset()
        {
            if (relativeVelocity != Vector3.zero && Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) > limit_idle && instate_count >= 24)
            {
                Vector3 rotationHorizontal = new Vector3(relativeVelocity.x, 0, relativeVelocity.z);
                if (!Sideway() && !Rotating() && !jumping && actual_state != "falling" && actual_state != "idle")
                {
                    actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.LookRotation(backwards ? rotationHorizontal : -rotationHorizontal), Time.smoothDeltaTime * rotation_speed);
                }
                else
                {
                    if (Sideway())
                    {
                        Quaternion lr = Quaternion.LookRotation(rotationHorizontal);
                        actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.Euler(lr.eulerAngles.x, lr.eulerAngles.y + (rotationHorizontal.x > 0 ? -90 : 90), lr.eulerAngles.z), Time.smoothDeltaTime * rotation_speed);
                    }
                    else
                    {
                        if (jumping || actual_state == "stumble" || actual_state == "impact")
                        {
                            if (actual_state != "idle")
                            {
                                Quaternion lr = Quaternion.LookRotation(backwards ? rotationHorizontal : -rotationHorizontal);
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
        }

        Vector3 move;
        float LX, LY, RX, RY;
        void UpdateSticks()
        {
            LX = PlayerController.Instance.inputController.player.GetAxis(19);
            LY = PlayerController.Instance.inputController.player.GetAxis(20);
            RX = PlayerController.Instance.inputController.player.GetAxis(21);
            RY = PlayerController.Instance.inputController.player.GetAxis(22);
        }

        Quaternion cam_rotation = Quaternion.Euler(0, 0, 0);
        void Movement()
        {
            if (fs.rb)
            {
                Physics.SyncTransforms();
                if (left_grounded || right_grounded || climbingStairs)
                {
                    move = transform.right * LX + transform.forward * LY;
                    fs.rb.AddForce(-fs.rb.velocity * decay);
                    if (fs.rb.velocity.magnitude <= max_speed && !grinding)
                    {
                        fs.rb.AddRelativeForce(move * (speed * (actual_state == "running" ? 1.25f : 1f)));
                    }
                }

                if (!emoting) fs.rb.MoveRotation(Quaternion.Euler(fs.rb.rotation.eulerAngles.x, fs.rb.rotation.eulerAngles.y + (actual_state == "idle" ? RX : RX / 1.5F), fs.rb.rotation.eulerAngles.z));

                if (!emoting && (LX != 0 || LY != 0)) cam_rotation = Quaternion.Euler(0, 0, 0);
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

        void soundEmoteInput()
        {
            if (!Main.ui.sound_emote_config)
            {
                if (GetButtonDown("RB"))
                {
                    for (int i = 67; i <= 70; i++)
                    {
                        if (GetButtonDown(i))
                        {
                            if (GetButtonDown("A"))
                            {
                                setSelectedSoundEmote(getSoundEmoteString(i));
                                Main.ui.sound_emote_config = true;
                                UISounds.Instance.PlayOneShotSelectMajor();
                            }
                            PlaySoundEmote(getSoundEmote(i), Main.settings.emote_volume, getSoundEmoteString(i));
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

        void setSelectedSoundEmote(string name)
        {
            for (int i = 0; i < soundEmotes.Count; i++)
            {
                if (soundEmotes[i] == name) Main.ui.selected = i;
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

        AudioClip getSoundEmote(int i)
        {
            return i == 70 ? semote1 : i == 68 ? semote2 : i == 69 ? semote3 : semote4;
        }

        string getSoundEmoteString(int i)
        {
            return i == 70 ? Main.settings.semote1 : i == 68 ? Main.settings.semote2 : i == 69 ? Main.settings.semote3 : Main.settings.semote4;
        }

        bool SinglePress(string button)
        {
            return PlayerController.Instance.inputController.player.GetButtonSinglePressHold(button) || PlayerController.Instance.inputController.player.GetButtonSinglePressDown(button);
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
            if (SinglePress("B")) normalJump();
            else if (PlayerController.Instance.inputController.player.GetButtonDoublePressHold("B") || PlayerController.Instance.inputController.player.GetButtonDoublePressDown("B")) doubleJump();
        }

        void normalJump()
        {
            jumping = true;
            fs.rb.AddRelativeForce(-move * (speed / 3f));
            CallBack call = OnJumpEnd;
            Play(actual_state == "idle" ? jump : running_jump, call);
        }

        void doubleJump()
        {
            jumping = true;
            fs.rb.AddRelativeForce(-move * (speed / 2f));
            CallBack call = OnJumpEnd;
            Play(backwards ? back_flip : front_flip, call);
        }

        void JumpingOffset()
        {
            if (actual_anim.name == front_flip.name && front_flip.frame == 16) fs.rb.AddRelativeForce(0, 3f, 0, ForceMode.Impulse);

            if (actual_anim.name == back_flip.name && back_flip.frame == 18) fs.rb.AddRelativeForce(0, 3f, 0, ForceMode.Impulse);

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
                        fakeSkate.transform.rotation = Quaternion.Slerp(fakeSkate.transform.rotation, deck_target.transform.rotation, Time.smoothDeltaTime * 72f * multiplier);
                        fakeSkate.transform.position = Vector3.Lerp(fakeSkate.transform.position, deck_target.transform.position, Time.smoothDeltaTime * 72f * multiplier);
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
            if (rt_onspawn || lt_onspawn || Time.unscaledTime - enterBailTimestamp <= .4f)
            {
                if (PlayerController.Instance.inputController.player.GetButtonUp("RT") || !PlayerController.Instance.inputController.player.GetButton("RT")) rt_onspawn = false;
                if (PlayerController.Instance.inputController.player.GetButtonUp("LT") || !PlayerController.Instance.inputController.player.GetButton("LT")) lt_onspawn = false;
            }
            else
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
        }

        float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        public Transform[] fingers;
        float bail_magnitude = 0;
        bool projected = false;
        Vector3 velocityOnEnter = Vector3.zero;
        float respawnHeightOffset = .25f;
        bool lt_onspawn = false, rt_onspawn = false;
        float dot = 0;
        float enterBailTimestamp = 0f;

        void inPlayStateLogic()
        {
            fallbackCamera.transform.position = main_cam.transform.position;
            fallbackCamera.transform.rotation = main_cam.transform.rotation;

            if (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed)
            {
                if (!projected)
                {
                    dot = Quaternion.Dot(PlayerController.Instance.skaterController.skaterTransform.rotation, Quaternion.LookRotation(PlayerController.Instance.boardController.boardRigidbody.velocity));
                    bail_magnitude = Vector3.ProjectOnPlane(PlayerController.Instance.skaterController.skaterRigidbody.velocity, Vector3.up).magnitude;
                    projected = true;
                }
            }
            else
            {
                projected = false;
                bail_magnitude = 0;
            }

            if ((PlayerController.Instance.inputController.player.GetButton("A") && PlayerController.Instance.inputController.player.GetButton("X")) || (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed && bail_magnitude <= Main.settings.max_magnitude_bail))
            {
                press_count++;
                bool bailmode = (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed && dot >= 0f);
                if (press_count >= 12 || bailmode)
                {
                    DisableGameplay();
                    DestroyFS();
                    createFS();

                    enterBailTimestamp = Time.unscaledTime;
                    rt_onspawn = PlayerController.Instance.inputController.player.GetButtonDown("RT");
                    lt_onspawn = PlayerController.Instance.inputController.player.GetButtonDown("LT");

                    bool pressed = PlayerController.Instance.inputController.player.GetButton("A") && PlayerController.Instance.inputController.player.GetButton("X");
                    velocityOnEnter = PlayerController.Instance.skaterController.skaterRigidbody.velocity;
                    RaycastHit hit_ground;
                    Vector3 raycastOrigin = PlayerController.Instance.skaterController.skaterTransform.position + new Vector3(0, 2f, 0);
                    Vector3 old_pos = PlayerController.Instance.skaterController.skaterTransform.position;
                    if (!bailmode)
                    {
                        if (Physics.Raycast(raycastOrigin, Vector3.down, out hit_ground))
                        {
                            old_pos = hit_ground.point + new Vector3(0, fs.collider.height / 2f, 0);
                        }
                        else
                        {
                            old_pos = PlayerController.Instance.skaterController.skaterTransform.position + new Vector3(0, fs.collider.height / 2f, 0);
                        }
                    }
                    else
                    {
                        PlayerController.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);
                        fakeSkate.GetComponent<Rigidbody>().velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
                        fakeSkate.GetComponent<Rigidbody>().angularVelocity = PlayerController.Instance.boardController.boardRigidbody.angularVelocity;
                    }

                    Quaternion look = Quaternion.LookRotation(PlayerController.Instance.boardController.boardRigidbody.velocity);
                    Quaternion old_rot = PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude > .15f && PlayerController.Instance.boardController.Grounded ? Quaternion.Euler(0, look.eulerAngles.y, 0) : PlayerController.Instance.skaterController.skaterTransform.rotation;
                    if (PlayerController.Instance.IsSwitch && PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude > .15f) old_rot *= Quaternion.Euler(0, -180, 0);

                    last_rotation_offset = Quaternion.identity;

                    magnetized = PlayerController.Instance.currentStateEnum != PlayerController.CurrentState.Bailed;

                    inState = true;

                    cam_rotation = Quaternion.Euler(0, 0, 0);
                    fingers = (from t in fs.getPart("Skater_hand_l").GetComponentsInChildren<Transform>()
                               where !t.name.Contains("hand")
                               select t).Union(from t in fs.getPart("Skater_hand_r").GetComponentsInChildren<Transform>()
                                               where !t.name.Contains("hand")
                                               select t).ToArray();

                    press_count = 0;
                    fs.self.transform.position = old_pos;
                    fs.self.transform.rotation = old_rot;

                    if (PlayerController.Instance.IsSwitch) fs.self.transform.Rotate(0, 180, 0, Space.Self);

                    StopAll();
                    throwdown_state = false;
                    emoting = false;
                    jumping = false;
                    if (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed && !pressed)
                    {
                        actual_state = "stumble";
                        CallBack call = OnStumbleEnd;
                        Play(stumble, call);
                    }
                    else
                    {
                        actual_state = "idle";
                        Play(idle);
                    }

                    last_animation = actual_state;
                    set_bail = false;
                    last_pos = old_pos;
                    instate_count = 0;

                    Vector3 forward = PlayerController.Instance.skaterController.skaterRigidbody.transform.forward;
                    RespawnInfo respawnInfo = new RespawnInfo
                    {
                        position = fs.self.transform.position - new Vector3(0, .7f, 0),
                        IsBoardBackwards = false,
                        rotation = forward != Vector3.zero ? Quaternion.LookRotation(forward) : Quaternion.identity,
                        isSwitch = PlayerController.Instance.IsSwitch
                    };
                    RespawnRoutine(respawnInfo);
                    RespawnRoutineCoroutines();

                    if (MultiplayerManager.Instance.InRoom)
                    {
                        PlayerController.Instance.respawn.recentlyRespawned = false;
                        PlayerController.Instance.ForceBail();
                    }
                }
            }
            else
            {
                press_count = 0;
                if (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed) PlayerController.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Bail);
            }
        }

        void RespawnRoutineCoroutines()
        {
            PlayerController.Instance.boardController.ResetAll();
            PlayerController.Instance.comController.COMRigidbody.MovePosition(PlayerController.Instance.skaterController.skaterRigidbody.position);
            PlayerController.Instance.comController.COMRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
            PlayerController.Instance.ikController._finalIk.enabled = true;
            PlayerController.Instance.InvokeEnableArmPhysics();
            PlayerController.Instance.respawn.behaviourPuppet.SetState(BehaviourPuppet.State.Puppet);
            PlayerController.Instance.respawn.puppetMaster.mode = PuppetMaster.Mode.Active;
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(false);
            PlayerController.Instance.respawn.behaviourPuppet.unpinnedMuscleKnockout = true;
            PlayerController.Instance.respawn.behaviourPuppet.pinWeightThreshold = 0.2f;
            PlayerController.Instance.respawn.recentlyRespawned = false;
            Traverse.Create(PlayerController.Instance.respawn).Field("_canPress").SetValue(true);
            PlayerController.Instance.respawn.respawning = false;
            PlayerController.Instance.boardController.boardRigidbody.isKinematic = false;
            PlayerController.Instance.boardController.boardRigidbody.useGravity = true;
            PlayerController.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);
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
        int raycastCount = 7;
        float groundRaycastDistance = 1f;
        float pelvis_offset = .26f;
        float averageDistance = 0;
        void RaycastFloor()
        {
            Vector3 center_origin = fs.rb.transform.position;
            int mask = LayerUtility.GroundMask;

            Vector3 averageNormal = Vector3.zero, averagePoint = Vector3.zero;
            int hitCount = 0;
            averageDistance = 0;

            for (int i = 0; i < raycastCount; i++)
            {
                float angle = 360f / raycastCount * i;
                Vector3 direction = -fs.rb.transform.up;
                Vector3 raycastOrigin = center_origin + Quaternion.Euler(0, angle, 0) * -fs.rb.transform.right * (fs.collider.radius / 1.5f);
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

                if (averageDistance <= fs.collider.height)
                {
                    if (!respawning)
                    {
                        Quaternion rotation = Quaternion.FromToRotation(fs.self.transform.up, averageNormal);
                        fs.collider.transform.rotation = Quaternion.Slerp(fs.collider.transform.rotation, rotation * fs.collider.transform.rotation, Time.smoothDeltaTime * 4f);

                        if (!grounded && relativeVelocity.y > Main.settings.minVelocityRoll / 10f)
                        {
                            actual_state = "impact";
                            CallBack call = OnImpactEnd;
                            Play(impact_roll, call);
                        }
                        grounded = true;
                    }
                }
                else grounded = false;
            }
            else
            {
                grounded = false;
            }
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

            if (Physics.Raycast(ray_l, out hit_l, .05f, LayerUtility.GroundMask)) left_grounded = true;
            else left_grounded = false;

            if (Physics.Raycast(ray_r, out hit_r, .05f, LayerUtility.GroundMask)) right_grounded = true;
            else right_grounded = false;

            if (!last_l_grounded && left_grounded) PlayRandomOneShotFromArray(sounds, audioSource_left, Main.settings.volume);
            if (!last_r_grounded && right_grounded) PlayRandomOneShotFromArray(sounds, audioSource_right, Main.settings.volume);

            Movement();

            if (emoting)
            {
                float x = cam_rotation.eulerAngles.x + RY;
                cam_rotation = Quaternion.Euler(x, cam_rotation.eulerAngles.y + RX, 0);
            }

            last_l_grounded = left_grounded;
            last_r_grounded = right_grounded;
        }

        RaycastHit hit_stairs;
        Ray stairs;
        RaycastHit up_hit;

        bool climbingStairs = false;
        float climbingTimestamp = 0;
        bool shouldMoveStairs = false;

        void RaycastStairs()
        {
            if (Time.unscaledTime - enterBailTimestamp < .4f) return;

            Vector3 horizontalOrigin = TranslateWithRotation(fs.rb.transform.position, new Vector3(0, -(fs.collider.height / 2f) + .05f, -.1f), fs.rb.transform.rotation);
            stairs = new Ray(horizontalOrigin, fs.rb.transform.forward);

            if (!jumping && actual_state != "impact")
            {
                if (Physics.Raycast(stairs, out hit_stairs, fs.collider.radius * 2f, LayerUtility.GroundMask))
                {
                    //CubeAtPoint(hit_stairs.point, Color.cyan);
                    Vector3 origin = TranslateWithRotation(hit_stairs.point, new Vector3(0, .4f, .2f), fs.rb.transform.rotation);
                    if (Physics.Raycast(origin, Vector3.down, out up_hit, .4f, LayerUtility.GroundMask) && relativeVelocity.z <= 0)
                    {
                        float dotProduct = Vector3.Dot(up_hit.normal, Vector3.up);
                        if (FastApproximately(dotProduct, 1.0f, .025f))
                        {
                            climbingStairs = true;
                            shouldMoveStairs = true;
                            climbingTimestamp = Time.unscaledTime;
                            stairs_up.offset = new Vector3(0, -averageDistance, 0);
                        }
                        else shouldMoveStairs = false;
                    }
                    else shouldMoveStairs = false;
                }
            }

            if (Time.unscaledTime - climbingTimestamp >= .35f)
            {
                climbingStairs = false;
                shouldMoveStairs = false;
            }
        }

        public static bool FastApproximately(float a, float b, float threshold)
        {
            return ((a - b) < 0 ? ((a - b) * -1) : (a - b)) <= threshold;
        }

        void CubeAtPoint(Vector3 pos, Color color)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.localScale = new Vector3(.05f, .05f, .05f);
            sphere.GetComponent<SphereCollider>().enabled = false;
            sphere.transform.position = pos;
            sphere.GetComponent<MeshRenderer>().material.shader = Shader.Find("HDRP/Lit");
            sphere.GetComponent<MeshRenderer>().material.SetColor("_BaseColor", color);
        }

        public Vector3 TranslateWithRotation(Vector3 input, Vector3 translation, Quaternion rotation)
        {
            Vector3 rotatedTranslation = rotation * translation;
            Vector3 output = input + rotatedTranslation;
            return output;
        }

        public void UpdateCamera(bool pos, bool rot)
        {
            if (inState)
            {
                Quaternion rotation = Quaternion.Euler(cam_rotation.eulerAngles.x, fs.rb.rotation.eulerAngles.y + cam_rotation.eulerAngles.y, 0) * Main.settings.camera_rotation_offset;
                Vector3 rotatedTranslation = rotation * Main.settings.camera_offset;
                Vector3 output = fs.self.transform.position + rotatedTranslation;

                if (pos) fallbackCamera.transform.position = Vector3.Lerp(fallbackCamera.transform.position, output, Time.smoothDeltaTime * Main.settings.camera_pos_vel);
                if (rot) fallbackCamera.transform.rotation = Quaternion.Slerp(fallbackCamera.transform.rotation, rotation, Time.smoothDeltaTime * Main.settings.camera_rot_vel);

                main_cam.transform.position = fallbackCamera.transform.position;
                main_cam.transform.rotation = fallbackCamera.transform.rotation;

                fall_cam.m_Lens = main_cam.m_Lens;
                Camera.main.fieldOfView = fall_cam.m_Lens.FieldOfView;
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
                        fs.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        fs.rb.angularVelocity = Vector3.zero;
                        fs.rb.velocity = velocityOnEnter;
                        fs.rb.maxDepenetrationVelocity = .5f;
                        fs.rb.mass = 1.25f;
                    }
                    catch
                    {
                        Log("Error creating RB " + (fs.rb == null));
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

        int respawn_delay = 0;
        public void AddPushForce(float p_value)
        {
            push_frame = 0;
            PushOverTime(p_value);
        }

        int push_frame = 0;
        float last_force = 0;
        void PushOverTime(float p_value)
        {
            PlayerController.Instance.BoardFreezedAfterRespawn = false;

            last_force = p_value;
            p_value /= 2.5f;

            int num = push_frame;
            push_frame = num + 1;

            Vector3 forward = PlayerController.Instance.PlayerForward();
            if (PlayerController.Instance.IsSwitch || respawnSwitch) forward = -PlayerController.Instance.PlayerForward();

            if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude < 0.15f)
            {
                PlayerController.Instance.boardController.boardRigidbody.AddForce(forward * p_value * 1.4f, ForceMode.VelocityChange);
            }
            else
            {
                PlayerController.Instance.boardController.boardRigidbody.AddForce(forward * p_value, ForceMode.VelocityChange);
            }
        }

        bool delay_input = false;
        void Throwdown()
        {
            if (check_velocity && respawn_delay >= 1)
            {
                if (PlayerController.Instance.boardController.GroundCheck())
                {
                    PlayerController.Instance.boardController.ResetAll();
                    PlayerController.Instance.boardController.boardRigidbody.AddForce((respawnSwitch ? -PlayerController.Instance.PlayerForward() : PlayerController.Instance.PlayerForward()) * (15f + (15f * -relativeVelocity.z * Main.settings.throwdown_force)), ForceMode.VelocityChange);
                    //AddPushForce(PlayerController.Instance.GetPushForce() * (.35f + (-relativeVelocity.z * Main.settings.throwdown_force)));
                    check_velocity = false;
                    PlayerController.Instance.comController.COMRigidbody.MovePosition(PlayerController.Instance.skaterController.skaterRigidbody.position);
                    PlayerController.Instance.comController.COMRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
                    delay_input = true;
                    PlayerController.Instance.boardController.ResetBoardTargetPosition();
                    EventManager.Instance.EnterAir(respawnSwitch ? PopType.Switch : PopType.Ollie);
                }
            }

            if (updating && respawn_delay >= 0)
            {
                updating = false;
                check_velocity = true;
            }

            if (delay_input && respawn_delay >= 10)
            {
                PlayerController.Instance.inputController.enabled = true;
                delay_input = false;
            }

            if (updating || check_velocity || delay_input)
            {
                respawn_delay++;
                PlayerController.Instance.skaterController.skaterTargetTransform.position = PlayerController.Instance.boardController.boardTransform.position;
                Traverse.Create(ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1]).Field("didRespawn").SetValue(false);
                //Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(false);
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

            //last_nr = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();

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
            RespawnRoutine(respawnInfo);
            RespawnRoutineCoroutines();
            PlayerController.Instance.respawn.behaviourPuppet.BoostImmunity(1000f);
            PlayerController.Instance.respawn.behaviourPuppet.BoostImpulseMlp(1000f);
        }

        void OnImpactEnd()
        {
            actual_state = "walking";
        }

        void OnStumbleEnd()
        {
            if (actual_state != "impact") actual_state = "walking";
        }

        void UpdateGameplay()
        {
            PlayerController.Instance.skaterController.skaterTransform.position = fs.self.transform.position;
            PlayerController.Instance.skaterController.skaterTransform.rotation = fs.self.transform.rotation;
            PlayerController.Instance.boardController.boardTransform.position = fakeSkate.transform.position;
            PlayerController.Instance.boardController.boardTransform.rotation = fakeSkate.transform.rotation;
            PlayerController.Instance.skaterController.skaterTargetTransform.position = fakeSkate.transform.position;
            PlayerController.Instance.skaterController.skaterTargetTransform.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;
            PinMovementController.SetStartTransform(new Vector3(PlayerController.Instance.skaterController.skaterTransform.position.x, PlayerController.Instance.skaterController.skaterTransform.position.y + fs.collider.height, PlayerController.Instance.skaterController.skaterTransform.position.z), PlayerController.Instance.skaterController.skaterTransform.rotation);
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
                PlayerController.Instance.EnablePuppetMaster(true, false);
                for (int i = 0; i < PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles.Length; i++)
                {
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.isKinematic = false;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.useGravity = true;
                }
            }
            else PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.DisableImmediately();
        }

        void ReplaceBones(bool enabled)
        {
            Transform[] bones = GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>().skaterMainBones;

            if (original_bones == null)
            {
                original_bones = new Transform[32];
                Array.Copy(bones, original_bones, 32);
            }

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

        private AudioClip GetClip(string path)
        {
            WWW audioLoader = new WWW(path);
            while (!audioLoader.isDone) System.Threading.Thread.Sleep(1);
            return audioLoader.GetAudioClip();
        }

        void PlaySoundEmote(AudioClip semote, float vol, string name)
        {
            if (!soundEmoteSource.isPlaying)
            {
                soundEmoteSource.clip = semote;
                soundEmoteSource.volume = vol;
                soundEmoteSource.Play();

                if (MultiplayerManager.Instance.InRoom)
                {
                    PhotonView photonView = PhotonView.Get(MultiplayerManager.Instance.localPlayer);
                    object[] content = new object[] { name };
                    PhotonNetwork.RaiseEvent(64, content, new RaiseEventOptions
                    {
                        Receivers = ReceiverGroup.Others
                    }, SendOptions.SendReliable);
                }
            }
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        void IOnEventCallback.OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == 64 && PhotonNetwork.InRoom && photonEvent.Sender > 0)
            {
                object[] array = (object[])photonEvent.CustomData;
                Player player = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
                NetworkPlayerController playerController = MonoBehaviourPunCallbacksSingleton<MultiplayerManager>.Instance.GetPlayerController(player.ActorNumber);
                if (playerController.GetBody().Find("SoundEmoteSource") == null)
                {
                    GameObject source = new GameObject("SoundEmoteSource");
                    AudioSource s = source.AddComponent<AudioSource>();
                    source.transform.parent = playerController.GetBody();
                    //s.maxDistance = 500;
                    //s.minDistance = 2;
                    s.playOnAwake = false;
                    s.spatialBlend = .1f;

                    /*try
                    {
                        AudioSource sp = playerController.gameObject.GetComponentInChildren<AudioSource>();
                        s.outputAudioMixerGroup = sp.outputAudioMixerGroup;
                    }
                    catch { }*/
                }

                GameObject audio_source_go = playerController.GetBody().Find("SoundEmoteSource").gameObject;
                AudioSource player_audio_source = audio_source_go.GetComponent<AudioSource>();

                float distance = Vector3.Distance(playerController.GetBody().position, PlayerController.Instance.skaterController.skaterTransform.position);

                if (audioCache[(string)array[0]] && distance <= 20f)
                {
                    List<IPlayerNameGraphic> names = playerController.GetComponentsInChildren<IPlayerNameGraphic>(true).ToList<IPlayerNameGraphic>();
                    names.ForEach(delegate (IPlayerNameGraphic t)
                    {
                        t.SetName(playerController.NickName + " ♫");
                    });
                    float distance_volume = map01(distance, 20, 2);
                    distance_volume = distance_volume < 0 ? 0 : distance_volume > 1 ? 1 : distance_volume;
                    player_audio_source.clip = audioCache[(string)array[0]];
                    player_audio_source.volume = Main.settings.emote_volume * distance_volume;
                    player_audio_source.loop = false;
                    player_audio_source.Play();

                    if (!multi_sound_check.ContainsKey(player_audio_source)) multi_sound_check.Add(player_audio_source, playerController);
                }
            }
        }

        IDictionary<AudioSource, NetworkPlayerController> multi_sound_check = new Dictionary<AudioSource, NetworkPlayerController>();

        void CheckAudioSources()
        {
            foreach (var item in multi_sound_check)
            {
                if (!item.Key.isPlaying)
                {
                    NetworkPlayerController playerController = item.Value;
                    List<IPlayerNameGraphic> names = playerController.GetComponentsInChildren<IPlayerNameGraphic>(true).ToList<IPlayerNameGraphic>();
                    names.ForEach(delegate (IPlayerNameGraphic t)
                    {
                        t.SetName(playerController.NickName);
                    });

                    multi_sound_check.Remove(item);
                }
            }
        }

        void PlayRandomOneShotFromArray(AudioClip[] array, AudioSource source, float vol)
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

        void RespawnRoutine(RespawnInfo respawnInfos)
        {
            PinMovementController.startPositionSet = false;
            Time.timeScale = .0001f;
            Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(false);
            PlayerController.Instance.respawn.needRespawn = false;
            PlayerController.Instance.respawn.respawning = true;
            respawning = true;
            PlayerController.Instance.BoardFreezedAfterRespawn = false;
            PlayerController.Instance.DisableArmPhysics();
            PlayerController.Instance.respawn.behaviourPuppet.pinWeightThreshold = 0f;
            PlayerController.Instance.respawn.recentlyRespawned = false;
            //PlayerController.Instance.playerSM.OnRespawnSM();
            PlayerController.Instance.respawn.behaviourPuppet.StopAllCoroutines();
            PlayerController.Instance.respawn.behaviourPuppet.unpinnedMuscleKnockout = false;
            PlayerController.Instance.respawn.behaviourPuppet.SetState(BehaviourPuppet.State.Puppet);
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.DisableImmediately();
            Transform[] componentsInChildren = PlayerController.Instance.ragdollHips.GetComponentsInChildren<Transform>();
            for (int i = 0; i < componentsInChildren.Length; i++)
            {
                componentsInChildren[i].gameObject.layer = LayerUtility.RagdollNoInternalCollision;
            }
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(true);
            PlayerController.Instance.respawn.puppetMaster.mode = PuppetMaster.Mode.Kinematic;
            PlayerController.Instance.ikController._finalIk.enabled = false;
            PlayerController.Instance.respawn.puppetMaster.targetRoot.position = respawnInfos.position + respawnInfos.rotation * PlayerController.Instance.respawn.GetOffsetPositions(respawnInfos.isSwitch)[0];
            PlayerController.Instance.respawn.puppetMaster.targetRoot.rotation = respawnInfos.playerRotation;
            PlayerController.Instance.respawn.puppetMaster.angularLimits = false;
            PlayerController.Instance.respawn.puppetMaster.state = PuppetMaster.State.Alive;
            PlayerController.Instance.respawn.puppetMaster.Teleport(respawnInfos.position + respawnInfos.rotation * PlayerController.Instance.respawn.GetOffsetPositions(respawnInfos.isSwitch)[1] + new Vector3(0f, 1f, 0f), respawnInfos.playerRotation, false);
            for (int j = 0; j < PlayerController.Instance.respawn.getSpawn.Length; j++)
            {
                Vector3 position = respawnInfos.position + respawnInfos.rotation * PlayerController.Instance.respawn.GetOffsetPositions(respawnInfos.isSwitch)[j];
                Quaternion rotation = respawnInfos.rotation * PlayerController.Instance.respawn.GetOffsetRotations(respawnInfos.isSwitch)[j];
                PlayerController.Instance.respawn.getSpawn[j].position = position;
                PlayerController.Instance.respawn.getSpawn[j].rotation = rotation;
            }
            PlayerController.Instance.skaterController.skaterTransform.position = TranslateWithRotation(PlayerController.Instance.boardController.boardTransform.position, new Vector3(0, .73f, 0), PlayerController.Instance.boardController.boardTransform.rotation);
            PlayerController.Instance.ResetIKOffsets();
            PlayerController.Instance.cameraController.ResetAllCamera();
            PlayerController.Instance.cameraController._leanForward = false;
            PlayerController.Instance.cameraController._pivot.rotation = PlayerController.Instance.cameraController._pivotCentered.rotation;
            PlayerController.Instance.skaterController.skaterRigidbody.useGravity = false;
            PlayerController.Instance.skaterController.skaterRigidbody.velocity = Vector3.zero;
            PlayerController.Instance.skaterController.skaterRigidbody.angularVelocity = Vector3.zero;
            PlayerController.Instance.boardController.boardRigidbody.velocity = Vector3.zero;
            PlayerController.Instance.boardController.boardRigidbody.angularVelocity = Vector3.zero;
            PlayerController.Instance.boardController.IsBoardBackwards = respawnInfos.IsBoardBackwards;
            PlayerController.Instance.SetBoardToMaster();
            PlayerController.Instance.SetTurningMode(InputController.TurningMode.Grounded);
            PlayerController.Instance.ResetAllAnimations();
            PlayerController.Instance.boardController.firstVel = 0f;
            PlayerController.Instance.boardController.secondVel = 0f;
            PlayerController.Instance.boardController.thirdVel = 0f;
            PlayerController.Instance.SetLeftIKLerpTarget(0f);
            PlayerController.Instance.SetRightIKLerpTarget(0f);
            PlayerController.Instance.SetMaxSteeze(0f);
            PlayerController.Instance.AnimSetPush(false);
            PlayerController.Instance.AnimSetMongo(false);
            SoundManager.Instance.StopGrindSound(0f);
            PlayerController.Instance.SetIKOnOff(1f);
            PlayerController.Instance.skaterController.skaterRigidbody.constraints = RigidbodyConstraints.None;
            PlayerController.Instance.respawn.bail.bailed = false;
            PlayerController.Instance.playerSM.OnRespawnSM();
            PlayerController.Instance.boardController.ResetBoardTargetPosition();
            if (!respawnSwitch) PlayerController.Instance.skaterController.ResetSwitchAnims();
            PlayerController.Instance.animationController.SetValue("Switch", respawnSwitch);
            PlayerController.Instance.animationController.SetValue("AnimSwitch", respawnSwitch);
            PlayerController.Instance.ikController.ForceUpdateIK();
            Time.timeScale = 1f;
            PlayerController.Instance.cameraController.enabled = true;
            PlayerController.Instance.EnablePuppetMaster(true, false);
            PlayerController.Instance.animationController.ScaleAnimSpeed(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetIKLerpSpeed(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetLeftIKLerpTarget(0f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetRightIKLerpTarget(0f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetRightIKWeight(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetLeftIKWeight(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.IsInGrindState = false;
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.IsInCopingState = false;
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.NeedToSlowLerpCamera = false;
            MonoBehaviourSingleton<PlayerController>.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);
            MonoBehaviourSingleton<PlayerController>.Instance.comController.UpdateCOM(0.89f, 0);
            MonoBehaviourSingleton<PlayerController>.Instance.skaterController.InitializeSkateRotation();
            MonoBehaviourSingleton<PlayerController>.Instance.skaterController.skaterRigidbody.angularVelocity = Vector3.zero;
            MonoBehaviourSingleton<PlayerController>.Instance.boardController.triggerManager.spline = null;
            MonoBehaviourSingleton<PlayerController>.Instance.ResetBoardCenterOfMass();
            MonoBehaviourSingleton<PlayerController>.Instance.ResetBackTruckCenterOfMass();
            MonoBehaviourSingleton<PlayerController>.Instance.ResetFrontTruckCenterOfMass();
            MonoBehaviourSingleton<SoundManager>.Instance.StopPowerslideSound(1, Vector3.ProjectOnPlane(MonoBehaviourSingleton<PlayerController>.Instance.boardController.boardRigidbody.velocity, Vector3.up).magnitude);
            EventManager.Instance.ExitGrind();
            PlayerController.Instance.animationController.CrossFadeAnimation("Riding", .5f);
            PlayerController.Instance.animationController.ForceUpdateAnimators();
        }
    }
}