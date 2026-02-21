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
        public class Thruster
        {
            public IMyThrust ThrusterBlock { get; private set; }
            public Vector3 Vector => ThrusterBlock.WorldMatrix.Backward;
            public float MaxThrust => ThrusterBlock.MaxEffectiveThrust;
            public float ThrustOverride
            {
                get { return ThrusterBlock.ThrustOverride; }
                set 
                {
                    if (value < 0 || float.IsNaN(value)) value = 0;
                    ThrusterBlock.ThrustOverride = value; 
                }
            }
            public float ThrustOverridePercentage
            {
                get { return ThrusterBlock.ThrustOverridePercentage; }
                set 
                {
                    if (value < 0 || float.IsNaN(value)) value = 0;
                    ThrusterBlock.ThrustOverridePercentage = value;
                }
            }
            public Thruster(IMyThrust thruster)
            {
                ThrusterBlock = thruster;
                if (ThrusterBlock == null)
                {
                    throw new Exception($"Thruster is null!");
                }
            }

            public Thruster(string thrusterName)
            {
                thrusterName = thrusterName.ToUpper();
                ThrusterBlock = AllGridBlocks.FirstOrDefault(b => b is IMyThrust && b.CustomName.ToUpper().Contains(thrusterName)) as IMyThrust;
                if (ThrusterBlock == null)
                {
                    throw new Exception($"Thruster '{thrusterName}' not found!");
                }
            }
        }
    }
}
