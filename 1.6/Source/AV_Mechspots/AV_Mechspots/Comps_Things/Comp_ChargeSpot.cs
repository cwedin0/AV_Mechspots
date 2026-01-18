using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;


namespace AV_Mechspots
{
    public class Comp_ChargeSpot : ThingComp
    {



        public CompProperties_ChargeSpot Props => (CompProperties_ChargeSpot)props;

        private CompPowerTrader PowerTrader => parent.TryGetComp<CompPowerTrader>();       //seems to not care if it is CompPowerCharger in XML, probably becouse its its parent

        private CompAssignableToMech AssignedToMech => parent.TryGetComp<CompAssignableToMech>();

        private Comp_GiveHediffAbove CompHediffOnTop => parent.TryGetComp<Comp_GiveHediffAbove>();

        private int TickCounter = 0;
        private int ticksUntilCharge = 0;

        public int powerlevel = (int)MechspotsSettings.ChargingSocketPulseInterval;
        public EffecterDef ChargeEffect;
        public float energygainPercentage;


        public float FilledWithWaste = 0f;

        public float UnusedPowerNeed = -10f;

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            ResetCounter();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref TickCounter, "TickCounter", defaultValue: 0);
            Scribe_Values.Look(ref ticksUntilCharge, "ticksUntilCharge", defaultValue: 0);
            Scribe_Values.Look(ref powerlevel, "powerlevel", defaultValue: 2);
            Scribe_Values.Look(ref FilledWithWaste, "FilledWithWaste", defaultValue: 0);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (MechspotsSettings.ChargingSocketProducesWaste)
            {
                if (FilledWithWaste >= MechspotsSettings.ChargingSocketWasteStorageSpace)
                {
                    SpawnWastepacks(previousMap);
                }
            }
        }


        public void ResetCounter()
        {
            if (MechspotsSettings.DebugLogging)
            {
                Log.Message("[AV]Mechspots.Comps_ChargeSpot: reset counter");
            }
            TickCounter = 0;
            CalcTicksAndEnergyGain();
        }

        public void CalcTicksAndEnergyGain()
        {
            ticksUntilCharge = Props.basechargingtime / powerlevel;
            energygainPercentage = Props.basepowerpercharge * MechspotsSettings.ChargingSocketEfficiency;
        }

        public void CalcTicksTillCharge()
        {
            ticksUntilCharge = Props.basechargingtime;                  //should time scale aswell?
            if (MechspotsSettings.DebugLogging)
            {
                Log.Message("[AV]Mechspots.Comps_ChargeSpot: ticksUntilPulse: " + ticksUntilCharge.ToString());
            }
        }

        private Pawn AssignedMech()
        {
            Pawn mech = AssignedToMech.AssignedPawns.FirstOrFallback();
            return mech;
        }

        public override void CompTick()
        {

            //check if power is on
            if (parent.MapHeld.gameConditionManager.ElectricityDisabled(parent.MapHeld))
            {
                return;
            }
            if (PowerTrader != null && !PowerTrader.PowerOn)
            {
                return;
            }
            else if (PowerTrader == null)
            {
                Log.Error("[AV]Mechspots.Comps_ChargeSpot: has no power trader and wont work!");
                return;
            }



            //UpdatePowerNeed();

            //add hediff
            if (AssignedMech() != null && !AssignedMech().Dead)
            {
                Pawn pawn = AssignedMech();

                if (pawn.Position != parent.Position)  //if not on targetposition, dont apply hediff
                {
                    UpdatePowerNeed(false);
                    return;
                }
                else
                {
                    UpdatePowerNeed(true);
                }
            }
            else
            {
                UpdatePowerNeed(false);
                return;
            }



            //initilize ticks Until spawn
            if (ticksUntilCharge <= 0)
            {
                ResetCounter();
            }

            // only apply charge when its time
            TickCounter++;
            if (TickCounter < ticksUntilCharge)
            {
                return;
            }

            Charge();
            ResetCounter();    //reset so new pulsecounter can start
        }

        public int CalcChargePower(int powerlevel)
        {
            return -(int)MechspotsSettings.ChargeSpotPowerUsage + ((powerlevel - 1) * -(int)MechspotsSettings.ChargeSpotPowerUsageAddition);
        }
        public void UpdatePowerNeed(bool inuse)
        {
            if (inuse)
            {
                PowerTrader.PowerOutput = CalcChargePower(powerlevel);
            }
            else
            {
                PowerTrader.PowerOutput = UnusedPowerNeed;
            }
        }


        public void Charge()
        {
            Pawn mech = AssignedMech();

            if (mech == null)    //failsafe
            {
                return;
            }

            if (mech.RaceProps.IsMechanoid && mech.needs.energy != null)    //is mechanoid with energy need
            {
                mech.needs.energy.CurLevelPercentage += energygainPercentage;

                if (MechspotsSettings.ChargingSocketProducesWaste && mech.needs.energy.CurLevelPercentage < 1f)
                {
                    FilledWithWaste += (mech.GetStatValue(StatDefOf.WastepacksPerRecharge) * energygainPercentage);

                    if (FilledWithWaste >= MechspotsSettings.ChargingSocketWasteStorageSpace)
                    {
                        SpawnWastepacks(parent.Map);
                    }
                }

                if (MechspotsSettings.DebugLogging)
                {
                    Log.Message("[AV]Mechspots.Comps_ChargeSpot: Charged Mech by " + energygainPercentage * 100 + "% energy");
                }

                if (mech.needs.energy.CurLevel >= mech.RaceProps.maxMechEnergy)     //only when energy is full
                {
                    Hediff hediff = mech.health.hediffSet.GetFirstHediffOfDef(Props.superchargedhediff);
                    if (hediff == null)
                    {
                        mech.health.AddHediff(Props.superchargedhediff, mech.health.hediffSet.GetBrain());
                    }
                }
            }
            ResetCounter();
        }

        public void SpawnWastepacks(Map map)
        {
            while (FilledWithWaste >= 1)
            {
                Thing thing = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Wastepack"));

                int zwischenstep = Math.Min((int)Math.Floor(FilledWithWaste), ThingDefOf.Wastepack.stackLimit);
                int dropAmount = Mathf.Min(zwischenstep, MechspotsSettings.ChargingSocketWasteStorageSpace);

                thing.stackCount = dropAmount;

                GenPlace.TryPlaceThing(thing, parent.Position, map, ThingPlaceMode.Near);

                FilledWithWaste -= dropAmount;
            }
        }

        public void ChoosePowerLevel()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>()
            {
                new FloatMenuOption("AV_Gizmo_Powerlvl_extreme".Translate().CapitalizeFirst() + " (" + -CalcChargePower(4) + "W)" , delegate
                {
                    ChangePowerLevel(4);
                    //powerlevel = 4;
                    //CalcTicksAndEnergyGain();
                    if (MechspotsSettings.DebugLogging) { Log.Message("[AV]Mechspots.Comps_ChargeSpot: powerlevel changed to " + powerlevel); }
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Powerlvl_high".Translate().CapitalizeFirst() + " (" + -CalcChargePower(3) + "W)", delegate
                {
                    ChangePowerLevel(3);
                    //powerlevel = 3;
                    //CalcTicksAndEnergyGain();
                    if (MechspotsSettings.DebugLogging) { Log.Message("[AV]Mechspots.Comps_ChargeSpot: powerlevel changed to " + powerlevel); }
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Powerlvl_medium".Translate().CapitalizeFirst() + " (" + -CalcChargePower(2) + "W)", delegate
                {
                    ChangePowerLevel(2);
                    //powerlevel = 2;
                    //CalcTicksAndEnergyGain();
                    if (MechspotsSettings.DebugLogging) { Log.Message("[AV]Mechspots.Comps_ChargeSpot: powerlevel changed to " + powerlevel); }
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Powerlvl_low".Translate().CapitalizeFirst() + " (" + -CalcChargePower(1) + "W)", delegate
                {
                    ChangePowerLevel(1);
                    //powerlevel = 1;
                    //CalcTicksAndEnergyGain();
                    if (MechspotsSettings.DebugLogging) { Log.Message("[AV]Mechspots.Comps_ChargeSpot: powerlevel changed to " + powerlevel); }
                }, MenuOptionPriority.High),
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ChangePowerLevel(int ChangeTo)
        {
            foreach (Thing thing in Find.Selector.SelectedObjectsListForReading.Cast<Thing>())
            {
                Building building = thing as Building;

                if (building is Building)
                {
                    if (building.HasComp<Comp_ChargeSpot>())
                    {
                        Comp_ChargeSpot comp = building.GetComp<Comp_ChargeSpot>();
                        comp.powerlevel = ChangeTo;
                        comp.CalcTicksAndEnergyGain();
                    }
                }
            }
        }

        private Texture2D GetPowerGizmo()
        {
            if (powerlevel == 1)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_Pawercharge_1");
            }
            else if (powerlevel == 2)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_Pawercharge_2");
            }
            else if (powerlevel == 3)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_Pawercharge_3");
            }
            else if (powerlevel == 4)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_Pawercharge_4");
            }
            return null;
        }


        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.defaultLabel = "DEV: Do pulse";
                command_Action.action = delegate
                {
                    Charge();
                };
                yield return command_Action;
            }

            Command_Action choosepowerlvl_Action = new Command_Action();
            choosepowerlvl_Action.defaultLabel = "AV_Gizmo_Powerlvl".Translate(); //+ powerlevel;
            choosepowerlvl_Action.icon = GetPowerGizmo();
            choosepowerlvl_Action.defaultDesc = "AV_GizmoDesc_Powerchange".Translate().CapitalizeFirst();
            choosepowerlvl_Action.action = delegate
            {
                ChoosePowerLevel();
            };
            yield return choosepowerlvl_Action;
        }

        public override string CompInspectStringExtra()
        {
            if (Props.writeTimeForCharge || MechspotsSettings.DebugLogging)
            {
                int time = ticksUntilCharge - TickCounter;

                if (CompHediffOnTop.AssignedOnTop(AssignedMech()))
                {
                    CalcTicksAndEnergyGain();
                }

                string waste_text = "";

                if (MechspotsSettings.ChargingSocketProducesWaste)
                {
                    waste_text = "AV_ChargeSpot_Waste".Translate().CapitalizeFirst() + ": " + Math.Round(FilledWithWaste / MechspotsSettings.ChargingSocketWasteStorageSpace * 100, 1) + "% (" + Math.Round(FilledWithWaste, 1) + "/" + MechspotsSettings.ChargingSocketWasteStorageSpace + ")\n";
                }

                float energyperday = energygainPercentage * (60000f / (float)ticksUntilCharge);

                string energycharged = "\n(" + energygainPercentage * 100f + "% " + "AV_ChargeSpot_EnergyCharged".Translate() + " | " + energyperday * 100f + "% " + "AV_ChargeSpot_EnergyPerDay".Translate() + ")";

                if (parent.MapHeld.gameConditionManager.ElectricityDisabled(parent.MapHeld))
                {
                    return waste_text + "AV_ChargeSpot_NextChargeIn".Translate().CapitalizeFirst() + ": " + time.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor) + " (" + "AV_ChargeSpot_NextChargeShutDown".Translate() + ")." + energycharged;
                }
                if (!PowerTrader.PowerOn)
                {
                    return waste_text + "AV_ChargeSpot_NextChargeIn".Translate().CapitalizeFirst() + ": " + time.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor) + " (" + "AV_ChargeSpot_NextChargeUnpowered".Translate() + ")." + energycharged;
                }
                if (PowerTrader.PowerOutput == UnusedPowerNeed)
                {
                    return waste_text + "AV_ChargeSpot_NextChargeIn".Translate().CapitalizeFirst() + ": " + time.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor) + " (" + "AV_ChargeSpot_NextChargeUnused".Translate() + ")." + energycharged;
                }
                if (MechspotsSettings.DebugLogging)
                {
                    return waste_text + "AV_ChargeSpot_NextChargeIn".Translate().CapitalizeFirst() + ": " + time.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor) + energycharged + "\nTick counter: " + TickCounter + ", Ticks until pulse: " + ticksUntilCharge;
                }
                else
                {
                    return waste_text + "AV_ChargeSpot_NextChargeIn".Translate().CapitalizeFirst() + ": " + time.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor) + energycharged;
                }

            }
            return "";

        }
    }
}
