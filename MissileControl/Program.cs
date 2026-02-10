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
        private const string _programVersion = "1.17";
        private static string _blockTag;

        private SystemCoordinator _systemCoordinator;
        private HashSet<long> _validGridIDs = new HashSet<long>();
        private bool _isInitialized = false;
        private MovingAverage _runTimeInfo = new MovingAverage(100);
        private StringBuilder _debugStringBuilder = new StringBuilder();
        private IMyTextSurface _debugScreen;
        private int _runCounter = 0;

        public Program()
        {
            _debugScreen = Me.GetSurface(0);
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

            _blockTag = Config.Get("Config", "BlockTag").ToString("NOT_SET");
            Config.Set("Config", "BlockTag", _blockTag);

            long secureBroadcastPIN = Config.Get("Config", "SecureBroadcastPIN").ToInt64(123456);
            Config.Set("Config", "SecureBroadcastPIN", secureBroadcastPIN);
            CommunicationHandler0 = new CommunicationHandler(0, secureBroadcastPIN);

            CommandHandler0 = new CommandHandler();
            CommandHandler0.RegisterCommand("TURN_ON", (args) => TurnOn());
            CommandHandler0.RegisterCommand("TURN_OFF", (args) => TurnOff());
            CommandHandler0.RegisterCommand("INIT", (args) => Init());

            MePb.CustomData = Config.ToString();

            Me.CubeGrid.CustomName = "Missile";
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            SystemTime += RuntimeInfo.TimeSinceLastRun.TotalSeconds;
            _runTimeInfo.Add(RuntimeInfo.LastRunTimeMs);

            if (_runCounter % 10 == 0)
            {
                _debugStringBuilder.Clear();
                _debugStringBuilder.AppendLine($"[{_programName}] | Version: {_programVersion}");
                _debugStringBuilder.Append("System Time: ").AppendFormat("{0:F2}s", SystemTime).AppendLine();
                _debugStringBuilder.Append("Last Run Time: ").AppendFormat("{0:F2}ms", RuntimeInfo.LastRunTimeMs).AppendLine();
                _debugStringBuilder.Append("Max Run Time: ").AppendFormat("{0:F2}ms", _runTimeInfo.Max).AppendLine();
                _debugStringBuilder.Append("Avg Run Time: ").AppendFormat("{0:F2}ms", _runTimeInfo.Average).AppendLine();
                _debugStringBuilder.Append("--------------------------------------");
                _debugScreen.WriteText(_debugStringBuilder.ToString());
            }

            if (argument != null)
            {
                CommandHandler0.RunCommands(argument);
            }
            CommunicationHandler0.Receive();

            if (_isInitialized)
            {
                _systemCoordinator.Run(SystemTime);
            }

            _runCounter++;
            if (_runCounter >= int.MaxValue) _runCounter = 0;
        }

        private void GetBlocks()
        {
            _allGridBlocks.Clear();
            _validGridIDs.Clear();
            List<IMyMechanicalConnectionBlock> temp = new List<IMyMechanicalConnectionBlock>();
            GridTerminalSystem.GetBlocksOfType(temp, b => b.IsSameConstructAs(Me) && b.CustomName.ToUpper().Contains(_blockTag.ToUpper()));
            foreach (var block in temp)
            {
                MyIni blockConfig = new MyIni();
                if (!blockConfig.TryParse(block.CustomData))
                {
                    blockConfig.Clear();
                }
                bool includeAttachedGrid = blockConfig.Get("Config", "IncludeAttachedGrid").ToBoolean(true);
                blockConfig.Set("Config", "IncludeAttachedGrid", includeAttachedGrid);
                block.CustomData = blockConfig.ToString();

                if (includeAttachedGrid && block.IsAttached)
                {
                    _validGridIDs.Add(block.TopGrid.EntityId);
                }
            }
            _validGridIDs.Add(Me.CubeGrid.EntityId);

            GridTerminalSystem.GetBlocksOfType(_allGridBlocks, b => _validGridIDs.Contains(b.CubeGrid.EntityId) && b.CustomName.ToUpper().Contains(_blockTag.ToUpper()));
        }

        private void Init()
        {
            GetBlocks();
            _systemCoordinator = new SystemCoordinator();
            Me.CustomData = Config.ToString();
            _isInitialized = true;
        }

        private void TurnOn()
        {
            RuntimeInfo.UpdateFrequency = UpdateFrequency.Update1;
        }

        private void TurnOff()
        {
            RuntimeInfo.UpdateFrequency = UpdateFrequency.None;
        }
    }
}
