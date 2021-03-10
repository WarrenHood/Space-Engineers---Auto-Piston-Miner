# Space-Engineers---Auto-Piston-Miner
This script will automate piston mining for you.

Simply place down a programmable block (with the display facing away from the desired forward direction!).  Then just place a few pistons going up, followed by a conveyor junction or some sort of conveyor part, followed by a few pistons going forwards, followed by another conveyor junction or whatever, followed by a few down pistons, an advanced rotor, and as many drills as you'd like under this. You can optionally add a few cameras facing downwards(level with your drills), and enable raycasting in the config if you want the drill to move down at a velocity proportional to the distance to the ground.

Add all of the pistons, the advanced rotor, cameras, and all the drills to a single group. Call it whatever you want. 

Open the programmable block (press k), then hit edit, then select my script. 

You can change the configuration at the top of the script.  

Set the minerGroupName in the config to whatever you called that group you made for your auto piston miner. 

You can also change the drillHeadRadius to the number of drills you have going outward from the middle drill (don't count the middle drill) in any direction. 

I do recommend that you set the minVerticalAltitude to 0.0f when starting out. If the drillhead is going too high up and wasting time in the air before reaching the ground, you can increase this value slightly. 

Then, you can run the script with the following arguments:

start - Starts the mining process 
stop - Pauses the mining process
reset - Resets the drill to it's initial position

These can be added to your hotbar or to buttons etc.
