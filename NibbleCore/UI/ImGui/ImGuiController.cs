﻿using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NbCore.Common;
using NbCore;
using System.Reflection;
using System.Runtime.InteropServices;
using NbCore.Platform.Windowing;

namespace NbCore.UI.ImGui
{
    public class ImGuiController : IDisposable
    {
        private bool _frameBegun;

        private int _vertexArray;
        private int _vertexBuffer;
        private int _vertexBufferSize;
        private int _indexBuffer;
        private int _indexBufferSize;

        private ImGuiTexture _fontTexture;
        private ImGuiShader _shader;
        
        private NbWindow WindowRef { get; }
        private System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;
        private float _scrollFactor = 0.5f;
        
        /// <summary>
        /// Constructs a new ImGuiController.
        /// </summary>
        public ImGuiController(NbWindow win)
        {
            WindowRef = win;
            
            IntPtr context = ImGuiNET.ImGui.CreateContext();
            ImGuiNET.ImGui.SetCurrentContext(context);
            var io = ImGuiNET.ImGui.GetIO();
            //io.Fonts.AddFontDefault();
            //Load Logo Texture to the GPU
            byte[] fontdata = Callbacks.getResourceFromAssembly(Assembly.GetEntryAssembly(),
                "Roboto-Medium.ttf");
            
            GCHandle pinnedArray = GCHandle.Alloc(fontdata, GCHandleType.Pinned);
            IntPtr fontPtr = pinnedArray.AddrOfPinnedObject();
            io.Fonts.AddFontFromMemoryTTF(fontPtr, fontdata.Length, 16.0f);
            io.Fonts.AddFontFromMemoryTTF(fontPtr, fontdata.Length, 11.0f);
            io.Fonts.AddFontFromMemoryTTF(fontPtr, fontdata.Length, 20.0f);
            io.Fonts.AddFontFromMemoryTTF(fontPtr, fontdata.Length, 25.0f);
            io.Fonts.AddFontFromMemoryTTF(fontPtr, fontdata.Length, 30.0f);
            io.Fonts.AddFontFromMemoryTTF(fontPtr, fontdata.Length, 35.0f);

            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
            io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; //Enable Docking
            io.ConfigFlags |= ImGuiConfigFlags.ViewportsEnable; //Enable MultipleViewport

            
            CreateDeviceResources();
            SetKeyMappings();
            SetPerFrameImGuiData(1f / 60f);

            pinnedArray.Free();
            
            ImGuiNET.ImGui.NewFrame();
            _frameBegun = true;
        }

        public void DestroyDeviceObjects()
        {
            Dispose();
        }

        public void CreateDeviceResources()
        {
            ImGuiUtil.CreateVertexArray("ImGui", out _vertexArray);

            _vertexBufferSize = 10000;
            _indexBufferSize = 2000;

            
            ImGuiUtil.CreateVertexBuffer("ImGui", out _vertexBuffer);
            ImGuiUtil.CreateElementBuffer("ImGui", out _indexBuffer);
            GL.NamedBufferData(_vertexBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.NamedBufferData(_indexBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

            RecreateFontDeviceTexture();

            string VertexSource = @"#version 330 core
uniform mat4 projection_matrix;
layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
layout(location = 2) in vec4 in_color;
out vec4 color;
out vec2 texCoord;
void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;
    texCoord = in_texCoord;
}";
            string FragmentSource = @"#version 330 core
uniform sampler2D in_fontTexture;
in vec4 color;
in vec2 texCoord;
out vec4 outputColor;
void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
}";
            _shader = new ImGuiShader("ImGui", VertexSource, FragmentSource);
            
            GL.VertexArrayVertexBuffer(_vertexArray, 0, _vertexBuffer, IntPtr.Zero, Unsafe.SizeOf<ImDrawVert>());
            GL.VertexArrayElementBuffer(_vertexArray, _indexBuffer);

            GL.EnableVertexArrayAttrib(_vertexArray, 0);
            GL.VertexArrayAttribBinding(_vertexArray, 0, 0);
            GL.VertexArrayAttribFormat(_vertexArray, 0, 2, VertexAttribType.Float, false, 0);

            GL.EnableVertexArrayAttrib(_vertexArray, 1);
            GL.VertexArrayAttribBinding(_vertexArray, 1, 0);
            GL.VertexArrayAttribFormat(_vertexArray, 1, 2, VertexAttribType.Float, false, 8);

            GL.EnableVertexArrayAttrib(_vertexArray, 2);
            GL.VertexArrayAttribBinding(_vertexArray, 2, 0);
            GL.VertexArrayAttribFormat(_vertexArray, 2, 4, VertexAttribType.UnsignedByte, true, 16);

            ImGuiUtil.CheckGLError("End of ImGui setup");
        }

