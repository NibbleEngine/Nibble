using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using NbCore;
using NbCore.Systems;

using ImGuiCore = ImGuiNET.ImGui;

namespace NbCore.UI.ImGui
{
    public class ImGuiObjectViewer
    {
        private SceneGraphNode _model;
        private ImGuiManager _manager;
        
        //Imgui variables to reference 
        private int current_material_sampler = 0;
        private int current_material_flag = 0;
        private int slider_test = 0;
        
        public ImGuiObjectViewer(ImGuiManager mgr)
        {
            _manager = mgr;
        }

        public void SetModel(SceneGraphNode m)
        {
            if (m == null)
                return;
            _model = m;
        }

        public void Draw()
        {

            //Assume that a Popup has begun
            //ImGui.Begin("Info", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

            

            if (_model != null)
            {
                if (_model.IsDisposed())
                {
                    _model = null;
                    return;
                }

                DrawModel();
            }
                
            //ImGui.End();
        
        }

        private void RequestNodeUpdateRecursive(SceneGraphNode n)
        {
            _manager.EngineRef.transformSys.RequestEntityUpdate(n);
            
            foreach (SceneGraphNode child in n.Children)
                RequestNodeUpdateRecursive(child);
        }

        private void DrawModel()
        {
            GUIDComponent gc = _model.GetComponent<GUIDComponent>() as GUIDComponent;
            //Name
            if (ImGuiCore.BeginTable("##NodeInfo", 2, ImGuiTableFlags.None))
            {
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("GUID");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.Text(gc.ID.ToString());
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Type");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.Text(_model.Type.ToString());
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("LOD");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.Text("TODO");
                ImGuiCore.TableNextRow();
                ImGuiCore.TableSetColumnIndex(0);
                ImGuiCore.Text("Name");
                ImGuiCore.TableSetColumnIndex(1);
                ImGuiCore.PushItemWidth(-1.0f);
                ImGuiCore.InputText("##Name", ref _model.Name, 30);
                ImGuiCore.PopItemWidth();
                ImGuiCore.EndTable();
            }
            
            
            //Draw Transform
            TransformData td = TransformationSystem.GetEntityTransformData(_model);
            if (ImGuiCore.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGuiCore.BeginTable("##TransformTable", 4, ImGuiTableFlags.None))
                {
                    //Draw TransformMatrix
                    bool transform_changed = false;
                    ImGuiCore.TableNextRow();
                    ImGuiCore.TableSetColumnIndex(0);
                    ImGuiCore.Text("Translation");
                    ImGuiCore.TableSetColumnIndex(1);
                    ImGuiCore.PushItemWidth(-1.0f);
                    transform_changed |= ImGuiCore.DragFloat("##TransX", ref td.TransX, 0.005f);
                    ImGuiCore.TableSetColumnIndex(2);
                    ImGuiCore.PushItemWidth(-1.0f);
                    transform_changed |= ImGuiCore.DragFloat("##TransY", ref td.TransY, 0.005f);
                    ImGuiCore.TableSetColumnIndex(3);
                    ImGuiCore.PushItemWidth(-1.0f);
                    transform_changed |= ImGuiCore.DragFloat("##TransZ", ref td.TransZ, 0.005f);
                    ImGuiCore.TableNextRow();
                    ImGuiCore.TableSetColumnIndex(0);
                    ImGuiCore.Text("Rotation");
                    ImGuiCore.TableSetColumnIndex(1);
                    transform_changed |= ImGuiCore.DragFloat("##RotX", ref td.RotX);
                    ImGuiCore.TableSetColumnIndex(2);
                    transform_changed |= ImGuiCore.DragFloat("##RotY", ref td.RotY);
                    ImGuiCore.TableSetColumnIndex(3);
                    transform_changed |= ImGuiCore.DragFloat("##RotZ", ref td.RotZ);
                    ImGuiCore.TableNextRow();
                    ImGuiCore.TableSetColumnIndex(0);
                    ImGuiCore.Text("Scale");
                    ImGuiCore.TableSetColumnIndex(1);
                    transform_changed |= ImGuiCore.DragFloat("##ScaleX", ref td.ScaleX, 0.005f);
                    ImGuiCore.TableSetColumnIndex(2);
                    transform_changed |= ImGuiCore.DragFloat("##ScaleY", ref td.ScaleY, 0.005f);
                    ImGuiCore.TableSetColumnIndex(3);
                    transform_changed |= ImGuiCore.DragFloat("##ScaleZ", ref td.ScaleZ, 0.005f);

                    if (transform_changed)
                        RequestNodeUpdateRecursive(_model);

                    ImGuiCore.EndTable();
                }
            }

            //Draw Components


