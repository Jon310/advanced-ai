﻿#region

using System;
using AdvancedAI.Helpers;
using AdvancedAI.Managers;

using Styx;
using Styx.CommonBot;
using Styx.CommonBot.POI;
using Styx.CommonBot.Routines;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using System.Globalization;
using Styx.Common;
using System.Drawing;
using System.Collections.Generic;
using Styx.Common.Helpers;

#endregion

namespace AdvancedAI.Utilities
{
    public static class EventHandlers
    {
        private static bool _combatLogAttached;

        public static void Init()
        {
            // get locale specific messasge strings we'll check for
            InitializeLocalizedValues();

            // set default values for timed error states
            LastLineOfSightFailure = DateTime.MinValue;
            LastUnitNotInfrontFailure = DateTime.MinValue;
            LastShapeshiftFailure = DateTime.MinValue;

            // hook combat log event if we are debugging or not in performance critical circumstance
            //if (!StyxWoW.Me.CurrentMap.IsBattleground && !StyxWoW.Me.CurrentMap.IsRaid)
            //    AttachCombatLogEvent();

            // add context handler that reacts to context change with above rules for logging
            AdvancedAI.OnWoWContextChanged += HandleContextChanged;

            // hook PVP start timer so we can identify end of prep phase
            //PVP.AttachStartTimer();

            // also hook wow error messages
            Lua.Events.AttachEvent("UI_ERROR_MESSAGE", HandleErrorMessage);
        }

