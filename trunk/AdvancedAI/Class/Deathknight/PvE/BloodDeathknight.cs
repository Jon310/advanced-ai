﻿using AdvancedAI.Managers;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using AdvancedAI.Helpers;
using System.Linq;
using Action = Styx.TreeSharp.Action;

namespace AdvancedAI.Spec
{
    class BloodDeathknight
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        internal static int BloodRuneSlotsActive { get { return Me.GetRuneCount(0) + Me.GetRuneCount(1); } }
        internal static int FrostRuneSlotsActive { get { return Me.GetRuneCount(2) + Me.GetRuneCount(3); } }
        internal static int UnholyRuneSlotsActive { get { return Me.GetRuneCount(4) + Me.GetRuneCount(5); } }

        internal static Composite CreateBDKCombat
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        BloodDeathknightPvP.CreateBDKPvPCombat),
                    Common.CreateInterruptBehavior(),
                    Spell.WaitForCastOrChannel(),
                    CreateApplyDiseases(),
                    CreateBDKBuffs,
                    new Action(ret => { Item.UseHands(); return RunStatus.Failure; }),
                    new Decorator(ret => AdvancedAI.BossMechs && DeadlyBossMods.Bars.Count(u => DeadlyBossMods.FindBarByPartialId("Evil Spell").Id == u.Id) > 0 && DeadlyBossMods.FindBarByPartialId("Evil Spell").TimeLeft.TotalSeconds < 10, // DeadlyBossMods.FindBarByPartialId("Triple Puncture CD").TimeLeft.TotalSeconds < 3,
                        Spell.Cast("Strangulate")),
                    new Throttle(1, 2,
                        new PrioritySelector(
                            Spell.Cast("Blood Boil",
                                ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 &&
                                        TalentManager.IsSelected((int)DeathKnightTalents.RoillingBlood) &&
                                        !Me.HasAura("Unholy Blight") &&
                                        AdvancedAI.Aoe &&
                                        ShouldSpreadDiseases))),
                    new Throttle(1, 2,
                        new PrioritySelector(
                            Spell.Cast("Pestilence",
                                ret => Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 2 &&
                                !Me.HasAura("Unholy Blight") &&
                                AdvancedAI.Aoe &&
                                ShouldSpreadDiseases))),
                    Spell.Cast("Death Strike", ret => Me.HealthPercent < 40 || 
                                                      (Me.UnholyRuneCount + Me.FrostRuneCount + Me.DeathRuneCount >= 4) || 
                                                      (Me.HealthPercent < 90 && (Me.GetAuraTimeLeft("Blood Shield").TotalSeconds < 2))),
                    Spell.Cast("Blood Boil", ret => Me.HasAura(81141)),
                    Spell.Cast("Rune Tap", ret => Me.HealthPercent <= 80 && Me.BloodRuneCount >= 1),
                    Spell.Cast("Rune Strike", ret => (Me.CurrentRunicPower >= 60 || Me.HealthPercent > 90) && Me.CurrentRunicPower >= 30 && 
                                                     (Me.UnholyRuneCount == 0 || Me.FrostRuneCount == 0) && !Me.HasAura("Lichborne")),
                    Spell.Cast("Soul Reaper", ret => Me.BloodRuneCount > 0 && Me.CurrentTarget != null && Me.CurrentTarget.HealthPercent < 35), 
                    Spell.Cast("Heart Strike", ret => Me.BloodRuneCount >= 1),
                    Spell.CastOnGround("Death and Decay", ret => Me.CurrentTarget.Location, ret => AdvancedAI.Aoe && Unit.UnfriendlyUnitsNearTarget(12f).Count() >= 3),
                    Spell.Cast("Horn of Winter", ret => Me.CurrentRunicPower < 90));
            }
        }

        internal static Composite CreateBDKBuffs
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        BloodDeathknightPvP.CreateBDKPvPBuffs),
                    Spell.Cast("Bone Shield", ret => !Me.HasAura("Bone Shield")),
                    Spell.Cast("Vampiric Blood", ret => Me.HealthPercent < 60
                            && (!Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"))),
                    Spell.Cast("Icebound Fortitude", ret => StyxWoW.Me.HealthPercent < 30
                            && (!Me.HasAnyAura("Bone Shield", "Vampiric Blood", "Dancing Rune Weapon", "Lichborne", "Icebound Fortitude"))),
                    Spell.Cast("Might of Ursoc", ret => Me.HealthPercent < 60),
                    Spell.Cast("Raise Dead", ret => Me.HealthPercent < 45 && !GhoulMinionIsActive),
                    Spell.Cast("Death Pact", ret => StyxWoW.Me.HealthPercent < 45),
                    Spell.Cast("Army of the Dead", ret => StyxWoW.Me.HealthPercent < 40),
                    Spell.Cast("Empower Rune Weapon", ret => StyxWoW.Me.HealthPercent < 45 && !SpellManager.CanCast("Death Strike")),
                    Spell.Cast("Blood Tap", ret => Me.HasAura("Blood Charge", 5) && (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0)),
                    Spell.Cast("Plague Leech", ret => CanCastPlagueLeech));
            }
        }

        public static Composite CreateApplyDiseases()
        {
            return new Throttle(
                new PrioritySelector(
                    Spell.Cast("Unholy Blight", ret => SpellManager.CanCast("Unholy Blight")
                            && Unit.NearbyUnfriendlyUnits.Any(u => (u.IsPlayer || u.IsBoss()) && u.Distance < (u.MeleeDistance() + 5) && u.HasAuraExpired("Blood Plague"))),
                    Spell.Cast("Outbreak", ret => Me.CurrentTarget.HasAuraExpired("Frost Fever") || Me.CurrentTarget.HasAuraExpired("Blood Plague")),
                    Spell.Cast("Icy Touch", ret => !Me.CurrentTarget.IsImmune(WoWSpellSchool.Frost) && Me.CurrentTarget.HasAuraExpired("Frost Fever")),
                    Spell.Cast("Plague Strike", ret => Me.CurrentTarget.HasAuraExpired("Blood Plague"))));
        }

        internal static bool ShouldSpreadDiseases
        {
            get
            {
                int radius = TalentManager.HasGlyph("Pestilence") ? 15 : 10;

                return !Me.CurrentTarget.HasAuraExpired("Blood Plague")
                    && !Me.CurrentTarget.HasAuraExpired("Frost Fever")
                    && Unit.NearbyUnfriendlyUnits.Any(u => Me.SpellDistance(u) < radius && u.HasAuraExpired("Blood Plague") && u.HasAuraExpired("Frost Fever"));
            }
        }

        internal static bool CanCastPlagueLeech
        {
            get
            {
                if (!Me.GotTarget)
                    return false;

                int frostFever = (int)Me.CurrentTarget.GetAuraTimeLeft("Frost Fever").TotalMilliseconds;
                int bloodPlague = (int)Me.CurrentTarget.GetAuraTimeLeft("Blood Plague").TotalMilliseconds;
                return (frostFever.Between(350, 3000) || bloodPlague.Between(350, 3000))
                    && (BloodRuneSlotsActive == 0 || FrostRuneSlotsActive == 0 || UnholyRuneSlotsActive == 0);
            }
        }

        internal const uint Ghoul = 26125;
        internal static bool GhoulMinionIsActive
        {
            get { return Me.Minions.Any(u => u.Entry == Ghoul); }
        }

        #region DeathKnightTalents
        public enum DeathKnightTalents
        {
            RoillingBlood = 1,//Tier 1
            PlagueLeech,
            UnholyBlight,
            LichBorne,//Tier 2
            AntiMagicZone,
            Purgatory,
            DeathsAdvance,//Tier 3
            Chilblains,
            Asphyxiate,
            DeathPact,//Tier 4
            DeathSiphon,
            Conversion,
            BloodTap,//Tier 5
            RunicEmpowerment,
            RunicCorruption,
            GorefiendsGrasp,//Tier 6
            RemoreselessWinter,
            DesecratedGround
        }
        #endregion
    }
}
