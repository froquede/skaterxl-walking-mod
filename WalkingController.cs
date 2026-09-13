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
using System.Reflection;
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
        public GameObject fallbackCamera;
        public GameObject fakeSkate;
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

        void Init()
        {
            initializing = true;
            init = true;
            try
            {
                InitInternal();
            }
            catch (Exception e)
            {
                Utils.Log("Error initializing " + e);
            }
            finally
            {
                // Main.Load pauses the game until init, never leave it frozen if something above throws
                Time.timeScale = 1f;
                initializing = false;
            }
        }

        static readonly AccessTools.FieldRef<bool> isInPlayState = AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(typeof(PlayTime), "isInPlayState"));

        void InitInternal()
        {
            try
            {
                playtimeobj = GameObject.Find("PlayTime").GetComponent<PlayTime>();
            }
            catch
            {
                Utils.Log("Error getting PlayTime component");
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

            actual_anim = new AnimController();
            //SceneManager.activeSceneChanged += OnSceneLoaded;

            LoadEmotes();
            LoadSoundEmotes();

            EventManager.Instance.onGPEvent += onRunEvent;

            MessageSystem.QueueMessage(MessageDisplayData.Type.Success, "Walking mod loaded", 2f);
        }

        public void LoadEmotes()
        {
            string[] temp_emotes = Directory.GetFiles(Path.Combine(Main.modEntry.Path, "animations"), "*.json");
            temp_emotes = temp_emotes.Concat(Directory.GetFiles(documents, "*.json")).ToArray();
            Array.Sort(temp_emotes);

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
                    // indexer so "Reload only sound emotes" doesn't throw on names already cached
                    audioCache[name] = clip;
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
                    Utils.Log("Received bail event " + respawning);
                    break;
            }
        }

        int walking_crossfade = 0;
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
            //running_jump.crossfade = 1;
            running_jump.anchorRootFade = false;

            left_turn = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\left_turn.json"), fs));
            right_turn = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\right_turn.json"), fs));
            front_flip = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\front_flip.json"), fs, false, true));

            back_flip = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\back_flip.json"), fs, false, true));
            back_flip.speed = 1.1f;

            throwdown_lhrf = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\run_throwdown_lhrf.json"), fs, false));
            throwdown_lhrf.timeLimit = 0.425f;

            throwdown_lhlf = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\run_throwdown_lhlf.json"), fs, false));
            throwdown_lhlf.timeLimit = 0.425f;

            throwdown_lhrf.speed = throwdown_lhlf.speed = 1.25f;

            falling = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\falling.json"), fs, Quaternion.Euler(0, 0, 0)));
            falling.crossfade = 6;
            falling.anchorRoot = true;
            falling.anchorRootFade = false;

            impact_roll = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\impact_roll.json"), fs, false));
            impact_roll.crossfade = 1;
            impact_roll.speed = 1.25f;
            impact_roll.timeLimitStart = .04f;

            stumble = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\stumble.json"), fs, false));
            stumble.crossfade = 1;
            stumble.speed = 1.25f;

            stairs_up = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\stairs.json"), fs));
            stairs_up_running = LoadAnim(new AnimController(Path.Combine(Main.modEntry.Path, "animations\\stairs_running.json"), fs));
            //stairs_up_running.crossfade = stairs_up.crossfade = 1;
            /*stairs_up.anchorRoot = true;
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
            semote1 = GetCachedClip(Main.settings.semote1);
            semote2 = GetCachedClip(Main.settings.semote2);
            semote3 = GetCachedClip(Main.settings.semote3);
            semote4 = GetCachedClip(Main.settings.semote4);
        }

        AudioClip GetCachedClip(string key)
        {
            AudioClip clip;
            if (key != null && audioCache.TryGetValue(key, out clip)) return clip;
            Utils.Log("Sound emote not found: " + key);
            return null;
        }

        public float speed = 10f;
        public float jumpForce = 10.0f;
        public Vector3 velocity;
        bool jumping = false, throwdown_state = false;
        // in 60 fps frames
        float press_count = 0;
        string actual_state = "";
        float max_speed = 7f;
        float walk_speed = 2f;
        float running_speed = 2.5f;
        bool emoting = false;
        bool respawnSwitch = false;
        float limit_idle = .2f;
        float decay = .95f;

        Vector3 last_pos = Vector3.zero;
        string last_restore_state = "";

        CharacterMotor motor;
        bool motorIgnoresBoard = false;

        // skateboard only collides with the walker while it's on the ground, not while carried
        void UpdateBoardCollision()
        {
            if (motor == null || !fakeSkate) return;
            if (fakeSkateColliders == null) fakeSkateColliders = fakeSkate.GetComponentsInChildren<Collider>();
            bool ignore = magnetized;
            if (ignore == motorIgnoresBoard) return;
            foreach (Collider c in fakeSkateColliders)
            {
                if (c) Physics.IgnoreCollision(fs.collider, c, ignore);
            }
            motorIgnoresBoard = ignore;
        }

        // online the real skater (capsule, feet, ragdoll) stays active and is moved onto the walker every frame,
        // so its colliders would keep shoving the walking body around
        void IgnorePlayerCollisions()
        {
            if (!MultiplayerManager.Instance.InRoom || fs == null || !fs.collider || !GameStateMachine.Instance.PlayObject) return;
            foreach (Collider c in GameStateMachine.Instance.PlayObject.GetComponentsInChildren<Collider>(true))
            {
                if (c && c != fs.collider) Physics.IgnoreCollision(fs.collider, c, true);
            }
        }

        int WalkableMask()
        {
            int mask = ~(1 << LayerUtility.Character | 1 << LayerUtility.Ragdoll | 1 << LayerUtility.RagdollNoInternalCollision);
            if (magnetized || jumping) mask &= ~(1 << LayerUtility.Skateboard);
            return mask;
        }

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
                ApplyLaunch();
            }
        }

        // after a throwdown the game's respawn has placed the skater on the board, carry the walking speed into it for a few steps
        Vector3 launchVelocity;
        float launchTimer = 0f, inputDelayTimer = 0f;
        void ApplyLaunch()
        {
            if (launchTimer <= 0f) return;
            launchTimer -= Time.fixedDeltaTime;

            PlayerController pc = PlayerController.Instance;
            pc.boardController.boardRigidbody.isKinematic = false;
            // horizontal only: overriding the vertical speed of the body and center of mass held them still while the
            // center of mass controller kept building force, which then threw the skater up once the launch ended
            SetPlanarVelocity(pc.boardController.boardRigidbody, launchVelocity);
            SetPlanarVelocity(pc.skaterController.skaterRigidbody, launchVelocity);
            SetPlanarVelocity(pc.comController.COMRigidbody, launchVelocity);
            SetPlanarVelocity(pc.skaterController.leanProxy, launchVelocity);
        }

        // the respawn point on the ground under the walker, like the game's own respawn, so the board never starts inside a step or slope
        Vector3 GroundBelowWalker()
        {
            Vector3 feet = fs.rb.position - new Vector3(0, fs.collider.height / 2f, 0);
            RaycastHit hit;
            if (Physics.Raycast(feet + Vector3.up * .5f, Vector3.down, out hit, 1.5f, LayerUtility.GroundMask, QueryTriggerInteraction.Ignore)) return hit.point;
            return feet;
        }

        static void SetPlanarVelocity(Rigidbody body, Vector3 planar)
        {
            if (body == null || body.isKinematic) return;
            body.velocity = new Vector3(planar.x, body.velocity.y, planar.z);
        }

        bool should_run = false, throwed = false;
        void Update()
        {
            if (PlayerController.Instance.inputController.controlsActive && !init) Init();
            if (initializing || !init) return;

            if (!inState) inPlayStateLogic();
            else inStateLogicUpdate();

            // brief input lock after getting back on the board so held buttons don't pop or turn straight away
            if (!inState && inputDelayTimer > 0f)
            {
                inputDelayTimer -= Time.unscaledDeltaTime;
                if (inputDelayTimer <= 0f) PlayerController.Instance.inputController.enabled = true;
            }

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
                fakeSkateColliders = null;
                lastFrictionType = null;
            }
            catch
            {
                Utils.Log("Error destroying fs");
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
                fs.Create(PlayerController.Instance.skaterController.skaterTransform.position + new Vector3(0, -.2f, 0));

                GameObject left_source = new GameObject("Left Audio Source");
                audioSource_left = left_source.AddComponent<AudioSource>();
                SetupStepSource(audioSource_left);
                //left_source.AddComponent<AudioSourceTracker>();
                left_source.transform.parent = fs.self.transform;

                GameObject right_source = new GameObject("Right Audio Source");
                audioSource_right = right_source.AddComponent<AudioSource>();
                SetupStepSource(audioSource_right);
                //right_source.AddComponent<AudioSourceTracker>();
                right_source.transform.parent = fs.self.transform;

                GameObject emote_source = new GameObject("Emote Audio Source");
                soundEmoteSource = emote_source.AddComponent<AudioSource>();
                //right_source.AddComponent<AudioSourceTracker>();
                emote_source.transform.parent = fs.self.transform;

                //fs.self.AddComponent<TransformTracker>();
                fakeSkate = Instantiate(PlayerController.Instance.boardController.boardTransform.gameObject);
                fakeSkateColliders = null;
                lastFrictionType = null;
                skate_rb = fakeSkate.GetComponent<Rigidbody>();
                skate_rb.isKinematic = magnetized;
                fakeTrucks[0] = fakeSkate.transform.FindChildRecursively("Back Truck");
                fakeTrucks[1] = fakeSkate.transform.FindChildRecursively("Front Truck");
                fakeSkate.transform.position = PlayerController.Instance.boardController.transform.position;
                fakeSkate.transform.rotation = PlayerController.Instance.boardController.transform.rotation;
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
                    PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(false);
                    PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Utils.Log("Error creating FS " + e.Message);
            }
        }

        float last_nonplaytime = 0;
        Vector3 last_real_velocity = Vector3.zero;
        void DisableGameplay()
        {
            PlayerController.Instance.ikController.enabled = false;
            PlayerController.Instance.cameraController._camRigidbody.isKinematic = true;
            PlayerController.Instance.cameraController.enabled = false;
            last_real_velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;
            GameStateMachine.Instance.PinObject.SetActive(false);
            EventManager.Instance.EndTrickCombo(true, false);
            //EventManager.Instance.EndTrickCombo(false, true);
            TogglePlayObject(false);
            ReplaceBones(false);
            last_nonplaytime = (float)Traverse.Create(playtimeobj).Field("nonPlayTime").GetValue();
            PlayerController.Instance.inputController.enabled = false;
            // remember the collider state (other mods toggle it) so leaving walk mode puts it back
            if (cameraColliderWasEnabled == null)
            {
                if (!cinemachine_collider) cinemachine_collider = PlayerController.Instance.cameraController.gameObject.GetComponentInChildren<Cinemachine.CinemachineCollider>();
                cameraColliderWasEnabled = cinemachine_collider != null && cinemachine_collider.enabled;
            }
            DisableCameraCollider(false);
            fallbackCamera.GetComponent<CinemachineFallbackCamera>().enabled = false;

            SoundManager.Instance.deckSounds.MuteAll();
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(true);
        }

        Vector3 camera_offset = Vector3.zero;
        public float restore_timestamp = 0f;
        public void EnableGameplay(bool playObject = true)
        {
            UpdateGameplay();
            inState = false;

            restore_timestamp = Time.unscaledTime;

            resetJump();
            StopAll();

            DestroyFS();
            PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(true);
            //EventManager.Instance.EndTrickCombo(false, true);
            Utils.Log("Enabling gameplay " + playObject);
            if (playObject)
            {
                PlayerController.Instance.EnableGameplay();
                TogglePlayObject(true);
                if (EventManager.Instance.IsInCombo) EventManager.Instance.EndTrickCombo(false, true);
            }

            //PlayerController.Instance.comController.enabled = true;
            //PlayerController.Instance.ikController.enabled = true;
            //PlayerController.Instance.animationController.enabled = true;

            PlayerController.Instance.animationController.ToggleAnimators(true);
            PlayerController.Instance.inputController.enabled = true;
            ReplaceBones(true);

            SoundManager.Instance.deckSounds.UnMuteAll();
            SoundManager.Instance.ragdollSounds.MuteRagdollSounds(false);

            fallbackCamera.GetComponent<CinemachineFallbackCamera>().enabled = true;
            if (cameraColliderWasEnabled != null)
            {
                DisableCameraCollider(cameraColliderWasEnabled.Value);
                cameraColliderWasEnabled = null;
            }

            PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(true);

            if (last_nonplaytime != 0) Traverse.Create(playtimeobj).Field("nonPlayTime").SetValue(last_nonplaytime);

            PlayerController.Instance.cameraController._camRigidbody.isKinematic = false;
            PlayerController.Instance.cameraController._camRigidbody.velocity = Vector3.zero;
            PlayerController.Instance.ikController.enabled = true;
            PlayerController.Instance.cameraController.enabled = true;
        }


        bool Sideway()
        {
            return actual_anim.name == running_left.name || actual_anim.name == running_right.name || actual_anim.name == walking_left.name || actual_anim.name == walking_right.name;
        }

        bool Rotating()
        {
            return actual_anim.name == left_turn.name || actual_anim.name == right_turn.name;
        }

        float yCamVelocity = 0;
        // camera yaw in world space, the left stick moves relative to it
        float cam_yaw = 0f;
        void inStateUpdate()
        {
            if (!fs.self || !fs.rb || !fakeSkate || GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState) || !inState) return;

            if (!MultiplayerManager.Instance.InRoom) AddReplayFrame();
            else CheckAudioSources();

            UpdateVisualHeight();
            if (!hippieJump) RotationOffset(Time.deltaTime);
            try { actual_anim.Update(); } catch { Utils.Log("Error updating animation " + inState); }
            // an animation callback (throwdown end) can leave walk mode and destroy the fake skater mid-update
            if (!inState || !fs.self || !fs.rb || !fakeSkate) return;
            isInPlayState() = true;

            emoteInput();
            if (!emoting) ThrowdownInput();
            magnetInput();

            RaycastFeet();
            UpdateSticks();
            RaycastFloor();
            if (!inState || !fs.self || !fs.rb || !fakeSkate) return;

            UpdateStairs();
            RaycastInfinity();
            if (MultiplayerManager.Instance.InRoom && !respawning && !PlayerController.Instance.respawn.respawning) UpdateRagdoll();
            PlayerController.Instance.boardController.boardRigidbody.isKinematic = magnetized;
            if (!skate_rb) skate_rb = fakeSkate.GetComponent<Rigidbody>();
            skate_rb.isKinematic = magnetized;
            UpdateGameplay();

            // landing hard enough rolls; minVelocityRoll was a per-physics-tick distance at 120 Hz
            if (pendingLandingSpeed > 0f)
            {
                float landing = pendingLandingSpeed;
                pendingLandingSpeed = 0f;
                if (landing > Main.settings.minVelocityRoll * 12f && !throwdown_state && !emoting && actual_state != "stumble")
                {
                    actual_state = "impact";
                    CallBack call = OnImpactEnd;
                    Play(impact_roll, call);
                }
            }

            float planarSpeed = last_velocity.magnitude;
            if (!jumping && actual_state != "impact" && !climbingStairs && actual_state != "stumble" && grounded && !hippieJump)
            {
                // a little hysteresis so walk, run and idle don't flicker around the thresholds
                if (running_state) running_state = planarSpeed > running_speed - .35f;
                else running_state = planarSpeed >= running_speed + .15f;

                if (running_state) actual_state = "running";
                else if (actual_state == "idle") actual_state = planarSpeed > limit_idle + .15f ? "walking" : "idle";
                else actual_state = planarSpeed < limit_idle ? "idle" : "walking";
            }

            UpdateLocomotion(planarSpeed);

            // on stairs the regular animation handling is skipped, the stairs blend is part of the same locomotion mix
            if (climbingStairs) PlayLocomotion(planarSpeed);

            if (-fs.rb.velocity.y > 4.8f && !grounded && !jumping && actual_state != "impact" && !climbingStairs && actual_state != "stumble") actual_state = "falling";

            Board();

            if (PlayerController.Instance.inputController.player.GetButtonDown(Main.settings.pin_button))
            {
                RestoreGameplay(true, true);
            }

            if (!GetButtonDown("LB") && !GetButtonDown("RB") && !Main.ui.emote_config && !Main.ui.sound_emote_config)
            {
                if (PlayerController.Instance.inputController.player.GetButtonDown(68) || PlayerController.Instance.inputController.player.GetButton(68)) SetRespawn();
                if (PlayerController.Instance.inputController.player.GetButtonDown(67) || PlayerController.Instance.inputController.player.GetButton(67)) DoRespawn();
            }

            // right stick orbits the camera. Pitch lives in cam_rotation, yaw is a world angle so movement can be camera-relative.
            // pitch was tuned per frame at 60 fps
            float x = cam_rotation.eulerAngles.x - RY * Utils.FrameScale();
            cam_yaw = Mathf.SmoothDamp(cam_yaw, cam_yaw + (RX * 10f), ref yCamVelocity, .1f);
            cam_rotation = Quaternion.Euler(x, 0, 0);

            // small deadzone so stick drift doesn't count as camera input
            if (Mathf.Abs(RX) > .15f || Mathf.Abs(RY) > .15f) last_rotation_timestamp = Time.unscaledTime;

            if (!emoting && Main.settings.camera_auto_follow && Time.unscaledTime - last_rotation_timestamp >= Main.settings.camera_follow_delay)
            {
                // swing back behind the character while it moves, easing in with speed so it doesn't whip around on small steps
                float follow = Mathf.InverseLerp(limit_idle, walk_speed, last_velocity.magnitude);
                if (follow > 0f)
                {
                    float t = 1f - Mathf.Exp(-Main.settings.camera_follow_speed * follow * Time.deltaTime);
                    cam_yaw = Mathf.LerpAngle(cam_yaw, fs.self.transform.eulerAngles.y, t);
                    cam_rotation = Quaternion.Slerp(cam_rotation, Quaternion.identity, t);
                }
            }

            if (throwdown_state)
            {
                float step = .03f * (actual_anim.frame <= 4 ? actual_anim.frame : 4);
                actual_anim.offset = Vector3.Lerp(actual_anim.offset, new Vector3(0, -.80f + step, 0), Utils.FrameIndependentLerp(Time.fixedDeltaTime * 24f));
            }

            if (!jumping && !hippieJump) { }
            else JumpingOffset();
        }

        int instate_count = 0;
        Vector3 relativeVelocity;
        Quaternion last_rotation_offset = Quaternion.Euler(0, 0, 0);
        Vector3 last_velocity;
        float rotation_speed = 12f;
        bool hippieStarted = false, xUp = false, running_state = false;
        float last_rotation_timestamp = 0f;
        bool double_press_magnetize_button = false;
        bool runningInput = false;
        float runningInputMultiplier = 1f, runningVelocity = 0f;
        void inStateLogic()
        {
            if (!fs.self || !fs.rb || !fakeSkate || GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState) || !inState) return;

            runningInput = GetButtonDown(Main.settings.run_button);
            runningInputMultiplier = Mathf.SmoothDamp(runningInputMultiplier, runningInput ? 2f : 1f, ref runningVelocity, .4f);

            if (hippieJump) last_rotation_offset = Quaternion.Euler(0, 90f, 0);

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

            Movement();

            instate_count++;

            last_velocity = new Vector3(fs.rb.velocity.x, 0, fs.rb.velocity.z);
            last_pos = fs.rb.position;

            spawning = false;
        }

        void magnetInput()
        {
            if (PlayerController.Instance.inputController.player.GetButtonUp(Main.settings.magnetize_button) || (Time.fixedUnscaledTime - enterBailTimestamp >= 2f && !PlayerController.Instance.inputController.player.GetButton(Main.settings.magnetize_button))) xUp = true;

            if (xUp)
            {
                if (PlayerController.Instance.inputController.player.GetButtonShortPressDown(Main.settings.magnetize_button))
                {
                    magnetized = !magnetized;
                }
                if (PlayerController.Instance.inputController.player.GetButtonDoublePressHold(Main.settings.magnetize_button))
                {
                    if (!double_press_magnetize_button)
                    {
                        double_press_magnetize_button = true;
                        if (Main.settings.left_arm)
                        {
                            if (!Main.settings.mallgrab) Main.settings.mallgrab = true;
                            else
                            {
                                Main.settings.left_arm = Main.settings.mallgrab = false;
                            }
                        }
                        else
                        {
                            if (!Main.settings.mallgrab) Main.settings.mallgrab = true;
                            else
                            {
                                Main.settings.left_arm = true;
                                Main.settings.mallgrab = false;
                            }
                        }
                    }
                }
                else double_press_magnetize_button = false;
            }
        }

        public void inStateLogicUpdate()
        {
            inStateUpdate();

            if (!jumping && !hippieJump)
            {
                if (!emoting && actual_state != "impact" && !climbingStairs && actual_state != "stumble")
                {
                    HandleAnimations();
                    JumpInput();
                }
            }
        }

        static readonly MethodInfo respawnAt = AccessTools.Method(typeof(Respawn), "RespawnAt");

        // Uses the game's own respawn to put the skater on the board, then skips its short board freeze
        // and carries the given velocity into the board for a few physics steps.
        void RespawnOnBoard(RespawnInfo info, Vector3 travel, bool playObj)
        {
            EnableGameplay(playObj);

            Respawn respawn = PlayerController.Instance.respawn;
            respawnAt.Invoke(respawn, new object[] { info, false });

            respawn.CancelInvoke("EnableBoardPhysics");
            PlayerController.Instance.boardController.boardRigidbody.isKinematic = false;
            PlayerController.Instance.BoardFreezedAfterRespawn = false;

            // The recorder skips frames while respawning (0.25 s of frozen replay) and the respawn event marks the next
            // replay frame as a cut, so clear just those. The ragdoll keeps the game's half second in kinematic mode:
            // switching it active on the teleport frame launched the skater upward.
            respawn.CancelInvoke("EndRespawning");
            respawn.respawning = false;
            respawn.needRespawn = false;
            Traverse.Create(ReplayRecorder.Instance.transformReference).Field("didRespawn").SetValue(false);

            // roll along the board's own axis (switch respawns place it the other way round), toward where the walker was heading
            travel.y = 0f;
            launchVelocity = Vector3.zero;
            launchTimer = 0f;
            if (travel.sqrMagnitude > .01f)
            {
                Vector3 axis = Vector3.ProjectOnPlane(PlayerController.Instance.boardController.boardTransform.forward, Vector3.up);
                if (axis.sqrMagnitude < .0001f) axis = travel;
                axis.Normalize();
                if (Vector3.Dot(axis, travel) < 0f) axis = -axis;
                launchVelocity = axis * travel.magnitude;
                launchTimer = .12f;
            }
        }

        public void RestoreGameplay(bool originalRespawn = false, bool playObj = true, bool originalPoint = false, Vector3 boardVelocity = default(Vector3))
        {
            try
            {
                PlayerController.Instance.inputController.enabled = true;
                PlayerController.Instance.skaterController.skaterTransform.Find("Skater").gameObject.SetActive(true);
                PlayerController.Instance.boardController.boardTransform.gameObject.SetActive(true);

                UpdateGameplay();
                if (originalRespawn)
                {
                    EnableGameplay(playObj);
                    PlayerController.Instance.respawn.DoRespawn();
                }
                else
                {
                    RespawnInfo respawnInfo = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();
                    if (fs.rb != null && !originalPoint)
                    {
                        Vector3 forward = Vector3.ProjectOnPlane(fs.rb.transform.forward, Vector3.up);
                        respawnInfo = new RespawnInfo
                        {
                            position = GroundBelowWalker(),
                            IsBoardBackwards = false,
                            rotation = forward.sqrMagnitude > .0001f ? Quaternion.LookRotation(forward) : Quaternion.identity,
                            isSwitch = false
                        };
                    }

                    RespawnOnBoard(respawnInfo, boardVelocity, playObj);
                }

                GrindPart();

                isInPlayState() = GameStateMachine.Instance.CurrentState.GetType() == typeof(PlayState);
            }
            catch (Exception e)
            {
                Utils.Log("Error restoring gameplay");
                Utils.Log(e.Message);
                Utils.Log(e.StackTrace);
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
            golf = false;
            cam_rotation = Quaternion.identity;
            if (Time.unscaledTime - enterBailTimestamp >= Main.settings.bailLimit || force)
            {
                respawning = true;
                last_nr = (RespawnInfo)Traverse.Create(PlayerController.Instance.respawn).Field("markerRespawnInfos").GetValue();
                fs.self.transform.position = new Vector3(last_nr.position.x, last_nr.position.y + (fs.collider.height / 1.5f), last_nr.position.z);
                fs.self.transform.rotation = last_nr.rotation;
                cam_yaw = last_nr.rotation.eulerAngles.y;
                if (motor != null) motor.Teleport(fs.self.transform.position, last_nr.rotation.eulerAngles.y);
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
                cam_yaw = last_nr.rotation.eulerAngles.y;
                if (motor != null) motor.Teleport(fs.self.transform.position, last_nr.rotation.eulerAngles.y);
                fs.rb.velocity = Vector3.zero;
                fs.rb.angularVelocity = Vector3.zero;
                PlayerController.Instance.respawn.puppetMaster.Teleport(fs.self.transform.position + fs.self.transform.rotation * PlayerController.Instance.respawn.GetOffsetPositions(false)[1] + (Vector3)Traverse.Create(PlayerController.Instance.respawn).Field("_playerOffset").GetValue(), fs.self.transform.rotation, false);

                RestoreGameplay(true, true);
            }

            UpdateGameplay();
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

        PlayerTransformStateHalf? last_replay_state = null;
        float next_replay_frame = 0f, replay_time_accum = 0f;
        void AddReplayFrame()
        {
            // record at the replay frame rate like the game's recorder, not every rendered frame
            replay_time_accum += PlayTime.deltaTime;
            float fps = ReplaySettings.Instance.FPS > 0f ? ReplaySettings.Instance.FPS : 30f;
            if (PlayTime.time < next_replay_frame) return;
            next_replay_frame = next_replay_frame < PlayTime.time - 1f ? PlayTime.time + 1f / fps : next_replay_frame + 1f / fps;

            float time = replay_time_accum;
            replay_time_accum = 0f;
            //Log(time + " " + ReplayRecorder.Instance.LocalPlayerFrames.Count + " " + PlayTime.deltaTime);
            if (ReplayRecorder.Instance.LocalPlayerFrames == null) Traverse.Create(ReplayRecorder.Instance).Property("LocalPlayerFrames").SetValue(new List<ReplayPlayerFrameHalf>());

            List<ReplayPlayerFrameHalf> frames = ReplayRecorder.Instance.LocalPlayerFrames;
            if (frames.Count > 0 && frames[frames.Count - 1] != null) time += frames[frames.Count - 1].time;

            ReplayPlayerFrameHalf replayPlayerFrameHalf = new ReplayPlayerFrameHalf
            {
                time = time,
                serverTime = MultiplayerManager.Instance.InRoom ? PhotonNetwork.Time : double.MinValue,
                playingClips = new PlayingClipData[0],
                oneShotEvents = new OneShotEventData[0],
                paramChangeEvents = new AudioParamEventData[0],
                controllerState = ReplayRecorder.Instance.RecordControllerState()
            };

            // keep rotations continuous with the previous recorded frame, like PlayerTransformReference does,
            // otherwise playback can interpolate the long way round and flip
            PlayerTransformStateHalf transformState = default(PlayerTransformStateHalf);
            transformState.boardPosition = fakeSkate.transform.position;
            transformState.boardRotation = last_replay_state != null ? Utils.EnsureQuaternionContinuity(last_replay_state.Value.boardRotation, fakeSkate.transform.rotation) : fakeSkate.transform.rotation;

            transformState.boardWheelSpeeds.SetAll(0.01f);

            for (int i = 0; i < ReplayRecorder.Instance.transformReference.boardTruckTransforms.Length; i++)
            {
                transformState.boardTruckLocalPositions[i] = fakeTrucks[i].localPosition;
                transformState.boardTruckLocalRotations[i] = last_replay_state != null ? Utils.EnsureQuaternionContinuity(last_replay_state.Value.boardTruckLocalRotations[i], fakeTrucks[i].localRotation) : fakeTrucks[i].localRotation;
            }

            transformState.skaterRootPosition = fs.self.transform.position;
            transformState.skaterRootRotation = last_replay_state != null ? Utils.EnsureQuaternionContinuity(last_replay_state.Value.skaterRootRotation, fs.self.transform.rotation) : fs.self.transform.rotation;

            for (int j = 0; j < ReplayRecorder.Instance.transformReference.skaterMainBones.Length; j++)
            {
                Quaternion rot = fs.getPart(ReplayRecorder.Instance.transformReference.skaterMainBones[j].name).localRotation;
                transformState.skaterBoneLocalRotations[j] = last_replay_state != null ? Utils.EnsureQuaternionContinuity(last_replay_state.Value.skaterBoneLocalRotations[j], rot) : rot;
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
            transformState.camera.rotation = last_replay_state != null ? Utils.EnsureQuaternionContinuity(last_replay_state.Value.camera.rotation, fallbackCamera.transform.rotation) : fallbackCamera.transform.rotation;
            transformState.didRespawn = false;
            replayPlayerFrameHalf.transformState = transformState;

            frames.Add(replayPlayerFrameHalf);
            last_replay_state = transformState;

            // the game's recorder is off while walking, so drop frames past the replay length the same way it does
            float cutoff = time - ReplaySettings.Instance.MaxRecordedTime;
            int old = 0;
            while (old < frames.Count && frames[old].time < cutoff) old++;
            if (old > 0) frames.RemoveRange(0, old);

            try
            {
                // lastState is an auto-property, Field("lastState") never wrote it
                Traverse.Create(ReplayRecorder.Instance.transformReference).Property("lastState").SetValue(transformState);
                Traverse.Create(ReplayEditorController.Instance.playbackController.transformReference).Property("lastState").SetValue(transformState);
                Traverse.Create(ReplayRecorder.Instance).Field("nextRecordTime").SetValue(next_replay_frame);
            }
            catch (Exception e) { Utils.Log(e); }
        }


        float lean = 0f, leanVelocity = 0f;
        // The body itself turns to face movement, so animations play straight ahead of it,
        // leaning a little into turns with speed.
        void RotationOffset(float dt)
        {
            float leanTarget = 0f;
            if (motor != null && motor.grounded && (actual_state == "walking" || actual_state == "running") && !emoting && !throwdown_state)
            {
                // turning right (positive yaw rate) leans right, which is a negative roll
                leanTarget = Mathf.Clamp(-motor.yawRate * last_velocity.magnitude * .006f, -10f, 10f);
            }
            lean = Mathf.SmoothDamp(lean, leanTarget, ref leanVelocity, .12f, Mathf.Infinity, dt);

            // rotation_offset is applied in world space before the body rotation, so express the local roll in world space
            Quaternion body = fs.self.transform.rotation;
            Quaternion target = body * Quaternion.Euler(0f, 0f, lean) * Quaternion.Inverse(body);

            actual_anim.rotation_offset = Quaternion.Slerp(last_rotation_offset, target, 1f - Mathf.Exp(-rotation_speed * dt));
            last_rotation_offset = actual_anim.rotation_offset;
        }

        // Physics lifts the capsule onto a step in one tick; the animated body follows that height change smoothly.
        public float visualOffsetY = 0f;
        float visualY = 0f, visualYVelocity = 0f;
        bool easingHeight = false;
        void UpdateVisualHeight()
        {
            float bodyY = fs.self.transform.position.y;
            bool recentJump = motor != null && Time.time - motor.lastHeightJumpTime < .3f;

            if (recentJump || easingHeight)
            {
                easingHeight = true;
                visualY = Mathf.SmoothDamp(visualY, bodyY, ref visualYVelocity, .09f);
                if (!recentJump && Mathf.Abs(visualY - bodyY) < .003f) easingHeight = false;
            }
            else
            {
                visualY = bodyY;
                visualYVelocity = 0f;
            }

            visualOffsetY = Mathf.Clamp(visualY - bodyY, -.4f, .4f);
        }

        Vector3 inputDamp;
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
        Vector2 controls = Vector2.zero, controlsVel = Vector2.zero;
        Vector3 velRb = Vector3.zero;
        float pendingLandingSpeed = 0f;
        void Movement()
        {
            if (!fs.rb || motor == null) return;

            float dt = Time.fixedDeltaTime;
            int mask = WalkableMask();
            UpdateBoardCollision();

            // light smoothing only, heavier input smoothing on top of acceleration felt laggy
            controls = Vector2.SmoothDamp(controls, new Vector2(LX, LY), ref controlsVel, .06f);

            motor.Probe(mask, !hippieJump, dt);
            grounded = motor.grounded || motor.timeSinceGrounded < .1f;
            if (motor.landingSpeed > 0f) pendingLandingSpeed = Mathf.Max(pendingLandingSpeed, motor.landingSpeed);

            // left stick moves relative to the camera, holding the run button raises the target speed
            Vector3 input = Quaternion.Euler(0, cam_yaw, 0) * new Vector3(controls.x, 0, controls.y);
            if (input.sqrMagnitude > 1f) input.Normalize();
            float target_speed = Mathf.Lerp(walk_speed, max_speed, Mathf.Clamp01(runningInputMultiplier - 1f));
            Vector3 desired = emoting ? Vector3.zero : input * target_speed;

            if (motor.grounded) motor.Move(desired, 14f, 18f, dt);
            else motor.Move(desired.sqrMagnitude > .01f ? desired : last_velocity, 4f, 1f, dt);

            if (!emoting && !throwdown_state) motor.StepUp(desired, mask, dt);

            if (!hippieJump)
            {
                // face where the stick points, or keep facing the way the body is still sliding
                Vector3 facing = input.sqrMagnitude > .04f && !emoting ? input : (last_velocity.sqrMagnitude > .25f ? last_velocity : Vector3.zero);
                motor.Face(facing, motor.grounded ? .09f : .25f, dt);
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

            // Holding the emote buttons replays the emote as it ends, which is how emotes loop. Only blend out into the
            // stride once they're released, and only if that happens before the blend out would already have started,
            // otherwise every loop dipped into idle (a late release ends with the regular crossfade instead).
            if (emoting && IsEmote(actual_anim))
            {
                bool held = GetButtonDown("LB") && GetButtonDown(last_dpad);
                if (held) actual_anim.outTo = null;
                else if (actual_anim.outTo == null && actual_anim.remainingTime > actual_anim.blendOutDuration) actual_anim.outTo = walking;
            }
        }

        bool IsEmote(AnimController anim)
        {
            string n = anim.name;
            return (emote1 != null && n == emote1.name) || (emote2 != null && n == emote2.name) || (emote3 != null && n == emote3.name) || (emote4 != null && n == emote4.name);
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
        public bool GetButtonDown(string button)
        {
            return PlayerController.Instance.inputController.player.GetButtonDown(button) || PlayerController.Instance.inputController.player.GetButton(button) || PlayerController.Instance.inputController.player.GetButtonShortPressDown(button) || PlayerController.Instance.inputController.player.GetButtonLongPressDown(button);
        }

        bool GetButtonDown(int button)
        {
            return PlayerController.Instance.inputController.player.GetButtonDown(button) || PlayerController.Instance.inputController.player.GetButton(button) || PlayerController.Instance.inputController.player.GetButtonShortPressDown(button) || PlayerController.Instance.inputController.player.GetButtonLongPressDown(button);
        }

        bool backwards = false;
        float dSpeed = 1f, dSpeedVel = 0f;

        // how much of the run cycle is mixed into the walk cycle, following speed
        float strideWeight = 0f, strideWeightVel = 0f;
        // how much of the stride shows over idle, following speed
        float moveWeight = 0f, moveWeightVel = 0f;
        // how much of the stairs cycle shows over the stride
        float stairsWeight = 0f, stairsWeightVel = 0f;
        void UpdateStrideWeight(float planarSpeed)
        {
            stairsWeight = Mathf.SmoothDamp(stairsWeight, climbingStairs ? 1f : 0f, ref stairsWeightVel, .15f);
            strideWeight = Mathf.SmoothDamp(strideWeight, Mathf.InverseLerp(walk_speed + .1f, 4f, planarSpeed), ref strideWeightVel, .15f);
            moveWeight = Mathf.SmoothDamp(moveWeight, Mathf.InverseLerp(.05f, 1.2f, planarSpeed), ref moveWeightVel, .12f);
        }

        // Idle, walk, run and stairs are one animation mix: walk with run mixed in by speed, the stairs cycles (walk and
        // run mixed the same way) over that while climbing, and idle over everything when standing. The cycles stay in
        // step, so none of those changes switch clips.
        void PlayLocomotion(float planarSpeed)
        {
            Play(walking);
        }

        // Keeps the locomotion mix following speed every frame, and its clocks running while something else plays,
        // so jumps, rolls and emotes blend back into a live stride instead of restarting it.
        void UpdateLocomotion(float planarSpeed)
        {
            UpdateStrideWeight(planarSpeed);

            float walkLength = CycleLength(walking), runLength = CycleLength(running);
            float walkRate = Mathf.Lerp(.8f, 1.15f, Mathf.InverseLerp(limit_idle, running_speed, planarSpeed)) / walkLength;
            float runRate = Mathf.Lerp(.95f, 1.3f, Mathf.InverseLerp(running_speed, max_speed, planarSpeed)) / runLength;
            float strideRate = Mathf.Lerp(walkRate, runRate, strideWeight);

            // each stairs clip is two steps; on stairs play at the pace the steps are actually taken
            float stairsLength = CycleLength(stairs_up), stairsRunLength = CycleLength(stairs_up_running);
            float stairsCycles = 1f / Mathf.Max(2f * stairsStepInterval, .01f);
            float stairsRate = Mathf.Clamp(stairsCycles, Mathf.Lerp(.8f / stairsLength, .8f / stairsRunLength, strideWeight), Mathf.Lerp(1.5f / stairsLength, 1.5f / stairsRunLength, strideWeight));

            dSpeed = Mathf.SmoothDamp(dSpeed, Mathf.Lerp(strideRate, stairsRate, stairsWeight) * walkLength, ref dSpeedVel, .15f);

            walking.speed = dSpeed;
            walking.layer = running;
            walking.layerWeight = strideWeight;
            stairs_up.layer = stairs_up_running;
            stairs_up.layerWeight = strideWeight;
            walking.blendTarget = stairs_up;
            walking.blendWeight = stairsWeight;
            walking.idleLayer = idle;
            walking.idleWeight = 1f - moveWeight;

            if (actual_anim.name != walking.name)
            {
                walking.AdvanceLayerTime(Time.deltaTime);
                if (actual_anim.name != idle.name) idle.AdvanceLayerTime(Time.deltaTime);
            }
        }

        static float CycleLength(AnimController anim)
        {
            return Mathf.Max(anim.timeLimit - anim.timeLimitStart, .01f);
        }
        float jumpBufferedAt = -10f;
        void HandleAnimations()
        {
            float planarSpeed = last_velocity.magnitude;
            // the body faces its movement, so only the forward walk and run cycles are used; the strafe and
            // backward ones flickered in whenever turning made the sideways velocity briefly win
            backwards = false;

            if (actual_state == "falling")
            {
                Play(falling);
            }
            else
            {
                PlayLocomotion(planarSpeed);
            }
        }

        void JumpInput()
        {
            // only from the ground (with a moment of grace after walking off an edge); a press just before landing is kept briefly
            bool pressed = SinglePress(Main.settings.jump_button);
            if (!grounded)
            {
                if (pressed) jumpBufferedAt = Time.time;
                return;
            }
            if (pressed || Time.time - jumpBufferedAt < .15f)
            {
                jumpBufferedAt = -10f;
                normalJump();
            }
            else if (PlayerController.Instance.inputController.player.GetButtonDoublePressHold(Main.settings.jump_button) || PlayerController.Instance.inputController.player.GetButtonDoublePressDown(Main.settings.jump_button)) doubleJump();
        }

        void normalJump()
        {
            jumping = true;
            jump_force = true;
            fs.rb.AddRelativeForce(-move * (speed / 3f));
            CallBack call = OnJumpEnd;
            Play(actual_state == "idle" ? jump : running_jump, call);
        }

        void doubleJump()
        {
            jumping = true;
            jump_force = true;
            fs.rb.AddRelativeForce(-move * (speed / 2f));
            CallBack call = OnJumpEnd;
            // standing still does a back flip, moving does a front flip
            Play(last_velocity.magnitude < .5f && !hippieJump ? back_flip : front_flip, call);
        }

        bool hippieForceAdded = false, force_added = false;
        bool jump_force = false;
        float fm = 115f;
        void JumpingOffset()
        {
            if (!jump_force) return;


            // >= rather than ==, a key index can be skipped at low frame rates and the jump would get no force
            if (actual_anim.name == front_flip.name && front_flip.frame >= 16)
            {
                if (!hippieJump)
                {
                    fs.rb.AddForce(0, Main.settings.flip_jump_force * fm, 0, ForceMode.Impulse);
                    jump_force = false;
                }
                else
                {
                    if (!hippieForceAdded)
                    {
                        fs.rb.AddForce(0, Main.settings.hippie_jump_force * fm * 1.5f, 0, ForceMode.Impulse);
                        hippieForceAdded = true;
                        jump_force = false;
                    }
                }
            }

            if (actual_anim.name == back_flip.name && back_flip.frame >= 20)
            {
                fs.rb.AddForce(0, Main.settings.flip_jump_force * 1.1f * fm, 0, ForceMode.Impulse);
                jump_force = false;
            }

            if (actual_anim.name == running_jump.name || actual_anim.name == running_jump.name)
            {
                if (running_jump.frame >= 3)
                {
                    fs.rb.AddForce(0, Main.settings.running_jump_force * fm, 0, ForceMode.Impulse);
                    jump_force = false;
                }
            }

            if (actual_anim.name == jump.name || actual_anim.name == jump.name)
            {
                if (!hippieJump)
                {
                    if (jump.frame >= 22)
                    {
                        fs.rb.AddForce(0, Main.settings.idle_jump_force * fm, 0, ForceMode.Impulse);
                        jump_force = false;
                    }
                }
                else
                {
                    if (!hippieForceAdded)
                    {
                        fs.rb.AddForce(0, Main.settings.hippie_jump_force * fm, 0, ForceMode.Impulse);
                        hippieForceAdded = true;
                        jump_force = false;
                    }
                }
            }
        }

        GameObject deck_target;
        public bool magnetized = true;
        Rigidbody skate_rb;
        bool set_bail = false;
        int throwdown_detach = 6;
        Vector3 skate_vel_rot = Vector3.zero, skate_vel = Vector3.zero;
        Vector3 last_skate_position = Vector3.zero;
        Quaternion last_skate_rotation = Quaternion.identity;
        Vector3 fake_velocity = Vector3.zero, fake_angularVelocity = Vector3.zero;
        bool last_magnetized = false;
        // seconds the release velocity has been applied, it was 3 frames at 60 fps
        float fake_force_time = 0f;

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
                    if (Time.deltaTime > 0f)
                    {
                        fake_velocity = (fakeSkate.transform.position - last_skate_position) / Time.deltaTime;
                        Quaternion deltaRotation = fakeSkate.transform.rotation * Quaternion.Inverse(last_skate_rotation);
                        fake_angularVelocity = deltaRotation.eulerAngles / Time.deltaTime * Mathf.Deg2Rad;
                        fake_angularVelocity.z /= 10f;
                        fake_angularVelocity.y /= 7f;
                        fake_angularVelocity.x /= 2f;
                    }
                    fake_force_time = 0f;
                }
                else
                {
                    if (fake_force_time < 3f / 60f)
                    {
                        skate_rb.velocity = fake_velocity * 2.1f;
                        skate_rb.angularVelocity = fake_angularVelocity;
                        fake_force_time += Time.deltaTime;
                    }
                }

                if (magnetized && !actual_anim.skate_animation)
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
                                deck_target.transform.Rotate(90f, 0, 0, Space.Self);
                                deck_target.transform.Rotate(20f, -10f, -5, Space.Self);
                                deck_target.transform.Translate(-.225f, .035f, .1f, Space.Self);
                                if (Main.settings.left_arm)
                                {
                                    deck_target.transform.Rotate(0, 0, 180f, Space.Self);
                                    deck_target.transform.Rotate(45f, 0, 0f, Space.Self);
                                    deck_target.transform.Rotate(0, 0, 10, Space.Self);
                                    deck_target.transform.Translate(0, .015f, 0, Space.Self);
                                }

                                deck_target.transform.Translate(0, 0, -.165f, Space.Self);
                                deck_target.transform.Translate(0, .015f, 0, Space.Self);

                                deck_target.transform.Rotate(-7.5f, 0, 0, Space.Self);
                                deck_target.transform.Translate(0, -.015f, 0, Space.Self);
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
                                if (side == "r") deck_target.transform.Translate(0, -.15f, .41f, Space.Self);
                                else deck_target.transform.Translate(0, -.15f, -.41f, Space.Self);
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
                            if (actual_anim.frame >= 16) multiplier = 6.66f;
                            else
                            {
                                if (actual_anim.frame >= throwdown_detach) multiplier = 4.5f;
                                else multiplier = .4f;
                            }
                        }
                        else if (Main.settings.mallgrab) multiplier = .1f;

                        fakeSkate.transform.rotation = Utils.SmoothDampQuaternion(fakeSkate.transform.rotation, deck_target.transform.rotation, ref skate_vel_rot, .085f * (multiplier * (throwdown_anim ? .15f : .85f)));
                        fakeSkate.transform.position = Vector3.SmoothDamp(fakeSkate.transform.position, deck_target.transform.position, ref skate_vel, .01f * (multiplier * (throwdown_anim ? 1.25f : .85f)));

                        /*fakeSkate.GetComponent<Rigidbody>().MovePosition(fakeSkate.transform.position);
                        fakeSkate.GetComponent<Rigidbody>().MoveRotation(fakeSkate.transform.rotation);*/
                    }
                    catch
                    {
                        Utils.Log((fs.getPart("Skater_hand_l") == null) + " " + (fs.getPart("Skater_ForeArm_r") == null));
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

            last_skate_position = fakeSkate.transform.position;
            last_skate_rotation = fakeSkate.transform.rotation;
            last_magnetized = magnetized;
        }

        PhysicMaterial HighFriction, MediumFriction;
        Collider[] fakeSkateColliders;
        PlayerController.FrictionType? lastFrictionType = null;
        bool lastFrictionHippie = false;
        public void SetBoardPhysicsMaterial(PlayerController.FrictionType _frictionType)
        {
            // called every frame, only touch the colliders when the material actually changes
            if (fakeSkateColliders == null) fakeSkateColliders = fakeSkate.GetComponentsInChildren<Collider>();
            if (lastFrictionType == _frictionType && lastFrictionHippie == hippieJump) return;
            lastFrictionType = _frictionType;
            lastFrictionHippie = hippieJump;

            foreach (Collider collider in fakeSkateColliders)
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

                    if (PlayerController.Instance.inputController.player.GetButton("RT"))
                    {
                        run = true;
                        respawnSwitch = false;
                    }

                    if (PlayerController.Instance.inputController.player.GetButton("LT"))
                    {
                        run = true;
                        respawnSwitch = true;
                    }

                    if (run)
                    {
                        throwdown_state = true;
                        CallBack call = OnThrowdownEnd;
                        actual_state = "throwdown";
                        magnetized = true;
                        throwed = true;
                        Play(respawnSwitch ? (SettingsManager.Instance.stance == Stance.Goofy ? throwdown_lhlf : throwdown_lhrf) : (SettingsManager.Instance.stance == Stance.Goofy ? throwdown_lhrf : throwdown_lhlf), call);
                        PlayerController.Instance.ikController.enabled = true;
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
            if (GetButtonDown("A") && GetButtonDown("X"))
            {
                press_count += Utils.FrameScale();
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
        bool spawning = false;
        Vector3 lastBoardVelocity;
        void EnterWalkMode(bool bailmode, bool _magnetized = true)
        {
            if (respawning || busy) return;

            spawning = true;

            golf = false;

            if (EventManager.Instance.IsInCombo) EventManager.Instance.EndTrickCombo(false, true);

            lastBoardVelocity = PlayerController.Instance.boardController.boardRigidbody.velocity * 4f;
            actual_anim = new AnimController();
            busy = true;

            if (PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Bailed || lastBoardVelocity.magnitude / 4f >= Main.settings.step_off_limit) _magnetized = false;
            magnetized = _magnetized;

            camera_offset = PlayerController.Instance.skaterController.skaterTransform.position - PlayerController.Instance.cameraController._actualCam.position;
            DisableGameplay();

            DestroyFS();
            motor = null;
            createFS();

            //PlayerController.Instance.comController.enabled = false;
            //PlayerController.Instance.ikController.enabled = false;            

            enterFromBail = bailmode;

            xUp = false;
            enterBailTimestamp = Time.fixedUnscaledTime;
            // a trigger still held from skating (bailing mid-turn) has to be released before it counts as a throwdown
            rt_onspawn = PlayerController.Instance.inputController.player.GetButton("RT");
            lt_onspawn = PlayerController.Instance.inputController.player.GetButton("LT");

            velocityOnEnter = PlayerController.Instance.skaterController.skaterRigidbody.velocity;

            Vector3 raycastOrigin = PlayerController.Instance.skaterController.skaterTransform.position + new Vector3(0, 2f, 0);
            Vector3 old_pos = PlayerController.Instance.skaterController.skaterTransform.position;
            if (!bailmode && !hippieJump)
            {
                old_pos -= new Vector3(0, .2f, 0);
            }
            else
            {
                fakeSkate.GetComponent<Rigidbody>().useGravity = true;
                fakeSkate.GetComponent<Rigidbody>().isKinematic = false;
                fakeSkate.GetComponent<Rigidbody>().velocity = last_real_velocity;
                fakeSkate.GetComponent<Rigidbody>().angularVelocity = PlayerController.Instance.boardController.boardRigidbody.angularVelocity;

                fakeSkate.GetComponent<Rigidbody>().AddForce(last_real_velocity * 2f, ForceMode.VelocityChange);
            }

            last_velocity = new Vector3(velocityOnEnter.x, 0, velocityOnEnter.z);

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

            /*fs.self.transform.rotation = Quaternion.Slerp(PlayerController.Instance.skaterController.skaterTransform.rotation, spawnRotation, .5f);
            if (hippieJump) fs.self.transform.rotation = PlayerController.Instance.skaterController.transform.rotation;*/

            fs.self.transform.rotation = PlayerController.Instance.skaterController.transform.rotation;
            Physics.SyncTransforms();

            if (PlayerController.Instance.IsSwitch) fs.self.transform.Rotate(0, 180, 0, Space.Self);

            // start the camera behind the character and give movement a heading before the first step
            cam_yaw = fs.self.transform.eulerAngles.y;
            yCamVelocity = 0f;
            velRb = Vector3.zero;
            pendingLandingSpeed = 0f;
            launchTimer = 0f;

            // continue the replay rotations from the last skating frame
            last_replay_state = ReplayRecorder.Instance.transformReference.lastState;
            next_replay_frame = 0f;
            replay_time_accum = 0f;

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

                // Online the player object stays active so the ragdoll can be synced from the walker's pose. A real bail killed
                // the puppet and switched the ragdoll to the colliding layer, so it fought that pose; bring it back the way a
                // step off leaves it.
                if (bailmode)
                {
                    PlayerController.Instance.RagdollLayerChange(false);
                    PlayerController.Instance.respawn.puppetMaster.state = PuppetMaster.State.Alive;
                }
            }

            last_y = fs.self.transform.position.y;

            actual_state = "idle";
            Play(idle);

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

        void PlayEmote(AnimController target)
        {
            CallBack call = OnEmoteEnd;
            Play(target, call);
            // started with the buttons held, so it may loop; emoteInput turns the blend out on once they're released
            if (actual_anim == target) target.outTo = null;
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
        Vector3 rotation_velocity = Vector3.zero;
        Vector3 normal_velocity = Vector3.zero;
        Vector3 averageVelocity = Vector3.zero;

        void RaycastFloor()
        {
            if (actual_anim.offsetPelvis && !actual_anim.anchorRoot && grounded)
            {
                actual_anim.offset = new Vector3(0, -1.1f, 0);
            }

            // stepping onto the dropped board puts the skater back on it
            if (magnetized || throwdown_state || emoting || !fakeSkate) return;
            if (!((Time.unscaledTime - enterBailTimestamp >= 1f) || (hippieJump && Time.unscaledTime - enterBailTimestamp >= .3f))) return;
            if (!(grounded || hippieJump)) return;

            float roll = fakeSkate.transform.rotation.eulerAngles.z;
            if (!(roll >= 195f || roll <= 75f)) return;

            const int rays = 7;
            int boardHits = 0;
            Vector3 feet = fs.rb.position + Vector3.down * (fs.collider.height * .5f - .15f);
            for (int i = 0; i < rays; i++)
            {
                Vector3 origin = feet + Quaternion.Euler(0, 360f / rays * i, 0) * Vector3.forward * (fs.collider.radius * .5f);
                RaycastHit hit;
                if (Physics.Raycast(origin, Vector3.down, out hit, .35f, 1 << LayerUtility.Skateboard, QueryTriggerInteraction.Ignore)) boardHits++;
            }

            if (boardHits > rays / 2)
            {
                Vector3 boardVelocity = skate_rb ? new Vector3(skate_rb.velocity.x, 0, skate_rb.velocity.z) : Vector3.zero;
                throwdown_state = true;
                RestoreGameplay(false, true, false, boardVelocity);
                PlayerController.Instance.inputController.enabled = false;
                inputDelayTimer = .15f;
            }
        }

        bool left_grounded = false, right_grounded = false, last_l_grounded = false, last_r_grounded = false;
        RaycastHit hit_l, hit_r;
        Ray ray_l, ray_r;
        void RaycastFeet()
        {
            Transform left_origin = fs.getPart("Skater_Toe1_l");
            Transform right_origin = fs.getPart("Skater_Toe1_r");

            ray_l = new Ray(left_origin.position, -left_origin.transform.up);
            ray_r = new Ray(right_origin.position, -right_origin.transform.up);

            int mask = ~(1 << LayerUtility.Character | 1 << LayerUtility.Ragdoll | 1 << LayerUtility.RagdollNoInternalCollision);

            // a foot counts as down within 8 cm and lifted only past 14 cm, so a foot hovering at the edge doesn't flicker
            left_grounded = Physics.Raycast(ray_l, out hit_l, last_l_grounded ? .14f : .08f, mask);
            right_grounded = Physics.Raycast(ray_r, out hit_r, last_r_grounded ? .14f : .08f, mask);

            // one step sound per foot plant, with a short cooldown and only while the body is on the ground and moving;
            // both feet planting together (landing, shuffling in place) only makes one sound
            bool canStep = grounded && Time.time - last_any_step >= .12f;
            if (!last_l_grounded && left_grounded && canStep && Time.time - last_left_step >= .22f)
            {
                PlayStep(audioSource_left);
                last_left_step = last_any_step = Time.time;
                canStep = false;
            }
            if (!last_r_grounded && right_grounded && canStep && Time.time - last_right_step >= .22f)
            {
                PlayStep(audioSource_right);
                last_right_step = last_any_step = Time.time;
            }

            last_l_grounded = left_grounded;
            last_r_grounded = right_grounded;
        }

        float last_left_step = -10f, last_right_step = -10f, last_any_step = -10f;

        // footsteps vary a little in pitch and loudness and are softer when walking than running, the same clip at the same
        // volume on every step was grating. Played as one shots so a quick step isn't dropped while the last one still rings.
        void PlayStep(AudioSource source)
        {
            if (source == null || sounds == null || sounds.Length == 0) return;
            float speed = last_velocity.magnitude;
            if (speed < limit_idle) return;

            int num = UnityEngine.Random.Range(0, sounds.Length);
            if (num == last_selected && sounds.Length > 1) num = (num + UnityEngine.Random.Range(1, sounds.Length)) % sounds.Length;
            last_selected = num;

            float pace = Mathf.InverseLerp(walk_speed * .5f, max_speed, speed);
            source.pitch = UnityEngine.Random.Range(.92f, 1.04f);
            source.PlayOneShot(sounds[num], Main.settings.volume * Mathf.Lerp(.5f, 1f, pace) * UnityEngine.Random.Range(.8f, 1f));
        }

        static void SetupStepSource(AudioSource source)
        {
            // mostly positional at the feet, and the harsh top end taken off
            source.playOnAwake = false;
            source.spatialBlend = .6f;
            source.dopplerLevel = 0f;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            AudioLowPassFilter filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 5000f;
        }

        bool climbingStairs = false;
        float seenStepUpTime = -10f, stairsHoldUntil = -10f, stairsStepInterval = .5f, stairsAnimSpeed = 1f, stairsAnimSpeedVel;
        // one step of the stairs cycles (each clip is a left and a right step)
        const float StairsStepDuration = .59f, StairsRunStepDuration = .29f;

        // stairs animation only for a flight of steps (a single curb keeps the walk cycle), held between steps
        void UpdateStairs()
        {
            if (motor != null && motor.lastStepUpTime != seenStepUpTime)
            {
                float interval = motor.lastStepUpTime - seenStepUpTime;
                seenStepUpTime = motor.lastStepUpTime;
                if (interval < .8f)
                {
                    if (!climbingStairs) stairsStepInterval = interval;
                    else stairsStepInterval = Mathf.Lerp(stairsStepInterval, interval, .5f);
                    stairsHoldUntil = Time.time + Mathf.Clamp(stairsStepInterval * 1.6f, .35f, .8f);
                }
            }

            bool busy = jumping || hippieJump || throwdown_state || emoting || actual_state == "impact" || actual_state == "stumble";
            climbingStairs = Time.time < stairsHoldUntil && grounded && !busy && last_velocity.magnitude > limit_idle;
            if (!climbingStairs && busy) stairsHoldUntil = -10f;
        }

        // in 60 fps frames
        float infinity_cast = 0;
        void RaycastInfinity()
        {
            if (!Physics.Raycast(fs.self.transform.position + new Vector3(0, fs.collider.height / 2f, 0), Vector3.down, float.PositiveInfinity, LayerUtility.GroundMask))
            {
                infinity_cast += Utils.FrameScale();
            }
            else infinity_cast = 0;

            if (infinity_cast >= 32) DoRespawn(true);
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

        float last_y = 0f;
        Vector3 camera_velocity = Vector3.zero, camera_velocity_rotation = Vector3.zero;
        float y_velocity = 0;
        public void UpdateCamera(bool pos, bool rot)
        {
            if (inState)
            {
                // follow the interpolated body rather than the pelvis bone, which bobs with every step
                Vector3 target = golf ? ball.transform.position : fs.self.transform.position + Vector3.up * (.3f + visualOffsetY);
                Quaternion rotation = Quaternion.Euler(cam_rotation.eulerAngles.x, cam_yaw, 0) * Main.settings.camera_rotation_offset;
                Vector3 rotatedTranslation = rotation * Main.settings.camera_offset;
                Vector3 output = new Vector3(target.x, last_y, target.z) + rotatedTranslation;
                last_y = Mathf.SmoothDamp(last_y, target.y, ref y_velocity, (jumping ? Main.settings.camera_pos_vel / 65f : Main.settings.camera_pos_vel / 200f));

                if (pos) fallbackCamera.transform.position = Vector3.SmoothDamp(fallbackCamera.transform.position, output, ref camera_velocity, Main.settings.camera_pos_vel / 200f);
                if (rot) fallbackCamera.transform.rotation = Utils.SmoothDampQuaternion(fallbackCamera.transform.rotation, rotation, ref camera_velocity_rotation, Main.settings.camera_rot_vel / 200f);
                if (golf)
                {
                    Vector3 vel = ball.GetComponent<Rigidbody>().velocity;
                    if (vel.magnitude >= 1f)
                    {
                        Quaternion look = Quaternion.LookRotation(vel);
                        fallbackCamera.transform.rotation = Quaternion.Slerp(fallbackCamera.transform.rotation, look, Time.deltaTime * Main.settings.camera_rot_vel);
                    }
                }

                if (inState)
                {
                    if (MultiplayerManager.Instance.InRoom)
                    {
                        PlayerController.Instance.cameraController._actualCam.position = fallbackCamera.transform.position;
                    }
                    else
                    {
                        PlayerController.Instance.cameraController._actualCam.position = Utils.TranslateWithRotation(fs.self.transform.position, camera_offset, fallbackCamera.transform.rotation);
                    }
                    PlayerController.Instance.cameraController._actualCam.rotation = fallbackCamera.transform.rotation;
                }

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
                        fs.rb.angularVelocity = Vector3.zero;
                        fs.rb.velocity = velocityOnEnter;
                        fs.rb.maxDepenetrationVelocity = 2f;
                        fs.rb.mass = 80f;
                        fs.rb.solverIterations = 6;
                        fs.rb.solverVelocityIterations = 6;
                        motor = new CharacterMotor(fs.rb, fs.collider, fs.self.transform.eulerAngles.y);
                        motorIgnoresBoard = false;
                        UpdateBoardCollision();
                        IgnorePlayerCollisions();
                    }
                    catch
                    {
                        Utils.Log("Error creating RB " + (fs.rb == null));
                    }
                }

                if (!fs.visible && fs.self)
                {
                    fs.show();
                    fakeSkate.SetActive(true);
                }

                // once per rendered frame after animation, so the camera doesn't step at the physics rate
                if (fs.self && fs.rb && fakeSkate) UpdateCamera(true, true);
            }
        }

        // in 60 fps frames
        float respawn_delay = 0;
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

        bool IsGrinding()
        {
            return PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Grinding || PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.EnterCoping || PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.ExitCoping;
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
        void OnThrowdownEnd()
        {
            Vector3 forward = Vector3.ProjectOnPlane(fs.rb.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;

            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = GroundBelowWalker(),
                IsBoardBackwards = false,
                rotation = Quaternion.LookRotation(forward),
                isSwitch = respawnSwitch
            };

            // board speed from the walking speed; throwdown_force was tuned against per-tick distance at 120 Hz (8 * 25 / 120 ≈ 1.67 per m/s)
            float boardSpeed = 2f + last_velocity.magnitude * Main.settings.throwdown_force / 15f;
            // keep going the way the walker was moving, regular or switch; RespawnOnBoard lines it up with the board
            Vector3 travel = last_velocity.sqrMagnitude > .25f ? last_velocity.normalized : forward.normalized;

            throwdown_state = false;
            UpdateGameplay();

            RespawnOnBoard(respawnInfo, travel * boardSpeed, true);

            PlayerController.Instance.inputController.enabled = false;
            inputDelayTimer = .15f;

            // keep the view where the walking camera was instead of snapping behind the skater
            PlayerController.Instance.cameraController._camRigidbody.MovePosition(fallbackCamera.transform.position);
            PlayerController.Instance.cameraController._camRigidbody.MoveRotation(fallbackCamera.transform.rotation);

            EventManager.Instance.OnCatched(true, true);

            Traverse.Create(PlayerController.Instance).Field("_isSwitch").SetValue(respawnSwitch);
            if (!respawnSwitch) PlayerController.Instance.skaterController.ResetSwitchAnims();
            else
            {
                Traverse.Create(PlayerController.Instance.skaterController).Field("_animSwitch").SetValue(1f);
                Traverse.Create(PlayerController.Instance.skaterController).Field("_actualSwitch").SetValue(1f);
            }
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
            if (inState)
            {
                try
                {
                    PinMovementController.SetStartTransform(fs.self.transform.position, fallbackCamera.transform.rotation);

                    PlayerController.Instance.boardController.boardTransform.position = fakeSkate.transform.position;
                    PlayerController.Instance.boardController.boardTransform.rotation = fakeSkate.transform.rotation;
                    PlayerController.Instance.ikController.physicsBoard.transform.position = PlayerController.Instance.boardController.boardTransform.position;
                    /*PlayerController.Instance.ikController._finalIk.solver.leftFootEffector.position = fs.getPart("Skater_foot_l").position;
                    PlayerController.Instance.ikController._finalIk.solver.rightFootEffector.position = fs.getPart("Skater_foot_r").position;

                    Transform leftFoot = (Transform)(Traverse.Create(PlayerController.Instance.ikController).Field("skaterLeftFoot").GetValue());
                    leftFoot.position = fs.getPart("Skater_foot_l").position;
                    leftFoot.rotation = fs.getPart("Skater_foot_l").rotation;

                    Transform rightFoot = (Transform)(Traverse.Create(PlayerController.Instance.ikController).Field("skaterRightFoot").GetValue());
                    rightFoot.position = fs.getPart("Skater_foot_r").position;
                    rightFoot.rotation = fs.getPart("Skater_foot_r").rotation;*/

                    PlayerController.Instance.ikController.ForceUpdateIK();

                    PlayerController.Instance.skaterController.skaterRigidbody.velocity = PlayerController.Instance.boardController.boardRigidbody.velocity;

                    MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.position = MonoBehaviourSingleton<PlayerController>.Instance.skaterController.skaterTransform.position;
                    MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.transform.position = MonoBehaviourSingleton<PlayerController>.Instance.skaterController.skaterTransform.position;
                    MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.velocity = fs.rb.velocity;
                    MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.angularVelocity = fs.rb.angularVelocity;
                    PlayerController.Instance.skaterController.skaterTransform.up = fs.self.transform.up;
                    PlayerController.Instance.skaterController.skaterHips.position = fs.getPart("Skater_pelvis").position;
                    PlayerController.Instance.skaterController.skaterHips.rotation = fs.getPart("Skater_pelvis").rotation;

                    MonoBehaviourSingleton<PlayerController>.Instance.skaterController.leanProxy.position = MonoBehaviourSingleton<PlayerController>.Instance.skaterController.skaterTransform.position;
                    MonoBehaviourSingleton<PlayerController>.Instance.skaterController.leanProxy.transform.position = MonoBehaviourSingleton<PlayerController>.Instance.skaterController.skaterTransform.position;
                    MonoBehaviourSingleton<PlayerController>.Instance.skaterController.leanProxy.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;
                    MonoBehaviourSingleton<PlayerController>.Instance.skaterController.leanProxy.transform.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;
                    MonoBehaviourSingleton<PlayerController>.Instance.skaterController.leanProxy.velocity = MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.velocity;
                    MonoBehaviourSingleton<PlayerController>.Instance.skaterController.leanProxy.angularVelocity = MonoBehaviourSingleton<PlayerController>.Instance.comController.COMRigidbody.angularVelocity;

                    PlayerController.Instance.skaterController.skaterTargetTransform.position = PlayerController.Instance.boardController.boardRigidbody.position;
                }
                catch { }
            }

            if (fs.self != null)
            {
                PlayerController.Instance.skaterController.skaterTransform.position = Utils.TranslateWithRotation(fs.self.transform.position, new Vector3(0, -fs.collider.height / 2f, 0), fs.self.transform.rotation);
                PlayerController.Instance.skaterController.skaterTransform.rotation = fs.self.transform.rotation;
            }

            if (!MultiplayerManager.Instance.InRoom) PlayerController.Instance.respawn.puppetMaster.Teleport(PlayerController.Instance.skaterController.skaterTransform.position + PlayerController.Instance.skaterController.skaterTransform.rotation * PlayerController.Instance.respawn.GetOffsetPositions(respawnSwitch)[1], PlayerController.Instance.skaterController.skaterTransform.rotation, false);
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

            // the locomotion mix never restarts, its clock kept running; if the animation before it already blended all
            // the way into it there's nothing left to crossfade
            if (target.name == walking.name)
            {
                bool handedOver = actual_anim.outTo != null && actual_anim.outTo.name == walking.name && actual_anim.blendOutWeight > .98f;
                actual_anim.Stop();
                actual_anim = target;
                target.Resume(!handedOver);
                return;
            }

            target.outTo = ReturnsToLocomotion(target) ? walking : null;
            actual_anim.Stop();
            actual_anim = target;
            target.Play();
        }

        // one-shot animations that end back in the stride blend into it before they finish
        bool ReturnsToLocomotion(AnimController anim)
        {
            return !anim.isLoop && !hippieJump && anim.name != throwdown_lhlf.name && anim.name != throwdown_lhrf.name;
        }

        bool IsStrideCycle(AnimController anim)
        {
            string n = anim.name;
            return n == walking.name || n == running.name || n == stairs_up.name || n == stairs_up_running.name;
        }

        void Play(AnimController target, CallBack call)
        {
            if (actual_anim.name == target.name && target.isPlaying) return;
            if ((throwdown_state && (actual_anim.name == throwdown_lhlf.name || actual_anim.name == throwdown_lhrf.name)) || emoting) return;
            //Log(target.name + " callback");

            target.outTo = ReturnsToLocomotion(target) ? walking : null;
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
                object[] array = photonEvent.CustomData as object[];
                if (array == null || array.Length == 0 || !(array[0] is string)) return;

                // the sender may not have spawned yet, or use a sound this install doesn't have
                AudioClip remote_clip;
                if (!audioCache.TryGetValue((string)array[0], out remote_clip) || remote_clip == null) return;

                Player player = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
                if (player == null) return;
                NetworkPlayerController playerController = MonoBehaviourPunCallbacksSingleton<MultiplayerManager>.Instance.GetPlayerController(player.ActorNumber);
                if (playerController == null || playerController.GetBody() == null) return;

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

                if (distance <= 20f)
                {
                    List<IPlayerNameGraphic> names = playerController.GetComponentsInChildren<IPlayerNameGraphic>(true).ToList<IPlayerNameGraphic>();
                    names.ForEach(delegate (IPlayerNameGraphic t)
                    {
                        t.SetName(playerController.NickName + " ♫");
                    });
                    float distance_volume = map01(distance, 20, 2);
                    distance_volume = distance_volume < 0 ? 0 : distance_volume > 1 ? 1 : distance_volume;
                    player_audio_source.clip = remote_clip;
                    player_audio_source.volume = Main.settings.emote_volume * distance_volume;
                    player_audio_source.loop = false;
                    player_audio_source.Play();

                    if (!multi_sound_check.ContainsKey(player_audio_source)) multi_sound_check.Add(player_audio_source, playerController);
                }
            }
        }

        IDictionary<AudioSource, NetworkPlayerController> multi_sound_check = new Dictionary<AudioSource, NetworkPlayerController>();

        List<AudioSource> finished_sound_sources = new List<AudioSource>();
        void CheckAudioSources()
        {
            finished_sound_sources.Clear();
            foreach (var item in multi_sound_check)
            {
                // the player or its source can be gone after they leave
                if (item.Key == null || item.Value == null || !item.Key.isPlaying) finished_sound_sources.Add(item.Key);
            }

            // remove after the loop, removing while enumerating threw every time a remote emote ended
            foreach (AudioSource source in finished_sound_sources)
            {
                NetworkPlayerController playerController = multi_sound_check[source];
                if (playerController != null)
                {
                    List<IPlayerNameGraphic> names = playerController.GetComponentsInChildren<IPlayerNameGraphic>(true).ToList<IPlayerNameGraphic>();
                    names.ForEach(delegate (IPlayerNameGraphic t)
                    {
                        t.SetName(playerController.NickName);
                    });
                }

                multi_sound_check.Remove(source);
            }
        }

        int last_selected = -1;
        void PlayRandomOneShotFromArray(AudioClip[] array, AudioSource source, float vol)
        {
            if (!source.isPlaying)
            {
                if (array == null || array.Length == 0)
                {
                    return;
                }
                int num = UnityEngine.Random.Range(0, array.Length);
                if (num == last_selected && array.Length > 1)
                {
                    while (num == last_selected) num = UnityEngine.Random.Range(0, array.Length);
                }
                source.clip = array[num];
                source.volume = vol;
                source.Play();
                last_selected = num;
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

        void GrindPart()
        {
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
        }

        void EnterRiding()
        {
            MonoBehaviourSingleton<PlayerController>.Instance.currentStateEnum = PlayerController.CurrentState.Riding;
        }

        CinemachineCollider cinemachine_collider;
        bool? cameraColliderWasEnabled = null;
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

            return;

            Vector3[] skateVelocities = (Vector3[])Traverse.Create(PlayerController.Instance.boardController).Field("boardVelocites").GetValue();

            for (int j = 0; j < skateVelocities.Length; j++)
            {
                skateVelocities[j] = Vector3.zero;
            }

            Traverse.Create(PlayerController.Instance.boardController).Field("boardVelocites").SetValue(skateVelocities);
        }

        bool golf = false;
        GameObject ball;
        public void Golf()
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.localScale = new Vector3(.05f, .05f, .05f);
            ball.AddComponent<Rigidbody>();
            ball.AddComponent<GolfController>();
            ball.transform.position = fs.self.transform.position + new Vector3(0, 0, 1f);
            golf = true;
        }
    }
}