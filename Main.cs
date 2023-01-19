
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

            GUILayout.Space(12);

            if (!settings.experimental_bail)
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
            }
            GUILayout.EndVertical();
            settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {

        }
    }
}
