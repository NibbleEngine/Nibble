using NbCore.Common;
using Newtonsoft.Json;
using System;

namespace NbCore
{
    public abstract class NbScript
    {
        public Engine EngineRef;
        public ulong Hash; //Unique hash per object
        
        public NbScript(Engine _e)
        {
            EngineRef = _e;
        }

        public void Log(string msg, LogVerbosityLevel lvl)
        {
            Callbacks.Log(this, msg, lvl);
        }
        
        public abstract void OnFrameUpdate(SceneGraphNode node, double dt);
        public abstract void OnRenderUpdate(SceneGraphNode node, double dt);

    }
}
