using System.Collections.Generic;

namespace NbCore
{
    public class NbAnimationGroup
    {
        public SceneGraphNode AnimationRoot; //Reference node to be able to search for joints
        public NbMeshGroup RefMeshGroup = null; //Referenced group of animated meshes
        public Animation ActiveAnimation = null; //Use only one animation at a time for now
        public List<Animation> Animations = new();
    }
}
