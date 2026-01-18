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
    public class CompProperties_AssignableToMech : CompProperties_AssignableToPawn //CompProperties
    {
        public bool ForceShowAll = false;
        public bool AllowCombatMechs = false;
        public bool AllowNonCombatMechs = false;
        public bool ShowProgressbar = false;
        public PawnKindDef OnlySpecificPawnKind;

        //public bool OnlyGuarding = true;


        public CompProperties_AssignableToMech()
        {
            compClass = typeof(CompAssignableToMech);
        }

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            return base.ConfigErrors(parentDef);
        }

        public override void PostLoadSpecial(ThingDef parent)   //is this even needed???
        {
            if (parent.thingClass == typeof(Building_Bed))
            {
                maxAssignedPawnsCount = BedUtility.GetSleepingSlotsCount(parent.size);
            }
        }
    }
}
