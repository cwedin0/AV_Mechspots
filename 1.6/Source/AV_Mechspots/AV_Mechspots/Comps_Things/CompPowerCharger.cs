using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace AV_Mechspots
{
    public class CompPowerCharger : CompPowerTrader
    {
        private Comp_ChargeSpot ChargeSpot => parent.TryGetComp<Comp_ChargeSpot>();
        public override string CompInspectStringExtra()
        {
            if (parent is MinifiedThing)
            {
                if (MechspotsSettings.DebugLogging)
                {
                    Log.Message("AV_Mechspots.CompPowerCharger: I am minified");
                }
            }
            else
            {
                if (ChargeSpot == null)
                {
                    Log.Error("[AV]Mechspots.CompPowerCharger: has no Comp_ChargerSpot ");
                    return "ERROR [AV]Mechspots.CompPowerCharger: has no Comp_ChargerSpot";
                }
                float powerneeded = -PowerOutput;//-ChargeSpot.CalcChargePower();

                string text = "AV_DescPowerNeeded".Translate().CapitalizeFirst() + ": " + powerneeded + "W";

                return text;
            }

            return "";
        }
    }
}
