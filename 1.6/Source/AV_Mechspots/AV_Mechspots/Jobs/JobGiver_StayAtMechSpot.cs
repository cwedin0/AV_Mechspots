using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AV_Framework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;
using static RimWorld.MechClusterSketch;

namespace AV_Mechspots
{
    class JobGiver_StayAtMechSpot : ThinkNode_JobGiver
    {
        /// <summary>
        /// Wrapper class to allow redirecting jobs to
        /// AV_Mechspots.JobGiver_StayAtMechSpot
        /// <param name="pawn"></param>
        /// <returns></returns>
        public Job CallTryGiveJob(Pawn pawn)
        {
            return TryGiveJob(pawn);
        }

        protected override Job TryGiveJob(Pawn pawn)        //find a assigned meditation spot for fluoid to work on
        {
            if (!pawn.IsColonyMech)     //failsafe
            {
                return null;
            }
            if (pawn.ownership.AssignedMeditationSpot == null)
            {
                return null;
            }
            Map map = pawn.Map;
            if (map != null)
            {
                if (!MechspotsSettings.AllowUsageUnpoweredSpots && map.gameConditionManager.ElectricityDisabled(map))        //fix for non electric spots to stop working
                {
                    return null;
                }
            }
            else
            {
                return null;
            }

            Building spot = pawn.ownership.AssignedMeditationSpot;
            IntVec3 spotpos = spot.Position;

            MechSpotUtility.ForgetDestroyedOldSpot(pawn);   // bugfix for wrongly assigned spots

            if (spot.Map == null || pawn.Map == null || spot.Map != pawn.Map)
            {
                return null;
            }

            if (!spotpos.InAllowedArea(pawn))
            {
                return null;
            }

            if (!pawn.CanReserveAndReach(spotpos, PathEndMode.OnCell, Danger.None, 1, -1, null, false))
            {
                return null;
            }

            CompPowerTrader comp = spot.GetComp<CompPowerTrader>();

            if (comp != null && !comp.PowerOn && !MechspotsSettings.AllowUsageUnpoweredSpots)   //needs power and power is off  -> allows charged stone to work without electricity
            {
                return null;
            }

            MechSpotUtility.ReclaimOldSpotAfterRebirth(pawn);

            if (spot.TryGetComp<Comp_ChargeSpot>() != null)
            {
                return ChargeAtSpot(spotpos);
            }

            if (!pawn.RaceProps.IsWorkMech)
            {
                return GuardAtSpot(spotpos, pawn, spot);
            }
            else if (pawn.TryGetComp<Comp_SelectableSpawner>() != null)
            {
                return ProduceAtSpot(spotpos);
            }
            else
            {
                return StayAtSpot(spotpos);
            }
        }





        private Job ChargeAtSpot(IntVec3 spotpos)
        {
            Job charge = new Job(MechSpotDefOfs.ChargeAtMechSpot, spotpos);     //only difference is not showing weapon
            charge.reportStringOverride = "AV_DescChargingAtSpot".Translate().CapitalizeFirst(); ;
            return charge;
        }

        private Job GuardAtSpot(IntVec3 spotpos, Pawn pawn, Building spot)
        {
            int regionsToScan = pawn.mindState.anyCloseHostilesRecently ? 24 : 18;

            if (PawnUtility.EnemiesAreNearby(pawn, regionsToScan, passDoors: true))
            {
                if (!spot.TryGetComp<CompAssignableToMech>().ForceStayAtSpot)
                {
                    if (MechspotsSettings.DebugLogging) { Log.Message("[AV]Mechspots.JobGiver_StayAtMechSpot: Enemies are nearby, starting to patrol"); }
                    return null;    //Combat mechs try to patrol when they hear combat, but cant see it, they move towards enemies if they can see them
                }
                else
                {
                    if (MechspotsSettings.DebugLogging) { Log.Message("[AV]Mechspots.JobGiver_StayAtMechSpot: Enemies are nearby, force staying at spot is active"); }
                }
            }

            Job guard = new Job(MechSpotDefOfs.StayAtMechSpot, spotpos);
            guard.reportStringOverride = "AV_DescGuardingAtSpot".Translate().CapitalizeFirst();
            return guard;
        }

        private Job StayAtSpot(IntVec3 spotpos)
        {
            Job stay = new Job(MechSpotDefOfs.StayAtMechSpot, spotpos);
            stay.reportStringOverride = "AV_DescStayingAtSpot".Translate().CapitalizeFirst();
            return stay;
        }

        private Job ProduceAtSpot(IntVec3 spotpos)
        {
            Job produce = new Job(MechSpotDefOfs.StayAtMechSpot, spotpos);
            produce.reportStringOverride = "AV_DescProducingAtSpot".Translate().CapitalizeFirst();
            return produce;
        }

    }
}
