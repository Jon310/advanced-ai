﻿using AdvancedAI.Managers;
using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using AdvancedAI.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using Action = Styx.TreeSharp.Action;

namespace AdvancedAI.Spec
{
    class RestorationShaman
    {
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        static WoWUnit healtarget { get { return HealerManager.FindLowestHealthTarget(); } }
        private static string[] _doNotHeal;
        public static Composite CreateRSCombat
        {
            get
            {
                HealerManager.NeedHealTargeting = true;
                var cancelHeal = Math.Max(95, Math.Max(93, Math.Max(55, 25)));
                return new PrioritySelector(ctx => HealerManager.Instance.TargetList.Any(t => t.IsAlive) && !Me.Mounted,
                    Spell.WaitForCastOrChannel(),
                    new Decorator(ret => AdvancedAI.PvPRot,
                        RestorationShamanPvP.CreateRSPvPCombat),
                    new Decorator(ret => Me.Combat || healtarget.Combat || healtarget.GetPredictedHealthPercent() <= 99,
                        new PrioritySelector(
                            //Totems.CreateTotemsBehavior(),
                            RollRiptide(),
                            TidalWaves(),
                            new Decorator(ret => AdvancedAI.Dispell,
                                Dispelling.CreateDispelBehavior()),
                            Item.UsePotionAndHealthstone(40),
                            new Throttle(1, 1,
                                new PrioritySelector(
                                    Spell.Cast("Earth Shield", 
                                        on => GetBestEarthShieldTargetInstance(),
                                        ret => !GetBestEarthShieldTargetInstance().HasAura("Earth Shield")))),
                            Spell.Cast("Spirit Link Totem", 
                                on => healtarget,
                                ret => HealerManager.Instance.TargetList.Count(p => p.GetPredictedHealthPercent() < 40 && p.Distance <= Totems.GetTotemRange(WoWTotem.SpiritLink)) >= 3 && AdvancedAI.Burst),
                            new Decorator(ret => healtarget.HealthPercent < 25,
                                new Sequence(
                                    Spell.Cast("Ancestral Swiftness"),
                                    Spell.Cast("Greater Healing Wave", 
                                        on => healtarget))),
                            Spell.Cast("Healing Tide Totem",
                                ret => Me.Combat && HealerManager.Instance.TargetList.Count(p => p.GetPredictedHealthPercent() < 60 && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide)) >= (Me.GroupInfo.IsInRaid ? 3 : 2) && AdvancedAI.Burst),
                            Spell.Cast("Healing Stream Totem",
                                ret => Me.Combat && !Totems.Exist(WoWTotemType.Water) && HealerManager.Instance.TargetList.Any(p => p.GetPredictedHealthPercent() < 95 && p.Distance <= Totems.GetTotemRange(WoWTotem.HealingTide))),
                            Spell.Cast("Mana Tide Totem", 
                                ret => !Totems.Exist(WoWTotemType.Water) && Me.ManaPercent < 80),
                            HealingRain(),
                            ChainHeal(),
                            Spell.Cast("Greater Healing Wave", 
                                on => healtarget,
                                ret => healtarget.HealthPercent < 55,
                                cancel => healtarget.HealthPercent > cancelHeal),
                            Spell.Cast("Healing Wave", 
                                on => healtarget,
                                ret => healtarget.HealthPercent < 93,
                                cancel => healtarget.HealthPercent > cancelHeal),
                            Spell.Cast("Healing Surge",
                                on => healtarget,
                                ret => healtarget.HealthPercent < 25,
                                cancel => healtarget.HealthPercent > cancelHeal),
                            Spell.Cast("Ascendance",
                                ret => HealerManager.Instance.TargetList.Count(p => p.GetPredictedHealthPercent() < 50) >= 4 && !Me.HasAura("Ascendance") && AdvancedAI.Burst),
                            Riptide(),
                            new Decorator(ret => AdvancedAI.InterruptsEnabled,
                                Common.CreateInterruptBehavior()),
                            //Totems.CreateTotemsBehavior(),
                            Spell.Cast("Lightning Bolt",
                                on => BoltTar(), 
                                ret => TalentManager.HasGlyph("Telluric Currents"), 
                                cancel => healtarget.HealthPercent < 70))));
            }
        }

