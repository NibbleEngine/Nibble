using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using NbCore;
using OpenTK.Graphics.OpenGL;

namespace NbCore
{
    public enum NbCollisionType
    {
        MESH = 0x0,
        SPHERE,
        CYLINDER,
        BOX,
        CAPSULE
    }

    public class CollisionComponent : Component
    {
        public NbCollisionType CollisionType;

        public CollisionComponent()
        {
            
        }

        public override Component Clone()
        {
            CollisionComponent mc = new();
            mc.CopyFrom(this);
            return mc;
        }

        public override void CopyFrom(Component c)
        {
            if (c is not CollisionComponent)
                return;

            CollisionComponent mc = c as CollisionComponent;
        }
    }
}
