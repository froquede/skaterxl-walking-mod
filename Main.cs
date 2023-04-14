
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

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {

            if (GUILayout.Button("Reload emotes", GUILayout.Height(32), GUILayout.Width(128))) walking_go.LoadEmotes();
            GUILayout.Space(6);

            GUILayout.BeginVertical(GUILayout.Width(256));
                GUILayout.BeginVertical();
                    GUILayout.Label("Volume");
                    settings.volume = GUILayout.HorizontalScrollbar(settings.volume, .1f, 0f, 1f);
                GUILayout.EndVertical();
                GUILayout.BeginVertical();
                    GUILayout.Label("Sound emotes volume");
                    settings.emote_volume = GUILayout.HorizontalScrollbar(settings.emote_volume, .1f, 0f, 1f);
                GUILayout.EndVertical();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(420));
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Throwdown force", GUILayout.Width(100));
            settings.throwdown_force = GUILayout.HorizontalScrollbar(settings.throwdown_force, .1f, 0f, 50f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.throwdown_force = 25f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Max bail force (" + settings.max_magnitude_bail.ToString("N2") + ")", GUILayout.Width(100));
            settings.max_magnitude_bail = GUILayout.HorizontalScrollbar(settings.max_magnitude_bail, .1f, 0f, 20f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.max_magnitude_bail = 8f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Roll limiter (" + settings.minVelocityRoll.ToString("N2") + ")", GUILayout.Width(100));
            settings.minVelocityRoll = GUILayout.HorizontalScrollbar(settings.minVelocityRoll, .05f, 0f, 1f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.minVelocityRoll = .5f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Respawn on skate after bail limit (" + settings.bailLimit.ToString("N2") + ") seconds", GUILayout.Width(100));
            settings.bailLimit = GUILayout.HorizontalScrollbar(settings.bailLimit, .05f, 0f, 20f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.bailLimit = 1f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera position velocity", GUILayout.Width(100));
            settings.camera_pos_vel = GUILayout.HorizontalScrollbar(settings.camera_pos_vel, .1f, 0f, 20f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.camera_pos_vel = 10f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera rotation velocity", GUILayout.Width(100));
            settings.camera_rot_vel = GUILayout.HorizontalScrollbar(settings.camera_rot_vel, .1f, 0f, 20f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.camera_rot_vel = 4f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera offset x", GUILayout.Width(100));
            settings.camera_offset.x = GUILayout.HorizontalScrollbar(settings.camera_offset.x, .1f, -4f, 4f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.camera_offset.x = .05f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera offset y", GUILayout.Width(100));
            settings.camera_offset.y = GUILayout.HorizontalScrollbar(settings.camera_offset.y, .1f, -4f, 4f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.camera_offset.y = .12f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera offset z", GUILayout.Width(100));
            settings.camera_offset.z = GUILayout.HorizontalScrollbar(settings.camera_offset.z, .1f, -4f, 4f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.camera_offset.z = -1.3f;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera rotation offset x", GUILayout.Width(100));
            settings.temprot.x = GUILayout.HorizontalScrollbar(settings.temprot.x, 1f, -90f, 90f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.temprot.x = 0;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera rotation offset y", GUILayout.Width(100));
            settings.temprot.y = GUILayout.HorizontalScrollbar(settings.temprot.y, 1f, -180f, 180f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.temprot.y = 0;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Camera rotation offset z", GUILayout.Width(100));
            settings.temprot.z = GUILayout.HorizontalScrollbar(settings.temprot.z, 1f, -180f, 180f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.temprot.z = 0;
            GUILayout.EndHorizontal();

            settings.camera_rotation_offset = Quaternion.Euler(settings.temprot);

            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Idle jump force", GUILayout.Width(100));
            settings.idle_jump_force = GUILayout.HorizontalScrollbar(settings.idle_jump_force, .1f, 0f, 10.1f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.idle_jump_force = 1f;
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Running jump force", GUILayout.Width(100));
            settings.running_jump_force = GUILayout.HorizontalScrollbar(settings.running_jump_force, .1f, 0f, 10.1f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.running_jump_force = 2.5f;
            GUILayout.EndVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Flip jump force", GUILayout.Width(100));
            settings.flip_jump_force = GUILayout.HorizontalScrollbar(settings.flip_jump_force, .1f, 0f, 10.1f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.flip_jump_force = 3f;
            GUILayout.EndVertical();

            GUILayout.EndVertical();
            settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }
}
