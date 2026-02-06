using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public static Action<string> DebugEcho { get; private set; }
        public static Action<string, bool> DebugWrite { get; private set; }
        public static IMyProgrammableBlock MePb { get; private set; }
        public static IMyGridTerminalSystem GTS { get; private set; }
        public static IReadOnlyList<IMyTerminalBlock> AllGridBlocks => _allGridBlocks;
        public static IMyIntergridCommunicationSystem IGCS { get; private set; }
        public static IMyGridProgramRuntimeInfo RuntimeInfo { get; private set; }
        public static double SystemTime { get; private set; }
        public static MyIni Config { get; private set; }
        public static CommandHandler CommandHandler0 { get; private set; }
        public static CommunicationHandler CommunicationHandler0 { get; private set; }
        public static int DebugCounter { get; set; } = 0;

        private static List<IMyTerminalBlock> _allGridBlocks = new List<IMyTerminalBlock>();
        private const string _programName = "MissileControl";
        private const string _programVersion = "1.11";
        private static string _gridBlockTag;

        private SystemCoordinator _systemCoordinator;

        public Program()
        {
            DebugEcho = Echo;
            DebugWrite = (s, b) => Me.GetSurface(0).WriteText(s, b);
            GTS = GridTerminalSystem;
            IGCS = IGC;
            RuntimeInfo = Runtime;
            MePb = Me;
            Runtime.UpdateFrequency = UpdateFrequency.None;

            Config = new MyIni();
            if (!Config.TryParse(MePb.CustomData))
            {
                Config.Clear();
            }

            _gridBlockTag = Config.Get("Config", "GridBlockTag").ToString("NOT_SET");
            Config.Set("Config", "GridBlockTag", _gridBlockTag);
            GridTerminalSystem.GetBlocksOfType(_allGridBlocks, b => b.IsSameConstructAs(Me) && b.CustomName.ToUpper().Contains(_gridBlockTag.ToUpper()));

            long secureBroadcastPIN = Config.Get("Config", "SecureBroadcastPIN").ToInt64(123456);
            Config.Set("Config", "SecureBroadcastPIN", secureBroadcastPIN);
            CommunicationHandler0 = new CommunicationHandler(0, secureBroadcastPIN);

            CommandHandler0 = new CommandHandler();
            _systemCoordinator = new SystemCoordinator();

            MePb.CustomData = Config.ToString();
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            SystemTime += RuntimeInfo.TimeSinceLastRun.TotalSeconds;
            DebugEcho($"[{_programName}] | Version: {_programVersion}\n");
            DebugWrite($"[{_programName}] | Version: {_programVersion}\n", false);
            DebugEcho($"System Time: {SystemTime:F2}s\n");
            DebugWrite($"System Time: {SystemTime:F2}s\n", true);
            DebugEcho($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n");
            DebugWrite($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n", true);

            if (argument != null)
            {
                CommandHandler0.RunCommands(argument);
            }
            CommunicationHandler0.Receive();
            _systemCoordinator.Run(SystemTime);
        }
    }
}
