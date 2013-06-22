﻿using Styx;
using Styx.WoWInternals;
using Styx.Pathing;
using Styx.Common;

namespace AdvancedAI.Managers
{
    internal static class MovementManager
    {
        /// <summary>
        /// True: Singular movement is currently disabled.  This could be due to a setting,
        /// the current Bot, or a Hotkey toggled.  All code needing to check if
        /// movement is allowed should call this or MovementManager.IsMovementEnabled
        /// </summary>
        //public static bool IsMovementDisabled
        //{
        //    get
        //    {
        //        if (IsBotMovementDisabled)
        //            return true;

        //        if (SingularSettings.Instance.AllowMovement == AllowMovementType.Auto)
        //            return IsManualMovementBotActive;

        //        return SingularSettings.Instance.AllowMovement != AllowMovementType.All;
        //    }
        //}

        /// <summary>
        /// True: Bot movement should be disabled by Singular.  This is controlled
        /// only by the state of the Hotkeys toggle for movement since we only want
        /// to interfere with bot movement when the user tells us to
        /// </summary>
        //private static bool IsBotMovementDisabled
        //{
        //    get
        //    {
        //        return !HotkeyDirector.IsMovementEnabled || (SingularRoutine.IsQuestBotActive && StyxWoW.Me.InVehicle);
        //    }
        //}


        /// <summary>
        /// True: Singular Class specific movement is currently disabled.  This could be due to a setting,
        /// the current Bot, or a Hotkey toggled.  This should be used by all class specific spells
        /// such as Charge, Roll, Shadow Step, Wild Charge
        /// </summary>
        //public static bool IsClassMovementAllowed
        //{
        //    get
        //    {
        //        if (IsBotMovementDisabled)
        //            return false;

        //        if (SingularSettings.Instance.AllowMovement == AllowMovementType.Auto)
        //            return !IsManualMovementBotActive;

        //        return SingularSettings.Instance.AllowMovement >= AllowMovementType.ClassSpecificOnly;
        //    }
        //}

        /// <summary>
        /// True: a bot requiring user interaction is running (LazyRaider, RaidBot, etc.)
        /// </summary>
        /// <remarks>
        /// query the active bot only on a bot event and then cache the result.  we don't
        /// need to check more often than that
        /// </remarks>
        public static bool IsManualMovementBotActive { get; private set; }

        // static INavigationProvider _prevNavigation = null;
        static IPlayerMover _prevPlayerMover = null;
        static IStuckHandler _prevStuckHandler = null;

        #region Initialization

        internal static void Init()
        {
            MovementManager.Start();
            //SingularRoutine.OnBotEvent += (src, arg) =>
            //{
            //    IsManualMovementBotActive = SingularRoutine.IsBotInUse("LazyRaider", "Raid Bot");
            //    if (arg.Event == SingularBotEvent.BotStart)
            //        MovementManager.Start();
            //    else if (arg.Event == SingularBotEvent.BotStop)
            //        MovementManager.Stop();
            //    else if (arg.Event == SingularBotEvent.BotChanged)
            //        MovementManager.Change();
            //};
        }

        /// <summary>
        /// Update the current status of MovementManager.  Should be called when an
        /// outside influence has possibly caused a configuration change, such as
        /// settings window, user interface bot change, etc.
        /// </summary>
        public static void Update()
        {
                AllowMovement();
        }

        #endregion

        #region Event Handlers

        private static void Start()
        {
            Update();
        }

        private static void Stop()
        {
            // restore in case we had taken over 
            AllowMovement();
        }

        private static void Change()
        {
            // restore in case we had taken over
            AllowMovement();
        }

        #endregion

        #region Movement Handler Primitives

        private static void AllowMovement()
        {
            if (Navigator.PlayerMover == pNoPlayerMovement)
            {
                Logging.WriteDiagnostic("MovementManager: restoring Player Movement");
                //Logger.WriteDebug("MovementManager: restoring Player Movement");
                Navigator.PlayerMover = _prevPlayerMover;
            }

            //if (Navigator.NavigationProvider == pNoNavigation)
            //{
            //    Logger.WriteDebug("MovementManager: restoring Player Navigation");
            //    Navigator.NavigationProvider = _prevNavigation;
            //}

            if (Navigator.NavigationProvider.StuckHandler == pNoStuckHandling)
            {
                Logging.WriteDiagnostic("MovementManager: restoring Stuck Handler");
                //Logger.WriteDebug("MovementManager: restoring Stuck Handler");
                Navigator.NavigationProvider.StuckHandler = _prevStuckHandler;
            }
        }

        private static void SuppressMovement()
        {
            if (Navigator.PlayerMover != pNoPlayerMovement)
            {
                Logging.WriteDiagnostic("MovementManager: setting No Player Movement");
                //Logger.WriteDebug("MovementManager: setting No Player Movement");
                _prevPlayerMover = Navigator.PlayerMover;
                Navigator.PlayerMover = pNoPlayerMovement;
            }

            //if (Navigator.NavigationProvider != pNoNavigation)
            //{
            //    Logger.WriteDebug("MovementManager: setting No Player Navigation");
            //    _prevNavigation = Navigator.NavigationProvider;
            //    Navigator.NavigationProvider = pNoNavigation;
            //}

            if (Navigator.NavigationProvider.StuckHandler != pNoStuckHandling )
            {
                Logging.WriteDiagnostic("MovementManager: setting No Stuck Handling");
                //Logger.WriteDebug("MovementManager: setting No Stuck Handling");
                _prevStuckHandler = Navigator.NavigationProvider.StuckHandler ;
                Navigator.NavigationProvider.StuckHandler  = pNoStuckHandling ;
            }
        }

        #endregion

        #region Local Classes for No Movement Providers

        class NoNavigation : INavigationProvider
        {
            public bool CanNavigateFully(WoWPoint from, WoWPoint to, int maxHops) { return true; }
            public bool Clear() { return true; }
            public WoWPoint[] GeneratePath(WoWPoint from, WoWPoint to) { return new WoWPoint[] { new WoWPoint(from.X, from.Y, from.Z) }; }
            public MoveResult MoveTo(WoWPoint location) { return MoveResult.Moved; }
            public float PathPrecision { get; set; }
            public IStuckHandler StuckHandler { get; set; }

            public NoNavigation()
            {
                StuckHandler = new NoStuckHandling();
            }
        }

        class NoPlayerMovement : IPlayerMover
        {
            public void Move(WoWMovement.MovementDirection direction)   { }
            public void MoveStop() { }
            public void MoveTowards(WoWPoint location) { }
        }

        class NoStuckHandling : IStuckHandler
        {
            public bool IsStuck() { return false; }
            public void Reset() { }
            public void Unstick() { }
        }

        private static NoNavigation pNoNavigation = new NoNavigation();
        private static NoPlayerMovement pNoPlayerMovement = new NoPlayerMovement();
        private static NoStuckHandling pNoStuckHandling = new NoStuckHandling();
 
        #endregion
    }
}