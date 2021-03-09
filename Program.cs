using Sandbox.Game.Entities;
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
using VRage.Voxels;
using VRageMath;

namespace IngameScript {
    partial class Program : MyGridProgram {
        // Warren's Automatic Piston Miner
        // Configuration

        // Use programmable block display for output
        static bool usePBDisplay = true;

        // Percentage free cargo space at which to resume mining after paused (should be larger than stopMiningFreePercentage)
        static float startMiningFreePercentage = 0.4f;
        // Percentage free cargo space at which to pause mining
        static float stopMiningFreePercentage = 0.20f;

        // Name of the piston miner group to target (All of it's parts should be in one group)
        static string minerGroupName = "Autominer 1";
        // The max number of drills from the middle drill in all directions (excluding the middle drill)
        static float drillheadradius = 6;
        // RPM of your advanced rotor
        static float rotorVelocity = 0.4f;
        // Total piston movement velocities
        static float downwardsVelocity = 0.15f;
        static float upwardsVelocity = 1.0f;
        static float forwardVelocity = 1.0f;
        // Maximum impulse forces
        static float pistonMaxImpulseAxis = 200000;
        static float pistonMaxImpulseNonAxis = 200000;
        // Total piston length in to chop off from the top
        static float minVerticalAltitude = 0f;

        // End of configuration
        // Do not edit anything below this line

        static Color fontColor = Color.Green;

        // Echo redirection
        public bool firstOut = true;
        public void EchoOut(string text) {
            if (((IMyTextSurfaceProvider)Me).SurfaceCount > 0) {
                string extra = "";
                if (!firstOut && text.Length != 0) extra = Environment.NewLine;
                IMyTextSurface surface = ((IMyTextSurfaceProvider)Me).GetSurface(0);
                surface.FontColor = fontColor;
                surface.ContentType = ContentType.TEXT_AND_IMAGE;
                surface.WriteText(extra + text, !firstOut);
                firstOut = false;
            }
        }

        static float forwardIncrement = (2.5f * drillheadradius) + 1.25f;
        List<IMyPistonBase> basePistons;
        List<IMyPistonBase> forwardPistons;
        List<IMyPistonBase> downPistons;
        List<IMyShipDrill> drills;
        IMyMotorAdvancedStator rotor;

        string currentState = "stopped";
        public void SetupMiner() {
            basePistons = new List<IMyPistonBase>();
            forwardPistons = new List<IMyPistonBase>();
            downPistons = new List<IMyPistonBase>();
            drills = new List<IMyShipDrill>();

            IMyBlockGroup minerGroup = GridTerminalSystem.GetBlockGroupWithName(minerGroupName);

            if (minerGroup == null) {
                this.currentState = "stopped";
                Echo("Could not find group with name: " + minerGroupName);
                return;
            }

            List<IMyMotorAdvancedStator> rotors = new List<IMyMotorAdvancedStator>();
            minerGroup.GetBlocksOfType<IMyMotorAdvancedStator>(rotors);
            if (rotors.Count == 0) {
                Echo("Could not find an advanced rotor!");
                this.currentState = "stopped";
                return;
            }
            rotor = rotors[0];

            List<IMyPistonBase> pistons = new List<IMyPistonBase>();
            minerGroup.GetBlocksOfType<IMyPistonBase>(pistons);

            Echo("Detected " + pistons.Count + " pistons");

            var UP = Me.WorldMatrix.Up;
            var FORWARD = Me.WorldMatrix.Forward;
            var DOWN = Me.WorldMatrix.Down;
            var BACKWARD = Me.WorldMatrix.Backward;
            UP.Normalize();
            FORWARD.Normalize();
            DOWN.Normalize();
            BACKWARD.Normalize();

            foreach (IMyPistonBase piston in pistons) {
                piston.Velocity = 0.0f;
                Vector3D pbf = piston.Top.GetPosition() - piston.GetPosition();
                pbf.Normalize();
                double distanceup = pbf.Dot(UP);
                double distancedown = pbf.Dot(DOWN);
                double distanceforward = pbf.Dot(FORWARD);
                double distancebackward = pbf.Dot(BACKWARD);

                double maxDir = Math.Max(Math.Max(Math.Max(distanceup, distancedown), distanceforward), distancebackward);

                if (distanceup == maxDir) {
                    basePistons.Add(piston);
                }
                else if (distanceforward == maxDir) {
                    piston.MaxLimit = piston.CurrentPosition;
                    forwardPistons.Add(piston);
                }
                else if (distancedown == maxDir) {
                    downPistons.Add(piston);
                }
            }

            minerGroup.GetBlocksOfType<IMyShipDrill>(drills);

            // Set limits on the pistons
            float minLimitPerVertPiston = minVerticalAltitude / (basePistons.Count + downPistons.Count);
            foreach (var piston in basePistons) {
                piston.MinLimit = piston.LowestPosition;
                piston.MaxLimit = piston.HighestPosition - minLimitPerVertPiston;
                piston.SetValue<float>("MaxImpulseAxis", pistonMaxImpulseAxis);
                piston.SetValue<float>("MaxImpulseNonAxis", pistonMaxImpulseNonAxis);
                piston.SetValue<bool>("ShareInertiaTensor", true);
            }
            foreach (var piston in downPistons) {
                piston.MinLimit = minLimitPerVertPiston;
                piston.MaxLimit = piston.HighestPosition;
                piston.SetValue<float>("MaxImpulseAxis", pistonMaxImpulseAxis);
                piston.SetValue<float>("MaxImpulseNonAxis", pistonMaxImpulseNonAxis);
                piston.SetValue<bool>("ShareInertiaTensor", true);
            }

            foreach (var piston in forwardPistons) {
                piston.MinLimit = piston.LowestPosition;
                piston.MaxLimit = piston.HighestPosition;
                piston.SetValue<float>("MaxImpulseAxis", pistonMaxImpulseAxis);
                piston.SetValue<float>("MaxImpulseNonAxis", pistonMaxImpulseNonAxis);
                piston.SetValue<bool>("ShareInertiaTensor", true);
            }

            rotor.SetValue<bool>("ShareInertiaTensor", true);

            this.currentState = "ready";
        }

