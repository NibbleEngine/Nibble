using NbCore.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace NbCore
{
    public class ApplicationLayerStack : IDisposable
    {
        public List<ApplicationLayer> Layers = new();
        
        public void AddApplicationLayer(ApplicationLayer layer)
        {
            Layers.Add(layer);
        }

        public void OnRenderFrame(Queue<object> data, double dt)
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].OnRenderFrameUpdate(ref data, dt);
            }
        }

        public void OnFrameUpdate(Queue<object> data, double dt)
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].OnFrameUpdate(ref data, dt);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].Dispose();
            }
            
            Layers.Clear();
        }

        
    }

    public abstract class ApplicationLayer :IDisposable
    {
        //Layer Properties
        public string Name;
        public ApplicationLayer Next;
        protected Engine EngineRef;
        private bool disposedValue;

        public ApplicationLayer(Engine engine)
        {
            EngineRef = engine;
        }
        
        public virtual void OnRenderFrameUpdate(ref Queue<object> data, double dt)
        {

        }

        public virtual void OnFrameUpdate(ref Queue<object> data, double dt)
        {

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    EngineRef = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Log(string msg, LogVerbosityLevel lvl)
        {
            string outmsg = string.Format("* {0} : {1} - {2}",
                Name.ToUpper(), lvl.ToString(), msg);
            Callbacks.Log(outmsg, lvl);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
