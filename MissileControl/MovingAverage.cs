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
        public class MovingAverage
        {
            private int windowSize;
            private Queue<Vector3D> values;
            private Vector3D sum;
            public Vector3D average;

            public MovingAverage(int windowSize)
            {
                this.windowSize = windowSize;
                values = new Queue<Vector3D>();
                sum = Vector3D.Zero;
            }

            public void Update(Vector3D input)
            {
                values.Enqueue(input);
                sum += input;
                if (values.Count > windowSize)
                {
                    sum -= values.Dequeue();
                }
                if (values.Count == windowSize)
                {
                    average = sum / windowSize;

                    return;
                }
                else if (values.Count < windowSize)
                {
                    average = input;

                    return;
                }
            }
        }
    }
}
