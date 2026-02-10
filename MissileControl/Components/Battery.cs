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
        public class Battery
        {
            public IMyBatteryBlock BatteryBlock { get; private set; }
            public float CurrentStoredPower => BatteryBlock.CurrentStoredPower * 1000000;
            public float MaxStoredPower => BatteryBlock.MaxStoredPower * 1000000;
            public float ChargePercentage => MaxStoredPower != 0 ? CurrentStoredPower / MaxStoredPower * 100 : 0;
            public float CurrentInput => BatteryBlock.CurrentInput * 1000000;
            public float CurrentOutput => BatteryBlock.CurrentOutput * 1000000;
            public float MaxOutput => BatteryBlock.MaxOutput * 1000000;
            public float MaxInput => BatteryBlock.MaxInput * 1000000;
            public float NetInput => CurrentInput - CurrentOutput;
            public bool IsEmpty => CurrentStoredPower <= 0;
            public bool IsFull => CurrentStoredPower >= MaxStoredPower;
            public bool Exists => GTS.CanAccess(BatteryBlock);

            public Battery(string blockName)
            {
                blockName = blockName.ToUpper();
                BatteryBlock = AllGridBlocks.Where(b => b is IMyBatteryBlock && b.CustomName.ToUpper().Contains(blockName)).FirstOrDefault() as IMyBatteryBlock;
                if (BatteryBlock == null)
                {
                    throw new ArgumentException($"Battery block '{blockName}' not found!");
                }
            }
            public Battery(IMyBatteryBlock batteryBlock)
            {
                if (batteryBlock == null)
                {
                    throw new ArgumentException("Battery block is null!");
                }
                BatteryBlock = batteryBlock;
            }
        }
    }
}
