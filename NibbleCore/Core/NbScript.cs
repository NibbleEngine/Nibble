using System;

namespace NbCore
{
    public abstract class NbScript : Entity
    {
        public ulong Hash;
        public Engine EngineRef;
        
        public NbScript(Engine _e) : base(EntityType.Script)
        {
            EngineRef = _e;
        }

        public override Entity Clone()
        {
            throw new NotImplementedException();
        }
        
        public void Log(string msg, LogVerbosityLevel lvl)
        {
            Common.Callbacks.Log(this, msg, lvl);
        }
        
        public abstract void OnFrameUpdate(SceneGraphNode node, double dt);
        public abstract void OnRenderUpdate(SceneGraphNode node, double dt);

    }
}
