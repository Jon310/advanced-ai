﻿using System.Windows.Forms;
using Styx.Common;
using Styx.WoWInternals;

namespace AdvancedAI
{
    partial class AdvancedAI
    {
        public static bool InterruptsEnabled { get; set; }
        public static bool PvPRot { get; set; }
        public static bool PvERot { get; set; }
        public static bool Burst { get; set; }
        public static bool HexFocus { get; set; }
        public static bool Movement { get; set; }
        public static bool UsefulStuff { get; set; }
        public static bool Aoe { get; set; }
        public static bool BossMechs { get; set; }
        public static bool Weave { get; set; }
        public static bool Dispell { get; set; }
        public static bool Trace { get; set; }

        protected virtual void UnregisterHotkeys()
        {
            HotkeysManager.Unregister("Toggle Interrupt");
            HotkeysManager.Unregister("PvP Toggle");
            HotkeysManager.Unregister("PvE Toggle");
            HotkeysManager.Unregister("Burst");
            HotkeysManager.Unregister("Hex Focus");
            HotkeysManager.Unregister("Movement");
            HotkeysManager.Unregister("Useful Stuff");
            HotkeysManager.Unregister("AOE");
            HotkeysManager.Unregister("Boss Mechs");
            HotkeysManager.Unregister("Weave");
            HotkeysManager.Unregister("Dispelling");
            HotkeysManager.Unregister("Trace");
        }
        protected virtual void RegisterHotkeys()
        {
            HotkeysManager.Register("Trace",
                Keys.T,
                ModifierKeys.Alt,
                o =>
                {
                    Trace = !Trace;
                    Logging.Write("Trace enabled: " + Trace);
                });
            Dispell = false;

            HotkeysManager.Register("Dispelling",
                Keys.D,
                ModifierKeys.Alt,
                o =>
                {
                    Dispell = !Dispell;
                    Logging.Write("Dispelling enabled: " + Dispell);
                });
            Dispell = true;

            HotkeysManager.Register("Toggle Interupt",
                Keys.NumPad1,
                ModifierKeys.Alt,
                o =>
                {
                    InterruptsEnabled = !InterruptsEnabled;
                    Logging.Write("Interrupts enabled: " + InterruptsEnabled);
                });
            InterruptsEnabled = true;

            HotkeysManager.Register("PvP Toggle",
            Keys.P,
            ModifierKeys.Alt,
            o =>
            {
                PvPRot = !PvPRot;
                Logging.Write("PvP enabled: " + PvPRot);
                Lua.DoString("print('PvP Enabled: " + PvPRot + "')");
            });
            PvPRot = false;

            HotkeysManager.Register("PvE Toggle",
            Keys.O,
            ModifierKeys.Alt,
            o =>
            {
                PvERot = !PvERot;
                Logging.Write("PvE enabled: " + PvERot);
                Lua.DoString("print('PvE Enabled: " + PvERot + "')");
            });
            PvERot = false;

            HotkeysManager.Register("Burst",
            Keys.NumPad1,
            ModifierKeys.Control,
            o =>
            {
                Burst = !Burst;
                Logging.Write("Burst enabled: " + Burst);
                Lua.DoString("print('Burst Enabled: " + Burst + "')");
            });
            Burst = true;

            HotkeysManager.Register("Hex Focus",
            Keys.NumPad2,
            ModifierKeys.Control,
            o =>
            {
                HexFocus = !HexFocus;
                Logging.Write("Hex Focus enabled: " + HexFocus);
                Lua.DoString("print('Hex Focus Enabled: " + HexFocus + "')");
            });
            HexFocus = false;

            HotkeysManager.Register("Movement Enabled",
            Keys.M,
            ModifierKeys.Alt,
            o =>
            {
                Movement = !Movement;
                Logging.Write("Movement Enabled: " + Movement);
                Lua.DoString("print('Movement Enabled: " + Movement + "')");
            });
            Movement = false;

            HotkeysManager.Register("Tier Bonus",
            Keys.NumPad3,
            ModifierKeys.Control,
            o =>
            {
                UsefulStuff = !UsefulStuff;
                Logging.Write("Useful Stuff enabled: " + UsefulStuff);
                Lua.DoString("print('Useful Stuff Enabled: " + UsefulStuff + "')");
            });
            UsefulStuff = false;

            HotkeysManager.Register("AOE",
            Keys.NumPad4,
            ModifierKeys.Control,
            o =>
            {
                Aoe = !Aoe;
                Logging.Write("AOE enabled: " + Aoe);
                Lua.DoString("print('AOE Enabled: " + Aoe + "')");
            });
            Aoe = true;

            HotkeysManager.Register("Boss Mechs",
            Keys.NumPad5,
            ModifierKeys.Control,
            o =>
            {
                BossMechs = !BossMechs;
                Logging.Write("Boss Mechs enabled: " + BossMechs);
                Lua.DoString("print('Boss Mechs Enabled: " + BossMechs + "')");
            });
            BossMechs = false;

            HotkeysManager.Register("Weave",
            Keys.NumPad6,
            ModifierKeys.Control,
            o =>
            {
                Weave = !Weave;
                Logging.Write("Weave enabled: " + Weave);
                Lua.DoString("print('Weave Enabled: " + Weave + "')");
            });
            Weave = true;

        }

    }
}