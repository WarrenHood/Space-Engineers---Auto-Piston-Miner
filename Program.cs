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
        static float downwardsVelocity = 0.15f;
        static float rotorVelocity = 0.4f;
        static float forwardIncrement = 14.625f;
        static float minVerticalAltitude = 0f;
        static float upwardsVelocity = 1.0f;
        static float forwardVelocity = 1.0f;
        static float pistonMaxImpulseAxis = 200000;
        static float pistonMaxImpulseNonAxis = 200000;
        
        // End of configuration
        // Do not edit anything below this line

        List<IMyPistonBase> basePistons;
        List<IMyPistonBase> forwardPistons;
        List<IMyPistonBase> downPistons;
        List<IMyShipDrill> drills;
        IMyMotorAdvancedStator rotor;

        string currentState = "ready";
        public void SetupMiner() {
            basePistons = new List<IMyPistonBase>();
            forwardPistons = new List<IMyPistonBase>();
            downPistons = new List<IMyPistonBase>();
            drills = new List<IMyShipDrill>();
            rotor = (IMyMotorAdvancedStator)GridTerminalSystem.GetBlockWithName("Base Drill Advanced Rotor");
            GridTerminalSystem.GetBlockGroupWithName("Base Drill Base Pistons").GetBlocksOfType<IMyPistonBase>(basePistons);
            GridTerminalSystem.GetBlockGroupWithName("Base Drill Forward Pistons").GetBlocksOfType<IMyPistonBase>(forwardPistons);
            GridTerminalSystem.GetBlockGroupWithName("Base Drill Down Pistons").GetBlocksOfType<IMyPistonBase>(downPistons);
            GridTerminalSystem.GetBlockGroupWithName("Base Drill Drills").GetBlocksOfType<IMyShipDrill>(drills);

            

            // Set limits on the pistons
            float minLimitPerVertPiston = minVerticalAltitude / (basePistons.Count + downPistons.Count);
            foreach (var piston in basePistons) {
                piston.MinLimit = 0;
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
                piston.SetValue<float>("MaxImpulseAxis", pistonMaxImpulseAxis);
                piston.SetValue<float>("MaxImpulseNonAxis", pistonMaxImpulseNonAxis);
                piston.SetValue<bool>("ShareInertiaTensor", true);
            }

            rotor.SetValue<bool>("ShareInertiaTensor", true);
        }

        public void RaiseMiner() {
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

        public bool IsDoneExtendingForwards() {
            foreach (var piston in forwardPistons) {
                if (piston.CurrentPosition < piston.MaxLimit) return false;
            }
            return true;
        }

        public Program() {
            SetupMiner();
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            this.currentState = "ready";
        }

        public bool HasInventorySpace() {
            List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>();
            GridTerminalSystem.GetBlocksOfType<IMyCargoContainer>(cargoContainers, con => con.IsSameConstructAs(Me));

            foreach(var con in cargoContainers) {
                if (con.HasInventory && con.IsFunctional && !con.GetInventory().IsFull) return true;
            }

            return false;

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

            // movingup => movingforward
            else if (this.currentState == "movingup" && IsVerticallyRetracted()) {
                this.currentState = "movingforward";
                ExtendForwardsBy(forwardIncrement);
            }

            // movingforward => movingdown
            else if (this.currentState == "movingforward" && IsDoneExtendingForwards()) {
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

            
        }


        public void Main(string argument, UpdateType updateSource) {
            Tick();
            if (argument == "stop") {
                this.currentState = "stopped";
                PauseMiner();
            }
            else if (argument == "start") {
                this.currentState = "ready";
            }

            Echo("Warren's Auto Piston Miner");
            Echo("Base Piston Count: " + basePistons.Count);
            Echo("Forward Piston Count: " + forwardPistons.Count);
            Echo("Down Piston Count: " + downPistons.Count);
            Echo("Drill count: " + drills.Count);
            Echo("Advanced Rotor Exists: " + (rotor != null) );
            Echo("Auto Piston Miner State: " + this.currentState);   
        }
    }
}
