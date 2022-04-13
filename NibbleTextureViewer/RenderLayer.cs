using System;
using System.Collections.Generic;
using System.IO;
using NbCore;
#if OPENGL
using NbCore.Platform.Graphics; 
#endif

using OpenTK.Windowing.Common;

namespace NibbleTextureViewer
{
    public struct RenderTextureData
    {
        public int depth_id;
        public int mipmap_id;
        public NbCore.Math.NbVector4 channelToggle;
    }

    public class RenderLayer : ApplicationLayer
    {
        private NbTexture _texture;
        private NbCore.Math.NbVector2i _size;
        private NbCore.Math.NbVector2 offset;
        private float _scale = 1.0f;
        private RenderTextureData _renderData;
        private NbShader _shaderArray;
        private NbShader _shaderVolume;
        private NbShader _shaderSingle;
        private bool _captureInput = true;

        public RenderLayer(Engine engine) : base(engine)
        {
            //Default render quad
            NbCore.Primitives.Quad q = new NbCore.Primitives.Quad();

            NbMesh mesh = new()
            {
                Hash = (ulong)"default_renderquad".GetHashCode(),
                Data = q.geom.GetData(),
                MetaData = q.geom.GetMetaData(),
            };
            EngineRef.renderSys.Renderer.AddMesh(mesh);
            EngineRef.RegisterEntity(mesh);
            EngineRef.renderSys.GeometryMgr.AddPrimitiveMesh(mesh);
            q.Dispose();

            //Add Shader Sources to engine
            GLSLShaderSource ss = new("Shaders/texture_shader_vs.glsl", true);
            ss.Process();

            ss = new("Shaders/texture_shader_fs.glsl", true);
            ss.Process();
            
            //Compile Necessary Shaders
            GLSLShaderConfig conf = EngineRef.CreateShaderConfig(
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_vs.glsl"),
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_fs.glsl"),
                null, null, null, NbShaderMode.DEFAULT, "Texture");


            GLSLShaderConfig conf_multitex = EngineRef.CreateShaderConfig(
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_vs.glsl"),
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_fs.glsl"),
                null, null, null, NbShaderMode.DEFAULT, "MultiTexTexture");

            GLSLShaderConfig conf_voltex = EngineRef.CreateShaderConfig(
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_vs.glsl"),
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_fs.glsl"),
                null, null, null, NbShaderMode.DEFAULT, "VolumeTexture");

            _shaderArray = EngineRef.CreateShader(conf_multitex, new() { "_F55_MULTITEXTURE" });
            EngineRef.CompileShader(_shaderArray);

            _shaderSingle = EngineRef.CreateShader(conf);
            EngineRef.CompileShader(_shaderSingle);
            
            _shaderVolume = EngineRef.CreateShader(conf_voltex, new() { "_D_VOLUME_TEXTURE" });
            EngineRef.CompileShader(_shaderVolume);
        
        }

        public void OnRenderTextureDataChanged(object sender, RenderTextureData data)
        {
            _renderData = data;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Managed state disposal
            }

            if (_texture != null)
                _texture.Dispose();

            _shaderArray.Dispose();
            base.Dispose(disposing);
        }

        public void CaptureInput(object sender, bool state)
        {
            _captureInput = !state;
            //Console.WriteLine($"CAPTURE INPUT IN RENDER LAYER: {_captureInput}");
        }

        public void SetTexture(NbTexture tex)
        {
            _texture = tex;
        }

        public void SetViewportsize(NbCore.Math.NbVector2i vec)
        {
            _size = vec;
        }

        public void OnResize(ResizeEventArgs args)
        {
            _size = new NbCore.Math.NbVector2i(args.Width, args.Height);
        }

        public override void OnFrameUpdate(ref Queue<object> data, double dt)
        {
            
        }

        public override void OnRenderFrameUpdate(ref Queue<object> data, double dt)
        {
            //First argument should be the input state
            NbMouseState mouseState = (NbMouseState) data.Dequeue();
            NbCore.Platform.Graphics.GraphicsAPI renderer = EngineRef.renderSys.Renderer;

            //Compile updated shaders
           //Re-Compile requested shaders
            while (EngineRef.renderSys.ShaderMgr.ShaderCompilationQueue.Count > 0)
            {
                NbShader shader = EngineRef.renderSys.ShaderMgr.ShaderCompilationQueue.Dequeue();
                EngineRef.CompileShader(shader);
            }
            
            renderer.EnableBlend();
            renderer.Viewport(_size.X, _size.Y);
            renderer.ClearColor(new NbCore.Math.NbVector4(0.1f, 0.1f, 0.1f, 0.0f));
            renderer.ClearDrawBuffer(NbBufferMask.Color | NbBufferMask.Depth);

            if (_texture != null)
            {
                NbShader _shader;
                if (_texture.Data.target == NbTextureTarget.Texture3D)
                    _shader = _shaderVolume;
                else if ((_texture.Data.target == NbTextureTarget.Texture2DArray))
                    _shader = _shaderArray;
                else
                    _shader = _shaderSingle;


                if (_captureInput)
                {
                    //Process Input:
                    if (mouseState.IsButtonDown(NbMouseButton.LEFT))
                    {
                        offset.X += mouseState.PositionDelta.X / (_size.X * ((float)_texture.Data.Width / _texture.Data.Height));
                        offset.Y += mouseState.PositionDelta.Y / _size.Y;
                    }

                    _scale = Math.Max(0.05f, _scale + mouseState.Scroll.Y * 0.08f);

                    //Console.WriteLine($"{offset.X}, {offset.Y}, {_scale}");
                }

                //Set Shader State
                _shader.ClearCurrentState();

                _shader.CurrentState.AddSampler("InTex", new()
                {
                    Target = _texture.Data.target,
                    TextureID = _texture.texID
                });

                _shader.CurrentState.AddUniform("texture_depth", (float)_renderData.depth_id);
                //_shader.CurrentState.AddUniform("mipmap", (float)_renderData.mipmap_id);
                _shader.CurrentState.AddUniform("mipmap", 0.0f);
                _shader.CurrentState.AddUniform("aspect_ratio", (float) _texture.Data.Height / _texture.Data.Width);
                _shader.CurrentState.AddUniform("scale", _scale);
                _shader.CurrentState.AddUniform("offset", offset);
                _shader.CurrentState.AddUniform("channelToggle", _renderData.channelToggle);

                renderer.EnableShaderProgram(_shader);

                NbMesh nm = EngineRef.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode());
                renderer.RenderQuad(nm, _shader, _shader.CurrentState);
            }


            //Prepare data for the next layer
            data.Enqueue(mouseState);

        }

    }
}
