using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.UIElements;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AV_Mechspots
{
    public class Mechspots : Mod
    {
        private static Mechspots _instance;

        public static Mechspots Instance => _instance;

        public Mechspots(ModContentPack content)
            : base(content)
        {
            Harmony harmony = new Harmony("AV_Mechspots");

            harmony.PatchAllUncategorized();

            // Patch categories with names equal to a packageid
            // in the current active modlist.
            LoadedModManager.RunningModsListForReading
                .ForEach(mod => harmony.PatchCategory(mod.PackageId));

            _instance = this;
        }
    }

    /*
    //float menu option for spot / menhir
    [StaticConstructorOnStartup]
    public static class FloatMenuMakerMap_CarryMechPatch
    {
        [HarmonyPatch(typeof(FloatMenuMakerMap))]
        [HarmonyPatch("AddHumanlikeOrders", MethodType.Normal)]
        public static class FloatMenuMakerMap_AddHumanlikeOrders
        {
            [HarmonyPostfix]
            public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
            {
                //Log.Message("Trying to apply CarryMechToSpot Harmony Float patch");
                Job job;

                #region carry near mechspot
                foreach (LocalTargetInfo mech in GenUI.TargetsAt(clickPos, TargetparmAlliedMech(pawn), thingsOnly: true))
                {
                    if (mech.Pawn.ownership.AssignedMeditationSpot == null)
                    {
                        opts.Add(new FloatMenuOption("AV_Float_CarryToSpot_NotPossible".Translate(mech.Pawn.LabelShort).CapitalizeFirst(), null));
                        continue;
                    }
                    if (mech.Pawn.ownership.AssignedMeditationSpot != null)
                    {
                        if (!pawn.CanReach(mech, PathEndMode.OnCell, Danger.Deadly) || !pawn.CanReach(mech.Pawn.ownership.AssignedMeditationSpot.Position, PathEndMode.OnCell, Danger.Deadly))
                        {
                            opts.Add(new FloatMenuOption("AV_Float_CarryToSpot_NotPossible_2".Translate(mech.Pawn.LabelShort).CapitalizeFirst() + ": " + "NoPath".Translate().CapitalizeFirst(), null));
                            continue;
                        }
                        else
                        {
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("AV_Float_CarryToSpot_Possible".Translate(mech.Pawn.LabelShort).CapitalizeFirst(), delegate
                            {
                                job = JobMaker.MakeJob(MechSpotDefOfs.CarryNearMechSpot, mech.Thing, mech.Pawn.ownership.AssignedMeditationSpot);
                                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                            }, MenuOptionPriority.High), pawn, (LocalTargetInfo)mech.Thing, "ReservedBy"));  //this one is only added if targetinfo is working
                        }
                    }
                }
                #endregion

                #region force connect mech
                if (Prefs.DevMode)
                {
                    foreach (LocalTargetInfo mech in GenUI.TargetsAt(clickPos, TargetparmAlliedMech(pawn), thingsOnly: true))
                    {
                        
                        if (pawn.mechanitor == null)
                        {
                            //opts.Add(new FloatMenuOption("CannotControlMech".Translate(mech.Pawn.LabelShort) + ": " + "AV_Float_ForceConnect_NoMechanitor".Translate().CapitalizeFirst(), null));
                            continue;
                        }
                        
                        if (mech.Pawn.GetOverseer() != pawn)
                        {
                            if (!MechanitorUtility.CanControlMech(pawn, mech.Pawn))
                            {
                                AcceptanceReport acceptanceReport = MechanitorUtility.CanControlMech(pawn, mech.Pawn);
                                if (acceptanceReport.Reason.NullOrEmpty())
                                {
                                    opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("AV_Float_ForceConnect_Bugfix".Translate(mech.Pawn.LabelShort).CapitalizeFirst(), delegate
                                    {
                                        SoundDef sound = SoundDefOf.ControlMech_Complete;  //.PlayOneShotOnCamera(Cryohediff.pawn.Map);
                                        sound.PlayOneShot(new TargetInfo(mech.Pawn.Position, mech.Pawn.Map));

                                        if (mech.Pawn.Faction != pawn.Faction)
                                        {
                                            mech.Pawn.SetFaction(pawn.Faction);
                                        }
                                        mech.Pawn.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech.Pawn);
                                        pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech.Pawn);

                                        //does not work when acceptance report is null or empty

                                        //Job job22 = JobMaker.MakeJob(JobDefOf.ControlMech, mech);
                                        //pawn.jobs.TryTakeOrderedJob(job22, JobTag.Misc);
                                    }), pawn, mech));
                                }
                            }
                        }
                    }
                }

                #endregion
            }

            private static TargetingParameters TargetparmAlliedMech(Pawn mech)
            {
                return new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = false,
                    canTargetAnimals = false,
                    canTargetHumans = false,
                    canTargetMechs = true,
                    mapObjectTargetsMustBeAutoAttackable = false,
                    validator = delegate (TargetInfo targ)
                    {
                        return targ.Thing is Pawn mechanoid && mechanoid.IsColonyMech;
                    }
                };
            }

        }
    }
    */

    //mech gizmo jump to spot
    [StaticConstructorOnStartup]
    public static class Pawn_GetGizmos_Patch
    {
        [HarmonyPatch(typeof(Pawn))]
        [HarmonyPatch("GetGizmos")]
        public static class Pawn_GetGizmos
        {
            [HarmonyPostfix]
            public static void Postfix(ref IEnumerable<Gizmo> __result, Pawn __instance)
            {
                Pawn pawn = __instance;

                if (pawn == null || !pawn.IsColonyMech || pawn.ownership?.AssignedMeditationSpot == null)
                {
                    return;
                }
                if (MechSpotUtility.ForgetDestroyedOldSpot(pawn))
                {
                    return;
                }

                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "AV_DescSpawnerSpotLabel".Translate().CapitalizeFirst();
                command_Action.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/AV_where_socket");
                command_Action.defaultDesc = "AV_DescSpawnerJumpToSpot".Translate().CapitalizeFirst();
                command_Action.action = delegate
                {
                    if (CameraJumper.CanJump(pawn.ownership.AssignedMeditationSpot))
                    {
                        CameraJumper.TryJumpAndSelect(pawn.ownership.AssignedMeditationSpot);
                    }
                };

                __result = __result.AddItem(command_Action);

            }
        }
    }


    //use spot as backup recharger when in work mode
    [HarmonyPatch(typeof(JobGiver_GetEnergy_Charger))]
    [HarmonyPatch("TryGiveJob")]
    public static class JobGiver_GetEnergy_Charger_TryGiveJob_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Job __result, JobGiver_GetEnergy_Charger __instance, Pawn pawn)
        {
            if (__result == null)
            {
                if (pawn == null || !pawn.IsColonyMech || pawn.ownership?.AssignedMeditationSpot == null)
                {
                    return;
                }
                if (MechSpotUtility.ForgetDestroyedOldSpot(pawn))
                {
                    return;
                }

                if (pawn.ownership.AssignedMeditationSpot.Map != pawn.Map
                    || !pawn.ownership.AssignedMeditationSpot.HasComp<CompAssignableToMech>()
                    || !pawn.ownership.AssignedMeditationSpot.GetComp<CompAssignableToMech>().UseAsRecharger
                    || !pawn.CanReserveAndReach(pawn.ownership.AssignedMeditationSpot, PathEndMode.OnCell, Danger.Deadly)
                    || !pawn.ownership.AssignedMeditationSpot.Position.InAllowedArea(pawn))
                {
                    return;
                }

                if (MechSpotUtility.ShouldAutoRecharge(pawn)) // no charger available
                {
                    if (MechSpotUtility.ShouldSelfRechargeOnSpot(pawn))
                    {
                        MechSpotUtility.ReclaimOldSpotAfterRebirth(pawn);

                        Job job = JobMaker.MakeJob(JobDefOf.SelfShutdown, pawn.ownership.AssignedMeditationSpot.Position);
                        job.checkOverrideOnExpire = true;
                        job.expiryInterval = 30000; // = 120 rare ticks = 12 hours ingame
                        __result = job;
                    }
                }
            }
        }
    }


    //use spot as backup recharger when in recharge mode
    [HarmonyPatch(typeof(JobGiver_GetEnergy_SelfShutdown))]
    [HarmonyPatch("TryGiveJob")]
    public static class JobGiver_GetEnergy_SelfShutdown_TryGiveJob_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Job __result, JobGiver_GetEnergy_SelfShutdown __instance, Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonyMech || pawn.ownership?.AssignedMeditationSpot == null)
            {
                return;
            }
            if (MechSpotUtility.ForgetDestroyedOldSpot(pawn))
            {
                return;
            }

            if (pawn.ownership.AssignedMeditationSpot.Map != pawn.Map
                || !pawn.ownership.AssignedMeditationSpot.HasComp<CompAssignableToMech>()
                || !pawn.ownership.AssignedMeditationSpot.GetComp<CompAssignableToMech>().UseAsRecharger
                || !pawn.CanReserveAndReach(pawn.ownership.AssignedMeditationSpot, PathEndMode.OnCell, Danger.Deadly)
                || !pawn.ownership.AssignedMeditationSpot.Position.InAllowedArea(pawn)
                || pawn.ownership.AssignedMeditationSpot.GetComp<CompPowerTrader>() != null && pawn.ownership.AssignedMeditationSpot.GetComp<CompPowerTrader>().parent.IsBrokenDown()
                )
            {
                return;
            }

            MechSpotUtility.ReclaimOldSpotAfterRebirth(pawn);

            Job job = JobMaker.MakeJob(JobDefOf.SelfShutdown, pawn.ownership.AssignedMeditationSpot.Position);
            job.checkOverrideOnExpire = true;
            job.expiryInterval = 500;
            __result = job;
        }
    }


    //use spot as prefered selfshutdown spot
    [HarmonyPatch(typeof(JobGiver_SelfShutdown))]
    [HarmonyPatch("TryGiveJob")]
    public static class JobGiver_SelfShutdown_TryGiveJob_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref Job __result, JobGiver_SelfShutdown __instance, Pawn pawn)
        {
            if (pawn == null || !pawn.IsColonyMech || pawn.ownership?.AssignedMeditationSpot == null)
            {
                return;
            }

            if (MechSpotUtility.ForgetDestroyedOldSpot(pawn))
            {
                return;
            }

            if (pawn.ownership.AssignedMeditationSpot.Map != pawn.Map
                || !pawn.ownership.AssignedMeditationSpot.HasComp<CompAssignableToMech>()
                || !pawn.ownership.AssignedMeditationSpot.GetComp<CompAssignableToMech>().UseAsRecharger
                || !pawn.CanReserveAndReach(pawn.ownership.AssignedMeditationSpot, PathEndMode.OnCell, Danger.Deadly)
                || !pawn.ownership.AssignedMeditationSpot.Position.InAllowedArea(pawn)
                || pawn.ownership.AssignedMeditationSpot.GetComp<CompPowerTrader>() != null && pawn.ownership.AssignedMeditationSpot.GetComp<CompPowerTrader>().parent.IsBrokenDown()
                )
            {
                return;
            }



            if (MechSpotUtility.ShouldSelfShutDownOnSpot(pawn))
            {
                MechSpotUtility.ReclaimOldSpotAfterRebirth(pawn);
                Job job = JobMaker.MakeJob(JobDefOf.SelfShutdown, pawn.ownership.AssignedMeditationSpot.Position);
                job.checkOverrideOnExpire = true;
                job.expiryInterval = 500;
                __result = job;
            }


        }
    }






}



