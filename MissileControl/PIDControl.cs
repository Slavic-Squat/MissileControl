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
        public class PIDControl
        {
            private float _kp;
            private float _ki;
            private float _kd;

            private float _integralValue;
            private float _priorValue;

            private float _minInt;
            private float _maxInt;

            public PIDControl(float kp, float ki, float kd, float minInt = float.MinValue, float maxInt = float.MaxValue)
            {
                _kp = kp;
                _ki = ki;
                _kd = kd;
                _minInt = minInt;
                _maxInt = maxInt;
            }

            public float Run(float input, float timeDelta)
            {
                if (timeDelta == 0 || float.IsNaN(input) || float.IsNaN(timeDelta))
                {
                    return 0;
                }

                float differencial = (input - _priorValue) / timeDelta;
                _priorValue = input;
                GetIntegral(input, timeDelta);
                float result = _kp * input + _ki * _integralValue + _kd * differencial;

                return result;
            }

            public void GetIntegral(float input, float timeDelta)
            {
                _integralValue = MathHelper.Clamp(_integralValue + input * timeDelta, _minInt, _maxInt);
            }

        }
    }
}
