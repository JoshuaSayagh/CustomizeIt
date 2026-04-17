using Game.Simulation;
using HarmonyLib;

namespace CustomizeIt.Patches
{
    [HarmonyPatch(typeof(TourismSystem), nameof(TourismSystem.GetTargetTourists))]
    public static class GetTargetTouristsPatch
    {
        public static long CallCount;

        public static bool Prefix(int attractiveness, ref int __result)
        {
            CallCount++;

            Setting setting = Mod.Setting;
            if (setting == null)
                return true;

            int target = setting.TargetTouristCount;
            if (target <= 0)
                return true; // vanilla behavior

            __result = target;
            return false; // skip original
        }
    }
}
