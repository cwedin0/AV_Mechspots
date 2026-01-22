using System;
using System.Reflection;
using AV_Mechspots;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace AV_MechSpots
{
    [HarmonyPatch]
    [HarmonyPatchCategory("wvc.sergkart.biotech.moremechanoidsworkmodes")]
    public static class Patch_JobGiver_GoToShutdownZone
    {
        [HarmonyPrefix]
        public static bool TryGiveJob(Pawn pawn, ref Job __result)
        {
            /// Redirect JobGiver_GoToShutdownZone.TryGiveJob to
            /// AV_Mechspots.JobGiver_StayAtMechSpot.TryGiveJob
            __result = new JobGiver_StayAtMechSpot()
                .CallTryGiveJob(pawn);
            // Proceed to original method if no job was assigned
            return __result == null;
        }

        static MethodBase TargetMethod()
        {
            Type type = AccessTools.TypeByName(
                "WVC_WorkModes.JobGiver_GoToShutdownZone");
            if (type == null)
            {
                Log.Error("[AV] Could not find type "
                    + "WVC_WorkModes.JobGiver_GoToShutdownZone");
                return null;
            }
            return AccessTools.Method(type, "TryGiveJob");
        }
    }
}