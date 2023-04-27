
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    [EnableReloading]
    static class Main
    {
        public static Settings settings;
        public static Harmony harmonyInstance;
        public static WalkingController walking_go;
        public static UnityModManager.ModEntry modEntry;
        public static GameObject go;
        public static gui ui;
        public static Assembly assembly;

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            harmonyInstance = new Harmony(modEntry.Info.Id);

            go = new GameObject("WalkingModGameObject");
            walking_go = go.AddComponent<WalkingController>();
            ui = go.AddComponent<gui>();

            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = new Action<UnityModManager.ModEntry>(OnSaveGUI);
            modEntry.OnToggle = new Func<UnityModManager.ModEntry, bool, bool>(OnToggle);
            modEntry.OnUnload = Unload;
            Main.modEntry = modEntry;

            assembly = Assembly.GetExecutingAssembly();
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            settings.temprot = settings.camera_rotation_offset.eulerAngles;

            UnityModManager.Logger.Log("Loaded " + modEntry.Info.Id);
            UnityEngine.Object.DontDestroyOnLoad(go);
            UnityEngine.Object.DontDestroyOnLoad(walking_go);
            UnityEngine.Object.DontDestroyOnLoad(ui);
            return true;
        }
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            EventManager.Instance.onGPEvent -= walking_go.onRunEvent;

            if (walking_go.inState) walking_go.RestoreGameplay(true, true);
            UnityEngine.Object.Destroy(go);

            try
            {
                harmonyInstance.UnpatchAll(harmonyInstance.Id);
            }
            catch { }

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            UnityModManager.Logger.Log("Toggled " + modEntry.Info.Id);
            return true;
        }

        static string[] buttons = new string[] { "A", "B", "X" };
        static GUIStyle title = new GUIStyle();
        static GUIStyle subtitle = new GUIStyle();
        static GUIStyle text = new GUIStyle();
        static GUIStyle box = new GUIStyle("Box");
        static int width = 396;
        static int padding = 14;

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            title.fontSize = 16;
            title.normal.textColor = Color.white;
            subtitle.fontSize = 13;
            subtitle.normal.textColor = new Color32(212, 212, 212, 255);
            text.fontSize = 12;
            text.normal.textColor = Color.gray;
            box.padding.left = box.padding.right = box.padding.top = box.padding.bottom = padding;

            GUILayout.Label("   ");
            GUILayout.BeginHorizontal();
            {
                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical(GUILayout.Width(width));
                {
                    GUILayout.BeginVertical(box, GUILayout.Width(width));
                    {
                        GUILayout.Label("<b>Options</b>", title);
                        GUILayout.Space(12);
                        settings.throwdown_force = Slider("Throwdown force (" + settings.throwdown_force.ToString("N0") + ")", settings.throwdown_force, 0f, 50f, .1f, 25f);
                        settings.max_magnitude_bail = Slider("Max bail force to stumble (" + settings.max_magnitude_bail.ToString("N2") + ")", settings.max_magnitude_bail, 0f, 20f, .1f, 8f, "0 is always bail");
                        settings.minVelocityRoll = Slider("Roll limiter (" + settings.minVelocityRoll.ToString("N2") + ")", settings.minVelocityRoll, 0f, 1f, .05f, .5f, "0 is always roll after impact");
                        settings.bailLimit = Slider("Respawn on skate after bail limit (" + settings.bailLimit.ToString("N2") + ")", settings.bailLimit, 0f, 20f, .05f, 2f, "This limits the time you want to use to respawn walking or skating in \nseconds");
                        settings.frame_wait = (int)Slider("Button press wait (" + settings.frame_wait.ToString("N0") + ")", settings.frame_wait, 0f, 64f, 1f, 12f, "The amount of frames to wait to enter walk mode");
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(6);

                    GUILayout.BeginVertical(box, GUILayout.Width(width));
                    {

                        GUILayout.Label("<b>Volume</b>", title);
                        GUILayout.Space(12);
                        settings.volume = Slider("<b>Footsteps</b> (" + (settings.volume * 100).ToString("N0") + "%)", settings.volume, 0f, 1f, .1f, .4f);
                        settings.emote_volume = Slider("<b>Footsteps</b> (" + (settings.emote_volume * 100).ToString("N0") + "%)", settings.volume, 0f, 1f, .1f, .75f);
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(6);

                    GUILayout.BeginVertical(box, GUILayout.Width(width));
                    {
                        GUILayout.Label("<b>Jump force</b>", title);
                        GUILayout.Space(12);

                        settings.idle_jump_force = Slider("Idle (" + settings.idle_jump_force.ToString("N2") + ")", settings.idle_jump_force, 0f, 10f, .1f, 1f);
                        settings.running_jump_force = Slider("Running (" + settings.running_jump_force.ToString("N2") + ")", settings.running_jump_force, 0f, 10f, .1f, 2.5f);
                        settings.flip_jump_force = Slider("Flip (" + settings.flip_jump_force.ToString("N2") + ")", settings.flip_jump_force, 0f, 10f, .1f, 3f);
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();

                GUILayout.Space(14);

                GUILayout.BeginVertical(GUILayout.Width(width));
                {
                    GUILayout.BeginVertical(box, GUILayout.Width(width));
                    {
                        GUILayout.Label("<b>Camera</b>", title);
                        GUILayout.Space(12);

                        GUILayout.Label("<b>Position</b>", subtitle);
                        GUILayout.Space(10);
                        settings.camera_offset.x = Slider("X (" + settings.camera_offset.x.ToString("N2") + ")", settings.camera_offset.x, -4f, 4f, .05f, .05f);
                        settings.camera_offset.y = Slider("Y (" + settings.camera_offset.y.ToString("N2") + ")", settings.camera_offset.y, -4f, 4f, .05f, .12f);
                        settings.camera_offset.z = Slider("Z (" + settings.camera_offset.z.ToString("N2") + ")", settings.camera_offset.z, -4f, 4f, .05f, -1.3f);
                        GUILayout.Space(6);

                        GUILayout.Label("<b>Rotation</b>", subtitle);
                        GUILayout.Space(10);
                        settings.temprot.x = Slider("X (" + settings.temprot.x.ToString("N2") + ")", settings.temprot.x, -90f, 90f, 1f, 0);
                        settings.temprot.y = Slider("Y (" + settings.temprot.y.ToString("N2") + ")", settings.temprot.y, -180f, 180f, 1f, 0);
                        settings.temprot.z = Slider("Z (" + settings.temprot.z.ToString("N2") + ")", settings.temprot.z, -180f, 180f, 1f, 0);
                        settings.camera_rotation_offset = Quaternion.Euler(settings.temprot);
                        GUILayout.Space(6);

                        GUILayout.Label("<b>Velocity</b>", subtitle);
                        GUILayout.Space(10);
                        settings.camera_pos_vel = Slider("Position (" + settings.camera_pos_vel.ToString("N2") + ")", settings.camera_pos_vel, 0f, 20f, .1f, 10f);
                        settings.camera_rot_vel = Slider("Rotation (" + settings.camera_rot_vel.ToString("N2") + ")", settings.camera_rot_vel, 0f, 20f, .1f, 4f);
                        GUILayout.Space(6);
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(6);

                    GUILayout.BeginVertical(box, GUILayout.Width(width));
                    {

                        GUILayout.BeginHorizontal(GUILayout.Width(width));
                        {
                            GUILayout.Label("Skate hand side", GUILayout.Width(width - 88));
                            if (GUILayout.Button(settings.left_arm ? "Left" : "Right", GUILayout.Height(32), GUILayout.Width(80))) settings.left_arm = !settings.left_arm;
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.Width(width));
                        {
                            GUILayout.Label("Jump button", GUILayout.Width(width - 88));
                            if (GUILayout.Button(getButtonLabel(settings.jump_button), GUILayout.Height(32), GUILayout.Width(80))) settings.jump_button = getNextButton(settings.jump_button);
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.Width(width));
                        {
                            GUILayout.Label("Magnetize board button", GUILayout.Width(width - 88));
                            if (GUILayout.Button(getButtonLabel(settings.magnetize_button), GUILayout.Height(32), GUILayout.Width(80))) settings.magnetize_button = getNextButton(settings.magnetize_button);
                        }
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal(GUILayout.Width(width));
                        {
                            GUILayout.Label("Hippie jumps (experimental)", GUILayout.Width(width - 88));
                            if (GUILayout.Button(settings.hippie_jump ? "Enabled" : "Disabled", GUILayout.Height(32), GUILayout.Width(80))) settings.hippie_jump = !settings.hippie_jump;
                        }
                        GUILayout.EndHorizontal();

                        if (GUILayout.Button("Reload only emotes", GUILayout.Height(32), GUILayout.Width(width - 4))) walking_go.LoadEmotes();
                        if (GUILayout.Button("Reload only sound emotes", GUILayout.Height(32), GUILayout.Width(width - 4))) walking_go.LoadSoundEmotes();
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();
            }
            GUILayout.EndHorizontal();

            settings.Draw(modEntry);
        }

        static float Slider(string title, float value, float min, float max, float step, float default_value, string subtext = "")
        {
            GUILayout.BeginVertical(GUILayout.Width(width));
            GUILayout.Label("<b>" + title + "</b>", subtitle, GUILayout.Width(width));
            if (subtext != "")
            {
                GUILayout.Space(2);
                GUILayout.Label(subtext, text, GUILayout.Width(width));
            }
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            float result = GUILayout.HorizontalScrollbar(value, step, min, max + step);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) result = default_value;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            return result;
        }

        private static string getNextButton(string actual)
        {
            string next = "";
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == actual)
                {
                    if (i == buttons.Length - 1) next = buttons[0];
                    else next = buttons[i + 1];
                }
            }

            return next;
        }

        static string getButtonLabel(string button)
        {
            switch (button)
            {
                case "A":
                    return "A (X)";
                case "B":
                    return "B (Circle)";
                case "X":
                    return "X (Square)";
                default:
                    return button;
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }
}
