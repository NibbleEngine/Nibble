using NbCore.Common;
using NbCore.Platform.Windowing;
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

        public void OnRenderFrame(double dt)
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].OnRenderFrameUpdate(dt);
            }
        }

        public void OnFrameUpdate(double dt)
        {
            for (int i = 0; i < Layers.Count; i++)
            {
                Layers[i].OnFrameUpdate(dt);
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

    public abstract class ApplicationLayer : IDisposable
    {
        //Layer Properties
        public string Name;
        public ApplicationLayer Next;
        protected Engine EngineRef;
        protected NbWindow WindowRef;
        private bool disposedValue;

        public ApplicationLayer(NbWindow win, Engine engine)
        {
            EngineRef = engine;
            WindowRef = win;
        }

        public virtual void OnRenderFrameUpdate(double dt)
        {

        }

        public virtual void OnFrameUpdate(double dt)
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
            Callbacks.Log(Name.ToUpper(), msg, lvl);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}