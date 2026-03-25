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
        public static IReadOnlyList<IMyTerminalBlock> AllBlocks => _allBlocks;
        public static IMyIntergridCommunicationSystem IGCS { get; private set; }
        public static IMyGridProgramRuntimeInfo RuntimeInfo { get; private set; }
        public static double SystemTime { get; private set; }
        public static MyIni Config { get; private set; }
        public static CommandHandler CommandHandlerInst { get; private set; }
        public static CommunicationHandler CommunicationHandlerInst { get; private set; }

        private static List<IMyTerminalBlock> _allBlocks = new List<IMyTerminalBlock>();
        private const string _programName = "MissileControl";
        private const string _programVersion = "1.37";

        private SystemCoordinator _systemCoordinator;
        private bool _isInitialized = false;
        private MovingAverage _runTimeInfo = new MovingAverage(100);
        private StringBuilder _debugStringBuilder = new StringBuilder();
        private IMyTextSurface _debugScreen;
        string _lastExceptionMsg = string.Empty;
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

            string blockTag = Config.Get("Config", "BlockTag").ToString("NOT_SET");
            Config.Set("Config", "BlockTag", blockTag);
            MePb.CustomData = Config.ToString();

            CommandHandlerInst = new CommandHandler();
            CommandHandlerInst.RegisterCommand("INIT", (args) => Init());
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
                _debugStringBuilder.AppendLine("--------------------");
                _debugStringBuilder.Append(_lastExceptionMsg);
                _debugScreen.WriteText(_debugStringBuilder);
            }

            if (argument != null)
            {
                CommandHandlerInst.RunCommands(argument);
            }

            if (_isInitialized && (updateSource & (UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100)) != 0)
            {
                _systemCoordinator.Run(SystemTime);
            }

            _runCounter++;
            if (_runCounter >= int.MaxValue) _runCounter = 0;
        }

        private void Init()
        {
            _isInitialized = false;
            _lastExceptionMsg = string.Empty;
            _allBlocks.Clear();
            Config.Clear();
            CommunicationHandlerInst?.Reset();
            CommandHandlerInst.Clear();

            CommandHandlerInst.RegisterCommand("INIT", (args) => Init());

            if (!Config.TryParse(MePb.CustomData))
            {
                Config.Clear();
            }

            string blockTag = Config.Get("Config", "BlockTag").ToString("NOT_SET");
            Config.Set("Config", "BlockTag", blockTag);

            long secureBroadcastPIN = Config.Get("Config", "SecureBroadcastPIN").ToInt64(123456);
            Config.Set("Config", "SecureBroadcastPIN", secureBroadcastPIN);

            Runtime.UpdateFrequency = GetUpdateFrequency(Config.Get("Config", "UpdateFrequency").ToString("NONE"));
            Config.Set("Config", "UpdateFrequency", GetUpdateFrequencyStr(Runtime.UpdateFrequency));

            Me.CustomData = Config.ToString();

            CommunicationHandlerInst = new CommunicationHandler(secureBroadcastPIN);
            CommunicationHandlerInst.SetUnicastMessageCallback("MESSAGE_RECEIVED");
            CommandHandlerInst.RegisterCommand("MESSAGE_RECEIVED", (args) => Receive());
            CommunicationHandlerInst.RegisterTag("COMMANDS", true);

            GridTerminalSystem.GetBlocksOfType(_allBlocks, b => b.IsSameConstructAs(Me) && b.CustomName.ToUpper().Contains(blockTag.ToUpper()));

            try
            {
                _systemCoordinator = new SystemCoordinator();
            }
            catch (Exception ex)
            {
                CommandHandlerInst.Clear();
                Config.Clear();
                CommunicationHandlerInst.Reset();
                _lastExceptionMsg = ex.Message;
                _debugScreen.WriteText(_lastExceptionMsg, true);
                CommandHandlerInst.RegisterCommand("INIT", (args) => Init());
                Runtime.UpdateFrequency = UpdateFrequency.None;

                return;
            }

            _isInitialized = true;
        }

        private void Receive()
        {
            if (!_isInitialized) return;

            CommunicationHandlerInst.Receive();
            while (CommunicationHandlerInst.HasMessage("COMMANDS", true))
            {
                MyIGCMessage msg;
                if (CommunicationHandlerInst.TryRetrieveMessage("COMMANDS", true, out msg))
                {
                    string command = msg.As<string>();
                    CommandHandlerInst.RunCommands(command);
                }
            }
        }

        private UpdateFrequency GetUpdateFrequency(string frequencyStr)
        {
            switch (frequencyStr.ToUpper())
            {
                case "NONE":
                    return UpdateFrequency.None;
                case "UPDATE1":
                    return UpdateFrequency.Update1;
                case "UPDATE10":
                    return UpdateFrequency.Update10;
                case "UPDATE100":
                    return UpdateFrequency.Update100;
                default:
                    return UpdateFrequency.None;
            }
        }

        private string GetUpdateFrequencyStr(UpdateFrequency frequency)
        {
            switch (frequency)
            {
                case UpdateFrequency.None:
                    return "NONE";
                case UpdateFrequency.Update1:
                    return "UPDATE1";
                case UpdateFrequency.Update10:
                    return "UPDATE10";
                case UpdateFrequency.Update100:
                    return "UPDATE100";
                default:
                    return "NONE";
            }
        }
    }
}
