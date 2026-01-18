using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Verse;
using Verse.AI;
using Verse.Noise;
using Verse.Sound;
using static AV_Mechspots.CompAssignableToMech;
using static HarmonyLib.Code;
using static RimWorld.MechClusterSketch;
using static UnityEngine.GraphicsBuffer;

namespace AV_Mechspots
{
    public class Comp_GiveHediffAbove : ThingComp   //ssustainer and soundstuff from CauseHediff_AoE
    {
        private Sustainer activeSustainer;

        private bool lastIntervalActive;

        private CompProperties_GiveHediffAbove Props => (CompProperties_GiveHediffAbove)props;

        private CompPowerTrader CompPowerTrader => parent.TryGetComp<CompPowerTrader>();

        private CompAssignableToPawn CompAssigned => parent.TryGetComp<CompAssignableToPawn>();

        private CompRefuelable CompRefuelable => parent.TryGetComp<CompRefuelable>();   //neutroamine cooling???


        public override void CompTick()
        {
            if (parent.Map == null)
            {
                return;
            }
            if (!parent.IsHashIntervalTick(Props.checkInterval))
            {
                return;
            }

            if (CompPowerTrader != null && !CompPowerTrader.PowerOn)    //this is also when electricity is offline from events?
            {
                return;
            }

            if (CompRefuelable != null && !CompRefuelable.HasFuel)
            {
                return;
            }

            MaintainSustainer();

            lastIntervalActive = false;

            if (Props.onlyassigned)
            {
                ApplyAssigned();
            }
            else
            {
                ApplyAOE();
            }
        }

        public bool AssignedOnTop(Pawn pawn)       //for other classes to catch
        {
            if (pawn == null || pawn.Dead)
            {
                return false;
            }
            if (parent.Position == pawn.Position)
            {
                return true;
            }
            return false;
        }

        private void MaintainSustainer()
        {
            if (lastIntervalActive && Props.activeSound != null)
            {
                if (activeSustainer == null || activeSustainer.Ended)
                {
                    activeSustainer = Props.activeSound.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(parent)));
                }
                activeSustainer.Maintain();
            }
            else if (activeSustainer != null)
            {
                activeSustainer.End();
                activeSustainer = null;
            }
        }


        #region Assigned Content

        private Pawn AssignedPawn()
        {
            Pawn pawn = CompAssigned.AssignedPawns.FirstOrFallback();
            if (pawn != null)
            {
                if (Props.onlyTargetMechs && !pawn.RaceProps.IsMechanoid)
                {
                    Log.Error("[AV]Mechspots.CompGiveHediffAbove: Assigned Pawn is not a mechanoid while this mod only allows mechanoids. Check your XML...");
                }
                return pawn;
            }
            return null;
        }

        private void ApplyAssigned()
        {
            if (CompAssigned == null)
            {
                return;
            }
            else
            {
                Pawn pawn = AssignedPawn();
                if (pawn == null)
                {
                    return;
                }

                if (parent.Map == null || pawn.Map == null || parent.Map != pawn.Map)
                {
                    return;
                }

                if (parent.Position != pawn.Position)
                {
                    return;
                }
                else
                {
                    ApplyHediff(pawn);
                }
            }
        }

        #endregion


        #region AOE Content

        private bool IsPawnAffectedAOE(Pawn target)
        {
            //if (CompPowerTrader != null && !CompPowerTrader.PowerOn)
            //{
            //    return false;
            // }
            if (target.Dead || target.health == null)
            {
                return false;
            }
            if (target.Position.DistanceTo(parent.Position) <= Props.range)
            {
                if (target.Map == null || parent.Map != target.Map)  //is needed for it to not effect pawns on other maps, even if we use parent.Map.mapPawns.AllPawnsSpawned....
                {
                    return false;
                }

                if (Props.onlyTargetMechs)
                {
                    return target.RaceProps.IsMechanoid;
                }
                return true;
            }
            return false;
        }

        public void ApplyAOE()
        {
            foreach (Pawn item in parent.Map.mapPawns.AllPawnsSpawned)
            {
                if (!IsPawnAffectedAOE(item))
                {
                    continue;
                }
                ApplyHediff(item);
                lastIntervalActive = true;
            }
        }

        #endregion


        private void ApplyHediff(Pawn item)
        {
            Hediff hediff = item.health.hediffSet.GetFirstHediffOfDef(Props.hediff);
            if (hediff == null)
            {
                hediff = item.health.AddHediff(Props.hediff, item.health.hediffSet.GetBrain());
                hediff.Severity = Props.severity;
                HediffComp_Link hediffComp_Link = hediff.TryGetComp<HediffComp_Link>();
                if (hediffComp_Link != null)
                {
                    hediffComp_Link.drawConnection = false;
                    hediffComp_Link.other = parent;
                }
            }
            HediffComp_Disappears hediffComp_Disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (hediffComp_Disappears == null)
            {
                Log.Error("[AV]Mechspots.Comp_GiveHediffAbove:CompCauseHediff_AoE has a hediff in props which does not have a HediffComp_Disappears");
            }
            else
            {
                hediffComp_Disappears.ticksToDisappear = Props.checkInterval + 1;
            }
        }

        public override void PostDraw()
        {
            if (Props.drawLine)
            {
                if (Props.onlyassigned)
                {
                    Pawn pawn = AssignedPawn();
                    if (pawn != null)
                    {
                        if (pawn.Map == parent.Map)
                        {
                            GenDraw.DrawLineBetween(pawn.DrawPos, parent.DrawPos);
                        }
                    }
                }
                else
                {
                    if (!Find.Selector.SelectedObjectsListForReading.Contains(parent))
                    {
                        return;
                    }
                    foreach (Pawn item in parent.Map.mapPawns.AllPawnsSpawned)
                    {
                        if (IsPawnAffectedAOE(item))
                        {
                            GenDraw.DrawLineBetween(item.DrawPos, parent.DrawPos);
                        }
                    }
                }
            }
        }
    }
}
