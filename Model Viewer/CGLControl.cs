using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System.Diagnostics;
using System.IO;
using System.Reflection;

//Custom Imports
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using GLSLHelper;
using gImage;
using OpenTK.Graphics;
using ClearBufferMask = OpenTK.Graphics.OpenGL.ClearBufferMask;
using CullFaceMode = OpenTK.Graphics.OpenGL.CullFaceMode;
using EnableCap = OpenTK.Graphics.OpenGL4.EnableCap;
using PolygonMode = OpenTK.Graphics.OpenGL4.PolygonMode;
using GL = OpenTK.Graphics.OpenGL4.GL;
using System.ComponentModel;
using System.Threading;
using QuickFont;
using ProjProperties = WPFModelViewer.Properties;

namespace Model_Viewer
{
    public class CGLControl : GLControl
    {
        public model rootObject;

        //Common Transforms
        private Matrix4 rotMat, mvp;

        private Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
        private Camera activeCam;

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;
        
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;
        //Camera Movement Speed
        public int movement_speed = 1;

        //Control Identifier
        private int index;
        private int occludedNum = 0;
        private float fpsCount = 0;
        private bool has_focus;

        //Custom Palette
        private Dictionary<string,Dictionary<string,Vector4>> palette;

        //Animation Stuff
        private bool animationStatus = false;
        public List<scene> animScenes = new List<scene>();
        
        //Control private ResourceManagement
        public ResourceMgr resMgr = new ResourceMgr();

        public GBuffer gbuf;

        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem loadAnimationToolStripMenuItem;
        private OpenFileDialog openFileDialog1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private Form pform;

        //Timers
        public System.Timers.Timer inputPollTimer;
        public System.Timers.Timer resizeTimer;

        //Control Font and Text Objects
        private MVCore.Text.TextRenderer font_draw_object;
        
        //Private fps Counter
        private int frames = 0;
        private DateTime oldtime;

        //Gamepad Setup
        public GamepadHandler gpHandler;
        public KeyboardHandler kbHandler;
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        //Rendering Thread Stuff
        private Thread rendering_thread;
        private Queue<ThreadRequest> rt_req_queue = new Queue<ThreadRequest>();
        private bool rt_exit;
        