        public void RaiseMiner() {
            SwitchDrills(false);
            rotor.TargetVelocityRPM = 0.0f;
            float upVelocityPerPiston = upwardsVelocity / (basePistons.Count + downPistons.Count);

            foreach (var piston in basePistons) {
                piston.Velocity = upVelocityPerPiston;
            }
            foreach(var piston in downPistons) {
                piston.Velocity = -upVelocityPerPiston;
            }
        }

        public void SwitchDrills(bool onOff) {
            foreach(var drill in drills) {
                drill.Enabled = onOff;
            } 
        }

        public bool IsFullyExtendedDownwards() {
            foreach(var piston in downPistons) {
                if (piston.CurrentPosition < piston.HighestPosition) return false;
            }
            foreach(var piston in basePistons) {
                if (piston.CurrentPosition > 0) return false;
            }
            return true;
        }

        public bool IsFullyExtendedForwards() {
            foreach (var piston in forwardPistons) {
                if (piston.CurrentPosition < piston.HighestPosition) return false;
            }
            return true;
        }

        public bool IsVerticallyRetracted() {
            foreach (var piston in downPistons) {
                if (piston.CurrentPosition > piston.MinLimit) return false;
            }
            foreach (var piston in basePistons) {
                if (piston.CurrentPosition < piston.MaxLimit) return false;
            }
            return true;
        }

        public void BeginDescent() {
            SwitchDrills(true);
            rotor.TargetVelocityRPM = rotorVelocity;
            int totalVertPistons = basePistons.Count + downPistons.Count;
            if (totalVertPistons == 0) {
                Echo("Error: Could not find any vertical pistons... Aborting");
                RaiseMiner();
                SwitchDrills(false);
                return;
            }

            float individualPistonVelocity = downwardsVelocity / totalVertPistons;

            foreach(var piston in forwardPistons) {
                piston.Velocity = 0.0f;
            }

            foreach(var piston in downPistons) {
                piston.Velocity = individualPistonVelocity;
            }

            foreach(var piston in basePistons) {
                piston.Velocity = -individualPistonVelocity;
            }

        }

        public void PauseMiner() {
            foreach (var piston in downPistons) {
                piston.Velocity = 0f; ;
            }

            foreach (var piston in basePistons) {
                piston.Velocity = 0f;
            }
            rotor.TargetVelocityRPM = 0f;
            SwitchDrills(false);
            
        }

        public void ExtendForwardsBy(float dist) {
            float distPerIndividualPiston = dist / forwardPistons.Count;

            foreach (var piston in forwardPistons) {
                piston.MaxLimit = piston.CurrentPosition + distPerIndividualPiston;
                piston.Velocity = forwardVelocity / forwardPistons.Count;
            }
        }

        public void RetractForwards() {
            foreach (var piston in forwardPistons) {
                piston.MinLimit = piston.LowestPosition;
                piston.Velocity = -forwardVelocity / forwardPistons.Count;
            }
        }

        public bool IsDoneExtendingForwards() {
            foreach (var piston in forwardPistons) {
                if (piston.CurrentPosition < piston.MaxLimit) return false;
            }
            return true;
        }

