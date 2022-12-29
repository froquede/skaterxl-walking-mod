using Cinemachine;
using GameManagement;
using HarmonyLib;
using ModIO.UI;
using ReplayEditor;
using SkaterXL.Core;
using SkaterXL.Data;
using System;
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
        AnimController actual_anim = new AnimController();
        public bool inState = false;
        Camera camera;
        GameObject fallbackCamera;
        GameObject fakeSkate;
        CinemachineVirtualCamera virtual_cam, main_cam;

        void Start()
        {
            walking = new AnimController(Path.Combine(Main.modEntry.Path, "walking.json"), fs);
            walking_backwards = new AnimController(Path.Combine(Main.modEntry.Path, "walking_backwards.json"), fs);
            walking_left = new AnimController(Path.Combine(Main.modEntry.Path, "walking_left.json"), fs);
            walking_right = new AnimController(Path.Combine(Main.modEntry.Path, "walking_right.json"), fs);
            running = new AnimController(Path.Combine(Main.modEntry.Path, "running.json"), fs);
            running_backwards = new AnimController(Path.Combine(Main.modEntry.Path, "running_backwards.json"), fs);
            running_left = new AnimController(Path.Combine(Main.modEntry.Path, "running_left.json"), fs);
            running_right = new AnimController(Path.Combine(Main.modEntry.Path, "running_right.json"), fs);
            idle = new AnimController(Path.Combine(Main.modEntry.Path, "idle.json"), fs);
            jump = new AnimController(Path.Combine(Main.modEntry.Path, "jumping.json"), fs, false);
            running_jump = new AnimController(Path.Combine(Main.modEntry.Path, "running_jump.json"), fs, false);
            left_turn = new AnimController(Path.Combine(Main.modEntry.Path, "left_turn.json"), fs);
            right_turn = new AnimController(Path.Combine(Main.modEntry.Path, "right_turn.json"), fs);
            front_flip = new AnimController(Path.Combine(Main.modEntry.Path, "front_flip.json"), fs);
            back_flip = new AnimController(Path.Combine(Main.modEntry.Path, "back_flip.json"), fs);
            throwdown = new AnimController(Path.Combine(Main.modEntry.Path, "throwdown.json"), fs, false);
            emote1 = new AnimController(Path.Combine(Main.modEntry.Path, "flair.json"), fs, false);
            emote2 = new AnimController(Path.Combine(Main.modEntry.Path, "air_kick.json"), fs, false);
            emote3 = new AnimController(Path.Combine(Main.modEntry.Path, "ymca.json"), fs, false);
            emote4 = new AnimController(Path.Combine(Main.modEntry.Path, "dance.json"), fs, false);
            running_jump.crossfade = 1;
            emote1.crossfade = 1;
            emote2.crossfade = 1;
            emote3.crossfade = 1;
            emote4.crossfade = 1;

            animations = new AnimController[16] { walking, walking_backwards, walking_left, walking_right, running, running_backwards, running_left, running_right, idle, jump, running_jump, left_turn, right_turn, front_flip, back_flip, throwdown };

            GameStateMachine.Instance.allowPinMovement = true;

            camera = Camera.main.GetComponent<Camera>();
            fallbackCamera = PlayerController.Instance.skaterController.transform.parent.parent.Find("Fallback Camera").gameObject;
            virtual_cam = fallbackCamera.GetComponent<CinemachineVirtualCamera>();

            main_cam = MonoBehaviourSingleton<PlayerController>.Instance.cameraController._actualCam.GetComponent<CinemachineVirtualCamera>();
        }

        public float speed = 7.0f;
        public float jumpForce = 10.0f;
        public Vector3 velocity;
        bool jumping = false; int press_count = 0;
        int load_count = 0;
        bool isGrounded = false;
        string actual_state = "";
        float groundDistance = 0.2f;
        bool throwdown_state = false, flipping = false;
        float max_speed = 6.75f;
        float running_speed = 3.75f;
        int holded_count = 0;
        bool holded = false;
        bool emoting = false;
        bool respawnSwitch = false;
        float limit_idle = .15f;
        bool initiated = false;
        float decay = .7f;

        Vector3 last_pos = Vector3.zero;
        void FixedUpdate()
        {
            if (!fs.self)
            {
                if (SceneManager.GetActiveScene().isLoaded)
                {
                    load_count++;
                }

                if (Time.fixedUnscaledTime > 60)
                {
                    fs.Create();
                    fs.self.AddComponent<TransformTracker>();
                    DontDestroyOnLoad(fs.self);
                    fakeSkate = Instantiate(PlayerController.Instance.boardController.boardTransform.gameObject);
                    fakeSkate.GetComponent<Rigidbody>().isKinematic = true;
                    fakeSkate.AddComponent<TransformTracker>();
                    DontDestroyOnLoad(fakeSkate);
                    NotificationManager.Instance.ShowNotification("Walking loaded", 3f, false, NotificationManager.NotificationType.Normal, TextAlignmentOptions.TopRight, 0.1f);
                    UISounds.Instance.PlayOneShotSelectMajor();
                }
            }
            else
            {
                if (!fs.rb)
                {
                    fs.rb = fs.self.AddComponent<Rigidbody>();
                    fs.rb.constraints = RigidbodyConstraints.FreezeRotation;
                    fs.rb.freezeRotation = true;
                }
                else
                {
                    isGrounded = Physics.CheckCapsule(fs.collider.bounds.center, new Vector3(fs.collider.bounds.center.x, fs.collider.bounds.min.y, fs.collider.bounds.center.z), groundDistance, LayerUtility.GroundMask);
                }

                if (inState && !fs.visible) { fs.show(); fakeSkate.SetActive(true); }

                if (inState)
                {
                    if (GameStateMachine.Instance.CurrentState.GetType() != typeof(PlayState)) return;

                    PlayerController.Instance.BoardFreezedAfterRespawn = false;
                    PlayerController.Instance.skaterController.transform.position = fs.self.transform.position;
                    PlayerController.Instance.boardController.transform.position = fs.self.transform.position - new Vector3(0, .73f, 0);

                    //virtual_cam.m_Lens.FieldOfView = 90;
                    PlayerController.Instance.DisableGameplay();
                    //GameStateMachine.Instance.PlayObject.SetActive(false);
                    GameStateMachine.Instance.PinObject.SetActive(false);
                    float LX = PlayerController.Instance.inputController.player.GetAxis(19);
                    float LY = PlayerController.Instance.inputController.player.GetAxis(20);
                    float RX = PlayerController.Instance.inputController.player.GetAxis(21);

                    float horizontal = LX;
                    float vertical = LY;
                    Vector3 move = transform.right * horizontal + transform.forward * vertical;

                    fs.rb.AddForce(-fs.rb.velocity * decay);
                    Physics.SyncTransforms();
                    fs.rb.MoveRotation(Quaternion.Euler(fs.rb.transform.rotation.eulerAngles.x, fs.rb.transform.rotation.eulerAngles.y + (actual_state == "idle" ? RX : actual_state == "walking" ? RX / 1.5F : RX / 2.5F), fs.rb.transform.rotation.eulerAngles.z));

                    if (fs.rb.velocity.magnitude <= max_speed)
                    {
                        fs.rb.AddRelativeForce(move * speed);
                    }

                    if (fs.rb.velocity.magnitude >= running_speed)
                    {
                        actual_state = "running";
                    }
                    else
                    {
                        if (fs.rb.velocity.magnitude <= limit_idle) actual_state = "idle";
                        else actual_state = "walking";
                    }

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

                    Vector3 relativeVelocity = fs.rb.transform.InverseTransformDirection(last_pos - fs.self.transform.position);
                    float forward_velocity = relativeVelocity.z;
                    forward_velocity = forward_velocity < 0 ? -forward_velocity : forward_velocity;
                    float side_velocity = relativeVelocity.x;
                    side_velocity = side_velocity < 0 ? -side_velocity : side_velocity;
                    bool backwards = relativeVelocity.z > 0;
                    last_pos = fs.self.transform.position;

                    if (!jumping)
                    {
                        actual_anim.offset = new Vector3(0, -.73f, 0);
                        if (actual_state == "idle")
                        {
                            if (RX != 0)
                            {
                                if (RX < 0 && left_turn.path != actual_anim.path) Play(left_turn);
                                if (RX > 0 && right_turn.path != actual_anim.path) Play(right_turn);
                            }
                            else if (idle.path != actual_anim.path) Play(idle);
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
                    else
                    {
                        float frame_time = (float)actual_anim.frame;

                        if (actual_anim.path == front_flip.path || actual_anim.path == back_flip.path)
                        {
                            if (actual_anim.frame == 9) fs.rb.AddRelativeForce(0, 3.25f, 0, ForceMode.Impulse);
                            /*if (actual_anim.frame > 9 && actual_anim.frame < 28) actual_anim.offset = new Vector3(0, -.73f - (frame_time / 28), 0);
                            else actual_anim.offset = new Vector3(0, Mathf.Lerp(actual_anim.offset.y, -.73f, Time.fixedDeltaTime * 4f), 0);*/
                        }

                        if (actual_anim.path == running_jump.path || actual_anim.path == running_jump.path)
                        {
                            if (actual_anim.frame == 2) fs.rb.AddRelativeForce(0, 2f, 0, ForceMode.Impulse);
                            /*if (actual_anim.frame > 3 && actual_anim.frame < 7) actual_anim.offset = new Vector3(0, -.73f - (frame_time / 14), 0);
                            else actual_anim.offset = new Vector3(0, Mathf.Lerp(actual_anim.offset.y, -.73f, Time.fixedDeltaTime * 4f), 0);*/
                        }

                        if (actual_anim.path == jump.path || actual_anim.path == jump.path)
                        {
                            if (actual_anim.frame == 20) fs.rb.AddRelativeForce(0, 1f, 0, ForceMode.Impulse);
                        }
                    }

                    actual_anim.FixedUpdate();

                    fakeSkate.transform.position = throwdown_state ? fs.getPart("Skater_hand_l").transform.position : fs.getPart("Skater_ForeArm_r").transform.position;
                    fakeSkate.transform.rotation = throwdown_state ? fs.getPart("Skater_hand_l").transform.rotation : fs.getPart("Skater_ForeArm_r").transform.rotation;
                    fakeSkate.transform.Rotate(90f, 0, 0, Space.Self);
                    fakeSkate.transform.Rotate(10f, 20f, 0, Space.Self);
                    fakeSkate.transform.Translate(-.2f, .045f, -.1f, Space.Self);

                    if (!jumping)
                    {
                        if (PlayerController.Instance.inputController.player.GetButtonSinglePressHold("A"))
                        {
                            if (!holded)
                            {
                                jumping = true;
                                CallBack call = OnJumpEnd;
                                fs.rb.AddRelativeForce(-move * (speed / 3f));
                                StopAll();
                                Play(actual_state == "idle" ? jump : running_jump, call);
                            }
                        }
                        else
                        {
                            if (holded_count > 12) holded = false;
                            else holded_count++;

                            if (PlayerController.Instance.inputController.player.GetButtonDoublePressDown("A"))
                            {
                                jumping = true;
                                CallBack call = OnJumpEnd;
                                fs.rb.AddRelativeForce(-move * (speed / 2f));
                                Play(backwards ? back_flip : front_flip, call);
                                flipping = true;
                            }
                        }
                    }

                    if (jumping && actual_anim.frame >= 13 && actual_anim.frame <= 26)
                    {
                        //fs.rb.AddRelativeForce(0, .2f, 0, ForceMode.Impulse);
                    }

                    if (PlayerController.Instance.inputController.player.GetButtonDown("Y"))
                    {
                        inState = false;
                        TogglePlayObject(true);
                    }

                    if (PlayerController.Instance.inputController.player.GetButtonDown("B") || PlayerController.Instance.inputController.player.GetButtonDown("X"))
                    {
                        CallBack call = OnThrowdownEnd;
                        Play(throwdown, call);
                        throwdown_state = true;
                        actual_state = "throwdown";
                        respawnSwitch = PlayerController.Instance.inputController.player.GetButtonDown("X");
                    }
                }
                else
                {
                    if (PlayerController.Instance.inputController.player.GetButton("A") && PlayerController.Instance.inputController.player.GetButton("X"))
                    {
                        press_count++;
                        if (press_count >= 12)
                        {
                            press_count = 0;
                            fs.rb.velocity = Vector3.zero;
                            fs.rb.angularVelocity = Vector3.zero;

                            inState = true;
                            TogglePlayObject(false);

                            fs.rb.velocity = PlayerController.Instance.skaterController.skaterRigidbody.velocity;

                            fs.self.transform.position = PlayerController.Instance.skaterController.skaterTransform.position - new Vector3(0, .2f, 0);
                            fs.self.transform.rotation = PlayerController.Instance.skaterController.skaterTransform.rotation;

                            if (PlayerController.Instance.IsSwitch) fs.self.transform.Rotate(0, 180, 0, Space.Self);

                            holded = true;
                            holded_count = 0;

                            StopAll();
                            throwdown_state = false;
                            emoting = false;
                        }
                    }
                    else
                    {
                        press_count = 0;
                    }

                    if (fs.visible) { fs.hide(); fakeSkate.SetActive(false); }
                }

                UpdateCamera(true, false);
            }
        }

        void PlayEmote(AnimController target)
        {
            CallBack call = OnEmoteEnd;
            Play(target, call);
            emoting = true;
            actual_state = "emoting";
            if (jumping) OnJumpEnd();
        }

        public Vector3 raycastOffset = Vector3.zero;
        void Update()
        {
            if (inState)
            {
                Vector3 raycastOrigin = fs.self.transform.position + raycastOffset + fs.collider.center;
                Ray ray = new Ray(raycastOrigin, -fs.self.transform.up);

                if (Physics.Raycast(ray, out RaycastHit hit, 1.25f, LayerUtility.GroundMask))
                {
                    Quaternion rotation = Quaternion.FromToRotation(fs.self.transform.up, hit.normal);
                    fs.collider.transform.rotation = Quaternion.Slerp(fs.collider.transform.rotation, rotation * fs.collider.transform.rotation, Time.deltaTime * 6f);

                    Vector3 off = translateLocal(fs.self.transform, actual_anim.offset);
                    if (off.z > hit.point.z)
                    {
                        Vector3 newoffset = hit.point - (fs.self.transform.position + fs.collider.center);
                        actual_anim.offset = new Vector3(0, newoffset.y, 0);
                    }
                }
            }
        }

        public float smoothTime = 0.024f;
        Vector3 currentVelocity;
        public void UpdateCamera(bool pos, bool rot)
        {
            if (inState)
            {
                GameObject target = new GameObject();
                target.transform.position = fs.self.transform.position;
                target.transform.rotation = Quaternion.Euler(0, fs.self.transform.rotation.eulerAngles.y, 0);
                target.transform.Translate(-0.25f, .12f, -2.00f, Space.Self);

                if (pos) fallbackCamera.transform.position = Vector3.SmoothDamp(camera.transform.position, target.transform.position, ref currentVelocity, smoothTime);
                if (rot) fallbackCamera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, target.transform.rotation, Time.deltaTime * 3f);

                main_cam.transform.position = fallbackCamera.transform.position;
                main_cam.transform.rotation = fallbackCamera.transform.rotation;

                Destroy(target);
            }
        }

        bool updating = false;
        bool check_velocity = false;
        void LateUpdate()
        {
            if (updating == true)
            {
                updating = false;
                check_velocity = true;

                MonoBehaviourSingleton<PlayerController>.Instance.respawn.SetSpawnPoint(last_nr, Respawn.SpawnPointChangeMethod.Auto);
            }

            if (check_velocity)
            {
                if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude <= fs.rb.velocity.magnitude)
                {
                    int p_value = 10;
                    if (PlayerController.Instance.boardController.boardRigidbody.velocity.magnitude < 0.15f)
                    {
                        PlayerController.Instance.boardController.boardRigidbody.AddForce(MonoBehaviourSingleton<PlayerController>.Instance.PlayerForward() * p_value * 1.4f, ForceMode.Impulse);
                    }
                    else
                    {
                        PlayerController.Instance.boardController.boardRigidbody.AddForce(PlayerController.Instance.boardController.boardRigidbody.velocity.normalized * p_value, ForceMode.Impulse);
                    }
                }
                else check_velocity = false;
            }

            if(inState) UpdateCamera(false, true);
        }

        void OnJumpEnd()
        {
            UnityModManager.Logger.Log("OnJumpEnd");
            jumping = false;
        }

        RespawnInfo last_nr;
        void OnThrowdownEnd()
        {
            Vector3 forward = fs.rb.transform.forward;

            last_nr = (RespawnInfo)Traverse.Create(MonoBehaviourSingleton<PlayerController>.Instance.respawn).Field("markerRespawnInfos").GetValue();

            RespawnInfo respawnInfo = new RespawnInfo
            {
                position = fs.self.transform.position - new Vector3(0, .73f, 0),
                IsBoardBackwards = false,
                rotation = Quaternion.LookRotation(forward),
                isSwitch = respawnSwitch
            };

            PlayerController.Instance.EnableGameplay();
            //GameStateMachine.Instance.PlayObject.SetActive(true);

            PlayerController.Instance.BoardFreezedAfterRespawn = false;
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.SetSpawnPoint(respawnInfo, Respawn.SpawnPointChangeMethod.Auto);
            MonoBehaviourSingleton<PlayerController>.Instance.respawn.DoRespawn();

            TogglePlayObject(true);
            PlayerController.Instance.BoardFreezedAfterRespawn = false;

            inState = false;
            throwdown_state = false;
            updating = true;
        }

        Transform[] original_bones;
        void TogglePlayObject(bool enabled)
        {
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
            GameStateMachine.Instance.PlayObject.GetComponent<CharacterCustomizer>().enabled = enabled;

            /*if (enabled)
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
            }*/
        }

        void OnEmoteEnd()
        {
            UnityModManager.Logger.Log("OnEmoteEnd");
            emoting = false;
        }

        void Play(AnimController target)
        {
            if ((throwdown_state && actual_anim.path == throwdown.path) || emoting) return;
            if (actual_anim.path == target.path && target.isPlaying) return;
            actual_anim = target;
            target.Play();
        }

        void Play(AnimController target, CallBack call)
        {
            if ((throwdown_state && actual_anim.path == throwdown.path) || emoting) return;
            if (actual_anim.path == target.path && target.isPlaying) return;
            actual_anim = target;
            target.Play(call);
        }

        void StopAll()
        {
            for (int i = 0; i < animations.Length; i++) animations[i].Stop();
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