        /// <summary>
        /// Recreates the device texture used to render text.
        /// </summary>
        public void RecreateFontDeviceTexture()
        {
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();
            io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int bytesPerPixel);

            _fontTexture = new ImGuiTexture("ImGui Text Atlas", width, height, pixels);
            _fontTexture.SetMagFilter(TextureMagFilter.Linear);
            _fontTexture.SetMinFilter(TextureMinFilter.Linear);

            io.Fonts.SetTexID((IntPtr)_fontTexture.GLTexture);
            io.Fonts.ClearTexData();
        }

        /// <summary>
        /// Renders the ImGui draw list data.
        /// This method requires a <see cref="GraphicsDevice"/> because it may create new DeviceBuffers if the size of vertex
        /// or index data has increased beyond the capacity of the existing buffers.
        /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
        /// </summary>
        public void Render()
        {
            if (_frameBegun)
            {
                _frameBegun = false;
                ImGuiNET.ImGui.Render();
                RenderImDrawData(ImGuiNET.ImGui.GetDrawData());
            }
        }

        /// <summary>
        /// Updates ImGui input and IO configuration state.
        /// </summary>
        public void Update(float deltaSeconds)
        {
            if (_frameBegun)
            {
                ImGuiNET.ImGui.Render();
            }

            SetPerFrameImGuiData(deltaSeconds);
            UpdateImGuiInput();
            
            _frameBegun = true;
            ImGuiNET.ImGui.NewFrame();
        }

        /// <summary>
        /// Sets per-frame data based on the associated window.
        /// This is called by Update(float).
        /// </summary>
        private void SetPerFrameImGuiData(float deltaSeconds)
        {
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();
            io.DisplaySize = new System.Numerics.Vector2(
                WindowRef.Size.X / _scaleFactor.X,
                WindowRef.Size.Y / _scaleFactor.Y);
            io.DisplayFramebufferScale = _scaleFactor;
            io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
            
        }

        readonly List<char> PressedChars = new List<char>();

        private void UpdateImGuiInput()
        {
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();

            io.MouseDown[0] = WindowRef.IsMouseButtonDown(NbMouseButton.LEFT);
            io.MouseDown[1] = WindowRef.IsMouseButtonDown(NbMouseButton.RIGHT);
            io.MouseDown[2] = WindowRef.IsMouseButtonDown(NbMouseButton.MIDDLE);
            io.MouseWheel = _scrollFactor * WindowRef.MouseScrollDelta.Y;
            io.MouseWheelH = _scrollFactor * WindowRef.MouseScrollDelta.X; 
            
            io.MousePosPrev = new System.Numerics.Vector2(WindowRef.MousePositionPrevious.X, WindowRef.MousePositionPrevious.Y);
            io.MousePos = new System.Numerics.Vector2(WindowRef.MousePosition.X, WindowRef.MousePosition.Y);

            foreach (NbKey key in Enum.GetValues(typeof(NbKey)))
            {
                io.KeysDown[(int) key] = WindowRef.IsKeyDown(key);
            }

            //Use Keypad Enter as normal enter
            io.KeysDown[(int) NbKey.Enter] |= WindowRef.IsKeyDown(NbKey.KeyPadEnter);

            foreach (var c in PressedChars)
            {
                io.AddInputCharacter(c);
            }
            PressedChars.Clear();
            
            io.KeyCtrl = WindowRef.IsKeyDown(NbKey.LeftCtrl) || WindowRef.IsKeyDown(NbKey.RightCtrl);
            io.KeyAlt = WindowRef.IsKeyDown(NbKey.LeftAlt) || WindowRef.IsKeyDown(NbKey.RightAlt);
            io.KeyShift = WindowRef.IsKeyDown(NbKey.LeftShift) || WindowRef.IsKeyDown(NbKey.RightShift);
            io.KeySuper = WindowRef.IsKeyDown(NbKey.LeftSuper) || WindowRef.IsKeyDown(NbKey.RightSuper);
        }

        private static void SetKeyMappings()
        {
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();
            io.KeyMap[(int)ImGuiKey.Tab] = (int)NbKey.Tab;
            io.KeyMap[(int)ImGuiKey.LeftArrow] = (int)NbKey.LeftArrow;
            io.KeyMap[(int)ImGuiKey.RightArrow] = (int)NbKey.RightArrow;
            io.KeyMap[(int)ImGuiKey.UpArrow] = (int)NbKey.UpArrow;
            io.KeyMap[(int)ImGuiKey.DownArrow] = (int)NbKey.DownArrow;
            io.KeyMap[(int)ImGuiKey.PageUp] = (int)NbKey.PageUp;
            io.KeyMap[(int)ImGuiKey.PageDown] = (int)NbKey.PageDown;
            io.KeyMap[(int)ImGuiKey.Home] = (int)NbKey.Home;
            io.KeyMap[(int)ImGuiKey.End] = (int)NbKey.End;
            io.KeyMap[(int)ImGuiKey.Delete] = (int)NbKey.Delete;
            io.KeyMap[(int)ImGuiKey.Backspace] = (int)NbKey.Backspace;
            io.KeyMap[(int)ImGuiKey.Enter] = (int)NbKey.Enter;
            io.KeyMap[(int)ImGuiKey.KeypadEnter] = (int)NbKey.KeyPadEnter;
            io.KeyMap[(int)ImGuiKey.Escape] = (int)NbKey.Escape;
        }