            //SceneComponent
            if (_model.HasComponent<SceneComponent>())
            {
                SceneComponent sc = _model.GetComponent<SceneComponent>() as SceneComponent;

                if (ImGuiCore.CollapsingHeader("Scene Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    //Report Scene Statistics
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("Node Count");
                    ImGuiCore.Text("Light Count");
                    ImGuiCore.Text("Mesh Count");
                    ImGuiCore.Text("Joint Count");
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text(sc.Nodes.Count.ToString());
                    ImGuiCore.Text(sc.LightNodes.Count.ToString());
                    ImGuiCore.Text(sc.MeshNodes.Count.ToString());
                    ImGuiCore.Text(sc.JointNodes.Count.ToString());
                    ImGuiCore.Columns(1);
                }
            }


            //MeshComponent
            if (_model.HasComponent<MeshComponent>())
            {
                MeshComponent mc = _model.GetComponent<MeshComponent>() as MeshComponent;
                if (ImGuiCore.CollapsingHeader("Mesh Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (ImGuiCore.BeginTable("##MeshInfo", 2))
                    {
                        ImGuiCore.TableNextRow();
                        ImGuiCore.TableSetColumnIndex(0);
                        ImGuiCore.Text("Instance ID");
                        ImGuiCore.TableSetColumnIndex(1);
                        ImGuiCore.Text(mc.InstanceID.ToString());
                        ImGuiCore.TableNextRow();
                        ImGuiCore.TableSetColumnIndex(0);
                        ImGuiCore.Text("MeshGroup ID");
                        ImGuiCore.TableSetColumnIndex(1);
                        ImGuiCore.Text(mc.Mesh.Group != null ? mc.Mesh.Group.ID.ToString() : "-1");
                        ImGuiCore.TableNextRow();
                        ImGuiCore.TableSetColumnIndex(0);
                        ImGuiCore.Text("Material");
                        ImGuiCore.TableSetColumnIndex(1);

                        //Items
                        List<Entity> materialList = _manager.EngineRef.GetEntityTypeList(EntityType.Material);
                        materialList = materialList.FindAll(x => ((MeshMaterial)x).Shader != null);
                        materialList = materialList.FindAll(x => ((MeshMaterial)x).Shader.ProgramID != -1);

                        string[] items = new string[materialList.Count];
                        for (int i = 0; i < items.Length; i++)
                        {
                            MeshMaterial mm = (MeshMaterial) materialList[i];
                            items[i] = mm.Name == "" ? "Material_" + i : mm.Name;
                        }
                        
                        int material_id = materialList.IndexOf(mc.Mesh.Material);

                        if (ImGuiCore.Combo("##MaterialCombo", ref material_id, items, items.Length))
                            mc.Mesh.Material = (MeshMaterial) materialList[material_id];

                        ImGuiCore.EndTable();
                    }

                    ImGuiCore.Text("Mesh Uniforms");
                    ImGuiCore.NewLine();

                    if (ImGuiCore.BeginTable("##MeshUniforms", 2))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            ImGuiCore.TableNextRow();
                            ImGuiCore.TableSetColumnIndex(0);
                            ImGuiCore.Text("Uniform " + i);
                            ImGuiCore.TableSetColumnIndex(1);
                            ImGuiCore.PushItemWidth(-1.0f);
                            Math.NbVector4 uf = mc.InstanceUniforms[i].Values;
                            var val = new System.Numerics.Vector4();
                            val.X = uf.X;
                            val.Y = uf.Y;
                            val.Z = uf.Z;
                            val.W = uf.W;
                            
                            if (ImGuiCore.InputFloat4($"##uf{i}", ref val))
                            {
                                mc.InstanceUniforms[i].Values = new(val);
                                mc.IsUpdated = true;
                            }
                            
                            ImGuiCore.PopItemWidth();
                        }
                        ImGuiCore.EndTable();
                    }

                    if (ImGuiCore.TreeNode("Mesh"))
                    {
                        NbMesh mesh = mc.Mesh;
                        ImGuiCore.Columns(2);
                        ImGuiCore.Text("Instance Count");
                        ImGuiCore.NextColumn();
                        ImGuiCore.Text(mesh.InstanceCount.ToString());
                        ImGuiCore.Columns(1);
                        if (ImGuiCore.TreeNode("MetaData"))
                        {
                            ImGuiCore.Columns(2);
                            ImGuiCore.Text("Hash");
                            ImGuiCore.Text("BatchCount");
                            ImGuiCore.Text("Vertex Start Graphics");
                            ImGuiCore.Text("Vertex End Graphics");
                            ImGuiCore.NextColumn();
                            ImGuiCore.Text(mesh.MetaData.Hash.ToString());
                            ImGuiCore.Text(mesh.MetaData.BatchCount.ToString());
                            ImGuiCore.Text(mesh.MetaData.VertrStartGraphics.ToString());
                            ImGuiCore.Text(mesh.MetaData.VertrEndGraphics.ToString());
                            ImGuiCore.TreePop();
                        }
                        ImGuiCore.Columns(1);
                        ImGuiCore.TreePop();
                    }
                    
                    
                
                }
            }

