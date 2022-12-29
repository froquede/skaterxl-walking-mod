
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
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            go = new GameObject();
            walking_go = go.AddComponent<WalkingController>();
            UnityEngine.Object.DontDestroyOnLoad(walking_go);
            ui = go.AddComponent<gui>();
            UnityEngine.Object.DontDestroyOnLoad(ui);

            //modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = new Action<UnityModManager.ModEntry>(OnSaveGUI);
            modEntry.OnToggle = new Func<UnityModManager.ModEntry, bool, bool>(OnToggle);
            modEntry.OnUnload = Unload;
            Main.modEntry = modEntry;

            UnityModManager.Logger.Log("Loaded " + modEntry.Info.Id);
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
            settings.Draw(modEntry);
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }
    }
}
