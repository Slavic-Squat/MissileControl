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
        private const string _programVersion = "1.13";
        private static string _blockTag;

        private SystemCoordinator _systemCoordinator;
        private double _maxRunTime;
        private HashSet<long> _processedGrids = new HashSet<long>();
        private bool _isInitialized = false;

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
            if (_maxRunTime < RuntimeInfo.LastRunTimeMs)
            {
                _maxRunTime = RuntimeInfo.LastRunTimeMs;
            }
            DebugEcho($"[{_programName}] | Version: {_programVersion}\n");
            DebugWrite($"[{_programName}] | Version: {_programVersion}\n", false);
            DebugEcho($"System Time: {SystemTime:F2}s\n");
            DebugWrite($"System Time: {SystemTime:F2}s\n", true);
            DebugEcho($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n");
            DebugWrite($"Last Run Time: {RuntimeInfo.LastRunTimeMs:F2}ms\n", true);
            DebugEcho($"Max Run Time: {_maxRunTime:F2}ms\n");
            DebugWrite($"Max Run Time: {_maxRunTime:F2}ms\n", true);

            if (argument != null)
            {
                CommandHandler0.RunCommands(argument);
            }
            CommunicationHandler0.Receive();

            if (_isInitialized)
            {
                _systemCoordinator.Run(SystemTime);
            }
        }

        private void GetAllBlocks()
        {
            _allGridBlocks.Clear();
            _processedGrids.Clear();
            List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(temp, b => b.IsSameConstructAs(Me) && b.CustomName.ToUpper().Contains(_blockTag.ToUpper()));
            long gridEntityID = MePb.CubeGrid.EntityId;
            GetGridBlocks(temp, gridEntityID);
        }

        private void GetGridBlocks(List<IMyTerminalBlock> blocks, long gridEntityID)
        {
            _processedGrids.Add(gridEntityID);

            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                var block = blocks[i];

                if (block.CubeGrid.EntityId == gridEntityID)
                {
                    _allGridBlocks.Add(block);
                    blocks.RemoveAt(i);
                }

                if (block is IMyMechanicalConnectionBlock)
                {
                    MyIni blockConfig = new MyIni();
                    if (!blockConfig.TryParse(block.CustomData))
                    {
                        blockConfig.Clear();
                    }
                    bool includeAttachedGrid = blockConfig.Get("Config", "IncludeAttachedGrid").ToBoolean(true);
                    blockConfig.Set("Config", "IncludeAttachedGrid", includeAttachedGrid);
                    block.CustomData = blockConfig.ToString();

                    if (!includeAttachedGrid)
                    {
                        continue;
                    }
                    long attachedGridEntityID = (block as IMyMechanicalConnectionBlock).TopGrid?.EntityId ?? 0;
                    if (attachedGridEntityID != 0 && !_processedGrids.Contains(attachedGridEntityID))
                    {
                        GetGridBlocks(blocks, attachedGridEntityID);
                    }
                }
            }
        }

        private void Init()
        {
            GetAllBlocks();
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
