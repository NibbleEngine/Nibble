using System;
using System.Collections.Generic;
using System.IO;
using NbCore;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Windowing.Common;

namespace SimpleTextureRenderer
{
    public struct RenderTextureData
    {
        public int depth_id;
        public int mipmap_id;
    }

    public class RenderLayer : ApplicationLayer
    {
        private Texture _texture;
        private NbCore.Math.NbVector2i _size;
        private NbCore.Math.NbVector2 offset;
        private float _scale = 1.0f;
        private int depth_id = 0;
        private int mipmap_id = 0;
        private NbShader _shaderArray;
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
                null, null, null, new() { }, NbShaderMode.DEFAULT, "Texture");


            GLSLShaderConfig conf_multitex = EngineRef.CreateShaderConfig(
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_vs.glsl"),
                EngineRef.GetShaderSourceByFilePath("Shaders/texture_shader_fs.glsl"),
                null, null, null, new() { "_F55_MULTITEXTURE" }, 
                NbShaderMode.DEFAULT, "MultiTexTexture");

            _shaderArray = new();
            EngineRef.renderSys.Renderer.CompileShader(ref _shaderArray, conf_multitex);

            _shaderSingle = new();
            EngineRef.renderSys.Renderer.CompileShader(ref _shaderSingle, conf);

        }

        public void OnRenderTextureDataChanged(object sender, RenderTextureData data)
        {
            depth_id = data.depth_id;
            mipmap_id = data.mipmap_id;
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

        public void SetTexture(Texture tex)
        {
            if (_texture != null)
                _texture.Dispose();
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

        public void SetTextureDepth(int id)
        {
            depth_id = id;
        }

        public void SetMipmap(int id)
        {
            mipmap_id = id;
        }

        public override void OnFrameUpdate(ref Queue<object> data, double dt)
        {
            
        }

        public override void OnRenderFrameUpdate(ref Queue<object> data, double dt)
        {
            //First argument should be the input state
            NbMouseState mouseState = (NbMouseState) data.Dequeue();
            NbCore.Platform.Graphics.IGraphicsApi renderer = EngineRef.renderSys.Renderer;

            //Compile updated shaders
            while (EngineRef.renderSys.ShaderMgr.CompilationQueue.Count > 0)
            {
                NbShader shader = EngineRef.renderSys.ShaderMgr.CompilationQueue.Dequeue();
                //TODO: Recompile shader
            }

            renderer.EnableBlend();
            renderer.Viewport(_size.X, _size.Y);
            renderer.ClearColor(new NbCore.Math.NbVector4(0.1f, 0.1f, 0.1f, 0.0f));
            renderer.ClearDrawBuffer(NbCore.Platform.Graphics.NbBufferMask.Color |
                                    NbCore.Platform.Graphics.NbBufferMask.Depth);


            if (_texture != null)
            {
                NbShader _shader;
                if (_texture.target == NbTextureTarget.Texture2D)
                    _shader = _shaderSingle;
                else
                    _shader = _shaderArray;


                if (_captureInput)
                {
                    //Process Input:
                    if (mouseState.IsButtonDown(NbMouseButton.LEFT))
                    {
                        offset.X += mouseState.PositionDelta.X / (_size.X * ((float)_texture.Width / _texture.Height));
                        offset.Y += mouseState.PositionDelta.Y / _size.Y;
                    }

                    _scale = Math.Max(0.05f, _scale + mouseState.Scroll.Y * 0.08f);

                    //Console.WriteLine($"{offset.X}, {offset.Y}, {_scale}");
                }

                //Set Shader State
                _shader.ClearCurrentState();

                _shader.CurrentState.AddSampler("InTex", new()
                {
                    Target = _texture.target,
                    TextureID = _texture.texID
                });

                _shader.CurrentState.AddUniform("texture_depth", (float) depth_id);
                _shader.CurrentState.AddUniform("mipmap", (float) mipmap_id);
                _shader.CurrentState.AddUniform("aspect_ratio", (float) _texture.Height / _texture.Width);
                _shader.CurrentState.AddUniform("scale", _scale);
                _shader.CurrentState.AddUniform("offset", offset);

                renderer.EnableShaderProgram(_shader);

                NbMesh nm = EngineRef.GetPrimitiveMesh((ulong)"default_renderquad".GetHashCode());
                renderer.RenderQuad(nm, _shader, _shader.CurrentState);
            }


            //Prepare data for the next layer
            data.Enqueue(mouseState);

        }

    }
}
