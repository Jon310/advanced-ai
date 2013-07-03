﻿using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.Helpers;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using AdvancedAI.Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Action = Styx.TreeSharp.Action;

namespace AdvancedAI.Spec
{
    class BloodDeathknight// : AdvancedAI
    {
        //public override WoWClass Class { get { return WoWClass.DeathKnight; } }
        //public override WoWSpec Spec { get { return WoWSpec.DeathKnightBlood; } }
        static LocalPlayer Me { get { return StyxWoW.Me; } }

        internal static Composite CreateBDKCombat
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        BloodDeathknightPvP.CreateBDKPvPCombat));
            }
        }

        internal static Composite CreateBDKBuffs
        {
            get
            {
                return new PrioritySelector(
                    new Decorator(ret => AdvancedAI.PvPRot,
                        BloodDeathknightPvP.CreateBDKPvPBuffs));
            }
        }
    }
}
