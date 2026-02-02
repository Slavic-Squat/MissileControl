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
        public class ThrusterGroup
        {
            public IReadOnlyList<Thruster> Thrusters => _thrusters;
            public Vector3 Vector => _thrusters.Count > 0 ? _thrusters[0].Vector : Vector3.Zero;
            public float MaxThrust => _thrusters.Sum(t => t.MaxThrust);
            public float ThrustOverride
            {
                get { return _thrustOverride; }
                set 
                {
                    if (value < 0 || float.IsNaN(value)) value = 0;
                    _thrustOverride = value;
                    float maxThrust = MaxThrust;
                    _thrusters.ForEach(t => t.ThrustOverride = (maxThrust == 0) ? 0 : value * (t.MaxThrust / maxThrust));
                }
            }
            public float ThrustOverridePercentage
            {
                get { return _thrustOverridePercentage; }
                set 
                {
                    if (value < 0 || float.IsNaN(value)) value = 0;
                    _thrustOverridePercentage = value;
                    _thrusters.ForEach(t => t.ThrustOverridePercentage = value);
                }
            }

            private float _thrustOverride;
            private float _thrustOverridePercentage;
            private List<Thruster> _thrusters = new List<Thruster>();

            public ThrusterGroup(params Thruster[] thrusters)
            {
                _thrusters = thrusters.ToList();
            }
        }
    }
}