        public static Composite CreateRSBuffs
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        RestorationShamanPvP.CreateRSPvPBuffs),
                    Spell.Cast("Water Shield", on => Me, ret => !Me.HasMyAura("Water Shield")),
                    CreateShamanImbueMainHandBehavior(Imbue.Earthliving, Imbue.Flametongue),
                    CreateRSCombat);
            }
        }

        private static ulong guidLastEarthShield = 0;
        private static WoWUnit GetBestEarthShieldTargetInstance()
        {
            WoWUnit target = null;

            if (Unit.NearbyFriendlyPlayers.Any(m => m.HasMyAura("Earth Shield")))
                return null;

            if (Me.GroupInfo.IsInParty)
            {
                target = Group.Tanks.FirstOrDefault(IsValidEarthShieldTarget);
                if (Me.Combat && target == null)
                {
                    target = HealerManager.Instance.TargetList.Where(u => u.Combat && IsValidEarthShieldTarget(u))
                        .OrderByDescending(u => u.MaxHealth)
                        .FirstOrDefault();
                }
            }

            guidLastEarthShield = target != null ? target.Guid : 0;
            return target;
        }

        private enum Imbue
        {
            None = 0,
            Flametongue = 5,
            Earthliving = 3345,
        }

        private static Decorator CreateShamanImbueMainHandBehavior(params Imbue[] imbueList)
        {
            return new Decorator(ret => CanImbue(Me.Inventory.Equipped.MainHand),
                new PrioritySelector(
                    imb => imbueList.FirstOrDefault(i => SpellManager.HasSpell(i.ToString() + " Weapon")),

                    new Decorator(
                        ret => Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id != (int)ret
                            && SpellManager.HasSpell(((Imbue)ret).ToString() + " Weapon")
                            && SpellManager.CanCast(((Imbue)ret).ToString() + " Weapon", null, false, false),
                        new Sequence(
                            new Action(ret => Lua.DoString("CancelItemTempEnchantment(1)")),
                            new WaitContinue(1,
                                ret => Me.Inventory.Equipped.MainHand != null && (Imbue)Me.Inventory.Equipped.MainHand.TemporaryEnchantment.Id == Imbue.None,
                                new ActionAlwaysSucceed()),
                            new DecoratorContinue(ret => ((Imbue)ret) != Imbue.None,
                                new Sequence(
                                    new Action(ret => SpellManager.Cast(((Imbue)ret).ToString() + " Weapon", null)),
                                    new Action(ret => SetNextAllowedImbueTime())
                                    )
                                )
                            )
                        )
                    )
                );
        }

        private static DateTime nextImbueAllowed = DateTime.Now;

        public static bool CanImbue(WoWItem item)
        {
            if (item != null && item.ItemInfo.IsWeapon)
            {
                // during combat, only mess with imbues if they are missing
                if (Me.Combat && item.TemporaryEnchantment.Id != 0)
                    return false;

                // check if enough time has passed since last imbue
                // .. guards against detecting is missing immediately after a cast but before buff appears
                // .. (which results in imbue cast spam)
                if (nextImbueAllowed > DateTime.Now)
                    return false;

                switch (item.ItemInfo.WeaponClass)
                {
                    case WoWItemWeaponClass.Axe:
                        return true;
                    case WoWItemWeaponClass.AxeTwoHand:
                        return true;
                    case WoWItemWeaponClass.Dagger:
                        return true;
                    case WoWItemWeaponClass.Fist:
                        return true;
                    case WoWItemWeaponClass.Mace:
                        return true;
                    case WoWItemWeaponClass.MaceTwoHand:
                        return true;
                    case WoWItemWeaponClass.Polearm:
                        return true;
                    case WoWItemWeaponClass.Staff:
                        return true;
                    case WoWItemWeaponClass.Sword:
                        return true;
                    case WoWItemWeaponClass.SwordTwoHand:
                        return true;
                }
            }

            return false;
        }

        public static void SetNextAllowedImbueTime()
        {
            // 2 seconds to allow for 0.5 seconds plus latency for buff to appear
            nextImbueAllowed = DateTime.Now + new TimeSpan(0, 0, 0, 0, 500); // 1500 + (int) StyxWoW.WoWClient.Latency << 1);
        }

        //string ToSpellName(this Imbue i)
        //{
        //    return i.ToString() + " Weapon";
        //}

        private static Imbue GetImbue(WoWItem item)
        {
            if (item != null)
                return (Imbue)item.TemporaryEnchantment.Id;

            return Imbue.None;
        }

        public static bool IsImbuedForDPS(WoWItem item)
        {
            Imbue imb = GetImbue(item);
            return imb == Imbue.Flametongue;
        }

        public static bool IsImbuedForHealing(WoWItem item)
        {
            return GetImbue(item) == Imbue.Earthliving;
        }

        private static bool IsValidEarthShieldTarget(WoWUnit unit)
        {
            if (unit == null || !unit.IsValid || !unit.IsAlive || Unit.GroupMembers.All(g => g.Guid != unit.Guid) || unit.Distance > 99)
                return false;

            return unit.HasMyAura("Earth Shield") || !unit.HasAnyAura("Earth Shield", "Water Shield", "Lightning Shield");
        }

        private static Composite HealingRain()
        {
            return new PrioritySelector(
                context => GetBestHealingRainTarget(),
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Sequence(
                            BuffUnleashLife(on => HealerManager.Instance.TargetList.FirstOrDefault()),
                            Common.CreateWaitForLagDuration(ret => Spell.IsGlobalCooldown()),
                            new WaitContinue(TimeSpan.FromMilliseconds(1500),
                                             until => !Spell.IsGlobalCooldown(LagTolerance.No),
                                             new ActionAlwaysSucceed()),
                            Spell.CastOnGround("Healing Rain", on => (WoWUnit) on, req => true, false)))));
        }

        private static Composite ChainHeal()
        {
            return new PrioritySelector(
                ctx => GetBestChainHealTarget(),
                new Decorator(
                    ret => ret != null,
                    new PrioritySelector(
                        new Sequence(
                            Spell.Cast("Riptide", on => (WoWUnit) on, ret => !((WoWUnit)ret).HasAura("Riptide")),
                            new Wait(TimeSpan.FromMilliseconds(1500), until => !Spell.IsGlobalCooldown(),
                                     new ActionAlwaysFail())),
                        Spell.Cast("Chain Heal", on => (WoWUnit) on))));
        }

        private static Composite RollRiptide()
        {
            return new PrioritySelector(
                Spell.Cast("Riptide", on =>
                {
                    WoWUnit unit = GetBestRiptideTankTarget();
                    _doNotHeal = new[] { "Reshape Life", "Parasitic Growth", "Cyclone", "Dominate Mind", "Agressive Behavior", "Beast of Nightmares", "Corrupted Healing" };
                    if (unit != null && Spell.CanCastHack("Riptide", unit, skipWowCheck: true) && !unit.HasAnyAura(_doNotHeal))
                    {
                        return unit;
                    }
                    return null;
                }));
        }

        private static Composite TidalWaves()
        {
            return new Decorator(
                ret => IsTidalWavesNeeded,
                new PrioritySelector(
                    Spell.Cast("Riptide", on =>
                    {
                        WoWUnit unit = GetBestRiptideTarget();
                        return unit;
                    }, ret => !GetBestRiptideTarget().HasMyAura("Riptide"))));
        }

        private static bool IsTidalWavesNeeded
        {
            get
            {
                const int HW = 331;
                const int GHW = 77472;
                const int HS = 8004;

                if (Me.Level < 50 || Me.Specialization != WoWSpec.ShamanRestoration)
                    return false;

                // WoWAura tw = Me.GetAuraByName("Tidal Waves");
                uint stacks = Me.GetAuraStacks("Tidal Waves");

                // 2 stacks means we don't have an issue
                if (stacks >= 2)
                {
                    return false;
                }

                // 1 stack? special case and a spell that will consume it is in progress or our audit count shows its gone
                int castId = Me.CastingSpellId;
                string castname = Me.CastingSpell == null ? "(none)" : Me.CastingSpell.Name;
                if (stacks == 1 && castId != HW && castId != GHW && castId != HS)
                {
                    return false;
                }

                return true;
            }
        }

        private static Composite Riptide()
        {
            return new Decorator(ret =>
                    {
                        int rollCount = HealerManager.Instance.TargetList.Count(u => u.IsAlive && u.HasMyAura("Riptide"));
                        // Logger.WriteDebug("GetBestRiptideTarget:  currently {0} group members have my Riptide", rollCount);
                        return rollCount < 2;
                    },
                new PrioritySelector(
                    Spell.Cast("Riptide", on =>
                        {
                            // if tank needs Riptide, bail out on Rolling as they have priority
                            if (GetBestRiptideTankTarget() != null)
                                return null;
                            // get the best target from all wowunits in our group
                            WoWUnit unit = GetBestRiptideTarget();
                            return unit;
                        }, ret => !GetBestRiptideTarget().HasMyAura("Riptide"))));
        }

        private static WoWUnit GetBestRiptideTarget()
        {
            WoWUnit ripTarget = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
            return ripTarget;
        }

        private static WoWUnit GetBestChainHealTarget()
        {
            if (!Me.IsInGroup())
                return null;

            if (!Spell.CanCastHack("Chain Heal", Me, skipWowCheck: true))
            {
                return null;
            }

            // search players with Riptide first
            var targetInfo = ChainHealRiptidePlayers
                .Select(p => new { Unit = p, Count = Clusters.GetClusterCount(p, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange) })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => Group.Tanks.Any(t => t.Guid == v.Unit.Guid))
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            WoWUnit target = targetInfo == null ? null : targetInfo.Unit;
            int count = targetInfo == null ? 0 : targetInfo.Count;

            // too few hops? then search any group member
            if (count < 3)
            {
                target = Clusters.GetBestUnitForCluster(ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
                if (target != null)
                {
                    count = Clusters.GetClusterCount(target, ChainHealPlayers, ClusterType.Chained, ChainHealHopRange);
                    if (count < 3)
                        target = null;
                }
            }

            return target;
        }

        private static WoWUnit GetBestHealingRainTarget()
        {
            if (!Me.IsInGroup() || !Me.Combat)
                return null;

            if (!Spell.CanCastHack("Healing Rain", Me, skipWowCheck: true))
            {
                // Logger.WriteDebug("GetBestHealingRainTarget: CanCastHack says NO to Healing Rain");
                return null;
            }

            // note: expensive, but worth it to optimize placement of Healing Rain by
            // finding location with most heals, but if tied one with most living targets also
            // build temp list of targets that could use heal and are in range + radius
            List<WoWUnit> coveredTargets = HealerManager.Instance.TargetList
                .Where(u => u.IsAlive && u.DistanceSqr < 50 * 50)
                .ToList();
            List<WoWUnit> coveredRainTargets = coveredTargets
                .Where(u => u.HealthPercent < 95)
                .ToList();

            // search all targets to find best one in best location to use as anchor for cast on ground
            var t = coveredTargets
                .Where(u => u.DistanceSqr < 40 * 40)
                .Select(p => new
                {
                    Player = p,
                    Count = coveredRainTargets.Count(pp => pp.Location.DistanceSqr(p.Location) < 10 * 10),
                    Covered = coveredTargets.Count(pp => pp.Location.DistanceSqr(p.Location) < 10 * 10)
                })
                .OrderByDescending(v => v.Count)
                .ThenByDescending(v => v.Covered)
                .DefaultIfEmpty(null)
                .FirstOrDefault();

            if (t != null && t.Count >= 3)
            {
                return t.Player;
            }

            return null;

        }

        private static Composite BuffUnleashLife(UnitSelectionDelegate onUnit)
        {
            return new PrioritySelector(
                Spell.Cast("Unleash Elements",
                    onUnit,
                    ret => IsImbuedForHealing(Me.Inventory.Equipped.MainHand) && (Me.Combat || onUnit(ret).Combat)),
                new ActionAlwaysSucceed()
                );
        }

        private static float ChainHealHopRange
        {
            get
            {
                return TalentManager.Glyphs.Contains("Chaining") ? 25f : 12.5f;
            }
        }

        private static IEnumerable<WoWUnit> ChainHealPlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.GetPredictedHealthPercent() < 90)
                    .Select(u => (WoWUnit)u);
            }
        }

        private static IEnumerable<WoWUnit> ChainHealRiptidePlayers
        {
            get
            {
                // TODO: Decide if we want to do this differently to ensure we take into account the T12 4pc bonus. (Not removing RT when using CH)
                return HealerManager.Instance.TargetList
                    .Where(u => u.IsAlive && u.DistanceSqr < 40 * 40 && u.GetPredictedHealthPercent() < 90 && u.HasMyAura("Riptide"))
                    .Select(u => (WoWUnit)u);
            }
        }

        private static WoWUnit GetBestRiptideTankTarget()
        {
            WoWUnit ripTarget = Group.Tanks.Where(u => !u.HasAura("Reshape Life") && !u.HasAura("Parasitic Growth") && u.IsAlive && u.Combat && u.DistanceSqr < 40 * 40 && !u.HasMyAura("Riptide") && u.InLineOfSpellSight).OrderBy(u => u.HealthPercent).FirstOrDefault();
            return ripTarget;
        }

        private static WoWUnit BoltTar()
        {
            var bolttarget = Unit.NearbyUnitsInCombatWithMe.FirstOrDefault(u => u.IsTargetingUs() && u.IsHostile && Me.IsSafelyFacing(u));
            return bolttarget;
        }

        #region Spell math

        private static float _spellPower = Me.SpellPowerModifierPercent;
        private static float _haste = Me.HasteModifier;
        // && SpellManager.Spells["Healing Wave"].SpellEffect1.Amplitude > 10

        public static float SpellPower(LocalPlayer me)
        {
            _secondaryStats.Refresh();
            return _secondaryStats.SpellPower;
        }

        //where Mastery Bonus = (-1 x % Mastery x Target HP) + % Mastery |||| 4.0
        //Mastery bonus on heal = (1 – (% HP of Target/100)) x Maximum Mastery contribution |||| 5.0
        private double _masteryBonus = (1 - (healtarget.HealthPercent / 100)) * _secondaryStats.MasteryCR;

        private double _averageHeal(string healname)
        {
            double heal = SpellManager.Spells[healname].SpellEffect1.Amplitude;
            var average = _secondaryStats.Crit*(heal*_secondaryStats.MasteryCR*1.5*1.286) +
                                      (_secondaryStats.Crit - 100)*(heal*_secondaryStats.MasteryCR);
            return average;
        }

        //private double _average = _secondaryStats.Crit*(heal*_secondaryStats.MasteryCR*1.5*1.286) +
        //                              (_secondaryStats.Crit - 100)*(heal*_secondaryStats.MasteryCR);
        //Average Heal = Probability of a Crit Heal x (Base Heal x Mastery Bonus x 1.5 x 1.286) + Probability of a Non-Crit Heal x (Base Heal x Mastery Bonus)


        internal static SecondaryStats _secondaryStats;

        internal class SecondaryStats
        {
            public float MeleeHit { get; set; }

            public float SpellHit { get; set; }

            public float Expertise { get; set; }

            public float MeleeHaste { get; set; }

            public float SpellHaste { get; set; }

            public float SpellPen { get; set; }

            public float Mastery { get; set; }

            public float MasteryCR { get; set; }

            public float Crit { get; set; }

            public float Resilience { get; set; }

            public float PvpPower { get; set; }

            public float AttackPower { get; set; }

            public float Power { get; set; }

            public float Intellect { get; set; }

            public float SpellPower { get; set; }

            public SecondaryStats()
            {
                Refresh();
            }

            public void Refresh()
            {
                try
                {
                    MeleeHit = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(CR_HIT_MELEE)", 0);
                    SpellHit = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(CR_HIT_SPELL)", 0);
                    Expertise = StyxWoW.Me.Expertise;
                    MeleeHaste = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(CR_HASTE_MELEE)", 0);
                    SpellHaste = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(CR_HASTE_SPELL)", 0);
                    SpellPen = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetSpellPenetration()", 0);
                    Mastery = StyxWoW.Me.Mastery;
                    MasteryCR = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(CR_MASTERY)", 0);
                    Crit = StyxWoW.Me.CritPercent;
                    Resilience = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(COMBAT_RATING_RESILIENCE_CRIT_TAKEN)", 0);
                    PvpPower = Styx.WoWInternals.Lua.GetReturnVal<float>("return GetCombatRating(CR_PVP_POWER)", 0);
                    AttackPower = StyxWoW.Me.AttackPower;
                    Power = Styx.WoWInternals.Lua.GetReturnVal<float>("return select(7,UnitDamage(\"player\"))", 0);
                    Intellect = StyxWoW.Me.Intellect;
                    SpellPower = Styx.WoWInternals.Lua.GetReturnVal<float>("return math.max(GetSpellBonusDamage(1),GetSpellBonusDamage(2),GetSpellBonusDamage(3),GetSpellBonusDamage(4),GetSpellBonusDamage(5),GetSpellBonusDamage(6),GetSpellBonusDamage(7))", 0);
                }
                catch
                {
                    Logging.Write(" Lua Failed in SecondaryStats");
                }

            }
        }

        #endregion

        #region ShamanTalents
        public enum ShamanTalents
        {
            NaturesGuardian = 1,
            StoneBulwarkTotem,
            AstralShift,
            FrozenPower,
            EarthgrabTotem,
            WindwalkTotem,
            CallOfTheElements,
            TotemicRestoration,
            TotemicProjection,
            ElementalMastery,
            AncestralSwiftness,
            EchoOfTheElements,
            HealingTideTotem,
            AncestralGuidance,
            Conductivity,
            UnleashedFury,
            PrimalElementalist,
            ElementalBlast
        }
        #endregion
    }
}
