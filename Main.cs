
using HarmonyLib;
using System;
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

            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            UnityModManager.Logger.Log("Loaded " + modEntry.Info.Id);
            UnityEngine.Object.DontDestroyOnLoad(go);
            UnityEngine.Object.DontDestroyOnLoad(walking_go);
            UnityEngine.Object.DontDestroyOnLoad(ui);
            return true;
        }
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            UnityEngine.Object.Destroy(go);
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            UnityModManager.Logger.Log("Toggled " + modEntry.Info.Id);
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical(GUILayout.Width(256));
                GUILayout.BeginVertical();
                    GUILayout.Label("Volume");
                    settings.volume = GUILayout.HorizontalScrollbar(settings.volume, .1f, 0f, 1f);
                GUILayout.EndVertical();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(420));
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Throwdown force", GUILayout.Width(100));
            settings.throwdown_force = GUILayout.HorizontalScrollbar(settings.throwdown_force, .1f, 0f, 30f);
            if (GUILayout.Button("reset", GUILayout.Height(20), GUILayout.Width(60))) settings.throwdown_force = 18f;
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginVertical();

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

            GUILayout.EndVertical();

            /*if (!settings.experimental_bail)
            {
                if (GUILayout.Button("Enable experimental multiplayer on bail", GUILayout.Height(42))) {
                    settings.experimental_bail = true;
                }
            }
            else
            {
                if (GUILayout.Button("Disable experimental multiplayer on bail", GUILayout.Height(42)))
                {
                    settings.experimental_bail = false;
                }
            }*/
            GUILayout.EndVertical();
            settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {

        }
    }
}
