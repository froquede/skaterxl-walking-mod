using Cinemachine;
using GameManagement;
using HarmonyLib;
using ModIO.UI;
using Photon.Pun;
using ReplayEditor;
using SkaterXL.Core;
using SkaterXL.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace walking_mod
{
    public class WalkingController : MonoBehaviour
    {
        FakeSkater fs = new FakeSkater();
        AnimController walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown;
        public AnimController emote1, emote2, emote3, emote4;
        AnimController[] animations;
        AnimController actual_anim;
        public bool inState = false;
        GameObject fallbackCamera;
        GameObject fakeSkate;
        Transform[] fakeTrucks = new Transform[2];
        CinemachineVirtualCamera main_cam;
        PlayTime playtimeobj;

        void Start()
        {
            try
            {
                playtimeobj = GameObject.Find("PlayTime").GetComponent<PlayTime>();
            }
            catch
            {
                Log("Error playtime");
            }

            InitAnimations();
            animations = new AnimController[20] { walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown, emote1, emote2, emote3, emote4 };

            fallbackCamera = PlayerController.Instance.skaterController.transform.parent.parent.Find("Fallback Camera").gameObject;
            main_cam = MonoBehaviourSingleton<PlayerController>.Instance.cameraController._actualCam.GetComponent<CinemachineVirtualCamera>();

            actual_anim = new AnimController();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void InitAnimations()
        {
            walking = new AnimController(Path.Combine(Main.modEntry.Path, "walking.json"), fs);
            walking_backwards = new AnimController(Path.Combine(Main.modEntry.Path, "walking_backwards.json"), fs);
            walking_left = new AnimController(Path.Combine(Main.modEntry.Path, "walking_left.json"), fs);
            walking_right = new AnimController(Path.Combine(Main.modEntry.Path, "walking_right.json"), fs);
            running = new AnimController(Path.Combine(Main.modEntry.Path, "running.json"), fs, true, 6);
            running_backwards = new AnimController(Path.Combine(Main.modEntry.Path, "running_backwards.json"), fs);
            running_left = new AnimController(Path.Combine(Main.modEntry.Path, "running_left.json"), fs);
            running_right = new AnimController(Path.Combine(Main.modEntry.Path, "running_right.json"), fs);
            idle = new AnimController(Path.Combine(Main.modEntry.Path, "idle.json"), fs);
            jump = new AnimController(Path.Combine(Main.modEntry.Path, "jumping.json"), fs, false);
            running_jump = new AnimController(Path.Combine(Main.modEntry.Path, "running_jump.json"), fs, false, 1);
            left_turn = new AnimController(Path.Combine(Main.modEntry.Path, "left_turn.json"), fs);
            right_turn = new AnimController(Path.Combine(Main.modEntry.Path, "right_turn.json"), fs);
            front_flip = new AnimController(Path.Combine(Main.modEntry.Path, "front_flip.json"), fs);
            back_flip = new AnimController(Path.Combine(Main.modEntry.Path, "back_flip.json"), fs);
            throwdown = new AnimController(Path.Combine(Main.modEntry.Path, "throwdown.json"), fs, false);

            emote1 = new AnimController(Path.Combine(Main.modEntry.Path, "flair.json"), fs, false, 1);
            emote2 = new AnimController(Path.Combine(Main.modEntry.Path, "air_kick.json"), fs, false, 1);
            emote3 = new AnimController(Path.Combine(Main.modEntry.Path, "ymca.json"), fs, false, 1);
            emote4 = new AnimController(Path.Combine(Main.modEntry.Path, "dance.json"), fs, false, 1);
        }

        public float speed = 7.0f;
        public float jumpForce = 10.0f;
        public Vector3 velocity;
        bool jumping = false, throwdown_state = false;
        int press_count = 0;
        string actual_state = "";
        float max_speed = 6.75f;
        float running_speed = 3.75f;
        bool emoting = false;
        bool respawnSwitch = false;
        float limit_idle = .15f;
        float decay = .7f;

        Vector3 last_pos = Vector3.zero;

        void FixedUpdate()
        {
            if (inState == true) inStateLogic();
            else inPlayStateLogic();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log("OnSceneLoaded: " + scene.name + " " + mode);
            // if (gameplay_disabled) EnableGameplay();
        }

        void Log(object log)
        {
            UnityModManager.Logger.Log(log.ToString());
        }

        string last_customizations;

        void DestroyFS()
        {
            fs.rb = null;
            if (fs.self != null) Destroy(fs.self);
            fs.self = null;
            if (fakeSkate != null) Destroy(fakeSkate);
            fakeSkate = null;
        }

        void createFS()
        {
            Log("Creating new FS");

            try
            {
                last_customizations = PlayerController.Instance.characterCustomizer.CurrentCustomizations.ToString();

                fs.Create();
                //fs.self.AddComponent<TransformTracker>();
                fakeSkate = Instantiate(PlayerController.Instance.boardController.boardTransform.gameObject);
                fakeSkate.GetComponent<Rigidbody>().isKinematic = true;
                fakeTrucks[0] = fakeSkate.transform.FindChildRecursively("Back Truck");
                fakeTrucks[1] = fakeSkate.transform.FindChildRecursively("Front Truck");
                //fakeSkate.AddComponent<TransformTracker>();

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
            PlayerController.Instance.DisableGameplay();
            GameStateMachine.Instance.PinObject.SetActive(false);
            TogglePlayObject(false);
            gameplay_disabled = true;
            last_nonplaytime = (float)Traverse.Create(playtimeobj).Field("nonPlayTime").GetValue();
        }

        void EnableGameplay()
        {
            Log("Enabling gameplay");
            inState = false;
            PlayerController.Instance.EnableGameplay();
            TogglePlayObject(true);
            gameplay_disabled = false;
            DestroyFS();
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

        float record_delta = 0;
        Vector3 relativeVelocity;
        void inStateLogic()
        {
            if (!fs.self || !fs.rb || !fakeSkate)
            {
                Log(fs.self + " " + fs.rb + " " + fakeSkate);
                return;
            }

            try { actual_anim.FixedUpdate(); } catch { UnityModManager.Logger.Log("Error updating animation " + inState); }

            Traverse.Create(playtimeobj).Field("isInPlayState").SetValue(true);

            UpdateSticks();
            RaycastFloor();
            Movement();
            emoteInput();

            PlayerController.Instance.skaterController.transform.position = fs.self.transform.position;
            PlayerController.Instance.boardController.transform.position = fs.self.transform.position - new Vector3(0, .73f, 0);

            if (fs.rb.velocity.magnitude >= running_speed) actual_state = "running";
            else
            {
                if (fs.rb.velocity.magnitude <= limit_idle) actual_state = "idle";
                else actual_state = "walking";
            }

            relativeVelocity = fs.rb.transform.InverseTransformDirection(last_pos - fs.self.transform.position);
            last_pos = fs.self.transform.position;

            if (!jumping)
            {
                HandleAnimations();
                JumpInput();
            }
            else JumpingOffset();

            Board();

            if(!emoting) ThrowdownInput();

            AddReplayFrame();

            if (PlayerController.Instance.inputController.player.GetButtonDown("Y")) EnableGameplay();
            if (PlayerController.Instance.inputController.player.GetButtonDown("X")) magnetized = !magnetized;

            if (!PlayerController.Instance.inputController.player.GetButtonDown("LB") && !PlayerController.Instance.inputController.player.GetButton("LB"))
            {
                if (PlayerController.Instance.inputController.player.GetButtonDown(68) || PlayerController.Instance.inputController.player.GetButton(68)) SetRespawn();
                if (PlayerController.Instance.inputController.player.GetButtonDown(67) || PlayerController.Instance.inputController.player.GetButton(67)) DoRespawn();
            }
        }

        void DoRespawn()
        {
            last_nr = (RespawnInfo)Traverse.Create(MonoBehaviourSingleton<PlayerController>.Instance.respawn).Field("markerRespawnInfos").GetValue();
            fs.self.transform.position = last_nr.position;
            fs.self.transform.rotation = last_nr.rotation;
        }

        void SetRespawn()
        {
            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, .715f, 0),
                IsBoardBackwards = false,
                rotation = Quaternion.LookRotation(fs.rb.transform.forward),
                isSwitch = false
            };
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.SetSpawnPoint(respawnInfo);
        }

        void AddReplayFrame()
        {
            ReplayPlayerFrameHalf frame = new ReplayPlayerFrameHalf
            {
                time = ReplayRecorder.Instance.LocalPlayerFrames[ReplayRecorder.Instance.LocalPlayerFrames.Count - 1].time + PlayTime.deltaTime,
                serverTime = (MonoBehaviourPunCallbacksSingleton<MultiplayerManager>.Instance.InRoom ? PhotonNetwork.Time : double.MinValue),
                playingClips = new PlayingClipData[0],
                oneShotEvents = new OneShotEventData[0],
                paramChangeEvents = new AudioParamEventData[0],
                controllerState = ReplayRecorder.Instance.RecordControllerState()
            };
            ReplayPlayerFrameHalf replayPlayerFrameHalf = frame;
            PlayerTransformStateHalf transformState = default(PlayerTransformStateHalf);
            transformState.boardPosition = fakeSkate.transform.position;
            transformState.boardRotation = fakeSkate.transform.rotation;
            transformState.skaterRootPosition = fs.self.transform.position;

            for (int i = 0; i < ReplayRecorder.Instance.transformReference.boardTruckTransforms.Length; i++)
            {
                transformState.boardTruckLocalPositions[i] = fakeTrucks[i].localPosition;
                if (ReplayRecorder.Instance.transformReference.lastState != null)
                {
                    int i2 = i;
                    PlayerTransformStateHalf value = ReplayRecorder.Instance.transformReference.lastState.Value;
                    transformState.boardTruckLocalRotations[i2] = this.EnsureQuaternionContinuity(value.boardTruckLocalRotations[i], fakeTrucks[i].localRotation);
                }
                else
                {
                    transformState.boardTruckLocalRotations[i] = fakeTrucks[i].localRotation;
                }
            }

            for (int j = 0; j < ReplayRecorder.Instance.transformReference.skaterMainBones.Length; j++)
            {
                Quaternion rot = fs.getPart(ReplayRecorder.Instance.transformReference.skaterMainBones[j].name).localRotation;
                if (ReplayRecorder.Instance.transformReference.lastState != null)
                {
                    int i3 = j;
                    PlayerTransformStateHalf value = ReplayRecorder.Instance.transformReference.lastState.Value;
                    transformState.skaterBoneLocalRotations[i3] = EnsureQuaternionContinuity(value.skaterBoneLocalRotations[j], rot);
                }
                else
                {
                    transformState.skaterBoneLocalRotations[j] = rot;
                }
            }

            transformState.skaterRootRotation = fs.self.transform.rotation;
            transformState.skaterPelvisLocalPosition = fs.getPart("Skater_pelvis").localPosition;

            transformState.camera.position = fallbackCamera.transform.position;
            transformState.camera.rotation = fallbackCamera.transform.rotation;
            replayPlayerFrameHalf.transformState = transformState;
            Traverse.Create(ReplayRecorder.Instance.transformReference).Field("lastState").SetValue(transformState);

            ReplayRecorder.Instance.LocalPlayerFrames.Add(replayPlayerFrameHalf);
        }

        Vector3 move;
        float LX, LY, RX;

        void UpdateSticks()
        {
            LX = PlayerController.Instance.inputController.player.GetAxis(19);
            LY = PlayerController.Instance.inputController.player.GetAxis(20);
            RX = PlayerController.Instance.inputController.player.GetAxis(21);
        }

        void Movement()
        {
            if (fs.rb)
            {
                move = transform.right * LX + transform.forward * LY;
                Physics.SyncTransforms();
                fs.rb.AddForce(-fs.rb.velocity * decay);
                fs.rb.MoveRotation(Quaternion.Euler(fs.rb.rotation.eulerAngles.x, fs.rb.rotation.eulerAngles.y + (actual_state == "idle" ? RX : actual_state == "walking" ? RX / 1.5F : RX / 2.5F), fs.rb.rotation.eulerAngles.z));

                if (fs.rb.velocity.magnitude <= max_speed)
                {
                    fs.rb.AddRelativeForce(move * speed);
                }
            }
        }

        void emoteInput()
        {
            if (!emoting)
            {
                if (PlayerController.Instance.inputController.player.GetButtonDown("LB") || PlayerController.Instance.inputController.player.GetButton("LB"))
                {
                    if (PlayerController.Instance.inputController.player.GetButtonDown(70) || PlayerController.Instance.inputController.player.GetButton(70)) PlayEmote(emote1);
                    if (PlayerController.Instance.inputController.player.GetButtonDown(68) || PlayerController.Instance.inputController.player.GetButton(68)) PlayEmote(emote2);
                    if (PlayerController.Instance.inputController.player.GetButtonDown(69) || PlayerController.Instance.inputController.player.GetButton(69)) PlayEmote(emote3);
                    if (PlayerController.Instance.inputController.player.GetButtonDown(67) || PlayerController.Instance.inputController.player.GetButton(67)) PlayEmote(emote4);
                }
            }
        }

        bool backwards = false;
        void HandleAnimations()
        {
            float forward_velocity = relativeVelocity.z;
            forward_velocity = forward_velocity < 0 ? -forward_velocity : forward_velocity;
            float side_velocity = relativeVelocity.x;
            side_velocity = side_velocity < 0 ? -side_velocity : side_velocity;
            backwards = relativeVelocity.z > 0;

            actual_anim.offset = new Vector3(0, -.73f, 0);
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

        void JumpInput()
        {
            if (PlayerController.Instance.inputController.player.GetButtonSinglePressHold("B"))
            {
                jumping = true;
                CallBack call = OnJumpEnd;
                fs.rb.AddRelativeForce(-move * (speed / 3f));
                StopAll();
                Play(actual_state == "idle" ? jump : running_jump, call);
            }
            else
            {
                if (PlayerController.Instance.inputController.player.GetButtonDoublePressDown("B"))
                {
                    jumping = true;
                    CallBack call = OnJumpEnd;
                    fs.rb.AddRelativeForce(-move * (speed / 2f));
                    Play(backwards ? back_flip : front_flip, call);
                }
            }
        }

        void JumpingOffset()
        {
            if (actual_anim.name == front_flip.name || actual_anim.name == back_flip.name)
            {
                if (actual_anim.frame == 9) fs.rb.AddRelativeForce(0, 3.25f, 0, ForceMode.Impulse);
                /*if (actual_anim.frame > 9 && actual_anim.frame < 28) actual_anim.offset = new Vector3(0, -.73f - (frame_time / 28), 0);
                else actual_anim.offset = new Vector3(0, Mathf.Lerp(actual_anim.offset.y, -.73f, Time.fixedDeltaTime * 4f), 0);*/
            }

            if (actual_anim.name == running_jump.name || actual_anim.name == running_jump.name)
            {
                if (actual_anim.frame == 2) fs.rb.AddRelativeForce(0, 2f, 0, ForceMode.Impulse);
                /*if (actual_anim.frame > 3 && actual_anim.frame < 7) actual_anim.offset = new Vector3(0, -.73f - (frame_time / 14), 0);
                else actual_anim.offset = new Vector3(0, Mathf.Lerp(actual_anim.offset.y, -.73f, Time.fixedDeltaTime * 4f), 0);*/
            }

            if (actual_anim.name == jump.name || actual_anim.name == jump.name)
            {
                if (actual_anim.frame == 20) fs.rb.AddRelativeForce(0, 1f, 0, ForceMode.Impulse);
            }
        }

        GameObject deck_target;
        bool magnetized = true;
        Rigidbody skate_rb;

        void Board()
        {
            if (fs.self && fakeSkate)
            {
                if (!skate_rb) skate_rb = fakeSkate.GetComponent<Rigidbody>();
                if (deck_target == null) deck_target = new GameObject();

                if (magnetized)
                {
                    try
                    {
                        if (!skate_rb.isKinematic) skate_rb.isKinematic = true;
                        if (skate_rb.useGravity) skate_rb.useGravity = false;

                        deck_target.transform.position = throwdown_state ? fs.getPart("Skater_hand_l").transform.position : fs.getPart("Skater_ForeArm_r").transform.position;
                        deck_target.transform.rotation = throwdown_state ? fs.getPart("Skater_hand_l").transform.rotation : fs.getPart("Skater_ForeArm_r").transform.rotation;
                        deck_target.transform.Rotate(90f, 0, 0, Space.Self);
                        if (throwdown_state)
                        {
                            deck_target.transform.Rotate(90f, 0, 0, Space.Self);
                            deck_target.transform.Rotate(0, 0, -90f, Space.Self);
                            deck_target.transform.Rotate(0, -45f, 0, Space.Self);
                        }
                        else
                        {
                            deck_target.transform.Rotate(10f, 20f, 0, Space.Self);
                        }
                        if (throwdown_state) deck_target.transform.Translate(0, -.1f, .4f, Space.Self);
                        else deck_target.transform.Translate(-.2f, .045f, -.1f, Space.Self);

                        fakeSkate.transform.position = Vector3.Lerp(fakeSkate.transform.position, deck_target.transform.position, Time.smoothDeltaTime * 60f);
                        fakeSkate.transform.rotation = Quaternion.Slerp(fakeSkate.transform.rotation, deck_target.transform.rotation, Time.smoothDeltaTime * 40f);
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
            }
        }

        void inPlayStateLogic()
        {
            if (PlayerController.Instance.inputController.player.GetButton("A") && PlayerController.Instance.inputController.player.GetButton("X"))
            {
                press_count++;
                if (press_count >= 12)
                {
                    DestroyFS();
                    createFS();

                    press_count = 0;
                    inState = true;
                    fs.self.transform.position = PlayerController.Instance.skaterController.skaterTransform.position - new Vector3(0, .2f, 0);
                    fs.self.transform.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;

                    if (PlayerController.Instance.IsSwitch) fs.self.transform.Rotate(0, 180, 0, Space.Self);

                    StopAll();
                    throwdown_state = false;
                    emoting = false;
                    jumping = false;
                    magnetized = true;
                    actual_state = "idle";

                    DisableGameplay();
                }
            }
            else
            {
                press_count = 0;
            }

            fallbackCamera.transform.position = main_cam.transform.position;
            fallbackCamera.transform.rotation = main_cam.transform.rotation;
        }

        void PlayEmote(AnimController target)
        {
            CallBack call = OnEmoteEnd;
            Play(target, call);
            emoting = true;
            actual_state = "emoting";
        }

        public Vector3 raycastOffset = Vector3.zero;
        void RaycastFloor()
        {
            Vector3 raycastOrigin = fs.self.transform.position + raycastOffset + fs.collider.center;
            Ray ray = new Ray(raycastOrigin, -fs.self.transform.up);

            if (Physics.Raycast(ray, out RaycastHit hit, 10f, LayerUtility.GroundMask))
            {
                if (hit.distance <= 1.25)
                {
                    Quaternion rotation = Quaternion.FromToRotation(fs.self.transform.up, hit.normal);
                    fs.collider.transform.rotation = Quaternion.Slerp(fs.collider.transform.rotation, rotation * fs.collider.transform.rotation, Time.smoothDeltaTime * 6f);
                }

                Vector3 off = translateLocal(fs.self.transform, actual_anim.offset);
                if (off.y > hit.point.y)
                {
                    Vector3 newoffset = hit.point - raycastOrigin;
                    actual_anim.offset = new Vector3(0, Mathf.Lerp(actual_anim.offset.y, newoffset.y, Time.smoothDeltaTime * 4f), 0);
                }
                else actual_anim.offset = new Vector3(0, Mathf.Lerp(actual_anim.offset.y, -.7f, Time.smoothDeltaTime * 10f), 0);
            }
        }

        GameObject target;
        public void UpdateCamera(bool pos, bool rot)
        {
            if (inState)
            {
                if (target == null) target = new GameObject();
                target.transform.position = fs.self.transform.position;
                target.transform.rotation = Quaternion.Euler(0, fs.self.transform.rotation.eulerAngles.y, 0);
                target.transform.Translate(0, .12f, -2.00f, Space.Self);

                if (pos) fallbackCamera.transform.position = Vector3.Lerp(fallbackCamera.transform.position, target.transform.position, Time.smoothDeltaTime * 10f);
                if (rot) fallbackCamera.transform.rotation = Quaternion.Slerp(fallbackCamera.transform.rotation, target.transform.rotation, Time.smoothDeltaTime * 4f);

                main_cam.transform.position = fallbackCamera.transform.position;
                main_cam.transform.rotation = fallbackCamera.transform.rotation;
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
            else Throwdown();
        }

        void Throwdown()
        {
            if (updating)
            {
                updating = false;
                check_velocity = true;

                PlayerController.Instance.respawn.SetSpawnPoint(last_nr, Respawn.SpawnPointChangeMethod.Auto);
            }

            if (check_velocity)
            {
                if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude <= (limit_idle * 2) && PlayerController.Instance.currentStateEnum == PlayerController.CurrentState.Riding)
                {
                    PlayerController.Instance.AddPushForce(PlayerController.Instance.GetPushForce() * (.3f + (-relativeVelocity.z * 10f)));
                }
                else check_velocity = false;
            }
        }

        void OnJumpEnd()
        {
            Log("OnJumpEnd");
            jumping = false;
        }

        RespawnInfo last_nr;
        void OnThrowdownEnd()
        {
            Vector3 forward = fs.rb.transform.forward;

            last_nr = (RespawnInfo)Traverse.Create(MonoBehaviourSingleton<PlayerController>.Instance.respawn).Field("markerRespawnInfos").GetValue();

            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, .715f, 0),
                IsBoardBackwards = false,
                rotation = Quaternion.LookRotation(forward),
                isSwitch = respawnSwitch
            };

            throwdown_state = false;
            updating = true;

            PlayerController.Instance.BoardFreezedAfterRespawn = false;
            EnableGameplay();
            PlayerController.Instance.BoardFreezedAfterRespawn = false;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.SetSpawnPoint(respawnInfo);
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.DoRespawn();
        }

        Transform[] original_bones;
        void TogglePlayObject(bool enabled)
        {
            Log("Toggled play object " + enabled);
            GameStateMachine.Instance.PlayObject.SetActive(enabled);
            /*return;
            GameStateMachine.Instance.PlayObject.transform.Find("GameplayUI").gameObject.SetActive(enabled);
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
            GameStateMachine.Instance.PlayObject.transform.Find("NewSkater").gameObject.SetActive(enabled);

            GameStateMachine.Instance.PlayObject.GetComponent<PlayerController>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<InputController>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<SettingsManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<SoundManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<PlayerPrefsManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<EventManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<TrickManager>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<RagdollSounds>().enabled = enabled;
            GameStateMachine.Instance.PlayObject.GetComponent<CharacterCustomizer>().enabled = enabled;*/

            if (enabled)
            {
                if (original_bones != null) GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>().skaterMainBones = original_bones;
            }
            else
            {
                if (original_bones == null)
                {
                    original_bones = new Transform[32];
                    Array.Copy(GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>().skaterMainBones, original_bones, 32);
                }

                if (fs.self)
                {
                    GameStateMachine.Instance.PlayObject.GetComponent<PlayerTransformReference>().skaterMainBones = new Transform[]
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
            UnityModManager.Logger.Log("OnEmoteEnd");
            emoting = false;
        }

        void Play(AnimController target)
        {
            if (actual_anim.name == target.name && target.isPlaying) return;
            if ((throwdown_state && actual_anim.name == throwdown.name) || emoting) return;
            actual_anim.Stop();
            actual_anim = target;
            UnityModManager.Logger.Log(target.name);
            target.Play();
        }

        void Play(AnimController target, CallBack call)
        {
            if (actual_anim.name == target.name && target.isPlaying) return;
            if ((throwdown_state && actual_anim.name == throwdown.name) || emoting) return;
            actual_anim.Stop();
            actual_anim = target;
            UnityModManager.Logger.Log(target.name + " callback");
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

        Vector3 translateLocal(Transform origin, Vector3 offset)
        {
            GameObject copy = new GameObject();
            copy.transform.position = origin.position;
            copy.transform.rotation = origin.rotation;

            copy.transform.Translate(offset, Space.Self);

            Vector3 result = copy.transform.position;

            Destroy(copy);

            return result;
        }
    }
}