        private static void InitializeLocalizedValues()
        {
            // get localized copies of spell failure error messages
            LocalizedLineOfSightFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_LINE_OF_SIGHT");
            LocalizedUnitNotInfrontFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_UNIT_NOT_INFRONT");
            LocalizedNoPocketsToPickFailure = GetSymbolicLocalizeValue( "SPELL_FAILED_TARGET_NO_POCKETS");
            LocalizedAlreadyPickPocketedError = GetSymbolicLocalizeValue("ERR_ALREADY_PICKPOCKETED");

            // monitor ERR_ strings in Error Message Handler
            LocalizedShapeshiftMessages = new Dictionary<string, string>();

            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_CANT_INTERACT_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_MOUNT_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_NOT_WHILE_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_NO_ITEMS_WHILE_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_SHAPESHIFT_FORM_CANNOT_EQUIP");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "ERR_TAXIPLAYERSHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_CUSTOM_ERROR_125");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_CUSTOM_ERROR_99");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_NOT_SHAPESHIFT");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_FAILED_NO_ITEMS_WHILE_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_NOT_SHAPESHIFTED");
            LocalizedShapeshiftMessages.AddSymbolicLocalizeValue( "SPELL_NOT_SHAPESHIFTED_NOSPACE");
        }

        internal static void HandleContextChanged(object sender, WoWContextEventArg e)
        {
            // Since we hooked this in ctor, make sure we are the selected CC
            if (RoutineManager.Current.Name != AdvancedAI.Instance.Name)
                return;

            if (AdvancedAI.CurrentWoWContext != WoWContext.Battlegrounds && !StyxWoW.Me.CurrentMap.IsRaid)
                AttachCombatLogEvent();
            else
                DetachCombatLogEvent();
        }

        /// <summary>
        /// time of last "Target not in line of sight" spell failure.
        /// Used by movement functions for situations where the standard
        /// LoS and LoSS functions are true but still fails in WOW.
        /// See CreateMoveToLosBehavior() for usage
        /// </summary>
        public static DateTime LastLineOfSightFailure { get; set; }
        public static DateTime LastUnitNotInfrontFailure { get; set; }
        public static DateTime LastShapeshiftFailure { get; set; }

        public static WoWUnit LastLineOfSightTarget { get; set; }

        public static Dictionary<ulong, int> MobsThatEvaded = new Dictionary<ulong, int>();

        /// <summary>
        /// the value of localized values for testing certain types of spell failures
        /// </summary>
        private static string LocalizedLineOfSightFailure;
        private static string LocalizedUnitNotInfrontFailure;
        private static string LocalizedNoPocketsToPickFailure;
        private static string LocalizedAlreadyPickPocketedError;

        // a combination of errors and spell failures we search for Druid shape shift errors
        private static Dictionary<string,string> LocalizedShapeshiftMessages;

        private static void AttachCombatLogEvent()
        {
            if (_combatLogAttached)
                return;

            // DO NOT EDIT THIS UNLESS YOU KNOW WHAT YOU'RE DOING!
            // This ensures we only capture certain combat log events, not all of them.
            // This saves on performance, and possible memory leaks. (Leaks due to Lua table issues.)
            Lua.Events.AttachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);

            string filterCriteria =
                "return args[4] == UnitGUID('player')"
                + " and (args[2] == 'SPELL_MISSED'"
                + " or args[2] == 'RANGE_MISSED'"
                + " or args[2] == 'SWING_MISSED'"
                + " or args[2] == 'SPELL_CAST_FAILED')";

            if (!Lua.Events.AddFilter("COMBAT_LOG_EVENT_UNFILTERED", filterCriteria))
            {
                Logging.Write("ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
                //Logger.Write( "ERROR: Could not add combat log event filter! - Performance may be horrible, and things may not work properly!");
            }

            Logging.WriteDiagnostic("Attached combat log");
            //Logger.WriteDebug("Attached combat log");
            _combatLogAttached = true;
        }

        private static void DetachCombatLogEvent()
        {
            if (!_combatLogAttached)
                return;

            Logging.WriteDiagnostic("Removed combat log filter");
            //Logger.WriteDebug("Removed combat log filter");
            Lua.Events.RemoveFilter("COMBAT_LOG_EVENT_UNFILTERED");
            Logging.WriteDiagnostic("Detached combat log");
            //Logger.WriteDebug("Detached combat log");
            Lua.Events.DetachEvent("COMBAT_LOG_EVENT_UNFILTERED", HandleCombatLog);
            _combatLogAttached = false;
        }

        private static void HandleCombatLog(object sender, LuaEventArgs args)
        {
            if (RoutineManager.Current.Name != AdvancedAI.Instance.Name)
                return;

            var e = new CombatLogEventArgs(args.EventName, args.FireTimeStamp, args.Args);
            if (e.SourceGuid != StyxWoW.Me.Guid)
                return;

            // Logger.WriteDebug("[CombatLog] " + e.Event + " - " + e.SourceName + " - " + e.SpellName);

            switch (e.Event)
            {
                // spell_cast_failed only passes filter in Singular debug mode
                case "SPELL_CAST_FAILED":
                    Logging.WriteDiagnostic("[CombatLog] {0} {1}#{2} failure: '{3}'", e.Event, e.Spell.Name, e.SpellId, e.Args[14]);

                    if (e.Args[14].ToString() == LocalizedLineOfSightFailure)
                    {
                        ulong guid;
                        try
                        {
                            LastLineOfSightTarget = e.DestUnit;
                            guid = LastLineOfSightTarget == null ? 0 : LastLineOfSightTarget.Guid;
                        }
                        catch
                        {
                            LastLineOfSightTarget = StyxWoW.Me.CurrentTarget;
                            guid = StyxWoW.Me.CurrentTargetGuid;
                        }

                        LastLineOfSightFailure = DateTime.Now;
                        Logging.WriteDiagnostic("[CombatLog] cast fail due to los reported at {0} on target {1:X}", LastLineOfSightFailure.ToString("HH:mm:ss.fff"), e.DestGuid);
                    }
                    else if (StyxWoW.Me.Class == WoWClass.Druid)
                    {
                        if (LocalizedShapeshiftMessages.ContainsKey(e.Args[14].ToString()))
                        {
                            string symbolicName = LocalizedShapeshiftMessages[e.Args[14].ToString()];
                            LastShapeshiftFailure = DateTime.Now;
                            Logging.WriteDiagnostic("[CombatLog] cast fail due to shapeshift error '{0}' while questing reported at {1}", symbolicName, LastShapeshiftFailure.ToString("HH:mm:ss.fff"));
                        }
                    }
                    else if (StyxWoW.Me.Class == WoWClass.Rogue)
                    {
                        if (e.Args[14].ToString() == LocalizedNoPocketsToPickFailure)
                        {
                            // args on this event don't match standard SPELL_CAST_FAIL
                            // -- so, Singular only casts on current target so use that assumption
                            WoWUnit unit = StyxWoW.Me.CurrentTarget;
                            if (unit == null)
                                Logging.WriteDiagnostic("[CombatLog] has no pockets event did not have a valid unit");
                            else
                            {
                                Logging.WriteDiagnostic("[CombatLog] {0} has no pockets, blacklisting from pick pocket for 2 minutes", unit.SafeName());
                                Blacklist.Add(unit.Guid, BlacklistFlags.Node, TimeSpan.FromMinutes(2));
                            }
                        }
                    }
                    break;

#if SOMEONE_USES_LAST_SPELL_AT_SOME_POINT
                case "SPELL_AURA_APPLIED":
                case "SPELL_CAST_SUCCESS":
                    if (e.SourceGuid != StyxWoW.Me.Guid)
                    {
                        return;
                    }

                    // Update the last spell we cast. So certain classes can 'switch' their logic around.
                    Spell.LastSpellCast = e.SpellName;
                    //Logger.WriteDebug("Successfully cast " + Spell.LastSpellCast);

                    // following commented block should not be needed since rewrite of Pet summon
                    //
                    //// Force a wait for all summoned minions. This prevents double-casting it.
                    //if (StyxWoW.Me.Class == WoWClass.Warlock && e.SpellName.StartsWith("Summon "))
                    //{
                    //    StyxWoW.SleepForLagDuration();
                    //}
                    break;
#endif

                case "SWING_MISSED":
                    if (e.Args[11].ToString() == "EVADE")
                    {
                        HandleEvadeBuggedMob(args, e);
                    }
                    else if (e.Args[11].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logging.WriteDiagnostic("{0} is immune to Physical spell school", unit.Name);
                            SpellImmunityManager.Add(unit.Entry, WoWSpellSchool.Physical);
                        }
                    }
                    break;

                case "SPELL_MISSED":
                case "RANGE_MISSED":

                    if (e.Args[14].ToString() == "EVADE")
                    {
                        HandleEvadeBuggedMob(args, e);
                    }
                    else if (e.Args[14].ToString() == "IMMUNE")
                    {
                        WoWUnit unit = e.DestUnit;
                        if (unit != null && !unit.IsPlayer)
                        {
                            Logging.WriteDiagnostic("{0} is immune to {1} spell school", unit.Name, e.SpellSchool);
                            SpellImmunityManager.Add(unit.Entry, e.SpellSchool);
                        }
                    }
                    break;
            }
        }

        private static void HandleEvadeBuggedMob(LuaEventArgs args, CombatLogEventArgs e)
        {
            WoWUnit unit = e.DestUnit;
            ulong guid = e.DestGuid;

            if (unit == null && StyxWoW.Me.CurrentTarget != null)
            {
                unit = StyxWoW.Me.CurrentTarget;
                guid = StyxWoW.Me.CurrentTargetGuid;
                Logging.WriteDiagnostic("Evade: bugged mob guid:{0}, so assuming current target instead", args.Args[7]);
            }

            if (unit != null)
            {
                if (!MobsThatEvaded.ContainsKey(unit.Guid))
                    MobsThatEvaded.Add(unit.Guid, 0);

                MobsThatEvaded[unit.Guid]++;
                if (MobsThatEvaded[unit.Guid] <= 5)
                {
                    Logging.WriteDiagnostic("Mob {0} has evaded {1} times.  Keeping an eye on {2:X0} for now!", unit.SafeName(), MobsThatEvaded[unit.Guid], unit.Guid);
                }
                else
                {
                    const int MinutesToBlacklist = 5;

                    if (Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                        Logging.WriteDiagnostic("Mob {0} has evaded {1} times. Previously blacklisted {2:X0} for {3} minutes!", unit.SafeName(), MobsThatEvaded[unit.Guid], unit.Guid, MinutesToBlacklist);
                    else
                    {
                        Logging.Write("Mob {0} has evaded {1} times. Blacklisting {2:X0} for {3} minutes!", unit.SafeName(), MobsThatEvaded[unit.Guid], unit.Guid, MinutesToBlacklist);
                        Blacklist.Add(unit.Guid, BlacklistFlags.Combat, TimeSpan.FromMinutes(MinutesToBlacklist));
                        if (!Blacklist.Contains(unit.Guid, BlacklistFlags.Combat))
                        {
                            Logging.Write("error: blacklist does not contain entry for {0} so adding {1}", unit.SafeName(), unit.Guid);
                        }
                    }

                    if (BotPoi.Current.Guid == unit.Guid)
                    {
                        Logging.WriteDiagnostic("EvadeHandling: Current BotPOI type={0} is Evading, clearing now...", BotPoi.Current.Type);
                        BotPoi.Clear("Singular recognized Evade bugged mob");
                    }

                    if (StyxWoW.Me.CurrentTargetGuid == guid)
                    {
                        foreach (var target in Targeting.Instance.TargetList)
                        {
                            if (target.IsAlive && Unit.ValidUnit(target) && !Blacklist.Contains(target, BlacklistFlags.Combat))
                            {
                                Logging.Write("Setting target to {0} to get off evade bugged mob!", target.SafeName());
                                target.Target();
                                return;
                            }
                        }

                        Logging.Write("BotBase has 0 entries in Target list not blacklisted -- nothing else we can do at this point!");
                        // StyxWoW.Me.ClearTarget();
                    }
                }

            }

            /// line below was originally in Evade logic, but commenting to avoid Sleeps
            // StyxWoW.SleepForLagDuration();
        }

        private static void HandleErrorMessage(object sender, LuaEventArgs args)
        {
            bool handled = false;

            if (StyxWoW.Me.Class == WoWClass.Rogue && args.Args[0].ToString() == LocalizedAlreadyPickPocketedError)
            {
                if (StyxWoW.Me.GotTarget)
                {
                    WoWUnit unit = StyxWoW.Me.CurrentTarget;
                    Logging.WriteDiagnostic("[WowErrorMessage] already pick pocketed {0}, blacklisting from pick pocket for 2 minutes", unit.SafeName());
                    Blacklist.Add(unit.Guid, BlacklistFlags.Node, TimeSpan.FromMinutes(2));
                    handled = true;
                }
            }

            if (StyxWoW.Me.Class == WoWClass.Druid)
            {
                if (LocalizedShapeshiftMessages.ContainsKey(args.Args[0].ToString()))
                {
                    string symbolicName = LocalizedShapeshiftMessages[args.Args[0].ToString()];
                    LastShapeshiftFailure = DateTime.Now;
                    Logging.WriteDiagnostic("[WowErrorMessage] cast fail due to shapeshift error '{0}' while questing reported at {1}", symbolicName, LastShapeshiftFailure.ToString("HH:mm:ss.fff"));
                    handled = true;
                }
            }

            if (!handled)
            {
                Logging.WriteDiagnostic("[WoWRedError] {0}", args.Args[0].ToString());
            }
        }

        private static string GetSymbolicLocalizeValue(string symbolicName)
        {
            string localString = Lua.GetReturnVal<string>("return " + symbolicName, 0);
            return localString;
        }

        private static void AddSymbolicLocalizeValue( this Dictionary<string,string> dict, string symbolicName)
        {
            string localString = GetSymbolicLocalizeValue(symbolicName);
            if (!string.IsNullOrEmpty(localString) && !dict.ContainsKey(localString))
            {
                dict.Add(localString, symbolicName);
            }
        }
    }
}