# ImageReplication
Uses "evolution" to recreate an input image out of a set of any sprites you like (from basic polygons to Mario and Luigi).
The resulting "image" is a replica of the input image but made out of a bunch of sprites in 3D space (so you can orbit the camera and the image breaks apart). Takes 30+ hours sometimes to get a good replication, check out Assets/ReplicationProgressSaves/ to see what it ends up looking like. Note that these are .prefab file extensions and need to be opened in some version of Unity.

## Process
- Spawn a bunch of random sprites at a random location
- Sample color using a box kernel at sprite location on the target image
- Check diff of colors between all random sprites
- Select the best 10 (customizable in settings)
- Make a bunch of mutated copies of each. Mutate size, pos, color
- Individually check diff of colors against the replication image with and without the new sprite
- Repeat for a few generations
- Select the sprite that minimizes the diff of colors between the target image and replication image with this new sprite in it
- Repeat an insane amount of times until satisfied
