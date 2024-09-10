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
        public class MissileGuidance
        {
            private int maxSpeed;
            private float N;
            private float maxForwardAccel;
            private float maxRadialAccel;
            private MovingAverage signalSmoother;

            public Vector3 vectorToAlign;
            public Vector3 totalAcceleration;

            public MissileGuidance(float maxForwardAccel, float maxRadialAccel, float N, int smoothing, int maxSpeed = 100)
            {
                this.maxSpeed = maxSpeed;
                this.N = N;
                this.maxForwardAccel = maxForwardAccel;
                this.maxRadialAccel = maxRadialAccel;
                signalSmoother = new MovingAverage(smoothing);
            }

            public void Run(Vector3 missileVelocity, Vector3 missilePosition, Vector3 targetVelocity, Vector3 targetPosition)
            {
                float maxTotalAccel = (float)Math.Sqrt((maxRadialAccel * maxRadialAccel) + (maxForwardAccel * maxForwardAccel));
                float maxAccelAngle = (float)Math.Atan(maxForwardAccel / maxRadialAccel);

                Vector3 missileVelocityHeading = Vector3.Normalize(missileVelocity);
                Vector3 rangeToTarget = targetPosition - missilePosition;
                Vector3 directionToTarget = Vector3.Normalize(rangeToTarget);

                if (rangeToTarget.Length() > 5000)
                {
                    rangeToTarget = directionToTarget * 5000;
                }
                Vector3 relativeTargetVelocity = targetVelocity - missileVelocity;
                Vector3 rotationVector = Vector3.Cross(rangeToTarget, relativeTargetVelocity) / Vector3.Dot(rangeToTarget, rangeToTarget);

                Vector3 proNavAcceleration = N * (relativeTargetVelocity.Length() / missileVelocity.Length()) * Vector3.Cross(rotationVector, missileVelocity);
                signalSmoother.Update(proNavAcceleration);
                proNavAcceleration = signalSmoother.average;
                Vector3 proNavAccelerationDirection = Vector3.Normalize(proNavAcceleration);
                float proNavAccelerationMagnitude = proNavAcceleration.Length();

                Vector3 accelerationAlongVelocity;
                if (proNavAccelerationMagnitude < maxRadialAccel)
                {
                    float accelerationAlongVelocityMagnitude = maxForwardAccel;
                    accelerationAlongVelocity = missileVelocityHeading * accelerationAlongVelocityMagnitude;

                    vectorToAlign = missileVelocityHeading;
                }
                else
                {
                    if (proNavAccelerationMagnitude > maxTotalAccel)
                    {
                        proNavAccelerationMagnitude = maxTotalAccel;
                        proNavAcceleration = proNavAccelerationDirection * proNavAccelerationMagnitude;
                    }
                    float totalAccelerationAngle = (float)Math.Acos(proNavAccelerationMagnitude / maxTotalAccel);
                    float accelerationAlongVelocityMagnitude = (float)Math.Sin(totalAccelerationAngle) * maxTotalAccel;
                    accelerationAlongVelocity = missileVelocityHeading * accelerationAlongVelocityMagnitude;

                    float rotation = maxAccelAngle - totalAccelerationAngle;
                    Vector3 axisOfRotation = Vector3.Cross(proNavAccelerationDirection, missileVelocityHeading);
                    axisOfRotation.Normalize();
                    Quaternion alignmentCorrection = Quaternion.CreateFromAxisAngle(axisOfRotation, -rotation);

                    vectorToAlign = Vector3.Transform(missileVelocityHeading, alignmentCorrection);
                }

                if (missileVelocity.Length() > (maxSpeed - 1))
                {
                    accelerationAlongVelocity = Vector3.Zero;
                }

                totalAcceleration = accelerationAlongVelocity + proNavAcceleration;
            }
        }
    }
}
