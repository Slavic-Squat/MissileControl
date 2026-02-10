using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Emit;
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
        public class MovingAverage
        {
            private readonly Queue<double> _values;
            private readonly int _windowSize;
            private double _sum;

            public double Average => _values.Count > 0 ? _sum / _values.Count : 0;
            public int Count => _values.Count;
            public double Max => _values.Count > 0 ? _values.Max() : 0;
            public double Min => _values.Count > 0 ? _values.Min() : 0;
            public MovingAverage(int windowSize)
            {
                if (windowSize <= 0)
                    throw new ArgumentException("Window size must be positive");

                _windowSize = windowSize;
                _values = new Queue<double>(windowSize);
                _sum = 0;
            }
            public double Add(double value)
            {
                _values.Enqueue(value);
                _sum += value;

                if (_values.Count > _windowSize)
                {
                    _sum -= _values.Dequeue();
                }

                return Average;
            }
            
            public void Clear()
            {
                _values.Clear();
                _sum = 0;
            }
        }
    }
}