        public bool IsDoneRetractingForwards() {
            foreach (var piston in forwardPistons) {
                if (piston.CurrentPosition > piston.LowestPosition) return false;
            }
            return true;
        }

        public Program() {
            if (usePBDisplay) Echo = EchoOut;
            SetupMiner();
            if (Storage.Length > 0) this.currentState = Storage;
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public bool HasInventorySpace() {
            List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargoContainers, con => con.IsSameConstructAs(Me));
            double totalFreePerc = 0f;
            double totalCap = 0f;

            foreach(var con in cargoContainers) {
                if (con.HasInventory && con.IsFunctional && drills.Count > 0 && con.GetInventory().IsConnectedTo(drills[0].GetInventory())) {
                    totalFreePerc += ((double)(con.GetInventory().MaxVolume.ToIntSafe() - con.GetInventory().CurrentVolume.ToIntSafe()));
                    totalCap += (double)con.GetInventory().MaxVolume;
                }
            }
            totalFreePerc /= totalCap;

            if (this.currentState == "paused" && totalFreePerc < startMiningFreePercentage) {
                fontColor = Color.Red;
            }
            else if (totalFreePerc > stopMiningFreePercentage && totalFreePerc <= startMiningFreePercentage ) {
                fontColor = Color.Orange;
            }
            Echo("Free cargo space: " + totalFreePerc*100.0f + "%");

            if (this.currentState == "paused") {
                return totalFreePerc >= startMiningFreePercentage;
            }
            return totalFreePerc >= stopMiningFreePercentage;

        }


        public void Tick() {

            // ready state => Begin Mining
            if (this.currentState == "ready") {
                BeginDescent();
                this.currentState = "movingdown";
            }

            // movingdown => movingup
            else if (this.currentState == "movingdown" && IsFullyExtendedDownwards()) {
                RaiseMiner();
                this.currentState = "movingup";
            }

            // movingup => movingforward || reset_retracting
            else if (this.currentState == "movingup" && IsVerticallyRetracted()) {
                if (IsFullyExtendedForwards()) {
                    this.currentState = "reset_retracting";
                    PauseMiner();
                    RetractForwards();
                }
                else {
                    this.currentState = "movingforward";
                    ExtendForwardsBy(forwardIncrement);
                }
            }

            // movingforward => movingdown
            else if (this.currentState == "movingforward" && (IsDoneExtendingForwards() || IsFullyExtendedForwards())) {
                this.currentState = "movingdown";
                BeginDescent();
            }

            // movingdown => paused
            else if (this.currentState == "movingdown" && !HasInventorySpace()) {
                this.currentState = "paused";
                PauseMiner();
            }

            // paused => movingdown
            else if (this.currentState == "paused" && HasInventorySpace()) {
                this.currentState = "movingdown";
                BeginDescent();
            }

            // reset_start => reset_lifting
            else if (this.currentState == "reset_start") {
                this.currentState = "reset_lifting";
                RaiseMiner();
            }

            // reset_lifting => reset_rectracting
            else if (this.currentState == "reset_lifting" && IsVerticallyRetracted()) {
                this.currentState = "reset_retracting";
                RetractForwards();
            }

            // reset_retracting => stopped
            else if (this.currentState == "reset_retracting" && IsDoneRetractingForwards()) {
                this.currentState = "stopped";
                PauseMiner();
            }
            
        }

        public void Save() {
            Storage = currentState;
        }


        public void Main(string argument, UpdateType updateSource) {
            firstOut = true;
            fontColor = Color.Green;
            Echo("Warren's Auto Piston Miner");
            if (argument == "stop") {
                this.currentState = "stopped";
                PauseMiner();
            }
            else if (argument == "start") {
                this.currentState = "ready";
            }
            else if (argument == "moveup") {
                this.currentState = "movingup";
                RaiseMiner();
            }
            else if (argument == "reset") {
                this.currentState = "reset_start";
            }
            Tick();
            
            Echo("Miner Group: " + minerGroupName);
            Echo("Base Piston Count: " + basePistons.Count);
            Echo("Forward Piston Count: " + forwardPistons.Count);
            Echo("Down Piston Count: " + downPistons.Count);
            Echo("Drill count: " + drills.Count);
            Echo("Advanced Rotor Exists: " + (rotor != null) );
            Echo("Forward Increment: " + forwardIncrement);
            if (this.currentState == "paused") {
                fontColor = Color.Red;
            }
            if (this.currentState == "stopped") {
                fontColor = Color.Yellow;
            }
            Echo("Auto Piston Miner State: " + this.currentState);
        }
    }
}
