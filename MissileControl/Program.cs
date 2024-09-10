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
        MissileControl missile;
        Dictionary<string, Action<string>> commands = new Dictionary<string, Action<string>>();
        MyCommandLine commandLine = new MyCommandLine();

        public Program()
        {
            missile = new MissileControl(this, 0, false);

            commands["Launch"] = missile.Launch;
        }

        public void Save()
        {

        }

        public void Main(string argument, UpdateType updateSource)
        {
            missile.Run();

            if (commandLine.TryParse(argument))
            {
                string commandName = commandLine.Argument(0);
                string commandArgument = commandLine.Argument(1);
                Action<string> command;

                if (commandName != null && commandArgument != null)
                {
                    if (commands.TryGetValue(commandName, out command))
                    {
                        command(commandArgument);
                    }
                }
            }
        }
    }
}
