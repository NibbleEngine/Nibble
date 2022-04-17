using System;
using System.Collections.Generic;
using System.IO;
using NbCore;
using NbCore.Common;
using NbCore.Math;
using NbCore.Plugins;
using NbCore.Systems;
using NbCore.UI.ImGui;

using ImGuiCore = ImGuiNET.ImGui;


namespace NibbleOBJPlugin
{
    public class Plugin : PluginBase
    {
        private static readonly string PluginName = "OBJPlugin";
        private static readonly string PluginVersion = "1.0.0";
        private static readonly string PluginDescription = "OBJ Plugin for Nibble Engine. Created by gregkwaste";
        private static readonly string PluginCreator = "gregkwaste";

        private OpenFileDialog openFileDialog;

        
        public Plugin(Engine e) : base(e)
        {
            Name = PluginName;
            Version = PluginVersion;
            Description = PluginDescription;
            Creator = PluginCreator;
        }

        public override void OnLoad()
        {
            var assemblypath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            openFileDialog = new("obj-open-file", ".obj", false); //Initialize OpenFileDialog
            openFileDialog.SetDialogPath(assemblypath);
            Log("Loaded OBJ Plugin", LogVerbosityLevel.INFO);
        }

        public override void Import(string filepath)
        {
            SceneGraphNode node = ParseObj(filepath);
            foreach (SceneGraphNode child in node.Children)
                EngineRef.ImportScene(child);
        }

        private NbMesh GenerateMesh(List<NbVector3> lverts, List<NbVector3i> ltris)
        {
            NbMeshData data = GenerateGeometryData(lverts, ltris);
            NbMeshMetaData metadata = GenerateGeometryMetaData(data);

            //Generate NbMesh
            NbMesh mesh = new()
            {
                Hash = NbHasher.CombineHash(data.Hash, metadata.GetHash()),
                Data = data,
                MetaData = metadata
            };

            return mesh;
        }

        private NbMeshMetaData GenerateGeometryMetaData(NbMeshData data)
        {
            NbMeshMetaData metadata = new()
            {
                BatchCount = data.IndexBuffer.Length / 0x4,
                FirstSkinMat = 0,
                LastSkinMat = 0,
                VertrEndGraphics = data.VertexBuffer.Length / (0x3 * 0x4) - 1,
                VertrEndPhysics = data.VertexBuffer.Length / (0x3 * 0x4)
            };

            return metadata;
        }

        private NbMeshData GenerateGeometryData(List<NbVector3> lverts, List<NbVector3i> ltris)
        {
            NbMeshData data = NbMeshData.Create();
            
            //Save vertices
            int vxbytecount = lverts.Count * 3 * 4;
            int ixbytecount = ltris.Count * 3 * 4;
            data.VertexBuffer = new byte[vxbytecount];
            data.IndexBuffer = new byte[ixbytecount];

            MemoryStream ms = new MemoryStream(data.VertexBuffer);
            BinaryWriter bw = new BinaryWriter(ms);
            bw.Seek(0, SeekOrigin.Begin);

            for (int i = 0; i < lverts.Count; i++)
            {
                bw.Write(lverts[i].X);
                bw.Write(lverts[i].Y);
                bw.Write(lverts[i].Z);
                
            }
            bw.Flush();
            bw.Close();


            ms = new MemoryStream(data.IndexBuffer);
            bw = new BinaryWriter(ms);
            bw.Seek(0, SeekOrigin.Begin);

            for (int i = 0; i < ltris.Count; i++)
            {
                bw.Write(ltris[i].X);
                bw.Write(ltris[i].Y);
                bw.Write(ltris[i].Z);
            }

            //Create Buffers
            data.buffers = new bufInfo[1];
            
            bufInfo buf = new bufInfo()
            {
                count = 3,
                normalize = false,
                offset = 0,
                sem_text = "vPosition",
                semantic = 0,
                stride = 12,
                type = NbPrimitiveDataType.Float
            };

            data.buffers[0] = buf;
            //Use buffer information to calculate the per vertex stride
            data.VertexBufferStride = 0x4 * 0x3;

            data.Hash = NbHasher.CombineHash(NbHasher.Hash(data.VertexBuffer),
                                             NbHasher.Hash(data.IndexBuffer));
            return data;
        }

