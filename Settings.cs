using System;
using System.Collections.Generic;
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

        public void OnChange()
        {
            throw new NotImplementedException();
        }
    }
}