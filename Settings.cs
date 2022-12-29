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

        public void OnChange()
        {
            throw new NotImplementedException();
        }
    }
}