        private SceneGraphNode SubmitMesh(string name, MeshMaterial mat, List<NbVector3> Vertices, List<NbVector3i> Tris)
        {
            NbMesh mesh = GenerateMesh(Vertices, Tris);
            mesh.Material = mat;
            return EngineRef.CreateMeshNode(name, mesh);
        }
        
        public SceneGraphNode ParseObj(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Open);
            StreamReader sr = new StreamReader(fs);

            List<NbVector3> VertexPositions = new();
            List<NbVector3> VertexNormals = new();
            List<NbVector2> VertexUVs = new();

            //Final Data
            List<NbVector3> Vertices = new();
            List<NbVector3> Normals = new();
            List<NbVector3i> Tris = new();
            int indexCount = 0;
            bool mesh_submitted = true;
            string node_name = "";

            //Generate Material
            //TODO: Parse all material from the mtl file.
            MeshMaterial mat = new();
            
            mat.Name = "objMat";
            NbUniform uf = new()
            {
                Name = "mpCustomPerMaterial.gMaterialColourVec4",
                State = new()
                {
                    Type = NbUniformType.Vector4,
                    ShaderBinding = "mpCustomPerMaterial.uniforms[0]",
                },
                Values = new(0.0f, 1.0f, 1.0f, 1.0f)
            };
            mat.Uniforms.Add(uf);

            GLSLShaderConfig conf = EngineRef.GetShaderConfigByName("UberShader_Deferred_Lit");
            ulong shader_hash = EngineRef.CalculateShaderHash(conf, EngineRef.GetMaterialShaderDirectives(mat));

            NbShader shader = EngineRef.GetShaderByHash(shader_hash);
            if (shader == null)
            {
                shader = new()
                {
                    directives = EngineRef.GetMaterialShaderDirectives(mat)
                };

                shader.SetShaderConfig(conf);
                EngineRef.CompileShader(shader);
            }

            mat.AttachShader(shader);


            SceneGraphNode root = EngineRef.CreateLocatorNode("obj_root");

            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();

