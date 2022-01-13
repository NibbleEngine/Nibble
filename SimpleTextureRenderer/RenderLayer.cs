using System;
using System.Collections.Generic;
using System.IO;
using NbCore;
using NbCore.Platform.Graphics.OpenGL;
using OpenTK.Windowing.Common;

namespace SimpleTextureRenderer
{
    public class RenderLayer : ApplicationLayer
    {
        private Texture _texture;
        private NbCore.Math.NbVector2i _size;
        private NbCore.Math.NbVector2 offset;
        private float _scale = 1.0f;
        private int depth_id = 0;
        private int mipmap_id = 0;
        private GLSLShaderConfig _shaderArray;
        private GLSLShaderConfig _shaderSingle;

        public RenderLayer(Engine engine) : base(engine)
        {
            //Compile Necessary Shaders
            
            string vs_path = "Shaders/texture_shader_vs.glsl";
            vs_path = Path.GetFullPath(vs_path);
            vs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, vs_path);

            string fs_path = "Shaders/texture_shader_fs.glsl";
            fs_path = Path.GetFullPath(fs_path);
            fs_path = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, fs_path);

            GLSLShaderSource vs = EngineRef.GetShaderSourceByFilePath(vs_path);
            GLSLShaderSource fs = EngineRef.GetShaderSourceByFilePath(fs_path);

            //Pass Shader
            _shaderArray = GLShaderHelper.compileShader(vs, fs, null, null, null,
                new() { "_F55_MULTITEXTURE" }, SHADER_TYPE.MATERIAL_SHADER, SHADER_MODE.DEFAULT);

            //Pass Shader
            _shaderSingle = GLShaderHelper.compileShader(vs, fs, null, null, null,
                new(), SHADER_TYPE.MATERIAL_SHADER, SHADER_MODE.DEFAULT);
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

        public override void OnFrameUpdate(ref Queue<object> data)
        {
            
        }

        public override void OnRenderUpdate(ref Queue<object> data, double dt)
        {
            //First argument should be the input state
            NbMouseState mouseState = (NbMouseState) data.Dequeue();
            NbCore.Platform.Graphics.IGraphicsApi renderer = EngineRef.renderSys.Renderer;

            //Compile updated shaders
            while (EngineRef.renderSys.ShaderMgr.CompilationQueue.Count > 0)
            {
                GLSLShaderConfig shader = EngineRef.renderSys.ShaderMgr.CompilationQueue.Dequeue();
                GLShaderHelper.compileShader(shader);
            }


            renderer.Viewport(_size.X, _size.Y);
            renderer.ClearColor(new NbCore.Math.NbVector4(0.0f, 0.0f, 0.0f, 0.0f));
            renderer.ClearDrawBuffer(NbCore.Platform.Graphics.NbBufferMask.Color |
                                    NbCore.Platform.Graphics.NbBufferMask.Depth);


            

            if (_texture != null)
            {
                GLSLShaderConfig _shader;
                if (_texture.target == NbTextureTarget.Texture2D)
                    _shader = _shaderSingle;
                else
                    _shader = _shaderArray;

                //Process Input:
                if (mouseState.IsButtonDown(NbMouseButton.LEFT))
                {
                    offset.X += mouseState.PositionDelta.X / (_size.X * ((float)_texture.Width / _texture.Height));
                    offset.Y += mouseState.PositionDelta.Y / _size.Y;
                }

                _scale = Math.Max(0.05f, _scale + mouseState.Scroll.Y * 0.08f);

                //Console.WriteLine($"{offset.X}, {offset.Y}, {_scale}");

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