        private void registerFunctions()
        {
            this.Load += new System.EventHandler(this.genericLoad);
            //this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.Resize += new System.EventHandler(this.OnResize); 
            this.MouseHover += new System.EventHandler(this.genericHover);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.genericMouseMove);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.CGLControl_MouseClick);
            //this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.generic_KeyDown);
            this.Enter += new System.EventHandler(this.genericEnter);
            this.Leave += new System.EventHandler(this.genericLeave);
        }

        //Default Constructor
        public CGLControl(): base(new GraphicsMode(32, 24, 0, 8))
        {
            registerFunctions();

            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Input Polling Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 10;
            
            //Resize Timer
            resizeTimer = new System.Timers.Timer();
            resizeTimer.Elapsed += new System.Timers.ElapsedEventHandler(ResizeControl);
            resizeTimer.Interval = 10;
            
            //Set properties
            this.VSync = false;

            //Compile Shaders
        }

        //Constructor
        public CGLControl(int index, Form parent)
        {
            registerFunctions();
            
            //Set Control Identifiers
            this.index = index;

            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Assign new palette to GLControl
            palette = Model_Viewer.Palettes.createPalettefromBasePalettes();

            //Set parent form
            if (parent != null)
                pform = parent;

            //Control Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 10;
            inputPollTimer.Start();
        }

        private void input_poller(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            //gamepadController(); //Move camera according to input
            bool focused = false;

            this.Invoke((MethodInvoker) delegate
            {
                focused = this.Focused;
            });

            if (focused)
                keyboardController(); //Move camera according to input
        }

        private void rt_render()
        {
            //Update per frame data
            frameUpdate();

            gbuf.start();

            //Console.WriteLine(active_fbo);
            render_scene();

            //Store the dumps

            //gbuf.dump();
            //render_decals();

            //render_cameras();

            if (RenderOptions.RenderLights)
                render_lights();

            //Dump Gbuffer
            //gbuf.dump();
            //System.Threading.Thread.Sleep(1000);

            //Render Deferred
            gbuf.render();

            //No need to blit without a renderbuffer
            //gbuf?.stop();

            //Render info right on the 0 buffer
            if (RenderOptions.RenderInfo)
                render_info();
        }

        public void SetupItems()
        {
            //This function is used to setup all necessary additional parameters on the objects.
            
            //Set new palettes
            traverse_oblistPalette(rootObject, palette);
            //Find animScenes
            findAnimScenes();
            GC.Collect();
        }

        public void findAnimScenes()
        {
            foreach (scene scn in resMgr.GLScenes.Values)
            {
                if (scn.jointDict.Values.Count > 0)
                    this.animScenes.Add(scn);
            }
        }

        public void traverse_oblistPalette(model root,Dictionary<string,Dictionary<string,Vector4>> palette)
        {
            foreach (model in_m in root.children)
            {
                if (in_m.type != TYPES.MESH)
                {
                    if (in_m.children.Count != 0)
                        traverse_oblistPalette(in_m, palette);
                }

                meshModel m = (meshModel) in_m;

                //Fix New Recoulors
                if (m.material != null)
                {
                    m.material.palette = palette;
                    for (int i = 0; i < 8; i++)
                    {
                        PaletteOpt palOpt = m.material.palOpts[i];
                        if (palOpt != null)
                            m.material.reColourings[i] = new float[] { palette[palOpt.PaletteName][palOpt.ColorName][0],
                                                                       palette[palOpt.PaletteName][palOpt.ColorName][1],
                                                                       palette[palOpt.PaletteName][palOpt.ColorName][2],
                                                                                                                   1.0f };
                        else
                            m.material.reColourings[i] = new float[] { 1.0f, 1.0f, 1.0f, 0.0f};
                    }

                    //Recalculate Textures
                    GL.DeleteTexture(m.material.fDiffuseMap.bufferID);
                    GL.DeleteTexture(m.material.fMaskMap.bufferID);
                    GL.DeleteTexture(m.material.fNormalMap.bufferID);


                    m.material.prepTextures();
                    m.material.mixTextures();
                }
            }
        }

        //Per Frame Updates
        private void frameUpdate()
        {
            //Fetch Updates on Joints on all animscenes
            for (int i = 0; i < animScenes.Count; i++)
            {
                scene animScene = animScenes[i];
                foreach (Joint j in animScene.jointDict.Values)
                {
                    MathUtils.insertMatToArray16(animScene.JMArray, j.jointIndex * 16, j.worldMat);
                }

                //Calculate skinning matrices for each joint for each geometry object
                MathUtils.mulMatArrays(ref animScene.gobject.skinMats, animScene.gobject.invBMats,
                    animScene.JMArray, animScene.jointDict.Keys.Count);
            }
            
            
            rootObject?.update();

            //Camera & Light Positions
            //Update common transforms
            activeCam.aspect = (float) ClientSize.Width / ClientSize.Height;
                
            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(rot[0]));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(rot[1]));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(rot[2]));
            rotMat = Rotz * Rotx * Roty;
            mvp = rotMat * activeCam.viewMat; //Full mvp matrix
            MVCore.Common.RenderState.mvp = mvp;

            resMgr.GLCameras[0].updateViewMatrix();
            resMgr.GLCameras[1].updateViewMatrix();

            //Update Custom Light Position
            updateLightPosition(0);
            resMgr.GLlights[0].update(); //Update transforms

            //Update Frame Counter
            fps();

        }

        //Main Rendering Routines
        private void ControlLoop()
        {
            //Setup new Context
            IGraphicsContext new_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), this.WindowInfo);
            new_context.MakeCurrent(this.WindowInfo);
            this.MakeCurrent(); //This is essential

            //Add default primitives trying to avoid Vao Request queue traffic
            addDefaultLights();
            addDefaultTextures();
            addCamera();
            addCamera(cull:false); //Add second camera
            setActiveCam(0);
            addDefaultPrimitives();
            addTestObjects();

            //Create gbuffer
            gbuf = new GBuffer(this.resMgr, this.ClientSize.Width, this.ClientSize.Height);
            MVCore.Common.RenderState.gbuf = gbuf;
            gbuf.init();

            bool renderFlag = true; //Toggle rendering on/off

            //Rendering Loop
            while (!rt_exit)
            {
                //Check for new scene request
                if (rt_req_queue.Count > 0)
                {
                    ThreadRequest req;
                    lock (rt_req_queue)
                    {
                        //Try to group  Resizing requests
                        req = rt_req_queue.Dequeue();
                    }

                    lock (req)
                    {
                        switch (req.type)
                        {
                            case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                                lock (inputPollTimer)
                                {
                                    inputPollTimer.Stop();
                                    rt_addRootScene((string)req.arguments[0]);
                                    req.status = THREAD_REQUEST_STATUS.FINISHED;
                                    inputPollTimer.Start();
                                }
                                break;
                            case THREAD_REQUEST_TYPE.UPDATE_SCENE_REQUEST:
                                scene req_scn = (scene) req.arguments[0];
                                req_scn.update();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.RESIZE_REQUEST:
                                rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.MODIFY_SHADER_REQUEST:
                                GLShaderHelper.modifyShader((GLSLShaderConfig) req.arguments[0],
                                             (string)req.arguments[1],
                                             (OpenTK.Graphics.OpenGL4.ShaderType)req.arguments[2]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.TERMINATE_REQUEST:
                                rt_exit = true;
                                renderFlag = false;
                                inputPollTimer.Stop();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.PAUSE_RENDER_REQUEST:
                                renderFlag = false;
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.RESUME_RENDER_REQUEST:
                                renderFlag = true;
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.NULL:
                                break;
                        }
                    }
                }
                
                if (renderFlag)
                {
                    rt_render();
                }

                SwapBuffers();

                Thread.Sleep(2);

            }
        }

        #region Rendering Methods

        private void traverse_render(model root, int program)
        {
            int active_program = root.shader_programs[program];

            GL.UseProgram(active_program);

            if (active_program == -1)
                throw new ApplicationException("Shit program");

            int loc;

            if (root.renderable)
            {
                Matrix4 wMat = root.worldMat;
                GL.UniformMatrix4(10, false, ref wMat);

                //Send mvp to all shaders
                GL.UniformMatrix4(7, false, ref mvp);

                //Upload Selected Flag
                GL.Uniform1(208, root.selected);

                if (root.type == TYPES.MESH)
                {
                    //Sent rotation matrix individually for light calculations
                    GL.UniformMatrix4(9, false, ref rotMat);

                    //Send DiffuseFlag
                    GL.Uniform1(206, RenderOptions.UseTextures);

                    //Upload Selected Flag
                    GL.Uniform1(207, RenderOptions.UseLighting);

                    //Object program
                    //Local Transformation is the same for all objects 
                    //Pending - Personalize local matrix on each object
                    loc = GL.GetUniformLocation(active_program, "light");
                    GL.Uniform3(loc, this.resMgr.GLlights[0].localPosition);

                    //Upload Light Intensity
                    loc = GL.GetUniformLocation(active_program, "intensity");
                    GL.Uniform1(210, light_intensity);

                    //Upload camera position as the light
                    //GL.Uniform3(loc, cam.Position);

                    //Apply frustum culling only for mesh objects
                    if (activeCam.frustum_occlude((meshModel) root, rotMat))
                        root.render(program);
                    else occludedNum++;
                    
                }
                else if (root.type == TYPES.JOINT)
                {
                    if (RenderOptions.RenderJoints)
                        root.render(program);
                }
                else if (root.type == TYPES.COLLISION)
                {
                    if (RenderOptions.RenderCollisions)
                    {
                        //Send DiffuseFlag
                        GL.Uniform1(206, 0.0f);

                        //Upload Selected Flag
                        GL.Uniform1(207, 0.0f);
                        root.render(program);
                    }
                        
                }
                else if (root.type == TYPES.LOCATOR || root.type == TYPES.SCENE || root.type == TYPES.LIGHT)
                {
                    root.render(program);
                }
            }

            //Render children
            foreach (model child in root.children)
                traverse_render(child, program);

        }
        
        private void render_scene()
        {
            //Console.WriteLine("Rendering Scene Cam Position : {0}", this.activeCam.Position);
            //Console.WriteLine("Rendering Scene Cam Orientation: {0}", this.activeCam.Orientation);
            //GL.CullFace(CullFaceMode.Back);
            //GL.Enable(EnableCap.DepthTest);

            occludedNum = 0; //This will be incremented from traverse_render
            //Render only the first scene for now
            if (this.rootObject != null)
            {
                //Drawing Phase
                traverse_render(this.rootObject, 0);
                //Drawing Debug
                //if (RenderOptions.RenderDebug) traverse_render(this.mainScene, 1);
            }
        }

        private void render_lights()
        {
            int active_program = MVCore.Common.RenderState.activeResMgr.GLShaders["LIGHT_SHADER"];
            GL.UseProgram(active_program);
            
            //Send mvp to all shaders
            int loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);
            for (int i=0; i<resMgr.GLlights.Count; i++)
                resMgr.GLlights[i].render(0);
        }

        private void render_cameras()
        {
            int active_program = resMgr.GLShaders["BBOX_SHADER"];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref activeCam.viewMat);
            //Send object world Matrix to all shaders

            foreach (Camera cam in resMgr.GLCameras)
            {
                //Old rendering the inverse clip space
                //Upload uniforms
                //loc = GL.GetUniformLocation(active_program, "self_mvp");
                //Matrix4 self_mvp = cam.viewMat;
                //GL.UniformMatrix4(loc, false, ref self_mvp);

                //New rendering the exact frustum plane
                loc = GL.GetUniformLocation(active_program, "worldMat");
                Matrix4 test = Matrix4.Identity;
                test[0,0] = -1.0f;
                test[1,1] = -1.0f;
                test[2,2] = -1.0f;
                GL.UniformMatrix4(loc, false, ref test);

                //Render all inactive cameras
                if (!cam.isActive) cam.render();
                    
            }

        }

        private void render_info()
        {
            //GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            var size = font_draw_object.addDrawing(string.Format("FPS: {0:F1}", fpsCount),
                    new Vector3(Width - 80.0f, 30.0f, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.FPS);
            
            //Console.WriteLine(fpsCount.ToString());
            font_draw_object.addDrawing(string.Format("Occl.: {0}", occludedNum),
                    new Vector3(Width - 80.0f, 30.0f - size.Height, 0.0f), System.Drawing.Color.Yellow, MVCore.Text.GLTEXT_INDEX.MSG1);

            //Render drawings
            font_draw_object.render(Width, Height);

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

        }

        #endregion Rendering Methods

        #region GLControl Methods
        private void genericEnter(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //Debug.WriteLine("Entered Focus Control " + index);
            inputPollTimer.Start();
        }

        private void genericHover(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //this.MakeCurrent(); //Control should have been active on hover
            inputPollTimer.Start();
        }

        private void genericLeave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            //Debug.WriteLine("Left Focus of Control "+ index);
            inputPollTimer.Stop();

        }

        private void genericPaint(object sender, PaintEventArgs e)
        {
            //TODO: Should I add more stuff in here?
        }

        private void genericLoad(object sender, EventArgs e)
        {

            this.InitializeComponent();
            this.MakeCurrent();

            //Once the context is initialized compile the shaders
            compileShaders();

            //Load font should be done before being used by the rendering thread and after the shaders are live
            setupTextRenderer();


            kbHandler = new KeyboardHandler();
            //gpHandler = new GamepadHandler(); TODO: Add support for PS4 controller

            //Everything ready to swap threads
            setupRenderingThread();

            //Start Timers
            inputPollTimer.Start();
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            int delta_x = (e.X - mouse_x);
            int delta_y = (e.Y - mouse_y);

            delta_x = Math.Min(Math.Max(delta_x, -10), 10);
            delta_y = Math.Min(Math.Max(delta_y, -10), 10);

            if (e.Button == MouseButtons.Left)
            {
                //Debug.WriteLine("Deltas {0} {1} {2}", delta_x, delta_y, e.Button);
                activeCam.AddRotation(delta_x, delta_y);
            }

            mouse_x = e.X;
            mouse_y = e.Y;
            
        }

        private void generic_KeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Debug.WriteLine("Key pressed {0}",e.KeyCode);
            switch (e.KeyCode)
            {
                //Light Rotation
                case Keys.N:
                    this.light_angle_y -= 1;
                    break;
                case Keys.M:
                    this.light_angle_y += 1;
                    break;
                case Keys.Oemcomma:
                    this.light_angle_x -= 1;
                    break;
                case Keys.OemPeriod:
                    this.light_angle_x += 1;
                    break;
                /*
                //Toggle Wireframe
                case Keys.I:
                    if (RenderOptions.RENDERMODE == PolygonMode.Fill)
                        RenderOptions.RENDERMODE = PolygonMode.Line;
                    else
                        RenderOptions.RENDERMODE = PolygonMode.Fill;
                    break;
                //Toggle Texture Render
                case Keys.O:
                    RenderOptions.UseTextures = 1.0f - RenderOptions.UseTextures;
                    break;
                //Toggle Collisions Render
                case Keys.OemOpenBrackets:
                    RenderOptions.RenderCollisions = !RenderOptions.RenderCollisions;
                    break;
                //Toggle Debug Render
                case Keys.OemCloseBrackets:
                    RenderOptions.RenderDebug = !RenderOptions.RenderDebug;
                    break;
                */
                //Switch cameras
                case Keys.NumPad0:
                    if (this.resMgr.GLCameras[0].isActive)
                        setActiveCam(1);
                    else
                        setActiveCam(0);
                    break;
                //Animation playback (Play/Pause Mode) with Space
                case Keys.Space:
                    animationStatus = !animationStatus;
                    if (animationStatus)
                        backgroundWorker1.RunWorkerAsync();
                    else
                        backgroundWorker1.CancelAsync();
                    break;
                default:
                    //Console.WriteLine("Not Implemented Yet");
                    break;
            }

        }

        private void ResizeControl(object sender, System.Timers.ElapsedEventArgs e)
        {
            resizeTimer.Stop();

            //Make new request
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.RESIZE_REQUEST;
            req.arguments.Clear();
            req.arguments.Add(ClientSize.Width);
            req.arguments.Add(ClientSize.Height);

            issueRequest(req);
        }

        private void OnResize(object sender, EventArgs e)
        {
            //Check the resizeTimer
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadAnimationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToObjToolStripMenuItem,
            this.loadAnimationToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 70);
            // 
            // exportToObjToolStripMenuItem
            // 
            this.exportToObjToolStripMenuItem.Name = "exportToObjToolStripMenuItem";
            this.exportToObjToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.exportToObjToolStripMenuItem.Text = "Export to obj";
            this.exportToObjToolStripMenuItem.Click += new System.EventHandler(this.exportToObjToolStripMenuItem_Click);
            // 
            // loadAnimationToolStripMenuItem
            // 
            this.loadAnimationToolStripMenuItem.Name = "loadAnimationToolStripMenuItem";
            this.loadAnimationToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.loadAnimationToolStripMenuItem.Text = "Load Animation";
            this.loadAnimationToolStripMenuItem.Click += new System.EventHandler(this.loadAnimationToolStripMenuItem_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.WorkerSupportsCancellation = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);
            // 
            // CGLControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Name = "CGLControl";
            this.Size = new System.Drawing.Size(314, 213);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

               
        private void CGLControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(Control.MousePosition);
            }
            //TODO: ADD SELECT OBJECT FUNCTIONALITY IN THE FUTURE
            //else if ((e.Button == MouseButtons.Left) && (ModifierKeys == Keys.Control))
            //{
            //    selectObject(e.Location);
            //}
        }

        #endregion GLControl Methods

        #region ShaderMethods

        public void issuemodifyShaderRequest(GLSLShaderConfig config, string shaderText, OpenTK.Graphics.OpenGL4.ShaderType shadertype)
        {
            Console.WriteLine("Sending Shader Modification Request");
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.MODIFY_SHADER_REQUEST;
            req.arguments.Add(config);
            req.arguments.Add(shaderText);
            req.arguments.Add(shadertype);
            
            //Send request
            issueRequest(req);
        }

        //GLPreparation
        private void compileShader(string vs, string fs, string gs, string tes, string tcs, string name, ref string log)
        {
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(vs, fs, gs, tcs, tes, name);
            //Set modify Shader delegate
            shader_conf.modifyShader = issuemodifyShaderRequest;

            compileShader(shader_conf);
            resMgr.GLShaderConfigs[shader_conf.name] = shader_conf;
            resMgr.GLShaders[shader_conf.name] = shader_conf.program_id;
            log += shader_conf.log; //Append log
        }


        private void compileShaders()
        {

#if(DEBUG)
            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
                if (s.Contains("16"))
                    Console.WriteLine(s);
            }

            //Query maximum buffer sizes
            Console.WriteLine("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));
#endif

            //Populate shader list
            string log = "";

            //Geometry Shader
            //Compile Object Shaders
            //Create Shader Config
            compileShader("Shaders/Simple_VSEmpty.glsl",
                            "Shaders/Simple_FSEmpty.glsl",
                            "Shaders/Simple_GS.glsl",
                            "", "", "DEBUG_SHADER", ref log);

            //Picking Shaders
            compileShader(ProjProperties.Resources.pick_vert,
                            ProjProperties.Resources.pick_frag,
                            "", "", "", "PICKING_SHADER", ref log);


            //Main Object Shader
            compileShader("Shaders/Simple_VS.glsl",
                            "Shaders/Simple_FS.glsl",
                            "", "", "", "MESH_SHADER", ref log);


            //BoundBox Shader
            compileShader("Shaders/Bound_VS.glsl",
                            "Shaders/Bound_FS.glsl",
                            "", "", "", "BBOX_SHADER", ref log);

            //Texture Mixing Shader
            compileShader("Shaders/pass_VS.glsl",
                            "Shaders/pass_FS.glsl",
                            "", "", "", "TEXTURE_MIXING_SHADER", ref log);

            //GBuffer Shaders
            compileShader("Shaders/Gbuffer_VS.glsl",
                            "Shaders/Gbuffer_FS.glsl",
                            "", "", "", "GBUFFER_SHADER", ref log);

            //Decal Shaders
            compileShader("Shaders/decal_VS.glsl",
                            "Shaders/Decal_FS.glsl",
                            "", "", "", "DECAL_SHADER", ref log);

            //Locator Shaders
            compileShader(ProjProperties.Resources.locator_vert,
                            ProjProperties.Resources.locator_frag,
                            "", "", "", "LOCATOR_SHADER", ref log);

            //Joint Shaders
            compileShader(ProjProperties.Resources.joint_vert,
                            ProjProperties.Resources.joint_frag,
                            "", "", "", "JOINT_SHADER", ref log);

            //Text Shaders
            compileShader(ProjProperties.Resources.text_vert,
                            ProjProperties.Resources.text_frag,
                            "", "", "", "TEXT_SHADER", ref log);

            //Light Shaders
            compileShader(ProjProperties.Resources.light_vert,
                            ProjProperties.Resources.light_frag,
                            "", "", "", "LIGHT_SHADER", ref log);

            //Camera Shaders
            compileShader(ProjProperties.Resources.camera_vert,
                            ProjProperties.Resources.camera_frag,
                            "", "", "", "CAMERA_SHADER", ref log);

        }

        public void compileShader(GLSLShaderConfig config)
        {
            int vertexObject;
            int fragmentObject;

            if (config.program_id != -1)
                GL.DeleteProgram(config.program_id);

            GLShaderHelper.CreateShaders(config, out vertexObject, out fragmentObject, out config.program_id);
        }

        
        #endregion ShaderMethods

        #region ContextMethods

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to obj");
            SaveFileDialog sv = new SaveFileDialog();
            sv.Filter = "OBJ Files | *.obj";
            sv.DefaultExt = "obj";
            DialogResult res = sv.ShowDialog();

            if (res != DialogResult.OK)
                return;

            StreamWriter obj = new StreamWriter(sv.FileName);

            obj.WriteLine("# No Mans Model Viewer OBJ File:");
            obj.WriteLine("# www.3dgamedevblog.com");

            //Iterate in objects
            uint index = 1;
            findGeoms(rootObject, obj, ref index);
            
            obj.Close();
            
        }

        private void findGeoms(model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type==TYPES.COLLISION)
            {
                //Get converted text
                meshModel me = (meshModel) m;
                me.writeGeomToStream(s, ref index);

            }
            foreach (model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }

        private void loadAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AnimationSelectForm aform = new AnimationSelectForm(this);
            aform.Show();
        }

        #endregion ContextMethods

        #region ControlSetup_Init

        //Setup
        public void setupTextRenderer()
        {
            //Use QFont
            //string font = "C:\\WINDOWS\\FONTS\\LUCON.TTF";
            string font = "C:\\WINDOWS\\FONTS\\ARIAL.TTF";
            font_draw_object = new MVCore.Text.TextRenderer(font, 10);
        }

        public void setupRenderingThread()
        {
            
            //Setup rendering thread
            Context.MakeCurrent(null);
            rendering_thread = new Thread(ControlLoop);
            rendering_thread.IsBackground = true;
            rendering_thread.Priority = ThreadPriority.AboveNormal;

            //Start RT Thread
            rendering_thread.Start();
        }

        #endregion ControlSetup_Init

        #region Camera Update Functions
        public void setActiveCam(int index)
        {
            if (activeCam != null)
                activeCam.isActive = false;
            activeCam = resMgr.GLCameras[index];
            activeCam.isActive = true;
            Console.WriteLine("Switching Camera to {0}", index);
        }

        public void updateActiveCam(int FOV, float zNear, float zFar)
        {
            //TODO: REMOVE, FOR TESTING I"M WORKING ONLY ON THE FIRST CAM
            resMgr.GLCameras[0].setFOV(FOV);
            resMgr.GLCameras[0].zFar = zFar;
            resMgr.GLCameras[0].zNear = zNear;
        }

        public void updateActiveCam(Vector3 pos)
        {
            activeCam.Position = pos;
        }

        #endregion

        public void updateControlRotation(float rx, float ry)
        {
            rot.X = rx;
            rot.Y = ry;
        }

        #region AddObjectMethods

        private void addCamera(bool cull = true)
        {
            //Set Camera position
            Camera cam = new Camera(60, this.resMgr.GLShaders["BBOX_SHADER"], 0, cull);
            for (int i = 0; i < 20; i++)
                cam.Move(0.0f, -0.1f, 0.0f);
            cam.isActive = false;
            resMgr.GLCameras.Add(cam);
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //Add Default textures
            //White tex
            string texpath = Path.Combine(execpath, "default.dds");
            Texture tex = new Texture(texpath);
            this.resMgr.GLtextures["default.dds"] = tex;
            //Transparent Mask
            texpath = Path.Combine(execpath, "default_mask.dds");
            tex = new Texture(texpath);
            this.resMgr.GLtextures["default_mask.dds"] = tex;

        }

        private void addDefaultPrimitives()
        {
            //Default quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad(1.0f, 1.0f);
            resMgr.GLPrimitiveVaos["default_quad"] = q.getVAO();

            //Default render quad
            q = new MVCore.Primitives.Quad();
            resMgr.GLPrimitiveVaos["default_renderquad"] = q.getVAO();

            //Default cross
            MVCore.Primitives.Cross c = new MVCore.Primitives.Cross();
            resMgr.GLPrimitiveVaos["default_cross"] = c.getVAO();

            //Default cube
            MVCore.Primitives.Box bx = new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f);
            resMgr.GLPrimitiveVaos["default_box"] = bx.getVAO();

            //Default sphere
            MVCore.Primitives.Sphere sph = new MVCore.Primitives.Sphere(new Vector3(0.0f,0.0f,0.0f), 100.0f);
            resMgr.GLPrimitiveVaos["default_sphere"] = sph.getVAO();
        }

        private void addTestObjects()
        {
            
        }

        #endregion AddObjectMethods


        public void issueRequest(ThreadRequest r)
        {
            lock (rt_req_queue)
            {
                rt_req_queue.Enqueue(r);
            }
        }

        private void rt_ResizeViewport(int w, int h)
        {
            gbuf?.resize(w, h);
            GL.Viewport(0, 0, w, h);
            //GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
        }

        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();

            //Clear Form Resources
            resMgr.Cleanup();
            ModelProcGen.procDecisions.Clear();
            //Clear animScenes
            animScenes.Clear();
            //Throw away the old model
            rootObject.Dispose();
            rootObject = null;

            //Add defaults
            addDefaultLights();
            addDefaultTextures();
            addCamera(true);
            addCamera(false);
            setActiveCam(0);
            addDefaultPrimitives();

            //Setup new object
            rootObject = GEOMMBIN.LoadObjects(filename);
             
            //find Animation Capable Scenes
            this.findAnimScenes();

            //Refresh all transforms
            rootObject.update();

        }

        //Light Functions
        private void addDefaultLights()
        {
            //Add one and only light for now
            Light light = new Light();
            light.shader_programs = new int[] { this.resMgr.GLShaders["LIGHT_SHADER"] };
            light.localPosition = new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0)));

            this.resMgr.GLlights.Add(light);
        }

        public void updateLightPosition(int light_id)
        {
            Light light = resMgr.GLlights[light_id];
            light.updatePosition(new Vector3 ((float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Sin(MathUtils.radians(light_angle_y))),
                                                (float)(light_distance * Math.Sin(MathUtils.radians(light_angle_x))),
                                                (float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Cos(MathUtils.radians(light_angle_y)))));
        }

        private void fps()
        {
            //Get FPS
            DateTime now = DateTime.UtcNow;
            TimeSpan time = now - oldtime;
            int measurement_interval = 100;

            if (time.TotalMilliseconds > measurement_interval)
            {
                fpsCount = 1000 * frames / (float) measurement_interval;
                //Console.WriteLine("{0} {1}", frames, fps);
                //Reset
                frames = 0;
                oldtime = now;
            }
            else
                frames += 1;

        }


        #region INPUT_HANDLERS

        //Gamepad handler
        private void gamepadController()
        {
            if (gpHandler == null) return;
            
            //This Method handles and controls the gamepad input
            gpHandler.updateState();
            //gpHandler.reportAxes();
            
            //Move camera
            //Console.WriteLine(gpHandler.getBtnState(1) - gpHandler.getBtnState(0));
            //Console.WriteLine(gpHandler.getAxsState(0, 1));
            for (int i = 0; i < movement_speed; i++)
                activeCam.Move(0.1f * gpHandler.getAxsState(0, 0),
                               0.1f * gpHandler.getAxsState(0, 1),
                               gpHandler.getBtnState(1) - gpHandler.getBtnState(0));
            
            //Rotate Camera
            //for (int i = 0; i < movement_speed; i++)
            activeCam.AddRotation(-3.0f * gpHandler.getAxsState(1, 0), 3.0f * gpHandler.getAxsState(1, 1));
            //Console.WriteLine("Camera Orientation {0} {1}", activeCam.Orientation.X,
            //    activeCam.Orientation.Y,
            //    activeCam.Orientation.Z);
        }

        //Keyboard handler
        private void keyboardController()
        {
            if (kbHandler == null) return;

            //This Method handles and controls the gamepad input
            
            kbHandler.updateState();
            //gpHandler.reportAxes();

            //Camera Movement
            float step = movement_speed * 0.01f;
            activeCam.Move(
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.D) - kbHandler.getKeyStatus(OpenTK.Input.Key.A)),
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.W) - kbHandler.getKeyStatus(OpenTK.Input.Key.S)),
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.R) - kbHandler.getKeyStatus(OpenTK.Input.Key.F)));

            
            //Rotate Axis
            rot.Y += step * (kbHandler.getKeyStatus(OpenTK.Input.Key.E) - kbHandler.getKeyStatus(OpenTK.Input.Key.Q));
            rot.X += step * (kbHandler.getKeyStatus(OpenTK.Input.Key.C) - kbHandler.getKeyStatus(OpenTK.Input.Key.Z));
            
        }

        #endregion

        #region ANIMATION_PLAYBACK
        //Animation Playback
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                double pause = (1000.0d / (double) RenderOptions.animFPS);
                System.Threading.Thread.Sleep((int)(Math.Round(pause, 1)));
                backgroundWorker1.ReportProgress(0);

                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }
        
        //Animation Worker
        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            foreach (scene s in animScenes)
                if (s.animMeta != null)
                {
                    s.animate();
                }

        }

        #endregion ANIMATION_PLAYBACK

        #region DISPOSE_METHODS

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                rootObject.Dispose();
                gbuf.Dispose();
                font_draw_object.Dispose();

            }

            //Free unmanaged resources
            disposed = true;
        }

        #endregion DISPOSE_METHODS

    }

}