                if (line.StartsWith("#"))
                    continue;
                else if (line.StartsWith("o"))
                {
                    if (!mesh_submitted)
                        root.AddChild(SubmitMesh(node_name, mat, Vertices, Tris));
                    
                    string[] split = line.Split(' ');
                    
                    //Start new mesh
                    node_name = split[1];
                    
                    //Final Data
                    Vertices = new();
                    Tris = new();
                    indexCount = 0;
                    mesh_submitted = false;
                }
                else if (line.StartsWith("vt"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    VertexUVs.Add(new NbVector2(x, y));
                } else if (line.StartsWith("vn"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    float.TryParse(split[3], out var z);
                    VertexNormals.Add(new NbVector3(x, y, z));
                }
                else if (line.StartsWith("v"))
                {
                    //Parse Vertex
                    string[] split = line.Split(' ');
                    float.TryParse(split[1], out var x);
                    float.TryParse(split[2], out var y);
                    float.TryParse(split[3], out var z);
                    VertexPositions.Add(new NbVector3(x, y, z));
                }
                else if (line.StartsWith("f"))
                {
                    //Parse Face Data
                    string[] split = line.Split(' ');
                    
                    if (split.Length == 5) //Quad
                    {
                        int v1_pos_id = int.Parse(split[1].Split('/')[0]) - 1;
                        int v1_norm_id = int.Parse(split[1].Split('/')[2]) - 1;
                        int v1_uv_id = int.Parse(split[1].Split('/')[1]) - 1;

                        int v2_pos_id = int.Parse(split[2].Split('/')[0]) - 1;
                        int v2_norm_id = int.Parse(split[2].Split('/')[2]) - 1;
                        int v2_uv_id = int.Parse(split[2].Split('/')[1]) - 1;

                        int v3_pos_id = int.Parse(split[3].Split('/')[0]) - 1;
                        int v3_norm_id = int.Parse(split[3].Split('/')[2]) - 1;
                        int v3_uv_id = int.Parse(split[3].Split('/')[1]) - 1;

                        int v4_pos_id = int.Parse(split[4].Split('/')[0]) - 1;
                        int v4_norm_id = int.Parse(split[4].Split('/')[2]) - 1;
                        int v4_uv_id = int.Parse(split[4].Split('/')[1]) - 1;

                        Vertices.Add(VertexPositions[v1_pos_id]);
                        Vertices.Add(VertexPositions[v2_pos_id]);
                        Vertices.Add(VertexPositions[v3_pos_id]);

                        Normals.Add(VertexNormals[v1_norm_id]);
                        Normals.Add(VertexNormals[v2_norm_id]);
                        Normals.Add(VertexNormals[v3_norm_id]);

                        Vertices.Add(VertexPositions[v1_pos_id]);
                        Vertices.Add(VertexPositions[v3_pos_id]);
                        Vertices.Add(VertexPositions[v4_pos_id]);

                        Normals.Add(VertexNormals[v1_norm_id]);
                        Normals.Add(VertexNormals[v3_norm_id]);
                        Normals.Add(VertexNormals[v4_norm_id]);

                        //Save 2 triangles
                        Tris.Add(new NbVector3i(indexCount, indexCount + 1, indexCount + 2));
                        Tris.Add(new NbVector3i(indexCount + 3, indexCount + 4, indexCount + 5));
                        indexCount += 6;
                    } else if (split.Length == 4) //Triangle
                    {
                        int v1_pos_id = int.Parse(split[1].Split('/')[0]) - 1;
                        int v1_norm_id = int.Parse(split[1].Split('/')[2]) - 1;
                        int v1_uv_id = int.Parse(split[1].Split('/')[1]) - 1;

                        int v2_pos_id = int.Parse(split[2].Split('/')[0]) - 1;
                        int v2_norm_id = int.Parse(split[2].Split('/')[2]) - 1;
                        int v2_uv_id = int.Parse(split[2].Split('/')[1]) - 1;

                        int v3_pos_id = int.Parse(split[3].Split('/')[0]) - 1;
                        int v3_norm_id = int.Parse(split[3].Split('/')[2]) - 1;
                        int v3_uv_id = int.Parse(split[3].Split('/')[1]) - 1;

                        Vertices.Add(VertexPositions[v1_pos_id]);
                        Vertices.Add(VertexPositions[v2_pos_id]);
                        Vertices.Add(VertexPositions[v3_pos_id]);

                        Normals.Add(VertexNormals[v1_norm_id]);
                        Normals.Add(VertexNormals[v2_norm_id]);
                        Normals.Add(VertexNormals[v3_norm_id]);
                        
                        //Save triangle
                        Tris.Add(new NbVector3i(indexCount, indexCount + 1, indexCount + 2));
                        indexCount += 3;
                    } else
                    {
                        //I Have no idea what else...
                    }
                }
                else
                {
                    Log($"Unknown obj directive {line}. Skipping...", LogVerbosityLevel.WARNING);
                }
            
            }

            //Submit last mesh
            if (!mesh_submitted)
                root.AddChild(SubmitMesh(node_name, mat, Vertices, Tris));

            sr.Close();
            
            
            return root;
        }

        public override void Export(string filepath)
        {
            Log("Not supported yet", LogVerbosityLevel.INFO);
        }

        public override void OnUnload()
        {
            throw new NotImplementedException();
        }

        public override void DrawImporters()
        {
            if (ImGuiCore.MenuItem("Import from obj", "", false, true))
            {
                openFileDialog.Open();
            }

        }

        public override void DrawExporters(SceneGraph scn)
        {
            return;
        }

        public override void Draw()
        {
            if (openFileDialog.Draw(new System.Numerics.Vector2(600, 400)))
            {
                Import(openFileDialog.GetSelectedFile());
            }
        }
    }
}