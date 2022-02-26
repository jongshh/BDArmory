using System;
using System.Linq;
using BDArmory.Modules;
using UnityEngine;

namespace BDArmory.Analysis
{
    public sealed class AirframeReport
    {
        public readonly double pitchMoment, rollMoment, yawMoment;
        public readonly double pitchInertia, rollInertia, yawInertia;
        public AirframeReport(
            double pitchMoment, double rollMoment, double yawMoment,
            double pitchInertia, double rollInertia, double yawInertia)
        {
            this.pitchMoment = pitchMoment;
            this.pitchInertia = pitchInertia;
            this.rollMoment = rollMoment;
            this.rollInertia = rollInertia;
            this.yawMoment = yawMoment;
            this.yawInertia = yawInertia;
        }

        public double pitchFactor()
        {
            return pitchMoment / pitchInertia;
        }

        public double rollFactor()
        {
            return rollMoment / rollInertia;
        }

        public double yawFactor()
        {
            return yawMoment / yawInertia;
        }

        public override string ToString()
        {
            return string.Format("inertia={0:0.00}, {1:0.00}, {2:0.00}; moment={3:0.00}, {4:0.00}, {5:0.00}, factor={6:0.00}, {7:0.00}, {8:0.00}", pitchInertia, rollInertia, yawInertia, pitchMoment, rollMoment, yawMoment, pitchFactor(), rollFactor(), yawFactor());
        }
    }

    public class FlightDynamics
    {
        private Vessel vessel;

        private Func<Part, double> liftingSurfaceLambda = e =>
        {
            var module = e.GetComponent<ModuleLiftingSurface>();
            var coeff = module.deflectionLiftCoeff;
            float minLift, maxLift;
            module.liftCurve.FindMinMaxValue(out minLift, out maxLift);
            return coeff * Math.Max(minLift, maxLift);
        };

        private Func<Part, double> controlSurfaceLambda = e =>
        {
            var module = e.GetComponent<ModuleControlSurface>();
            return Math.Max(module.GetPotentialLift(true).magnitude, module.GetPotentialLift(false).magnitude);
        };


        public FlightDynamics()
        {
        }

        public bool LoadVessel(Vessel vessel)
        {
            this.vessel = vessel;
            return vessel != null;
        }

        /// <summary>
        /// Parse the vessel part tree to compute moments of inertia and control authority.
        ///
        /// Must invoke LoadVessel first and only invoke this method if it returns true.
        /// </summary>
        /// <returns>A report of the loaded vessel, including pitch/roll/yaw components of moment of inertia and control authority.</returns>
        public AirframeReport GenerateReport()
        {
            // find CoM
            Vector3 com = vessel.CoM;
            Debug.Log(string.Format("FlightDynamics::GenerateReport com={0:0.000},{1:0.000},{2:0.000}", com.x, com.y, com.z));

            // debug
            vessel.parts.ForEach(e => Debug.Log(string.Format("FlightDynamics::GenerateReport part={0}, mass={1:0.000}, pos={2:0.000},{3:0.000},{4:0.000}", e.name, e.mass, e.orgPos.x, e.orgPos.y, e.orgPos.z)));

            // compute moments of inertia
            double pitchInertia = vessel.parts.Select(e => e.mass * (Math.Pow(e.orgPos.y - com.y, 2) + Math.Pow(e.orgPos.z - com.z, 2))).Sum();
            double rollInertia = vessel.parts.Select(e => e.mass * (Math.Pow(e.orgPos.x - com.x, 2) + Math.Pow(e.orgPos.z - com.z, 2))).Sum();
            double yawInertia = vessel.parts.Select(e => e.mass * (Math.Pow(e.orgPos.x - com.x, 2) + Math.Pow(e.orgPos.y - com.y, 2))).Sum();

            // compute control moments
            var liftingSurfaces = vessel.parts.Where(e => e.HasModuleImplementing<ModuleLiftingSurface>());
            var controlSurfaces = vessel.parts.Where(e => e.HasModuleImplementing<ModuleControlSurface>());
            Debug.Log(string.Format("FlightDynamics::GenerateReport lift={0}, ctrl={1}", liftingSurfaces.Count(), controlSurfaces.Count()));

            liftingSurfaces.ToList().ForEach(e => Debug.Log(string.Format("FlightDynamics::GenerateReport part={0}, lift={1}", e.name, liftingSurfaceLambda(e))));
            controlSurfaces.ToList().ForEach(e => Debug.Log(string.Format("FlightDynamics::GenerateReport part={0}, ctrl={1}", e.name, controlSurfaceLambda(e))));

            // note[aubrey]: doing all this with functional map-reduce pattern is likely less efficient than a single pass through the parts list
            double pitchMoment = 0;
            pitchMoment += liftingSurfaces.Select(e => liftingSurfaceLambda(e) * Math.Sqrt(Math.Pow(e.orgPos.y - com.y, 2) + Math.Pow(e.orgPos.z - com.z, 2))).Sum();
            pitchMoment += controlSurfaces.Select(e => controlSurfaceLambda(e) * Math.Sqrt(Math.Pow(e.orgPos.y - com.y, 2) + Math.Pow(e.orgPos.z - com.z, 2))).Sum();
            double rollMoment = 0;
            rollMoment += liftingSurfaces.Select(e => liftingSurfaceLambda(e) * Math.Sqrt(Math.Pow(e.orgPos.x - com.x, 2) + Math.Pow(e.orgPos.z - com.z, 2))).Sum();
            rollMoment += controlSurfaces.Select(e => controlSurfaceLambda(e) * Math.Sqrt(Math.Pow(e.orgPos.x - com.x, 2) + Math.Pow(e.orgPos.z - com.z, 2))).Sum();
            double yawMoment = 0;
            yawMoment += liftingSurfaces.Select(e => liftingSurfaceLambda(e) * Math.Sqrt(Math.Pow(e.orgPos.x - com.x, 2) + Math.Pow(e.orgPos.y - com.y, 2))).Sum();
            yawMoment += controlSurfaces.Select(e => controlSurfaceLambda(e) * Math.Sqrt(Math.Pow(e.orgPos.x - com.x, 2) + Math.Pow(e.orgPos.y - com.y, 2))).Sum();

            AirframeReport report = new AirframeReport(pitchMoment, rollMoment, yawMoment, pitchInertia, rollInertia, yawInertia);
            Debug.Log(string.Format("FlightDynamics::GenerateReport {0}", report));

            return report;
        }
    }
}
