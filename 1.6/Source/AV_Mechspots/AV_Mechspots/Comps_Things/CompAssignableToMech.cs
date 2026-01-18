using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using AV_Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace AV_Mechspots
{
    public class CompAssignableToMech : CompAssignableToPawn
    {
        public new CompProperties_AssignableToMech Props => (CompProperties_AssignableToMech)props;

        public int AssignmentAllowCounter = 4;  // 0 before

        public bool ForceStayAtSpot = true;    //used in JobGiver to decide beheivior

        public bool UseAsRecharger = MechspotsSettings.DefaultAllowRechargeOnSpot;    //used in JobGiver_GetEnergy_Charger_TryGiveJob_Patch to self recharge when low

        public Pawn MinifiedPawn = null;    //so we safe assignment when uninstalled

        public enum AllowedMechsForAssignment
        {
            notChoosen,         //0
            onlyCombatMechs,    //1
            onlyWorkMechs,      //2
            disallowAll,        //3
            allowAll            //4
        }
        private string AllowCounterToString()
        {
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyWorkMechs)
            {
                return "OnlyWorkMechs".Translate();
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyCombatMechs)
            {
                return "OnlyCombatMechs".Translate();
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.allowAll)
            {
                return "AllMechsAllowed".Translate();
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.disallowAll)
            {
                return "AllMechsDisallowed".Translate();
            }
            return "AV_Mechspots.CompAssignableToMech.AllowCounterToString: Could not find a meaning for AssignmentAllowCounter";
        }


        public enum AllowedRotationForAssignment
        {
            any,        //0
            random,     //1
            north,      //2
            south,      //3
            east,       //4
            west        //5
        }

        public int AllowedRotation = (int)AllowedRotationForAssignment.any;

        public override void Initialize(CompProperties props)        //set AssignmentAllowCounter depending on props
        {
            this.props = props;

            AssignmentAllowCounter = (int)AllowedMechsForAssignment.allowAll;   //force allow all

            /*
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.notChoosen)
            { 
                if (Props.AllowCombatMechs && Props.AllowNonCombatMechs)
                {
                    AssignmentAllowCounter = (int)AllowedMechsForAssignment.allowAll;
                }
                else if (!Props.AllowCombatMechs && Props.AllowNonCombatMechs)
                {
                    AssignmentAllowCounter = (int)AllowedMechsForAssignment.onlyWorkMechs;
                }
                else if (Props.AllowCombatMechs && !Props.AllowNonCombatMechs)
                {
                    AssignmentAllowCounter = (int)AllowedMechsForAssignment.onlyCombatMechs;
                }
                else
                {
                    AssignmentAllowCounter = (int)AllowedMechsForAssignment.disallowAll;
                }
                //Log.Message("Assignmentcounter initialized it is " + AssignmentAllowCounter);
               

            } */
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (MinifiedPawn != null)
            {
                if (MinifiedPawn.ownership.AssignedMeditationSpot == null)
                {
                    TryAssignPawn(MinifiedPawn);
                }
            }

            base.PostSpawnSetup(respawningAfterLoad);
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            if (assignedPawns.Any())
            {
                Pawn pawn = assignedPawns.First();
                TryUnassignPawn(pawn);
                //ForceRemovePawn(pawn);
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (assignedPawns.Any())
            {
                Pawn pawn = assignedPawns.First();
                TryUnassignPawn(pawn);
                //ForceRemovePawn(pawn);
            }
        }

        public bool WorkMechsAllowed()
        {
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyWorkMechs || AssignmentAllowCounter == (int)AllowedMechsForAssignment.allowAll)
            {
                return true;
            }
            return false;
        }

        public bool CombatMechsAllowed()
        {
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyCombatMechs || AssignmentAllowCounter == (int)AllowedMechsForAssignment.allowAll)
            {
                return true;
            }
            return false;
        }

        public override IEnumerable<Pawn> AssigningCandidates
        {
            get
            {
                if (!parent.Spawned)
                {
                    return Enumerable.Empty<Pawn>();
                }

                List<Pawn> AllSelectablePawns = new List<Pawn>();
                AllSelectablePawns = parent.Map.mapPawns.AllPawns;

                List<Pawn> AllPlayerMechs = new List<Pawn>();

                if (Props.ForceShowAll)
                {
                    Log.Warning("[AV]Mechtech.CompAssignableToMech: No List Filtering! Force Show All Pawns is active!");
                    return parent.Map.mapPawns.AllPawns.OrderByDescending((Pawn p) => CanAssignTo(p).Accepted);
                }

                for (int i = 0; i < AllSelectablePawns.Count; i++)
                {
                    if (AllSelectablePawns[i].RaceProps.IsMechanoid && AllSelectablePawns[i].Faction.IsPlayer)
                    {
                        if (Props.OnlySpecificPawnKind != null)
                        {
                            if (!WriteModInfo.PatchedThinkTreeDefs.Contains(AllSelectablePawns[i].RaceProps.thinkTreeMain) || AllSelectablePawns[i].kindDef.race.HasComp<CompMechPowerCell>())
                            //if (AllSelectablePawns[i].RaceProps.thinkTreeMain != VanillaDefOfs.Mechanoid || AllSelectablePawns[i].kindDef.race.HasComp<CompMechPowerCell>())
                            {
                                continue;   //skip if not useable
                            }

                            if (AllSelectablePawns[i].kindDef == Props.OnlySpecificPawnKind)
                            {
                                AllPlayerMechs.Add(AllSelectablePawns[i]);
                            }
                        }
                        //else
                        else if (WriteModInfo.PatchedThinkTreeDefs.Contains(AllSelectablePawns[i].RaceProps.thinkTreeMain))
                        {
                            if (CombatMechsAllowed())
                            {
                                if (!AllSelectablePawns[i].RaceProps.IsWorkMech)
                                {
                                    AllPlayerMechs.Add(AllSelectablePawns[i]);
                                }
                            }
                            if (WorkMechsAllowed())
                            {
                                if (AllSelectablePawns[i].RaceProps.IsWorkMech)
                                {
                                    AllPlayerMechs.Add(AllSelectablePawns[i]);
                                }
                            }
                        }
                    }
                }
                if (AllSelectablePawns.Count <= 0)
                {
                    Log.Warning("[AV]Mechspots.CompAssignableToMech: AllSelectablePawns List is empty");
                    return AllPlayerMechs;
                }

                if (AllPlayerMechs.Count <= 0)
                {
                    if (MechspotsSettings.DebugLogging)
                    {
                        Log.Warning("[AV]Mechspots.CompAssignableToMech: Mech List is empty, player probably has no mechs...");
                        //return parent.Map.mapPawns.AllPawns.OrderByDescending((Pawn p) => CanAssignTo(p).Accepted);  
                    }
                    return AllPlayerMechs;
                }
                //return AllPlayerMechs.OrderByDescending((Pawn p) => CanAssignTo(p).Accepted);

                return AllPlayerMechs.OrderByDescending((Pawn p) => CanAssignTo(p).Accepted).ThenBy((Pawn p) => p.KindLabel); //p.LabelShort);

            }
        }

        protected override string GetAssignmentGizmoDesc()
        {
            return "AV_GizmoDesc_Assignment".Translate().CapitalizeFirst();
        }

        public override string CompInspectStringExtra()
        {
            //string test = "\nAssignmentCounter: " + AssignmentAllowCounter.ToString();

            if (base.AssignedPawnsForReading.Count == 0)
            {
                return "Owner".Translate() + ": " + "Nobody".Translate();
            }
            if (base.AssignedPawnsForReading.Count == 1)
            {
                string deadOrDestroyed = "";
                if (MinifiedPawn != null && MinifiedPawn.Dead)
                {
                    if (MinifiedPawn.Corpse != null)
                    {
                        deadOrDestroyed = " (dead)";
                    }
                    else
                    {
                        deadOrDestroyed = " (destroyed)";
                    }
                }

                return "Owner".Translate() + ": " + base.AssignedPawnsForReading[0].Label + deadOrDestroyed;
            }
            return "";
        }

        public override bool AssignedAnything(Pawn pawn)
        {
            return pawn.ownership.AssignedMeditationSpot != null;
        }

        public override void TryAssignPawn(Pawn pawn)
        {
            if (MinifiedPawn != null)
            {
                if (MinifiedPawn.ownership.AssignedMeditationSpot == parent)
                {
                    TryUnassignPawn(MinifiedPawn);
                }
            }

            pawn.ownership.ClaimMeditationSpot((Building)parent);
            MinifiedPawn = pawn;
        }

        public override void TryUnassignPawn(Pawn pawn, bool sort = true, bool uninstall = false)
        {
            pawn.ownership.UnclaimMeditationSpot();
            //MinifiedPawn = null;  //safe it!
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            if (Scribe.mode == LoadSaveMode.PostLoadInit && assignedPawns.RemoveAll((Pawn x) => x.ownership.AssignedMeditationSpot != parent) > 0)
            {
                Log.Warning(parent.ToStringSafe() + " had pawns assigned that don't have it as an assigned meditation spot. Removing.");
            }
            //remove this in 1.6 / only relevant for old safes that still have the old assignment filtering
            AssignmentAllowCounter = (int)AllowedMechsForAssignment.allowAll;
            Scribe_Values.Look(ref AssignmentAllowCounter, "AssignmentAllowCounter");
            //remove this in 1.6 / only relevant for old safes that still have the old assignment filtering

            Scribe_Values.Look(ref AllowedRotation, "AllowedRotation");
            Scribe_Values.Look(ref ForceStayAtSpot, "ForceStayAtSpot");
            Scribe_Values.Look(ref UseAsRecharger, "UseAsRecharger");
            Scribe_References.Look(ref MinifiedPawn, "MinifiedPawn", true); //safe pawn even if dead/destroyed

            if (Scribe.mode == LoadSaveMode.PostLoadInit)   //reassign dead pawn, game otherwise deletes the assigned pawn in this building, but not in the pawn
            {
                if (MinifiedPawn != null && MinifiedPawn.Dead && MinifiedPawn.Corpse != null)
                {
                    MinifiedPawn.ownership.ClaimMeditationSpot((Building)parent);
                    assignedPawns.Clear();
                    ForceAddPawn(MinifiedPawn);
                }
            }
        }

        #region Jump

        private void Camerajump()
        {
            Pawn pawn = assignedPawns.First();

            if (CameraJumper.CanJump(pawn))
            {
                CameraJumper.TryJumpAndSelect(pawn);
            }
        }

        #endregion

        #region Allowed mechtype

        public String GetAllowedDesc()
        {
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyWorkMechs)
            {
                return "AV_Gizmo_AllowedMechs_OnlyWorkMechs".Translate();
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyCombatMechs)
            {
                return "AV_Gizmo_AllowedMechs_OnlyCombatMechs".Translate();
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.allowAll)
            {
                return "AV_Gizmo_AllowedMechs_AllMechsAllowed".Translate();
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.disallowAll)
            {
                return "AV_Gizmo_AllowedMechs_AllMechsDisallowed".Translate();
            }
            return "AV_Mechspots.CompAssignableToMech.AllowCounterToString: Could not find a meaning for AssignmentAllowCounter";
        }

        public Texture2D GetAllowedTexture()
        {
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyWorkMechs)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_Only_Workmechs");
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.onlyCombatMechs)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_Only_CombatMechs");
            }
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.disallowAll)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_No_Mechs");
            }
            return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_All_Mechs");
        }

        public void ChooseAllowedMechTypes()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>()
            {
                new FloatMenuOption("AV_Gizmo_ChooseMechType_AllMechtypes".Translate().CapitalizeFirst(), delegate
                {
                    ChangeAllowedMechType((int)AllowedMechsForAssignment.allowAll);
                    if(MechspotsSettings.DebugLogging)
                    {
                        Log.Message("Assignmentcounter changed to " + AllowCounterToString());
                    }
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_ChooseMechType_OnlyWorkMechs".Translate().CapitalizeFirst(), delegate
                {
                    ChangeAllowedMechType((int)AllowedMechsForAssignment.onlyWorkMechs);
                    if(MechspotsSettings.DebugLogging)
                    {
                        Log.Message("Assignmentcounter changed to " + AllowCounterToString());
                    }
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_ChooseMechType_OnlyCombatMechs".Translate().CapitalizeFirst(), delegate
                {
                    ChangeAllowedMechType((int)AllowedMechsForAssignment.onlyCombatMechs);
                    if(MechspotsSettings.DebugLogging)
                    {
                        Log.Message("Assignmentcounter changed to " + AllowCounterToString());
                    }
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_ChooseMechType_NoAssignment".Translate().CapitalizeFirst(), delegate
                {
                    ChangeAllowedMechType((int)AllowedMechsForAssignment.disallowAll, true);

                    if(MechspotsSettings.DebugLogging)
                    {
                        Log.Message("Assignmentcounter changed to " + AllowCounterToString());
                    }
                }, MenuOptionPriority.High),
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ChangeAllowedMechType(int ChangeTo, bool removeAssigned = false)
        {
            foreach (Thing thing in Find.Selector.SelectedObjectsListForReading)
            {
                Building building = thing as Building;

                if (building is Building)
                {
                    if (building.HasComp<CompAssignableToMech>())
                    {
                        CompAssignableToMech comp = building.GetComp<CompAssignableToMech>();
                        comp.AssignmentAllowCounter = ChangeTo;

                        if (removeAssigned)
                        {
                            if (comp.assignedPawns.Count >= 1)
                            {
                                TryUnassignPawn(comp.assignedPawns.First());
                            }
                        }

                    }

                }
            }
        }

        #endregion

        #region Rotation
        public Texture2D GetRotationIcon()
        {
            if (AllowedRotation == (int)AllowedRotationForAssignment.any)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_mechrotation_any");
            }
            else if (AllowedRotation == (int)AllowedRotationForAssignment.random)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_mechrotation_random");
            }
            else if (AllowedRotation == (int)AllowedRotationForAssignment.north)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_mechrotation_north");
            }
            else if (AllowedRotation == (int)AllowedRotationForAssignment.east)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_mechrotation_east");
            }
            else if (AllowedRotation == (int)AllowedRotationForAssignment.south)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_mechrotation_south");
            }
            else if (AllowedRotation == (int)AllowedRotationForAssignment.west)
            {
                return ContentFinder<Texture2D>.Get("UI/Gizmos/AV_mechrotation_west");
            }
            return null;
        }

        public void ChooseRotation()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>()
            {
                new FloatMenuOption("AV_Gizmo_Rotation_Any".Translate(), delegate
                {
                    ChangeRotation((int)AllowedRotationForAssignment.any);
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Rotation_Random".Translate(), delegate
                {
                    ChangeRotation((int)AllowedRotationForAssignment.random);
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Rotation_North".Translate(), delegate
                {
                    ChangeRotation((int)AllowedRotationForAssignment.north);
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Rotation_West".Translate(), delegate
                {
                    ChangeRotation((int)AllowedRotationForAssignment.west);
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Rotation_East".Translate(), delegate
                {
                    ChangeRotation((int)AllowedRotationForAssignment.east);
                }, MenuOptionPriority.High),
                new FloatMenuOption("AV_Gizmo_Rotation_South".Translate(), delegate
                {
                    ChangeRotation((int)AllowedRotationForAssignment.south);
                }, MenuOptionPriority.High),

            };
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ChangeRotation(int ChangeTo)
        {
            foreach (Thing thing in Find.Selector.SelectedObjectsListForReading)
            {
                Building building = thing as Building;

                if (building is Building)
                {
                    if (building.HasComp<CompAssignableToMech>())
                    {
                        CompAssignableToMech comp = building.GetComp<CompAssignableToMech>();
                        comp.AllowedRotation = ChangeTo;
                    }
                }
            }
        }

        #endregion


        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (ShouldShowAssignmentGizmo()) // means if player owned
            {
                /*
                //mechtype filter
                if (Props.OnlySpecificPawnKind == null)     //all mech defs are allowed
                {
                    Command_Action command_Action = new Command_Action();
                    command_Action.icon = GetAllowedTexture();
                    command_Action.defaultLabel = "AV_DescGizmo_AssignableMechTypes".Translate().CapitalizeFirst();
                    command_Action.defaultDesc = GetAllowedDesc();
                    command_Action.action = ChooseAllowedMechTypes;
                    yield return command_Action;
                }
                */

                //assign pawn
                if (AssignmentAllowCounter != (int)AllowedMechsForAssignment.disallowAll)
                {
                    //assign pawn (same as parent)
                    Command_Action command_Action = new Command_Action();
                    command_Action.defaultLabel = GetAssignmentGizmoLabel();
                    command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/AssignOwner");
                    command_Action.defaultDesc = GetAssignmentGizmoDesc();
                    command_Action.action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_AssignMechSpotOwner(this));
                        //Find.WindowStack.Add(new Dialog_AssignBuildingOwner(this));
                    };
                    command_Action.hotKey = KeyBindingDefOf.Misc4;
                    if (!Props.noAssignablePawnsDesc.NullOrEmpty() && !AssigningCandidates.Any())
                    {
                        command_Action.Disable(Props.noAssignablePawnsDesc);
                    }
                    yield return command_Action;
                }

                if (Props.OnlySpecificPawnKind != null || AssignmentAllowCounter != (int)AllowedMechsForAssignment.disallowAll)
                {
                    //rotate
                    Command_Action choosepowerlvl_Action = new Command_Action();
                    choosepowerlvl_Action.defaultLabel = "AV_DescGizmoLabel_Rotation".Translate().CapitalizeFirst();
                    choosepowerlvl_Action.icon = GetRotationIcon();
                    choosepowerlvl_Action.defaultDesc = "AV_DescGizmo_Rotation".Translate().CapitalizeFirst();
                    choosepowerlvl_Action.action = delegate
                    {
                        ChooseRotation();
                    };
                    yield return choosepowerlvl_Action;

                }

                if (assignedPawns.Any())
                {
                    if (!assignedPawns[0].RaceProps.IsWorkMech)
                    {
                        Command_Toggle command_Toggle = new Command_Toggle();
                        command_Toggle.defaultLabel = "AV_GizmoLabel_FightToggle".Translate().CapitalizeFirst();
                        command_Toggle.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/AV_ForceFightOnSpot");
                        command_Toggle.defaultDesc = "AV_GizmoDesc_FightToggle".Translate().CapitalizeFirst();
                        command_Toggle.isActive = () => ForceStayAtSpot;
                        command_Toggle.toggleAction = delegate
                        {
                            ForceStayAtSpot = !ForceStayAtSpot;
                        };
                        yield return command_Toggle;
                    }

                    //jump to pawn gizmo

                    Command_Action command_Action = new Command_Action();
                    command_Action.defaultLabel = "AV_GizmoLabel_JumpToMech".Translate().CapitalizeFirst();
                    command_Action.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/AV_where_mech");
                    command_Action.defaultDesc = "AV_GizmoDesc_JumpToMech".Translate().CapitalizeFirst();
                    command_Action.action = delegate
                    {
                        Camerajump();
                    };
                    yield return command_Action;


                    Command_Toggle command_Toggle_recharge = new Command_Toggle();
                    command_Toggle_recharge.defaultLabel = "AV_GizmoLabel_RechargeToggle".Translate().CapitalizeFirst();
                    command_Toggle_recharge.icon = ContentFinder<Texture2D>.Get("UI/Gizmos/AV_RechargeOnSpot");
                    command_Toggle_recharge.defaultDesc = "AV_GizmoDesc_RechargeToggle".Translate().CapitalizeFirst();
                    command_Toggle_recharge.isActive = () => UseAsRecharger;
                    command_Toggle_recharge.toggleAction = delegate
                    {
                        UseAsRecharger = !UseAsRecharger;
                    };
                    yield return command_Toggle_recharge;

                }

                if (Find.Selector.SelectedObjectsListForReading.Count <= 1)
                {
                    Command_Action copy_Action = new Command_Action();
                    copy_Action.defaultLabel = "CommandCopyZoneSettingsLabel".Translate().CapitalizeFirst();
                    copy_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings");
                    copy_Action.defaultDesc = "AV_DescGizmo_Copy".Translate().CapitalizeFirst();
                    copy_Action.action = delegate
                    {
                        MechSpotUtility.CopyFromSpot(this);
                    };
                    yield return copy_Action;
                }

                if (MechSpotUtility.CanPaste)
                {
                    Command_Action paste_Action = new Command_Action();
                    paste_Action.defaultLabel = "CommandPasteZoneSettingsLabel".Translate().CapitalizeFirst();
                    paste_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings");
                    paste_Action.defaultDesc = "AV_DescGizmo_Paste".Translate().CapitalizeFirst();
                    paste_Action.action = delegate
                    {
                        MechSpotUtility.PasteToSpot(this);
                    };
                    yield return paste_Action;
                }
            }
        }



        public override void DrawGUIOverlay()
        {
            if (AssignmentAllowCounter == (int)AllowedMechsForAssignment.disallowAll)    //only draw if assignment is allowed from spot
            {
                return;
            }

            if (Find.CameraDriver.CurrentZoom != 0 || !PlayerCanSeeAssignments)          //only draw if not zoomed out too much
            {
                return;
            }
            if (MechspotsSettings.ShowUnassignedOnMechSpots && !assignedPawns.Any())     // draws unassigned
            {
                Color defaultThingLabelColor_ = GenMapUI.DefaultThingLabelColor;
                GenMapUI.DrawThingLabel(parent, "Unowned".Translate(), defaultThingLabelColor_);
            }
            if (MechspotsSettings.ShowAssignedOnMechSpots)     // draws assigned
            {
                if (assignedPawns.Any())
                {
                    if (assignedPawns.Count == 1)
                    {
                        Pawn pawn = assignedPawns[0];

                        if (parent.PositionHeld == pawn.Position)    //dont draw if assigned mech is ontop (would draw twice, from here and from pawn)
                        {
                            return;
                        }
                        if (CanDrawOverlayForPawn(pawn))
                        {
                            Color defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;

                            // if (FrameworkSettings.UseColoredMechlinkRange && MechspotsSettings.AllowMechaitorColoredAssignmentLabels)
                            // {
                            //     try
                            //     {
                            //         defaultThingLabelColor = Current.Game.GetComponent<GameComponent_Mechlink>().GetColor(pawn.GetOverseer());
                            //     }
                            //     catch
                            //     {
                            //         defaultThingLabelColor = GenMapUI.DefaultThingLabelColor;
                            //     }
                            // }
                            // else
                            // {
                            //     if (MechspotsSettings.AllowDeadColoredAssignmentLabels)
                            //     {
                            //         if (pawn.Dead && pawn.Corpse == null)
                            //         {
                            //             defaultThingLabelColor = Color.red;
                            //         }
                            //         else if (pawn.Destroyed)
                            //         {
                            //             defaultThingLabelColor = Color.yellow;
                            //         }
                            //     }
                            // }

                            GenMapUI.DrawThingLabel(parent, assignedPawns[0].LabelShort, defaultThingLabelColor);
                        }
                    }
                }
            }
        }
    }
}
