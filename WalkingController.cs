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
        AnimController walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown_lhrf, throwdown_lhlf, falling, impact_roll, stumble, stairs_up, stairs_up_running;
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
        public AnimController last_animation;
        TrickUIController trickUIController;

        bool init = false, initializing = false;
        string documents;

        void Start()
        {
            documents = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SkaterXL/walking-mod");
            if (!Directory.Exists(documents)) Directory.CreateDirectory(documents);
            documents = Path.Combine(documents, "animations");
            if (!Directory.Exists(documents)) Directory.CreateDirectory(documents);
        }

        async void Init()
        {
            initializing = true;
            init = true;
            try
            {
                playtimeobj = GameObject.Find("PlayTime").GetComponent<PlayTime>();
            }
            catch
            {
                Log("Error getting PlayTime component");
            }

            HighFriction = new PhysicMaterial();
            HighFriction.dynamicFriction = .6f;
            HighFriction.staticFriction = .6f;
            HighFriction.bounciness = 0.1f;

            MediumFriction = new PhysicMaterial();
            MediumFriction.dynamicFriction = .6f;
            MediumFriction.staticFriction = .6f;
            MediumFriction.bounciness = 0.1f;

            StartCoroutine("InitAnimations");
            animations = new AnimController[] { walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown_lhlf, throwdown_lhrf, falling, impact_roll, stumble, stairs_up, stairs_up_running };

            fallbackCamera = PlayerController.Instance.skaterController.transform.parent.parent.Find("Fallback Camera").gameObject;
            fall_cam = fallbackCamera.GetComponent<CinemachineVirtualCamera>();
            main_cam = PlayerController.Instance.cameraController._actualCam.GetComponent<CinemachineVirtualCamera>();

            DisableCameraCollider(false);

            actual_anim = new AnimController();
            //SceneManager.activeSceneChanged += OnSceneLoaded;

            LoadEmotes();
            LoadSoundEmotes();

            EventManager.Instance.onGPEvent += onRunEvent;

            MessageSystem.QueueMessage(MessageDisplayData.Type.Success, "Walking mod loaded", 2f);
            initializing = false;
        }

        public void LoadEmotes()
        {
            string[] temp_emotes = Directory.GetFiles(Path.Combine(Main.modEntry.Path, "animations"), "*.json");
            temp_emotes = temp_emotes.Concat(Directory.GetFiles(documents, "*.json")).ToArray();

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

        public void LoadSoundEmotes()
        {
            string[] sound_emotes = Directory.GetFiles(Path.Combine(Main.modEntry.Path, "sounds"), "*.wav");
            soundEmotes = new List<string>();
            for (int i = 0; i < sound_emotes.Length; i++)
            {
                string[] pieces = sound_emotes[i].Split(Path.DirectorySeparatorChar);
                string name = pieces[pieces.Length - 1].Replace(".wav", String.Empty);
                bool add = true;

                if (name.Contains("footstep_")) add = false;

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

        public void onRunEvent(GPEvent runEvent)
        {
            switch (runEvent)
            {
                case BailEvent bail_event:
                    Log("Received bail event " + respawning);
                    break;
            }
        }

        int walking_crossfade = 1;
        Task InitAnimations()
        {
            walking = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\walking.json"), fs, true, walking_crossfade));
            walking.speed = 1.05f;
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

            throwdown_lhrf = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\run_throwdown_lhrf.json"), fs, false));
            throwdown_lhrf.timeLimit = 0.396f;

            throwdown_lhlf = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\run_throwdown_lhlf.json"), fs, false));
            throwdown_lhlf.timeLimit = 0.396f;

            throwdown_lhrf.speed = throwdown_lhlf.speed = 1.25f;

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
            stairs_up_running = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\stairs_running.json"), fs));
            /*stairs_up.crossfade = 3;
            stairs_up.anchorRoot = true;
            stairs_up.anchorRootFade = true;*/

            LoadUserEmotes();

            return Task.CompletedTask;
        }

        void LoadUserEmotes()
        {
            emote1 = LoadAnim(new AnimController(ResolvePath(Main.settings.emote1), fs, false, 1));
            emote2 = LoadAnim(new AnimController(ResolvePath(Main.settings.emote2), fs, false, 1));
            emote3 = LoadAnim(new AnimController(ResolvePath(Main.settings.emote3), fs, false, 1));
            emote4 = LoadAnim(new AnimController(ResolvePath(Main.settings.emote4), fs, false, 1));
        }

        string ResolvePath(string key)
        {
            string path = Path.Combine(Main.modEntry.Path, "animations\\" + key + ".json");
            if (!File.Exists(path)) path = Path.Combine(documents, key + ".json");
            return path;
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

            LoadSoundEmoteFromCache();
        }

        void LoadSoundEmoteFromCache()
        {
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

            if (inState == true)
            {
                busy = false;
                press_count = 0;
                inStateLogic();
            }
            else
            {
                respawning = false;

                if (GetButtonDown("A") && GetButtonDown("X"))
                {
                    press_count++;
                }
                else
                {
                    press_count = 0;
                }

                bool bailmode = (bail_magnitude < Main.settings.max_magnitude_bail) && (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed) && (dot >= 0f);
                if (press_count >= Main.settings.frame_wait || bailmode)
                {
                    EnterWalkMode(bailmode);
                }
            }
        }

        bool should_run = false, throwed = false;
        void Update()
        {
            if (PlayerController.Instance.inputController.controlsActive && !init) Init();
            if (initializing || !init) return;

            if (!inState) inPlayStateLogic();
            else inStateLogicUpdate();

            if (!inState && (updating || check_velocity || delay_input)) Throwdown();

            if (GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState) && GameStateMachine.Instance.CurrentState.GetType() != typeof(PauseState) && inState)
            {
                if (GameStateMachine.Instance.CurrentState.GetType() != typeof(GearSelectionState))
                {
                    RestoreGameplay(false, false);
                }

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
                Destroy(fakeSkate.GetComponent<BoardController>());
                Destroy(fakeSkate.GetComponent<TriggerManager>());
                Destroy(fakeSkate.GetComponent<Trajectory>());
                Destroy(fakeSkate.GetComponent<GrindDetection>());
                Destroy(fakeSkate.GetComponent<GrindCollisions>());
                Destroy(fakeSkate.GetComponent<BoardCollisionController>());

                BoxCollider[] colliders = fakeSkate.transform.GetComponentsInChildren<BoxCollider>();
                foreach (BoxCollider collider in colliders) collider.enabled = true;

                CapsuleCollider[] ccolliders = fakeSkate.transform.GetComponentsInChildren<CapsuleCollider>();
                foreach (CapsuleCollider collider in ccolliders) collider.enabled = true;

                SphereCollider[] scolliders = fakeSkate.transform.GetComponentsInChildren<SphereCollider>();
                foreach (SphereCollider collider in scolliders) collider.enabled = true;

                if (!magnetized) fakeSkate.GetComponent<Rigidbody>().velocity = lastBoardVelocity;

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
        Vector3 last_real_velocity = Vector3.zero;
        void DisableGameplay()
        {
            last_real_velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
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

        public float restore_timestamp = 0f;
        public void EnableGameplay(bool playObject = true)
        {
            inState = false;

            restore_timestamp = Time.unscaledTime;

            resetJump();
            StopAll();

            DestroyFS();
            PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(true);
            //EventManager.Instance.EndTrickCombo(false, true);
            Log("Enabling gameplay " + playObject);
            if (playObject)
            {
                PlayerController.Instance.EnableGameplay();
                TogglePlayObject(true);
                if (EventManager.Instance.IsInCombo) EventManager.Instance.EndTrickCombo(false, true);
            }

            PlayerController.Instance.animationController.enabled = true;
            PlayerController.Instance.animationController.ToggleAnimators(true);
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
        bool hippieStarted = false, xUp = false, running_state = false;
        float last_rotation_timestamp = 0f;
        void inStateLogic()
        {
            if (!fs.self || !fs.rb || !fakeSkate || GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState) || !inState) return;

            if (!MultiplayerManager.Instance.InRoom) AddReplayFrame();
            else CheckAudioSources();

            if (!hippieJump) RotationOffset();
            else
            {
                last_rotation_offset = Quaternion.Euler(0, 90f, 0);
            }

            try { actual_anim.Update(); } catch { Log("Error updating animation " + inState); }

            Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(true);

            if (hippieJump && !hippieStarted)
            {
                actual_state = "idle";
                int multiplier = 1;
                if (PlayerController.Instance.IsSwitch) multiplier = -1;

                if (doubleHippieJump)
                {
                    backwards = false;
                    front_flip.timeLimitStart = front_flip.animation.times[16];
                    front_flip.rotation_offset = Quaternion.Euler(0, (SettingsManager.Instance.stance == Stance.Goofy ? -90 : 90) * multiplier, 0);
                    front_flip.speed = 1.5f;
                    doubleJump();
                }
                else
                {
                    jump.rotation_offset = Quaternion.Euler(0, (SettingsManager.Instance.stance == Stance.Goofy ? -90 : 90) * multiplier, 0);
                    jump.speed = 1.5f;
                    jump.timeLimitStart = jump.animation.times[22];
                    normalJump();
                }

                hippieStarted = true;
            }

            UpdateSticks();
            RaycastFloor();
            RaycastFeet();
            Movement();
            RaycastStairs();
            RaycastInfinity();
            emoteInput();

            float x = cam_rotation.eulerAngles.x + RY;
            if (emoting)
            {
                cam_rotation = Quaternion.Euler(x, cam_rotation.eulerAngles.y + RX, 0);
            }
            else
            {
                x = cam_rotation.eulerAngles.x + (RY / 4f);

                if (RY != 0) last_rotation_timestamp = Time.fixedUnscaledTime;

                if (Time.fixedUnscaledTime - last_rotation_timestamp >= 1f)
                {
                    cam_rotation = Quaternion.Lerp(cam_rotation, Quaternion.identity, Time.fixedDeltaTime);
                }
                else
                {
                    if (!jumping) cam_rotation = Quaternion.Euler(x, Mathf.Lerp(cam_rotation.eulerAngles.y, 0, Time.fixedDeltaTime), 0);
                }
            }

            if (MultiplayerManager.Instance.InRoom && !respawning && !PlayerController.Instance.respawn.respawning) UpdateRagdoll();

            PlayerController.Instance.boardController.boardRigidbody.isKinematic = magnetized;

            UpdateGameplay();

            if (!jumping && actual_state != "impact" && !climbingStairs && actual_state != "stumble" && grounded && !hippieJump)
            {
                if (Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) >= running_speed)
                {
                    running_state = true;
                    actual_state = "running";
                }
                else
                {
                    running_state = false;

                    if (Mathf.Lerp(last_velocity.magnitude, fs.rb.velocity.magnitude, .5f) <= limit_idle) actual_state = "idle";
                    else actual_state = "walking";
                }
            }

            if (climbingStairs)
            {
                if (actual_anim.name != stairs_up.name && !running_state) Play(stairs_up);
                if (actual_anim.name != stairs_up_running.name && running_state) Play(stairs_up_running);
                if (shouldMoveStairs) fs.rb.MovePosition(Vector3.Lerp(fs.rb.position, new Vector3(up_hit.point.x, up_hit.point.y + (fs.collider.height / 2f), up_hit.point.z), Time.fixedDeltaTime * 8f));
            }

            if (relativeVelocity.y > 0.04f && !grounded && !jumping && actual_state != "impact" && !climbingStairs && actual_state != "stumble") actual_state = "falling";

            relativeVelocity = fs.rb.transform.InverseTransformDirection(last_pos - fs.self.transform.position);
            last_pos = fs.self.transform.position;

            Board();

            if (throwdown_state)
            {
                float step = .03f * (actual_anim.frame <= 4 ? actual_anim.frame : 4);
                actual_anim.offset = Vector3.Lerp(actual_anim.offset, new Vector3(0, -.80f + step, 0), Time.fixedDeltaTime * 24f);
            }

            if (!emoting) ThrowdownInput();

            if (PlayerController.Instance.inputController.player.GetButtonDown(Main.settings.pin_button))
            {
                RestoreGameplay(true, false);
                //Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(false);
            }

            if (xUp)
            {
                if (PlayerController.Instance.inputController.player.GetButtonShortPressDown(Main.settings.magnetize_button)) magnetized = !magnetized;
                if (PlayerController.Instance.inputController.player.GetButtonDoublePressDown(Main.settings.magnetize_button))
                {
                    Main.settings.left_arm = !Main.settings.left_arm;
                    if (Main.settings.left_arm) Main.settings.mallgrab = !Main.settings.mallgrab;
                }
            }

            if (PlayerController.Instance.inputController.player.GetButtonUp(Main.settings.magnetize_button) || (Time.fixedUnscaledTime - enterBailTimestamp >= 2f && !PlayerController.Instance.inputController.player.GetButton(Main.settings.magnetize_button))) xUp = true;

            if (!GetButtonDown("LB") && !GetButtonDown("RB") && !Main.ui.emote_config && !Main.ui.sound_emote_config)
            {
                if (PlayerController.Instance.inputController.player.GetButtonDown(68) || PlayerController.Instance.inputController.player.GetButton(68)) SetRespawn();
                if (PlayerController.Instance.inputController.player.GetButtonDown(67) || PlayerController.Instance.inputController.player.GetButton(67)) DoRespawn();
            }

            if (!jumping && !hippieJump)
            {
            }
            else JumpingOffset();

            instate_count++;

            last_velocity = fs.rb.velocity;
        }

        public void inStateLogicUpdate()
        {
            if (!jumping && !hippieJump)
            {
                if (!emoting && actual_state != "impact" && !climbingStairs && actual_state != "stumble")
                {
                    HandleAnimations();
                    JumpInput();
                }
            }
        }

        public void RestoreGameplay(bool originalRespawn = false, bool playObj = true, bool originalPoint = false)
        {
            try
            {
                PlayerController.Instance.inputController.enabled = true;
                PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(true);
                PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(true);

                UpdateGameplay();
                if (fs.rb != null && !originalRespawn)
                {
                    RespawnInfo respawnInfo = new RespawnInfo
                    {
                        position = fs.self.transform.position - new Vector3(0, .7f, 0),
                        IsBoardBackwards = false,
                        rotation = fs.rb.transform.forward != Vector3.zero ? Quaternion.LookRotation(fs.rb.transform.forward) : Quaternion.identity,
                        isSwitch = false
                    };

                    if (originalPoint) respawnInfo = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();

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
                Log(e.StackTrace);
            }
        }

        void UpdateRagdoll()
        {
            //PlayerController.Instance.respawn.recentlyRespawned = false;

            PlayerController.Instance.respawn.behaviourPuppet.StopAllCoroutines();
            PlayerController.Instance.respawn.behaviourPuppet.unpinnedMuscleKnockout = false;
            PlayerController.Instance.respawn.behaviourPuppet.SetState(BehaviourPuppet.State.Puppet);

            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.internalCollisions = false;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.position = fs.self.transform.position;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.rotation = fs.self.transform.rotation;

            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscleWeight = 0;
            PlayerController.Instance.respawn.puppetMaster.pinWeight = 0f;

            PlayerController.Instance.animationController.ToggleAnimators(false);

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

        public bool respawning = false;
        void DoRespawn(bool force = false)
        {
            cam_rotation = Quaternion.identity;
            if (Time.unscaledTime - enterBailTimestamp >= Main.settings.bailLimit || force)
            {
                respawning = true;
                last_nr = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();
                fs.self.transform.position = new Vector3(last_nr.position.x, last_nr.position.y + (fs.collider.height / 1.5f), last_nr.position.z);
                fs.self.transform.rotation = last_nr.rotation;
                fs.rb.velocity = Vector3.zero;
                fs.rb.angularVelocity = Vector3.zero;
                PlayerController.Instance.respawn.puppetMaster.Teleport(fs.self.transform.position + fs.self.transform.rotation * PlayerController.Instance.respawn.GetOffsetPositions(false)[1] + (Vector3)Traverse.Create(PlayerController.Instance.respawn).Field("_playerOffset").GetValue(), fs.self.transform.rotation, false);
            }
            else
            {
                respawning = true;
                last_nr = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();
                fs.self.transform.position = new Vector3(last_nr.position.x, last_nr.position.y + (fs.collider.height / 1.5f), last_nr.position.z);
                fs.self.transform.rotation = last_nr.rotation;
                fs.rb.velocity = Vector3.zero;
                fs.rb.angularVelocity = Vector3.zero;
                PlayerController.Instance.respawn.puppetMaster.Teleport(fs.self.transform.position + fs.self.transform.rotation * PlayerController.Instance.respawn.GetOffsetPositions(false)[1] + (Vector3)Traverse.Create(PlayerController.Instance.respawn).Field("_playerOffset").GetValue(), fs.self.transform.rotation, false);

                RestoreGameplay(true, true);
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
                    actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.LookRotation(backwards ? rotationHorizontal : -rotationHorizontal), Time.fixedDeltaTime * rotation_speed);
                }
                else
                {
                    if (Sideway())
                    {
                        Quaternion lr = Quaternion.LookRotation(rotationHorizontal);
                        actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.Euler(lr.eulerAngles.x, lr.eulerAngles.y + (rotationHorizontal.x > 0 ? -90 : 90), lr.eulerAngles.z), Time.fixedDeltaTime * rotation_speed);
                    }
                    else
                    {
                        if (jumping || actual_state == "impact")
                        {
                            if (actual_state != "idle")
                            {
                                Quaternion lr = Quaternion.LookRotation(backwards ? rotationHorizontal : -rotationHorizontal);
                                actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.Euler(0, lr.eulerAngles.y, 0), Time.fixedDeltaTime * rotation_speed);
                            }
                        }
                        else
                        {
                            if (actual_anim.name != falling.name) actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.identity, Time.fixedDeltaTime * rotation_speed);
                        }
                    }
                }
            }
            else actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, Quaternion.identity, Time.fixedDeltaTime * rotation_speed);
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
                if (left_grounded || right_grounded || climbingStairs || jumping)
                {
                    move = transform.right * LX + transform.forward * LY;
                    fs.rb.AddForce(-fs.rb.velocity * decay);
                    if (fs.rb.velocity.magnitude <= max_speed && !grinding)
                    {
                        fs.rb.AddRelativeForce(move * (speed * (jumping ? .5f : actual_state == "running" ? 1.25f : 1f)));
                    }
                }

                if (!emoting) fs.rb.MoveRotation(Quaternion.Euler(fs.rb.rotation.eulerAngles.x, fs.rb.rotation.eulerAngles.y + (actual_state == "idle" ? RX : RX / 1.5F), fs.rb.rotation.eulerAngles.z));

                //if (!emoting && (LX != 0 || LY != 0)) cam_rotation = Quaternion.Euler(0, 0, 0);
            }
        }

        public int last_dpad = 0;
        void emoteInput()
        {
            if (!Main.ui.emote_config && !Main.ui.sound_emote_config)
            {
                bool lb = GetButtonDown("LB"), rb = GetButtonDown("RB");
                if (lb || rb)
                {
                    for (int i = 67; i <= 70; i++)
                    {
                        if (GetButtonDown(i))
                        {
                            if (GetButtonDown("A"))
                            {
                                if (lb)
                                {
                                    setSelectedEmote(getEmote(i).name);
                                    Main.ui.emote_config = true;
                                }

                                if (rb)
                                {
                                    setSelectedSoundEmote(getSoundEmoteString(i));
                                    Main.ui.sound_emote_config = true;
                                }

                                UISounds.Instance.PlayOneShotSelectMajor();
                            }

                            if (lb) PlayEmote(getEmote(i));
                            if (rb) PlaySoundEmote(getSoundEmote(i), Main.settings.emote_volume, getSoundEmoteString(i));

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

        public void changeSoundEmote(string key)
        {
            if (last_dpad == 70) Main.settings.semote1 = key;
            if (last_dpad == 68) Main.settings.semote2 = key;
            if (last_dpad == 69) Main.settings.semote3 = key;
            if (last_dpad == 67) Main.settings.semote4 = key;

            Main.settings.Save(Main.modEntry);

            LoadSoundEmoteFromCache();
        }

        public void changeEmote(string key)
        {
            AnimController new_emote;
            if (!cache.ContainsKey(key))
            {
                new_emote = new AnimController(ResolvePath(key), fs, false, 1);
                cache.Add(key, new_emote);
            }
            else { new_emote = cache[key]; }

            if (last_dpad == 70)
            {
                emote1 = new_emote;
                Main.settings.emote1 = key;
            }
            if (last_dpad == 68)
            {
                emote2 = new_emote;
                Main.settings.emote2 = key;
            }
            if (last_dpad == 69)
            {
                emote3 = new_emote;
                Main.settings.emote3 = key;
            }
            if (last_dpad == 67)
            {
                emote4 = new_emote;
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

                    actual_anim.speed = 1f + (float)(Math.Sin(Time.fixedUnscaledTime) / 10f);
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
                        actual_anim.speed = 1f + (float)(Math.Sin(Time.fixedUnscaledTime) / 10f);
                    }
                }
            }
        }

        void JumpInput()
        {
            if (SinglePress(Main.settings.jump_button)) normalJump();
            else if (PlayerController.Instance.inputController.player.GetButtonDoublePressHold(Main.settings.jump_button) || PlayerController.Instance.inputController.player.GetButtonDoublePressDown(Main.settings.jump_button)) doubleJump();
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

        bool hippieForceAdded = false, force_added = false;
        void JumpingOffset()
        {
            if (actual_anim.name == front_flip.name && front_flip.frame == 16)
            {
                if (!hippieJump)
                {
                    fs.rb.AddForce(0, Main.settings.flip_jump_force, 0, ForceMode.Impulse);
                }
                else
                {
                    if (!hippieForceAdded)
                    {
                        fs.rb.AddForce(0, Main.settings.hippie_jump_force * 1.5f, 0, ForceMode.Impulse);
                        hippieForceAdded = true;
                    }
                }
            }

            if (actual_anim.name == back_flip.name && back_flip.frame == 18) fs.rb.AddForce(0, Main.settings.flip_jump_force, 0, ForceMode.Impulse);

            if (actual_anim.name == running_jump.name || actual_anim.name == running_jump.name)
            {
                if (running_jump.frame == 1) fs.rb.AddForce(0, Main.settings.running_jump_force, 0, ForceMode.Impulse);
            }

            if (actual_anim.name == jump.name || actual_anim.name == jump.name)
            {
                if (!hippieJump)
                {
                    if (jump.frame == 22) fs.rb.AddForce(0, Main.settings.idle_jump_force, 0, ForceMode.Impulse);
                }
                else
                {
                    if (!hippieForceAdded)
                    {
                        fs.rb.AddForce(0, Main.settings.hippie_jump_force, 0, ForceMode.Impulse);
                        hippieForceAdded = true;
                    }
                }
            }
        }

        GameObject deck_target;
        bool magnetized = true;
        Rigidbody skate_rb;
        bool set_bail = false;
        int throwdown_detach = 6;

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
                    SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);
                    bool throwdown_anim = throwdown_state && actual_anim.frame >= 0;
                    try
                    {
                        if (!skate_rb.isKinematic) skate_rb.isKinematic = true;
                        if (skate_rb.useGravity) skate_rb.useGravity = false;

                        string side = respawnSwitch ? SettingsManager.Instance.stance == Stance.Goofy ? "l" : "r" : SettingsManager.Instance.stance == Stance.Goofy ? "r" : "l";
                        Transform target = throwdown_anim ? actual_anim.frame >= throwdown_detach ? fs.getPart("Skater_Toe2_" + side) : fs.getPart("Skater_hand_l") : (Main.settings.left_arm ? fs.getPart("Skater_ForeArm_l") : fs.getPart("Skater_ForeArm_r"));
                        deck_target.transform.position = target.position;
                        deck_target.transform.rotation = target.rotation;

                        if (!throwdown_anim)
                        {
                            if (Main.settings.mallgrab)
                            {
                                deck_target.transform.Rotate(0f, 0, -90f, Space.Self);
                                deck_target.transform.Rotate(Main.settings.left_arm ? -90f : 90f, 0, 0f, Space.Self);
                                deck_target.transform.Translate(0, .15f, 0, Space.Self);
                                deck_target.transform.Translate(0, 0f, Main.settings.left_arm ? -.56f : .56f, Space.Self);
                                deck_target.transform.Rotate(Main.settings.left_arm ? 7.5f : -7.5f, 0, 0f, Space.Self);
                                deck_target.transform.Rotate(0, Main.settings.left_arm ? 10f : -10f, 0, Space.Self);
                                deck_target.transform.Rotate(Main.settings.left_arm ? 10f : -10f, 0, 0, Space.Self);
                            }
                            else
                            {
                                deck_target.transform.Rotate(Main.settings.left_arm ? -90f : 90f, 0, 0, Space.Self);
                                deck_target.transform.Rotate(Main.settings.left_arm ? -20f : 20f, -10f, -5, Space.Self);
                                deck_target.transform.Translate(-.225f, .035f, Main.settings.left_arm ? .1f : -.1f, Space.Self);
                            }
                        }
                        else
                        {
                            if (actual_anim.frame < throwdown_detach)
                            {
                                if (side == "r")
                                {
                                    deck_target.transform.Rotate(0, -180f, 0, Space.Self);
                                    deck_target.transform.Rotate(0, 0, -180f, Space.Self);
                                }

                                deck_target.transform.Rotate(0f, 0, -90f, Space.Self);
                                if (side == "r") deck_target.transform.Translate(0, -.1f, .375f, Space.Self);
                                else deck_target.transform.Translate(0, -.1f, -.375f, Space.Self);
                            }
                            else
                            {
                                deck_target.transform.Translate(0, -.05f, 0, Space.Self);
                                deck_target.transform.Rotate(0f, -180f, 0f, Space.Self);
                                deck_target.transform.Translate(0, 0, 0.05f, Space.Self);

                                if (side == "r")
                                {
                                    deck_target.transform.Rotate(-10f, -45f, 0f, Space.Self);
                                }
                                else
                                {
                                    deck_target.transform.Rotate(5f, 23f, 0f, Space.Self);
                                }
                            }
                        }

                        float multiplier = 1f;
                        if (throwdown_anim)
                        {
                            if (actual_anim.frame >= 16) multiplier = .15f;
                            else
                            {
                                if (actual_anim.frame >= throwdown_detach) multiplier = .325f;
                                else multiplier = .8f;
                            }
                        }
                        fakeSkate.transform.rotation = Quaternion.Slerp(fakeSkate.transform.rotation, deck_target.transform.rotation, Time.fixedDeltaTime * 72f * multiplier);
                        fakeSkate.transform.position = Vector3.Lerp(fakeSkate.transform.position, deck_target.transform.position, Time.fixedDeltaTime * 72f * multiplier);

                        fakeSkate.GetComponent<Rigidbody>().MovePosition(fakeSkate.transform.position);
                        fakeSkate.GetComponent<Rigidbody>().MoveRotation(fakeSkate.transform.rotation);
                    }
                    catch
                    {
                        Log((fs.getPart("Skater_hand_l") == null) + " " + (fs.getPart("Skater_ForeArm_r") == null));
                    }
                }
                else
                {
                    SetBoardPhysicsMaterial(PlayerController.FrictionType.Brake);
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

        PhysicMaterial HighFriction, MediumFriction;
        public void SetBoardPhysicsMaterial(PlayerController.FrictionType _frictionType)
        {
            foreach (Collider collider in fakeSkate.GetComponentsInChildren<Collider>())
            {
                switch (_frictionType)
                {
                    case PlayerController.FrictionType.Default:
                        collider.material = PlayerController.Instance.boardPhysicsMaterial;
                        break;
                    case PlayerController.FrictionType.Brake:
                        collider.material = hippieJump ? HighFriction : MediumFriction;
                        break;
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
                if (!throwdown_state)
                {
                    bool run = false;

                    if (PlayerController.Instance.inputController.player.GetButtonDown("RT"))
                    {
                        run = true;
                        respawnSwitch = false;
                    }

                    if (PlayerController.Instance.inputController.player.GetButtonDown("LT"))
                    {
                        run = true;
                        respawnSwitch = true;
                    }

                    if (run)
                    {
                        CallBack call = OnThrowdownEnd;
                        Play(respawnSwitch ? (SettingsManager.Instance.stance == Stance.Goofy ? throwdown_lhlf : throwdown_lhrf) : (SettingsManager.Instance.stance == Stance.Goofy ? throwdown_lhrf : throwdown_lhlf), call);
                        throwdown_state = true;
                        actual_state = "throwdown";
                        magnetized = true;
                        throwed = true;
                        actual_anim.speed = 1.25f + (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude / 100f);
                    }
                }
            }
        }

        float map01(float value, float min, float max)
        {
            return (value - min) * 1f / (max - min);
        }

        string last_state = "";
        void LogState()
        {
            if (PlayerController.Instance.currentStateEnum.ToString() != last_state)
            {
                last_state = PlayerController.Instance.currentStateEnum.ToString();
            }
        }

        public Transform[] fingers;
        float bail_magnitude = 0;
        bool projected = false;
        Vector3 velocityOnEnter = Vector3.zero;
        bool lt_onspawn = false, rt_onspawn = false;
        float dot = 0;
        public float enterBailTimestamp = 0f;
        bool hippieJump = false, doubleHippieJump = false;

        void inPlayStateLogic()
        {
            PlayStateInput();

            if (Time.unscaledTime - restore_timestamp >= 3f) respawning = false;

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
        }

        void PlayStateInput()
        {
            if (Main.settings.hippie_jump && (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Pop || PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Release || PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.InAir || PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Grinding) && !hippieJump)
            {
                if (SinglePress(Main.settings.jump_button))
                {
                    resetJump();
                    respawning = false;
                    hippieJump = true;
                    doubleHippieJump = false;
                    EnterWalkMode(false, false);
                }
                else if (PlayerController.Instance.inputController.player.GetButtonDoublePressHold(Main.settings.jump_button) || PlayerController.Instance.inputController.player.GetButtonDoublePressDown(Main.settings.jump_button))
                {
                    resetJump();
                    respawning = false;
                    hippieJump = true;
                    doubleHippieJump = true;
                    EnterWalkMode(false, false);
                }
            }

            if (!PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[0].rigidbody.useGravity)
            {
                PlayerController.Instance.EnablePuppetMaster(true, false);
                for (int i = 0; i < PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles.Length; i++)
                {
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.isKinematic = false;
                    PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[i].rigidbody.useGravity = true;
                }
            }
        }

        public bool enterFromBail = false;
        bool busy = false;
        Vector3 lastBoardVelocity;
        void EnterWalkMode(bool bailmode, bool _magnetized = true)
        {
            if (respawning || busy) return;
            if (MultiplayerManager.Instance.InRoom && bailmode) return;

            if (EventManager.Instance.IsInCombo) EventManager.Instance.EndTrickCombo(false, true);

            lastBoardVelocity = PlayerController.Instance.boardController.boardRigidbody.velocity * 4f;
            actual_anim = new AnimController();
            busy = true;

            if (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed || lastBoardVelocity.magnitude / 4f >= Main.settings.step_off_limit) _magnetized = false;
            magnetized = _magnetized;

            DisableGameplay();

            DestroyFS();
            createFS();

            actual_state = "idle";
            Play(idle);

            enterFromBail = bailmode;

            xUp = false;
            enterBailTimestamp = Time.fixedUnscaledTime;
            rt_onspawn = PlayerController.Instance.inputController.player.GetButtonDown("RT");
            lt_onspawn = PlayerController.Instance.inputController.player.GetButtonDown("LT");

            velocityOnEnter = PlayerController.Instance.skaterController.skaterRigidbody.velocity;
            
            Vector3 raycastOrigin = PlayerController.Instance.skaterController.skaterTransform.position + new Vector3(0, 2f, 0);
            Vector3 old_pos = PlayerController.Instance.skaterController.skaterTransform.position;
            if (!bailmode && !hippieJump)
            {
                old_pos = PlayerController.Instance.boardController.boardTransform.position + new Vector3(0, (fs.collider.height / 2f) + .15f, 0);
            }
            else
            {
                fakeSkate.GetComponent<Rigidbody>().useGravity = true;
                fakeSkate.GetComponent<Rigidbody>().isKinematic = false;
                fakeSkate.GetComponent<Rigidbody>().velocity = last_real_velocity;
                fakeSkate.GetComponent<Rigidbody>().angularVelocity = PlayerController.Instance.boardController.boardRigidbody.angularVelocity;

                fakeSkate.GetComponent<Rigidbody>().AddForce(last_real_velocity * 2f, ForceMode.VelocityChange);
            }

            last_velocity = velocityOnEnter;

            int multiplier = SettingsManager.Instance.stance == Stance.Goofy ? -1 : 1;

            PlayerController.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);

            inState = true;

            cam_rotation = Quaternion.Euler(0, 0, 0);
            fingers = (from t in fs.getPart("Skater_hand_l").GetComponentsInChildren<Transform>()
                       where !t.name.Contains("hand")
                       select t).Union(from t in fs.getPart("Skater_hand_r").GetComponentsInChildren<Transform>()
                                       where !t.name.Contains("hand")
                                       select t).ToArray();

            press_count = 0;
            fs.self.transform.position = old_pos;
            Quaternion spawnRotation;
            Vector3 velocityHorizontal = new Vector3(PlayerController.Instance.skaterController.skaterRigidbody.velocity.x, 0, PlayerController.Instance.skaterController.skaterRigidbody.velocity.z);
            if (PlayerController.Instance.skaterController.skaterRigidbody.velocity.magnitude > .05f && velocityHorizontal.magnitude > .05f)
            {
                spawnRotation = Quaternion.LookRotation(velocityHorizontal) * (PlayerController.Instance.IsSwitch ? Quaternion.Euler(0, 180, 0) : Quaternion.identity);
            }
            else
            {
                spawnRotation = PlayerController.Instance.skaterController.skaterTransform.rotation;
            }

            fs.self.transform.rotation = spawnRotation;
            if (hippieJump) fs.self.transform.rotation = PlayerController.Instance.skaterController.transform.rotation;
            Physics.SyncTransforms();

            if (PlayerController.Instance.IsSwitch) fs.self.transform.Rotate(0, 180, 0, Space.Self);

            StopAll();
            throwdown_state = false;
            emoting = false;
            jumping = false;

            set_bail = false;
            last_pos = old_pos;
            instate_count = 0;

            if (MultiplayerManager.Instance.InRoom)
            {
                if (!PlayerController.Instance.respawn.bail.bailed) PlayerController.Instance.ForceBailSMOnly();
                PlayerController.Instance.CancelRespawnInvoke();
                ResetSkater();
            }

            inStateLogic();

            if (!hippieJump)
            {
                if (!magnetized) fakeSkate.GetComponent<Rigidbody>().velocity = lastBoardVelocity;
                if (lastBoardVelocity.magnitude / 4f >= Main.settings.step_off_limit)
                {
                    fakeSkate.GetComponent<Rigidbody>().AddExplosionForce(lastBoardVelocity.magnitude * 40f, fs.self.transform.position - new Vector3(-.1f, -.4f, -.1f), 0, 2f);
                }

                if (bailmode)
                {
                    actual_state = "stumble";
                    CallBack call = OnStumbleEnd;
                    stumble.speed = 1.25f + (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude / 100f);
                    Play(stumble, call);
                }
                last_animation = new AnimController(actual_anim);
            }
        }

        public void RespawnRoutineCoroutines()
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
            PlayerController.Instance.respawn.needRespawn = false;
            PlayerController.Instance.respawn.respawning = false;
            PlayerController.Instance.boardController.boardRigidbody.isKinematic = false;
            PlayerController.Instance.boardController.boardRigidbody.useGravity = true;
            PlayerController.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);

            Traverse.Create(PlayerController.Instance.ikController).Field("_ikLeftPosLerp").SetValue(1f);
            Traverse.Create(PlayerController.Instance.ikController).Field("_ikRightPosLerp").SetValue(1f);

            if (!respawnSwitch) PlayerController.Instance.skaterController.ResetSwitchAnims();
            else
            {
                Traverse.Create(PlayerController.Instance.skaterController).Field("_animSwitch").SetValue(1f);
                Traverse.Create(PlayerController.Instance.skaterController).Field("_actualSwitch").SetValue(1f);
            }
            PlayerController.Instance.ikController.ForceUpdateIK();
            PlayerController.Instance.animationController.ScaleAnimSpeed(1f);
            PlayerController.Instance.CrossFadeAnimation("Riding", .5f);
            PlayerController.Instance.animationController.ForceUpdateAnimators();
            Traverse.Create(PlayerController.Instance.respawn).Field("_canPress").SetValue(true);

            //EnterRiding();
        }

        void PlayEmote(AnimController target)
        {
            CallBack call = OnEmoteEnd;
            Play(target, call);
            emoting = true;
            actual_state = "emoting";
        }

        public Vector3 last_offset = Vector3.zero;
        RaycastHit hit_body;
        bool grounded = true;
        int raycastCount = 7;
        float groundRaycastDistance = 1f;
        float pelvis_offset = .26f;
        float averageDistance = 0, last_average = 0;
        Vector3 averageNormal = Vector3.zero;

        void RaycastFloor()
        {
            Vector3 center_origin = TranslateWithRotation(fs.rb.transform.position, new Vector3(0, -fs.collider.height / 4f, 0), fs.collider.transform.rotation);
            int mask = ~(1 << LayerUtility.Character);

            groundRaycastDistance = (fs.collider.height / 1.5f);

            Vector3 averagePoint = Vector3.zero;
            int hitCount = 0;
            averageDistance = last_average;

            int notfiltered = 0;
            int skate_cast = 0;
            for (int i = 0; i < raycastCount; i++)
            {
                float angle = 360f / raycastCount * i;
                Vector3 direction = -fs.collider.transform.up;
                Vector3 raycastOrigin = center_origin + Quaternion.Euler(0, angle, 0) * -fs.rb.transform.right * (fs.collider.radius / 2f);
                Ray groundRay = new Ray(raycastOrigin, direction);
                RaycastHit groundHit;

                if (Physics.Raycast(groundRay, out groundHit, groundRaycastDistance, mask))
                {
                    notfiltered++;
                    if (groundHit.collider.gameObject.layer == LayerUtility.Skateboard && !magnetized)
                    {
                        if (fakeSkate.transform.rotation.eulerAngles.z >= 195f || fakeSkate.transform.rotation.eulerAngles.z <= 75f)
                        {
                            if (skate_cast >= Math.Floor(raycastCount / 2f))
                            {
                                if (((Time.unscaledTime - enterBailTimestamp >= 1f) || (hippieJump && Time.unscaledTime - enterBailTimestamp >= .3f)) && !throwdown_state && (grounded || hippieJump) && !emoting)
                                {
                                    throwdown_state = true;
                                    RestoreGameplay(false, true);
                                    PlayerController.Instance.inputController.enabled = false;
                                    updating = true;
                                    delay_input = true;
                                    return;
                                }
                            }
                            else skate_cast++;
                        }
                    }

                    if ((jumping && groundHit.collider.gameObject.layer != LayerUtility.Skateboard) || !jumping)
                    {
                        averageNormal += groundHit.normal;
                        averagePoint += groundHit.point;
                        averageDistance += groundHit.distance;
                        hitCount++;
                    }
                }
            }

            if (hitCount > 0)
            {
                averageNormal /= hitCount;
                averagePoint /= hitCount;
                averageDistance /= hitCount;

                last_average = averageDistance;

                if (averageDistance <= groundRaycastDistance)
                {
                    if (!respawning)
                    {
                        Quaternion rotation = Quaternion.FromToRotation(fs.self.transform.up, averageNormal);
                        fs.collider.transform.rotation = Quaternion.Slerp(fs.collider.transform.rotation, rotation * fs.collider.transform.rotation, Time.fixedDeltaTime * 4f);

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

                float side_angle = Vector3.Angle(fs.self.transform.right, averageNormal);
                float forward_angle = Vector3.Angle(fs.self.transform.forward, averageNormal);

                if (!Mathf.Approximately(side_angle, 90f) || !Mathf.Approximately(forward_angle, 90f))
                {
                    fs.self.transform.rotation = Quaternion.Lerp(fs.self.transform.rotation, Quaternion.Euler(averageNormal.x, fs.self.transform.rotation.eulerAngles.y, averageNormal.z), Time.fixedDeltaTime * 6f);
                }

                if (actual_anim.offsetPelvis && !actual_anim.anchorRoot)
                {
                    actual_anim.offset = new Vector3(0, -1.1f, 0);
                }
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

            ray_l = new Ray(left_origin.position, Vector3.down);
            ray_r = new Ray(right_origin.position, Vector3.down);

            if (Physics.Raycast(ray_l, out hit_l, .05f, LayerUtility.GroundMask)) left_grounded = true;
            else left_grounded = false;

            if (Physics.Raycast(ray_r, out hit_r, .05f, LayerUtility.GroundMask)) right_grounded = true;
            else right_grounded = false;

            if (!last_l_grounded && left_grounded) PlayRandomOneShotFromArray(sounds, audioSource_left, Main.settings.volume);
            if (!last_r_grounded && right_grounded) PlayRandomOneShotFromArray(sounds, audioSource_right, Main.settings.volume);

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

            Vector3 horizontalOrigin = TranslateWithRotation(fs.rb.transform.position, new Vector3(0, -(fs.collider.height / 2f) + .035f, -.1f), fs.rb.transform.rotation);
            stairs = new Ray(horizontalOrigin, fs.rb.transform.forward);

            if (!jumping && actual_state != "impact" && !throwdown_state)
            {
                float angle = Vector3.Angle(fs.self.transform.forward, averageNormal);
                if (angle >= 86f && angle <= 94f)
                {
                    if (Physics.Raycast(stairs, out hit_stairs, fs.collider.radius * 2f, LayerUtility.GroundMask))
                    {
                        //CubeAtPoint(hit_stairs.point, Color.cyan);
                        Vector3 origin = TranslateWithRotation(hit_stairs.point, new Vector3(0, .5f, .2f), fs.rb.transform.rotation);

                        if (Physics.Raycast(origin, Vector3.down, out up_hit, .5f, LayerUtility.GroundMask) && relativeVelocity.z <= 0)
                        {
                            //CubeAtPoint(up_hit.point, Color.magenta);
                            float dotProduct = Vector3.Dot(up_hit.normal, Vector3.up);
                            if (FastApproximately(dotProduct, 1.0f, .025f))
                            {
                                climbingStairs = true;
                                shouldMoveStairs = true;
                                climbingTimestamp = Time.unscaledTime;
                                fs.rb.isKinematic = true;
                            }
                            else shouldMoveStairs = false;
                        }
                        else shouldMoveStairs = false;
                    }
                    else shouldMoveStairs = false;
                }
                else shouldMoveStairs = false;
            }

            if (Time.unscaledTime - climbingTimestamp >= .35f)
            {
                climbingStairs = false;
                shouldMoveStairs = false;
            }

            if (!shouldMoveStairs)
            {
                fs.rb.isKinematic = false;
                last_offset = Vector3.zero;
            }
        }

        int infinity_cast = 0;
        void RaycastInfinity()
        {
            if (!Physics.Raycast(fs.self.transform.position + new Vector3(0, fs.collider.height / 2f, 0), Vector3.down, float.PositiveInfinity, LayerUtility.GroundMask))
            {
                infinity_cast++;
            }
            else infinity_cast = 0;

            if (infinity_cast >= 60) DoRespawn(true);
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

                if (pos) fallbackCamera.transform.position = Vector3.Lerp(fallbackCamera.transform.position, output, Time.fixedDeltaTime * Main.settings.camera_pos_vel);
                if (rot) fallbackCamera.transform.rotation = Quaternion.Slerp(fallbackCamera.transform.rotation, rotation, Time.fixedDeltaTime * Main.settings.camera_rot_vel);

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
                        fs.rb.maxDepenetrationVelocity = 2f;
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
        bool kinematic = true;
        void Throwdown()
        {
            PlayerController.Instance.comController.COMRigidbody.isKinematic = kinematic;

            if (check_velocity && respawn_delay >= 1)
            {
                if (PlayerController.Instance.boardController.GroundCheck())
                {
                    check_velocity = false;
                    delay_input = true;
                    kinematic = false;
                    PlayerController.Instance.comController.UpdateCOM(.89f, 1);
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

            Traverse.Create(PlayerController.Instance).Field("_isSwitch").SetValue(respawnSwitch);

            if (!respawnSwitch) PlayerController.Instance.skaterController.ResetSwitchAnims();
            else
            {
                Traverse.Create(PlayerController.Instance.skaterController).Field("_animSwitch").SetValue(1f);
                Traverse.Create(PlayerController.Instance.skaterController).Field("_actualSwitch").SetValue(1f);
            }

            PlayerController.Instance.boardController.ResetAll();
            PlayerController.Instance.boardController.boardRigidbody.velocity = (respawnSwitch ? -PlayerController.Instance.PlayerForward() : PlayerController.Instance.PlayerForward()) * (2f + (8f * -relativeVelocity.z * Main.settings.throwdown_force));
            PlayerController.Instance.comController.COMRigidbody.MovePosition(PlayerController.Instance.skaterController.skaterRigidbody.position);
            PlayerController.Instance.comController.COMRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
            PlayerController.Instance.skaterController.skaterRigidbody.MovePosition(PlayerController.Instance.skaterController.skaterRigidbody.position);
            PlayerController.Instance.skaterController.skaterRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
            PlayerController.Instance.cameraController._camRigidbody.MovePosition(fallbackCamera.transform.position);
            PlayerController.Instance.cameraController._camRigidbody.MoveRotation(fallbackCamera.transform.rotation);
            PlayerController.Instance.cameraController._camRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
        }

        void OnJumpEnd()
        {
            resetJump();
        }

        void resetJump()
        {
            jumping = false;
            hippieJump = false;
            doubleHippieJump = false;
            jump.speed = 1f;
            jump.timeLimitStart = jump.animation.times[0];
            front_flip.speed = 1f;
            front_flip.timeLimitStart = front_flip.animation.times[0];
            hippieStarted = false;
            hippieForceAdded = false;
        }

        RespawnInfo last_nr;
        Vector3 last_hand_l, last_hand_r;
        void OnThrowdownEnd()
        {
            Vector3 forward = fs.rb.transform.forward;

            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, fs.collider.height / 2, 0),
                IsBoardBackwards = false,
                rotation = Quaternion.LookRotation(forward),
                isSwitch = respawnSwitch
            };

            throwdown_state = false;
            updating = true;

            last_hand_l = fs.getPart("Skater_hand_l").position;
            last_hand_r = fs.getPart("Skater_hand_r").position;

            kinematic = true;
            PlayerController.Instance.comController.COMRigidbody.isKinematic = kinematic;
            //PlayerController.Instance.boardController.SetBoardControllerUpVector(fakeSkate.transform.up);
            UpdateGameplay();
            EnableGameplay();
            PlayerController.Instance.inputController.enabled = false;
            RespawnRoutine(respawnInfo);
            RespawnRoutineCoroutines();
            PlayerController.Instance.respawn.behaviourPuppet.BoostImmunity(1000f);
            PlayerController.Instance.respawn.behaviourPuppet.BoostImpulseMlp(1000f);

            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[6].transform.position = last_hand_l;
            PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.muscles[9].transform.position = last_hand_r;
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
            try
            {
                PlayerController.Instance.skaterController.skaterTransform.position = fs.self.transform.position;
                PlayerController.Instance.skaterController.skaterTransform.rotation = fs.self.transform.rotation;
                PlayerController.Instance.boardController.boardTransform.position = fakeSkate.transform.position;
                PlayerController.Instance.boardController.boardTransform.rotation = fakeSkate.transform.rotation;
                PinMovementController.SetStartTransform(new Vector3(PlayerController.Instance.skaterController.skaterTransform.position.x, PlayerController.Instance.skaterController.skaterTransform.position.y + fs.collider.height, PlayerController.Instance.skaterController.skaterTransform.position.z), fallbackCamera.transform.rotation);
            }
            catch { }
        }

        Transform[] original_bones;
        void TogglePlayObject(bool enabled)
        {
            if (MultiplayerManager.Instance.InRoom)
            {
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
            else
            {
                GameStateMachine.Instance.PlayObject.SetActive(enabled);
            }

            PlayerController.Instance.animationController.ToggleAnimators(enabled);
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
            if ((throwdown_state && (actual_anim.name == throwdown_lhlf.name || actual_anim.name == throwdown_lhrf.name)) || emoting) return;
            //Log(target.name + " normal");

            actual_anim.Stop();
            actual_anim = target;
            target.Play();
        }

        void Play(AnimController target, CallBack call)
        {
            if (actual_anim.name == target.name && target.isPlaying) return;
            if ((throwdown_state && (actual_anim.name == throwdown_lhlf.name || actual_anim.name == throwdown_lhrf.name)) || emoting) return;
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
                    animations[i].Stop(true);
                }
                catch { }
            }
        }

        bool loading = false;
        private AudioClip GetClip(string path)
        {
            WWW audioLoader = new WWW(path);
            while (!audioLoader.isDone) System.Threading.Thread.Sleep(100);
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

        public void RespawnRoutine(RespawnInfo respawnInfos)
        {
            ResetSkater();
            PinMovementController.startPositionSet = false;
            Time.timeScale = .01f;
            Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(false);
            PlayerController.Instance.respawn.needRespawn = false;
            PlayerController.Instance.respawn.respawning = true;
            respawning = true;
            PlayerController.Instance.BoardFreezedAfterRespawn = false;
            PlayerController.Instance.DisableArmPhysics();
            PlayerController.Instance.respawn.behaviourPuppet.pinWeightThreshold = 0f;
            PlayerController.Instance.respawn.recentlyRespawned = false;
            PlayerController.Instance.playerSM.OnRespawnSM();
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
            PlayerController.Instance.skaterController.skaterRigidbody.rotation = PlayerController.Instance.respawn.behaviourPuppet.puppetMaster.transform.rotation;
            PlayerController.Instance.skaterController.skaterTransform.position = PlayerController.Instance.boardController.boardTransform.position;
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
            PlayerController.Instance.ResetAllAnimations();
            MonoBehaviourSingleton<PlayerController>.Instance.AnimGrindTransition(false);
            MonoBehaviourSingleton<PlayerController>.Instance.AnimOllieTransition(false);
            MonoBehaviourSingleton<PlayerController>.Instance.AnimSetupTransition(false);
            PlayerController.Instance.boardController.ResetBoardTargetPosition();

            MonoBehaviourSingleton<PlayerController>.Instance.SetIKLerpSpeed(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetLeftIKLerpTarget(0f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetRightIKLerpTarget(0f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetRightIKWeight(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.SetLeftIKWeight(1f);
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.IsInGrindState = false;
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.IsInCopingState = false;
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.NeedToSlowLerpCamera = false;
            MonoBehaviourSingleton<PlayerController>.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);
            MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.angularVelocity = Vector3.zero;
            MonoBehaviourSingleton<PlayerController>.Instance.comController.UpdateCOM(0.89f, 1);
            MonoBehaviourSingleton<PlayerController>.Instance.skaterController.InitializeSkateRotation();
            MonoBehaviourSingleton<PlayerController>.Instance.skaterController.skaterRigidbody.angularVelocity = Vector3.zero;
            PlayerController.Instance.boardController.boardRigidbody.ResetInertiaTensor();
            PlayerController.Instance.boardController.ResetAll();
            PlayerController.Instance.comController.COMRigidbody.MovePosition(PlayerController.Instance.skaterController.skaterRigidbody.position);
            PlayerController.Instance.comController.COMRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;

            MonoBehaviourSingleton<PlayerController>.Instance.boardController.triggerManager.spline = null;
            MonoBehaviourSingleton<PlayerController>.Instance.ResetBoardCenterOfMass();
            MonoBehaviourSingleton<PlayerController>.Instance.ResetBackTruckCenterOfMass();
            MonoBehaviourSingleton<PlayerController>.Instance.ResetFrontTruckCenterOfMass();
            MonoBehaviourSingleton<SoundManager>.Instance.StopPowerslideSound(1, Vector3.ProjectOnPlane(MonoBehaviourSingleton<PlayerController>.Instance.boardController.boardRigidbody.velocity, Vector3.up).magnitude);
            EventManager.Instance.ExitGrind();

            MonoBehaviourSingleton<PlayerController>.Instance.RagdollLayerChange(false);
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.puppetMaster.pinWeight = 1f;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.puppetMaster.muscleWeight = 1f;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.behaviourPuppet.defaults.minMappingWeight = 0f;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.behaviourPuppet.masterProps.normalMode = BehaviourPuppet.NormalMode.Unmapped;
            MonoBehaviourSingleton<PlayerController>.Instance.SetBoardPhysicsMaterial(PlayerController.FrictionType.Default);
            MonoBehaviourSingleton<PlayerController>.Instance.cameraController.enabled = true;
            MonoBehaviourSingleton<PlayerController>.Instance.EnablePuppetMaster(true, false);

            main_cam.transform.position = fallbackCamera.transform.position;
            main_cam.transform.rotation = fallbackCamera.transform.rotation;

            PlayerController.Instance.DisableArmPhysics();

            PlayerController.Instance.CancelRespawnInvoke();

            Time.timeScale = 1f;

            EnterRiding();
        }

        void EnterRiding()
        {
            MonoBehaviourSingleton<PlayerController>.Instance.currentStateEnum = PlayerController.CurrentState.Riding;
        }

        CinemachineCollider cinemachine_collider;
        public void DisableCameraCollider(bool enabled)
        {
            if (!cinemachine_collider) cinemachine_collider = PlayerController.Instance.cameraController.gameObject.GetComponentInChildren<Cinemachine.CinemachineCollider>();
            if (cinemachine_collider != null) cinemachine_collider.enabled = enabled;
        }

        void ResetSkater()
        {
            Vector3[] skaterVelocities = (Vector3[])Traverse.Create(PlayerController.Instance.skaterController).Field("skaterVelocities").GetValue();

            for (int j = 0; j < skaterVelocities.Length; j++)
            {
                skaterVelocities[j] = Vector3.zero;
            }

            Traverse.Create(PlayerController.Instance.skaterController).Field("skaterVelocities").SetValue(skaterVelocities);
            Traverse.Create(PlayerController.Instance.skaterController).Field("lastSkaterPos").SetValue(PlayerController.Instance.skaterController.skaterTransform.position);


            Vector3[] skateVelocities = (Vector3[])Traverse.Create(PlayerController.Instance.boardController).Field("boardVelocites").GetValue();

            for (int j = 0; j < skateVelocities.Length; j++)
            {
                skateVelocities[j] = Vector3.zero;
            }

            Traverse.Create(PlayerController.Instance.boardController).Field("boardVelocites").SetValue(skateVelocities);
        }
    }
}