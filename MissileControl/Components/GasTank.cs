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
        public class GasTank
        {
            public IMyGasTank TankBlock { get; private set; }
            public float CurrentVolume => TankBlock.Capacity * (float)TankBlock.FilledRatio / 1000f;
            public float MaxVolume => TankBlock.Capacity / 1000f;
            public float FillPercentage => (float)TankBlock.FilledRatio * 100;
            public bool IsEmpty => TankBlock.FilledRatio <= 0;
            public bool IsFull => TankBlock.FilledRatio >= 1;
            public bool Exists => GTS.CanAccess(TankBlock);
            public GasTank(string blockName)
            {
                blockName = blockName.ToUpper();
                TankBlock = AllGridBlocks.Where(b => b is IMyGasTank && b.CustomName.ToUpper().Contains(blockName)).FirstOrDefault() as IMyGasTank;
                if (TankBlock == null)
                {
                    DebugEcho($"Error: Gas tank '{blockName}' not found!");
                    throw new ArgumentException($"Gas tank '{blockName}' not found!");
                }
            }

            public GasTank(IMyGasTank tankBlock)
            {
                if (tankBlock == null)
                {
                    DebugEcho("Error: Gas tank is null!");
                    throw new ArgumentException("Gas tank is null!");
                }
                TankBlock = tankBlock;
            }
        }
    }
}
