## Nibble Instance Buffer Management


When a mesh is generated, it is not also added automatically to any rendering queue, unless it has instances. For now by default all meshes are assumed to have more than one instances. This is subject to change at some point since Instanced Rendering will be used in very specific cases. A static class that is responsible for adding/removing mesh instances is the GLMeshBufferManager class that works as follows:

Every time the rendering status of an mesh instance (i.e an entity that has a mesh component that uses a particular mesh) is modified, the AddRenderInstance/RemoveRenderInstance
methods are called.

The **AddRenderInstance** method, stores the instance data of the requested instance at the end of the instance buffer.
It also sets the new render instance id to the requested meshcomponent and makes sure that there is enough space in the instance buffer for the next instance.

```
| 0 | 1 | 2 | 3 | 4 | * | <---- | x | x |
```

The **RemoveRenderInstance** method, is responsible for removing the requested instance from the buffer, 
using its stored renderInstanceID, which reveals its position in the buffer. In order to prevent the
update of all the instance refs of all intermediate instances, the method swaps the instance data
with just the last instance of the buffer and decreases the renderInstanceCounter.


Example: Removing Instance 2

```
Start:     
| 0 | 1 | 2 | 3 | 4 | x | x |
Swap 2 with 4 that is the last member:     
| 0 | 1 | 4 | 3 | 2 | x | x |
Data for 2 is still in the buffer, but the counter has been decreased so it won't be used. 
Note that on the next call to AddRenderInstance (if no other instance has been removed) that data will be overwritten
| 0 | 1 | 4 | 3 | 2 | x | x |
```



### Support or Contact
For questions, suggestions or inquiries please mail me at <gregkwaste@gmail.com>
