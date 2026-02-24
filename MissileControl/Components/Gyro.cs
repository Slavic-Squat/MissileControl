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
        public class Gyro
        {
            public IMyGyro GyroBlock { get; private set; }
            public float Pitch
            {
                get { return -GyroBlock.Pitch; }
                set 
                {
                    if (float.IsNaN(value)) value = 0;
                    GyroBlock.Pitch = -value;
                }
            }
            public float Yaw
            {
                get { return -GyroBlock.Yaw; }
                set 
                {
                    if (float.IsNaN(value)) value = 0;
                    GyroBlock.Yaw = -value;
                }
            }
            public float Roll
            {
                get { return -GyroBlock.Roll; }
                set 
                {
                    if (float.IsNaN(value)) value = 0;
                    GyroBlock.Roll = -value;
                }
            }

            public Gyro(IMyGyro gyro)
            {
                GyroBlock = gyro;

                if (GyroBlock == null)
                {
                    throw new Exception($"Gyro is null!");
                }
            }

            public Gyro(string gyroName)
            {
                gyroName = gyroName.ToUpper();
                GyroBlock = AllBlocks.FirstOrDefault(b => b is IMyGyro && b.CustomName.ToUpper().Contains(gyroName)) as IMyGyro;
                if (GyroBlock == null)
                {
                    throw new Exception($"Gyro '{gyroName}' not found!");
                }
            }
        }
    }
}
