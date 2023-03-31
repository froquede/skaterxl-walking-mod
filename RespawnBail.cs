using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace walking_mod
{
    [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.DoBailDelay), new Type[] { })]
    class RespawnBail
    {
        private static bool Prefix()
        {
            if (Main.settings.enabled && MultiplayerManager.Instance.InRoom && Main.walking_go.inState)
            {
                return false;
            }
            return true;
        }
    }
}
