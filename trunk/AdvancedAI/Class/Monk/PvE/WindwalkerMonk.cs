﻿using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals.WoWObjects;
using AdvancedAI.Helpers;
using System.Linq;

namespace AdvancedAI.Spec
{
    class WindwalkerMonk
    {        
        static LocalPlayer Me { get { return StyxWoW.Me; } }
        public static Composite CreateWMCombat
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        WindwalkerMonkPvP.CreateWMPvPCombat),
                    /*Things to fix
                     * energy capping
                     * need to check healing spheres 
                     * chi capping? need to do more checking
                    */
                    Spell.Cast("Spear Hand Strike", ret => StyxWoW.Me.CurrentTarget.IsCasting && StyxWoW.Me.CurrentTarget.CanInterruptCurrentSpellCast),

                    Spell.WaitForCastOrChannel(),

                    //Healing Spheres need to work on
                    //Spell.CastOnGround("Healing Sphere", on => Me.Location, ret => Me.HealthPercent <= 50),

                    //Tigerseye
                    Spell.Cast("Tigereye Brew", ctx => Me, ret => Me.HasAura("Tigereye Brew", 18)),

                    Spell.Cast("Energizing Brew", ctx => Me, ret => Me.CurrentEnergy < 25),

                    // Execute if we can
                    Spell.Cast("Touch of Death", ret => Me.CurrentChi >= 3 && Me.HasAura("Death Note")),

                    Spell.Cast("Tiger Palm", ret => Me.CurrentChi > 0 &&
                              (!Me.HasAura("Tiger Power") || Me.HasAura("Tiger Power") && Me.GetAuraTimeLeft("Tiger Power").TotalSeconds <= 3)),

                    //Need to do some Thinking on Adding Detox to My Self might use this pre fight not for sure yet
                    //Spell.Cast("Detox", on => Me, ret => 

                    Spell.Cast("Invoke Xuen, the White Tiger", ret => Me.CurrentTarget.IsBoss),

                    Spell.Cast("Rising Sun Kick"),

                    //Spell ID 116740 = Tigerseye Brew the dmg buff part not the brewing
                    Spell.Cast("Fists of Fury", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.IsWithinMeleeRange && Me.IsSafelyFacing(u)) >= 1 &&
                              !Me.HasAura("Energizing Brew") && Me.HasAura(116740) && Me.GetAuraTimeLeft(116740).TotalSeconds >= 4 &&
                              Me.EnergyPercent <= 65 && !PartyBuff.WeHaveBloodlust && !Me.IsMoving),

                    //Chi Talents
                    Spell.Cast("Chi Wave", ret => Me.EnergyPercent < 40),
                    //need to do math here and make it use 2 if im going to use it
                    //Spell.Cast("Zen Sphere", ret => !Me.HasAura("Zen Sphere")),

                    // free Tiger Palm or Blackout Kick... do before Jab
                    Spell.Cast("Blackout Kick", ret => Me.HasAura("Combo Breaker: Blackout Kick")),

                    Spell.Cast("Tiger Palm", ret => Me.HasAura("Combo Breaker: Tiger Palm")),

                    Spell.Cast("Spinning Crane Kick", ret => Unit.NearbyUnfriendlyUnits.Count(u => u.Distance <= 8) >= 4),

                    Spell.Cast("Expel Harm", ret => Me.CurrentChi <= 3 && Me.HealthPercent < 80),

                    Spell.Cast("Jab", ret => Me.CurrentChi <= 3),

                    // chi dump
                    Spell.Cast("Blackout Kick", ret => Me.CurrentChi >= 2 && SpellManager.Spells["Rising Sun Kick"].CooldownTimeLeft.TotalSeconds > 1)




                        );
            }
        }

        public static Composite CreateWMBuffs
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        WindwalkerMonkPvP.CreateWMPvPBuffs));
            }
        }

        #region MonkTalents
        public enum MonkTalents
        {
            Celerity = 1,//Tier 1
            TigersLust,
            Momentum,
            ChiWave,//Tier 2
            ZenSphere,
            ChiBurst,
            PowerStrikes,//Tier 3
            Ascension,
            ChiBrew,
            RingofPeace,//Tier 4
            ChargingOxWave,
            LegSweep,
            HealingElixirs,//Tier 5
            DampenHarm,
            DiffuseMagic,
            RushingJadeWind,//Tier 6
            InvokeXuen,
            ChiTorpedo
        }
        #endregion
    }
}
