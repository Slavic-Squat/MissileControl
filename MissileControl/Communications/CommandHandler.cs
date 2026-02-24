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
    partial class Program
    {
        public class CommandHandler
        {
            private MyCommandLine _commandLine = new MyCommandLine();
            private Dictionary<string, Action<string[]>> _commands = new Dictionary<string, Action<string[]>>();

            public CommandHandler()
            {

            }

            public void RunCommands(string commandsString)
            {
                if (commandsString == null || commandsString == "")
                {
                    return;
                }
                commandsString = commandsString.ToUpper();
                string[] separatedCommandStrings = commandsString.Split('|', '\n');
                foreach (string commandString in separatedCommandStrings)
                {
                    commandsString = commandString.Trim();
                    if (_commandLine.TryParse(commandString))
                    {
                        string commandName = _commandLine.Argument(0);
                        string[] commandArguments = new string[_commandLine.ArgumentCount - 1];
                        for (int i = 0; i < commandArguments.Length; i++)
                        {
                            commandArguments[i] = _commandLine.Argument(i + 1);
                        }
                        Action<string[]> command;

                        if (commandName != null)
                        {
                            if (_commands.TryGetValue(commandName, out command))
                            {
                                command(commandArguments);
                            }
                        }
                    }
                }
            }

            public void RegisterCommand(string commandName, Action<string[]> commandAction)
            {
                commandName = commandName.ToUpper();
                _commands[commandName] = commandAction;
            }

            public void Clear()
            {
                _commands.Clear();
            }
        }
    }
}