        internal void PressChar(char keyChar)
        {
            PressedChars.Add(keyChar);
        }

        private void RenderImDrawData(ImDrawDataPtr draw_data)
        {
            if (draw_data.CmdListsCount == 0)
            {
                return;
            }

            for (int i = 0; i < draw_data.CmdListsCount; i++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdListsRange[i];

                int vertexSize = cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>();
                if (vertexSize > _vertexBufferSize)
                {
                    int newSize = (int) System.Math.Max(_vertexBufferSize * 1.5f, vertexSize);
                    GL.NamedBufferData(_vertexBuffer, newSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                    _vertexBufferSize = newSize;
                    Console.WriteLine($"Resized dear imgui vertex buffer to new size {_vertexBufferSize}");
                }

                int indexSize = cmd_list.IdxBuffer.Size * sizeof(ushort);
                if (indexSize > _indexBufferSize)
                {
                    int newSize = (int) System.Math.Max(_indexBufferSize * 1.5f, indexSize);
                    GL.NamedBufferData(_indexBuffer, newSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                    _indexBufferSize = newSize;
                    Console.WriteLine($"Resized dear imgui index buffer to new size {_indexBufferSize}");
                }
            }

            // Setup orthographic projection matrix into our constant buffer
            ImGuiIOPtr io = ImGuiNET.ImGui.GetIO();
            Matrix4 mvp = Matrix4.CreateOrthographicOffCenter(
                0.0f,
                io.DisplaySize.X,
                io.DisplaySize.Y,
                0.0f,
                0.0f,
                1.0f);

            _shader.UseShader();
            GL.UniformMatrix4(_shader.GetUniformLocation("projection_matrix"), false, ref mvp);
            GL.Uniform1(_shader.GetUniformLocation("in_fontTexture"), 0);
            ImGuiUtil.CheckGLError("Projection");

            GL.BindVertexArray(_vertexArray);
            ImGuiUtil.CheckGLError("VAO");

            draw_data.ScaleClipRects(io.DisplayFramebufferScale);

            GL.Enable(EnableCap.Blend);
            GL.Enable(EnableCap.ScissorTest);
            GL.BlendEquation(BlendEquationMode.FuncAdd);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.DepthTest);

            // Render command lists
            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdListsRange[n];

                GL.NamedBufferSubData(_vertexBuffer, IntPtr.Zero, cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(), cmd_list.VtxBuffer.Data);
                ImGuiUtil.CheckGLError($"Data Vert {n}");

                GL.NamedBufferSubData(_indexBuffer, IntPtr.Zero, cmd_list.IdxBuffer.Size * sizeof(ushort), cmd_list.IdxBuffer.Data);
                ImGuiUtil.CheckGLError($"Data Idx {n}");

                int vtx_offset = 0;
                int idx_offset = 0;

                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
                {
                    ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
                    if (pcmd.UserCallback != IntPtr.Zero)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        GL.ActiveTexture(TextureUnit.Texture0);
                        GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);
                        ImGuiUtil.CheckGLError("Texture");

                        // We do _windowHeight - (int)clip.W instead of (int)clip.Y because gl has flipped Y when it comes to these coordinates
                        var clip = pcmd.ClipRect;
                        GL.Scissor((int)clip.X, WindowRef.Size.Y - (int)clip.W, (int)(clip.Z - clip.X), (int)(clip.W - clip.Y));
                        ImGuiUtil.CheckGLError("Scissor");

                        if ((io.BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                        {
                            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (IntPtr)((idx_offset + pcmd.IdxOffset) * sizeof(ushort)) , vtx_offset);
                        }
                        else
                        {
                            GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (int)pcmd.IdxOffset * sizeof(ushort));
                        }
                        ImGuiUtil.CheckGLError("Draw");
                    }

                }
                vtx_offset += cmd_list.VtxBuffer.Size;
            }

            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.ScissorTest);
        }

        /// <summary>
        /// Frees all graphics resources used by the renderer.
        /// </summary>
        public void Dispose()
        {
            _fontTexture.Dispose();
            _shader.Dispose();
        }
    }
}
