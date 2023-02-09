using System;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    [Serializable]

    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        public bool enabled = true;
        public bool experimental_bail = false;
        public float volume = .4f;
        public float emote_volume = .75f;
        public string emote1 = "flair", emote2 = "air_kick", emote3 = "jab", emote4 = "dance";
        public string semote1 = "catchulater", semote2 = "cmonletsmove", semote3 = "hey_you_guys", semote4 = "pretty_sick";
        public Vector3 camera_offset = new Vector3(.05f, .12f, -1.3f);
        public float throwdown_force = 18f;

        public void OnChange()
        {
            throw new NotImplementedException();
        }

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save<Settings>(this, modEntry);
        }
    }
}