            //LightComponent
            if (_model.HasComponent<LightComponent>())
            {
                LightComponent lc = _model.GetComponent<LightComponent>() as LightComponent;

                if (ImGuiCore.CollapsingHeader("Light Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    bool light_updated = false;
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("Intensity");
                    ImGuiCore.NextColumn();
                    if (ImGuiCore.InputFloat("##Intensity", ref lc.Data.Intensity))
                        light_updated = true;
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("FOV");
                    ImGuiCore.NextColumn();
                    if (ImGuiCore.InputFloat("##fov", ref lc.Data.FOV))
                        light_updated = true;
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("IsRenderable");
                    ImGuiCore.NextColumn();
                    if (ImGuiCore.Checkbox("##renderable", ref lc.Data.IsRenderable))
                        light_updated = true;
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("FallOff");
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text(lc.Data.Falloff.ToString());
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text("Color");
                    ImGuiCore.NextColumn();
                    
                    System.Numerics.Vector3 v = new(lc.Data.Color.X, lc.Data.Color.Y, lc.Data.Color.Z);
                    if (ImGuiCore.ColorPicker3("##Color", ref v))
                    {
                        lc.Data.Color = new(v.X, v.Y, v.Z);
                        light_updated = true;
                    }
                    ImGuiCore.Columns(1);

                    if (light_updated)
                        lc.Data.IsUpdated = true;
                }

            }
        
            //CollisionComponent
            if (_model.HasComponent<CollisionComponent>())
            {
                CollisionComponent cc = _model.GetComponent<CollisionComponent>() as CollisionComponent;

                if (ImGuiCore.CollapsingHeader("Collision Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("CollisionType");
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text(cc.CollisionType.ToString());
                    ImGuiCore.Columns(1);
                }
            }

            //ReferenceComponent
            if (_model.HasComponent<ReferenceComponent>())
            {
                ReferenceComponent rc = _model.GetComponent<ReferenceComponent>() as ReferenceComponent;

                if (ImGuiCore.CollapsingHeader("Reference Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("Reference");
                    ImGuiCore.NextColumn();
                    ImGuiCore.Text(rc.Reference.ToString());
                    ImGuiCore.Columns(1);
                }
            }

            //JointCOmponent
            if (_model.HasComponent<JointComponent>())
            {
                JointComponent jc = _model.GetComponent<JointComponent>() as JointComponent;

                if (ImGuiCore.CollapsingHeader("Joint Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGuiCore.Columns(2);
                    ImGuiCore.Text("JointIndex");
                    ImGuiCore.NextColumn();
                    ImGuiCore.InputInt("##JointIndex", ref jc.JointIndex);
                    ImGuiCore.Columns(1);
                }
            }

            //AnimationComponent
            if (_model.HasComponent<AnimComponent>())
            {
                AnimComponent ac = _model.GetComponent<AnimComponent>() as AnimComponent;

                float lineheight = ImGuiNET.ImGui.GetTextLineHeight();
                if (ImGuiCore.CollapsingHeader("Animation Component", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (ImGuiCore.TreeNode("Animations"))
                    {
                        if (ImGuiCore.BeginTable("##AnimationsTable", 3, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersOuter))
                        {
                            ImGuiCore.TableSetupColumn("Name");
                            ImGuiCore.TableSetupColumn("Actions");
                            ImGuiCore.TableSetupColumn("Frame");
                            ImGuiCore.TableHeadersRow();

                            foreach (Animation anim in ac.AnimGroup.Animations)
                            {
                                ImGuiCore.TableNextRow();
                                ImGuiCore.TableSetColumnIndex(0);
                                ImGuiCore.Selectable(anim.animData.MetaData.Name);
                                ImGuiCore.TableSetColumnIndex(1);
                                string button_title = anim.IsPlaying ? "Stop##" + anim.animData.MetaData.Name :
                                    "Play##" + anim.animData.MetaData.Name;
                                
                                if (ImGuiCore.Button(button_title))
                                {
                                    anim.IsPlaying = !anim.IsPlaying;
                                };
                                ImGuiCore.TableSetColumnIndex(2);
                                ImGuiCore.Checkbox("##Override" + anim.animData.MetaData.Name, ref anim.Override);
                                ImGuiCore.SameLine();

                                if (!anim.Override)
                                    ImGuiCore.PushItemFlag(ImGuiItemFlags.Disabled, true);

                                int temp_frame = anim.ActiveFrameIndex;
                                if (ImGuiCore.SliderInt("##AnimFrame" + anim.animData.MetaData.Name, ref temp_frame, 
                                    0, anim.animData.FrameCount - 1))
                                {
                                    anim.SetFrame(temp_frame); //Kinda stupid but whatever
                                }

                                if (!anim.Override)
                                    ImGuiCore.PopItemFlag();
                            }

                            ImGuiCore.EndTable();
                        }
                        ImGuiCore.TreePop();
                    }
                    

                }
            }
        }

        ~ImGuiObjectViewer()
        {

        }



    }
}
