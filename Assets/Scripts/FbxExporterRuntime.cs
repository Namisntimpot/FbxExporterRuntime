using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;
using System;
using System.IO;

using Autodesk.Fbx;

public class FbxExporterGOTree
{
    public GameObject gameObject;
    public FbxExporterGOTree parent = null;
    public List<FbxExporterGOTree> childs = new List<FbxExporterGOTree>();
    public FbxNode node = null;
    public FbxExporterGOTree(GameObject go)
    {
        gameObject = go;
    }
    public void AddChild(FbxExporterGOTree node)
    {
        childs.Add(node);
        node.parent = this;
    }
    public int ChildCount()
    {
        return childs == null ? 0 : childs.Count;
    }
    public FbxExporterGOTree GetParent()
    {
        return parent;
    }
    public GameObject GetGameObject()
    {
        return gameObject;
    }

    public static FbxExporterGOTree FromRootGameObject(GameObject root)
    {
        FbxExporterGOTree rootNode = new FbxExporterGOTree(root);
        int childCount = root.transform.childCount;
        for(int i=0; i<childCount; ++i)
        {
            Transform child = root.transform.GetChild(i);
            rootNode.AddChild(FromRootGameObject(child.gameObject));
        }
        return rootNode;
    }
}

public class FbxExporterRuntime
{
    // including materials and textures.
    public static bool EXPORT_MATERIAL = true;
    // if false, make sure that different textures have different names to avoid unexpected overwrite.
    public static bool EXPORT_EMBEDDED = true;

    public static string TEMP_DIR = GetTemporaryDir();

    public static int DISTANCE_SCALER = 100;
    public static int INTENSITY_SCALER = 10000;
    public static bool NO_ALPHA = true;    // ignore all alpha channels in all textures. 

    public static StreamWriter logger = null;

    private static void Log(string content, int space_level = 0){
        if(logger != null){
            string prefix = "";
            for(int i=0; i<space_level * 4; ++i){
                prefix += " ";
            }
            logger.WriteLine(prefix + content);
            logger.Flush();
        }
    }

    private static string GetTemporaryDir()
    {
        string tmpdir = Application.dataPath;
        tmpdir = Path.Combine(tmpdir, "temp_fbx_exporter");
        return tmpdir;
    }

    public static Vector3 GetLocalTranslation(GameObject par, GameObject chi)
    {
        return par != null ? chi.transform.position - par.transform.position : chi.transform.position;
    }

    public static Vector3 GetLocalRotation(GameObject par, GameObject chi)
    {
        if (par == null)
            return chi.transform.rotation.eulerAngles;
        Transform t_par = par.transform, t_chi = chi.transform;
        Quaternion rel = Quaternion.Inverse(t_par.rotation) * t_chi.rotation;
        return rel.eulerAngles;
    }

    public static Vector3 GetLocalScale(GameObject par, GameObject chi)
    {
        if (par == null)
            return chi.transform.lossyScale;
        Vector3 lossyScalePar = par.transform.lossyScale, lossyScaleChi = chi.transform.lossyScale;
        return new Vector3(lossyScaleChi.x / lossyScalePar.x, lossyScaleChi.y / lossyScalePar.y, lossyScaleChi.z / lossyScalePar.z);
    }

    /// <summary>
    /// Export unity objects into a single fbx file.
    /// </summary>
    /// <param name="unityObjects"></param>
    /// <param name="fileName"></param>
    public static void ExportFbx(GameObject[] unityObjects, string fileName)
    {
        Log("Export Fbx Path: " + Path.GetFullPath(fileName) + ". No hierarchy.", 0);
        Log("Temporary texture dir path: " + TEMP_DIR, 0);

        if (!Directory.Exists(TEMP_DIR))
        {
            Directory.CreateDirectory(TEMP_DIR);
        }
        using (FbxManager fbxManager = FbxManager.Create())
        {
            // configure IO settings.
            FbxIOSettings fbxIOSettings = FbxIOSettings.Create(fbxManager, Globals.IOSROOT);
            fbxIOSettings.SetBoolProp(Globals.EXP_FBX_MATERIAL, EXPORT_MATERIAL);
            fbxIOSettings.SetBoolProp(Globals.EXP_FBX_TEXTURE, EXPORT_MATERIAL);
            fbxIOSettings.SetBoolProp(Globals.EXP_FBX_EMBEDDED, EXPORT_EMBEDDED);
            fbxManager.SetIOSettings(fbxIOSettings);

            // Export the scene
            using (FbxExporter exporter = FbxExporter.Create(fbxManager, "myExporter"))
            {

                // Initialize the exporter.
                bool status = exporter.Initialize(fileName, -1, fbxManager.GetIOSettings());

                // Create a new scene to export
                FbxScene scene = FbxScene.Create(fbxManager, "myScene");

                // Export all meshes.
                Log("Export Meshes...", 0);
                ExportFBXMeshes(scene, unityObjects.Where((x)=>x.GetComponent<MeshFilter>() != null).ToArray());

                // Export all Lights
                Log("Export Lights...", 0);
                ExportFBXLights(scene, unityObjects.Where((x) => x.GetComponent<Light>() != null).ToArray());

                // Export the scene to the file.
                exporter.Export(scene);
            }
        }
    }

    public static void ExportFbx(FbxExporterGOTree[] trees, string fileName)
    {
        Log("Export Fbx Path: " + Path.GetFullPath(fileName) + ". With hierarchy.", 0);
        Log("Temporary texture dir path: " + TEMP_DIR, 0);

        if (!Directory.Exists(TEMP_DIR))
        {
            Directory.CreateDirectory(TEMP_DIR);
        }
        using (FbxManager fbxManager = FbxManager.Create())
        {
            // configure IO settings.
            FbxIOSettings fbxIOSettings = FbxIOSettings.Create(fbxManager, Globals.IOSROOT);
            fbxIOSettings.SetBoolProp(Globals.EXP_FBX_MATERIAL, EXPORT_MATERIAL);
            fbxIOSettings.SetBoolProp(Globals.EXP_FBX_TEXTURE, EXPORT_MATERIAL);
            fbxIOSettings.SetBoolProp(Globals.EXP_FBX_EMBEDDED, EXPORT_EMBEDDED);
            fbxManager.SetIOSettings(fbxIOSettings);

            // Export the scene
            using (FbxExporter exporter = FbxExporter.Create(fbxManager, "myExporter"))
            {

                // Initialize the exporter.
                bool status = exporter.Initialize(fileName, -1, fbxManager.GetIOSettings());

                // Create a new scene to export
                FbxScene scene = FbxScene.Create(fbxManager, "myScene");

                // traverse all trees.
                foreach(var tree in trees)
                {
                    Log("Traverse tree " + tree.gameObject.name, 0);
                    ExportFBXTraverseTree(scene, tree);
                }
                // Export the scene to the file.
                exporter.Export(scene);
            }
        }
    }

    private static void ExportFBXTraverseTree(FbxScene scene, FbxExporterGOTree tree)
    {
        GameObject thisObj = tree.gameObject, par = tree.parent != null ? tree.parent.gameObject : null;
        FbxNode thisNode = FbxNode.Create(scene, thisObj.name);
        Vector3 lcltrans = GetLocalTranslation(par, thisObj) * DISTANCE_SCALER;
        Vector3 lclrot = GetLocalRotation(par, thisObj);
        Vector3 lclscale = GetLocalScale(par, thisObj);
        thisNode.LclTranslation.Set(new FbxDouble3(lcltrans.x, lcltrans.y, lcltrans.z));
        thisNode.LclRotation.Set(new FbxDouble3(lclrot.x, lclrot.y, lclrot.z));
        thisNode.LclScaling.Set(new FbxDouble3(lclscale.x, lclscale.y, lclscale.z));
        MeshFilter thisMeshFilter = thisObj.GetComponent<MeshFilter>();
        Light thisLight = thisObj.GetComponent<Light>();
        if(thisMeshFilter != null)
        {
            FbxMesh fbxMesh = ExportFBXMeshGeometryFromGameObject(scene, thisMeshFilter);
            thisNode.SetNodeAttribute(fbxMesh);
            thisNode.SetShadingMode(FbxNode.EShadingMode.eTextureShading);
            if (EXPORT_MATERIAL)
            {
                ExportFBXBindMaterials(fbxMesh, thisObj);
            }
        }
        if(thisLight != null)
        {
            FbxLight fbxLight = ExportFBXLight(scene, thisLight);
            if(thisMeshFilter  != null)   // This gameobject has both mesh and light.
            {
                FbxNode tmpLightNode = FbxNode.Create(scene, thisObj.name + "_light"); // translation, rotation, scale are all default.
                tmpLightNode.SetNodeAttribute(fbxLight);
                thisNode.AddChild(tmpLightNode);
            }
            else     // This gameobject has only light component.
            {
                thisNode.SetNodeAttribute(fbxLight);
            }
        }

        tree.node = thisNode;
        if (tree.parent == null)
            scene.GetRootNode().AddChild(thisNode);
        else
            tree.parent.node.AddChild(thisNode);

        foreach(FbxExporterGOTree child in tree.childs)
        {
            ExportFBXTraverseTree(scene, child);
        }
    }

    private static void ExportFBXMeshes(FbxScene scene, GameObject[] meshGameObjects)
    {
        MeshFilter[] meshlist = meshGameObjects.Select((x) => x.GetComponent<MeshFilter>()).ToArray();
        foreach (MeshFilter meshFilter in meshlist)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) { continue; }
            
            Log("Mesh: " + meshFilter.gameObject.name, 1);

            FbxNode fbxNode = FbxNode.Create(scene, meshFilter.name);

            // Set transformations
            Vector3 pos = meshFilter.transform.position * DISTANCE_SCALER;
            fbxNode.LclTranslation.Set(new FbxDouble3(pos.x, pos.y, pos.z));
            fbxNode.LclRotation.Set(new FbxDouble3(meshFilter.transform.eulerAngles.x,
                                                   meshFilter.transform.eulerAngles.y,
                                                   meshFilter.transform.eulerAngles.z));
            fbxNode.LclScaling.Set(new FbxDouble3(meshFilter.transform.lossyScale.x,
                                                  meshFilter.transform.lossyScale.y,
                                                  meshFilter.transform.lossyScale.z));

            // ���뼸������
            FbxMesh fbxMesh = ExportFBXMeshGeometryFromGameObject(scene, meshFilter);

            fbxNode.SetNodeAttribute(fbxMesh);
            fbxNode.SetShadingMode(FbxNode.EShadingMode.eTextureShading);

            //FbxNode meshNode = FbxNode.Create(scene, fbxMesh.GetName());
            //meshNode.SetNodeAttribute(fbxMesh);
            //meshNode.SetShadingMode(FbxNode.EShadingMode.eTextureShading);

            // ������ʺ���������
            if (EXPORT_MATERIAL)
            {
                ExportFBXBindMaterials(fbxMesh, meshFilter.gameObject);
            }

            //fbxNode.AddChild(meshNode);
            scene.GetRootNode().AddChild(fbxNode);
        }
    }

    private static FbxMesh ExportFBXMeshGeometryFromGameObject(FbxScene scene, MeshFilter unityMeshFilter)
    {
        Mesh unityMesh = unityMeshFilter.sharedMesh;
        FbxMesh fbxMesh = FbxMesh.Create(scene, unityMesh.name);
        float scaleFactor = 100.0f; // Adjust this value as needed

        // Set vertex positions
        Vector3[] vertices = unityMesh.vertices;
        // Debug.Log($"Mesh: {unityMesh.name}, Vertices: {vertices.Length}, Triangles: {unityMesh.triangles.Length}");

        fbxMesh.InitControlPoints(vertices.Length);
        for (int i = 0; i < vertices.Length; i++)
        {
            fbxMesh.SetControlPointAt(new FbxVector4(vertices[i].x * scaleFactor,
                                                      vertices[i].y * scaleFactor,
                                                      vertices[i].z * scaleFactor,
                                                      1), i);
        }

        // Set triangles
        int[] triangles = unityMesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            fbxMesh.BeginPolygon();
            fbxMesh.AddPolygon(triangles[i]);
            fbxMesh.AddPolygon(triangles[i + 1]);
            fbxMesh.AddPolygon(triangles[i + 2]);
            fbxMesh.EndPolygon();
        }

        // Set normals
        Vector3[] normals = unityMesh.normals;
        if (normals.Length > 0)
        {
            var normalElement = FbxLayerElementNormal.Create(fbxMesh, "Normals");
            normalElement.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
            normalElement.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);

            var normalArray = normalElement.GetDirectArray();
            for (int i = 0; i < normals.Length; i++)
            {
                normalArray.Add(new FbxVector4(normals[i].x, normals[i].y, normals[i].z, 0));
            }

            fbxMesh.GetLayer(0).SetNormals(normalElement);
        }

        // Set vertex colors
        Color[] colors = unityMesh.colors;
        if (colors.Length > 0)
        {
            var colorElement = FbxLayerElementVertexColor.Create(fbxMesh, "Colors");
            colorElement.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
            colorElement.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);

            var colorArray = colorElement.GetDirectArray();
            for (int i = 0; i < colors.Length; i++)
            {
                colorArray.Add(new FbxVector4(colors[i].r, colors[i].g, colors[i].b, colors[i].a));
            }

            fbxMesh.GetLayer(0).SetVertexColors(colorElement);
        }

        // Set UV maps
        if (unityMesh.uv.Length > 0)
        {
            var uvElement = FbxLayerElementUV.Create(fbxMesh, "UVs");
            uvElement.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
            uvElement.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);

            var uvArray = uvElement.GetDirectArray();
            for (int i = 0; i < unityMesh.uv.Length; i++)
            {
                uvArray.Add(new FbxVector2(unityMesh.uv[i].x, unityMesh.uv[i].y));
            }

            fbxMesh.GetLayer(0).SetUVs(uvElement);
        }

        return fbxMesh;
    }

    private static void ExportFBXBindMaterials(FbxMesh fbxMesh, GameObject unityObject)
    {
        MeshRenderer meshRenderer = unityObject.GetComponent<MeshRenderer>();
        Mesh mesh = unityObject.GetComponent<MeshFilter>().sharedMesh;
        Log("Handling materials. NumSubMeshes: " + mesh.subMeshCount.ToString() + ", NumMaterials: " + meshRenderer.sharedMaterials.Length, 2);
        if (mesh.subMeshCount > 1 && mesh.subMeshCount == meshRenderer.sharedMaterials.Length)
        {
            // ����subMesh
            FbxLayerElementMaterial fbxLayerElementMaterial = FbxLayerElementMaterial.Create(fbxMesh, "Materials");
            fbxLayerElementMaterial.SetMappingMode(FbxLayerElement.EMappingMode.eByPolygon);
            fbxLayerElementMaterial.SetReferenceMode(FbxLayerElement.EReferenceMode.eIndexToDirect);
            for (int subMeshId = 0; subMeshId < mesh.subMeshCount; ++subMeshId)
            {
                SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(subMeshId);
                var topo = subMeshDescriptor.topology;
                int indices_per_primitive = topo == MeshTopology.Triangles ? 3 :
                                          topo == MeshTopology.Quads || topo == MeshTopology.LineStrip ? 4 :
                                          topo == MeshTopology.Lines ? 2 : 1;
                for (int i = 0; i < subMeshDescriptor.indexCount / indices_per_primitive; ++i)
                {
                    fbxLayerElementMaterial.GetIndexArray().Add(subMeshId);   // �����ĸ���������ĸ�����
                }
            }
            fbxMesh.GetLayer(0).SetMaterials(fbxLayerElementMaterial);

            // ���������Ӳ���
            foreach (Material mat in meshRenderer.sharedMaterials)
            {
                FbxSurfacePhong fbxMaterial = ExportFBXParseMaterial(fbxMesh, mat);
                fbxMesh.GetNode().AddMaterial(fbxMaterial);
            }
        }
        else
        {
            // ֻ�е�������
            Material mat = meshRenderer.sharedMaterials[0];
            // ��layer�д���material�Ķ���
            FbxLayerElementMaterial fbxLayerElementMaterial = FbxLayerElementMaterial.Create(fbxMesh, mat.name);
            fbxLayerElementMaterial.SetMappingMode(FbxLayerElement.EMappingMode.eAllSame);
            fbxLayerElementMaterial.SetReferenceMode(FbxLayerElement.EReferenceMode.eIndexToDirect);
            fbxLayerElementMaterial.GetIndexArray().Add(0);
            fbxMesh.GetLayer(0).SetMaterials(fbxLayerElementMaterial);

            // ������ת��Ϊfbx�Ĳ���.
            FbxSurfacePhong fbxMaterial = ExportFBXParseMaterial(fbxMesh, mat);
            fbxMesh.GetNode().AddMaterial(fbxMaterial);
        }
    }

    private static FbxSurfacePhong ExportFBXParseMaterial(FbxMesh fbxMesh, Material material)
    {
        Log("Handling Material: " + material.name, 3);
        FbxSurfacePhong fbxMaterial = FbxSurfacePhong.Create(fbxMesh, material.name);
        FbxDouble3 defaultColor = new FbxDouble3(0, 0, 0);
        fbxMaterial.Emissive.Set(ExportFBXColorConversion(material.GetColor("_EmissionColor")));
        fbxMaterial.Ambient.Set(defaultColor);
        fbxMaterial.AmbientFactor.Set(1.0);
        fbxMaterial.Diffuse.Set(ExportFBXColorConversion(material.GetColor("_Color")));
        fbxMaterial.DiffuseFactor.Set(1.0);
        fbxMaterial.SpecularFactor.Set(0.5);
        fbxMaterial.TransparencyFactor.Set(0);
        fbxMaterial.TransparentColor.Set(new FbxColor(0, 0, 0));

        // �����²��ʣ�_MainTex -> Diffuse; _BumpMap -> NormalMap;
        string tmpTextureDir = TEMP_DIR;
        // _MainTex
        string mainTexPath = null;
        Texture2D mainTex = material.GetTexture("_MainTex") as Texture2D;
        if (mainTex != null)
        {
            mainTex = ExportFBXGetUncompressedTexture(mainTex, noAlpha: NO_ALPHA);
            byte[] mainTexPng = mainTex.EncodeToPNG();
            if (mainTexPng != null)
            {
                // �ȱ���ΪͼƬ��������FbxFileTexture
                mainTexPath = Path.Combine(tmpTextureDir, mainTex.name + ".png");
                File.WriteAllBytes(mainTexPath, mainTexPng);
            }
        }
        if (mainTexPath != null)
        {
            FbxFileTexture mainTexFbxTexture = FbxFileTexture.Create(fbxMesh, "MainTex");
            mainTexFbxTexture.SetFileName(mainTexPath);
            mainTexFbxTexture.SetTextureUse(FbxTexture.ETextureUse.eStandard);
            mainTexFbxTexture.SetMappingType(FbxTexture.EMappingType.eUV);
            mainTexFbxTexture.SetMaterialUse(FbxFileTexture.EMaterialUse.eModelMaterial);
            mainTexFbxTexture.SetSwapUV(false);
            mainTexFbxTexture.SetTranslation(0, 0);
            mainTexFbxTexture.SetScale(1, 1);
            mainTexFbxTexture.SetRotation(0, 0);
            mainTexFbxTexture.UVSet.Set("UVs");
            fbxMaterial.Diffuse.ConnectSrcObject(mainTexFbxTexture);
        }
        // _BumpMap
        string bumpMapPath = null;
        Texture2D bumpMap = material.GetTexture("_BumpMap") as Texture2D;
        if (bumpMap != null)
        {
            bumpMap = ExportFBXGetUncompressedTexture(bumpMap, true, noAlpha: NO_ALPHA);
            byte[] bumpMapPng = bumpMap.EncodeToPNG();
            if (bumpMapPng != null)
            {
                bumpMapPath = Path.Combine(tmpTextureDir, bumpMap.name + ".png");
                File.WriteAllBytes(bumpMapPath, bumpMapPng);
            }
        }
        if (bumpMapPath != null)
        {
            FbxFileTexture bumpMapFbxTexture = FbxFileTexture.Create(fbxMesh, "BumpMap");
            bumpMapFbxTexture.SetFileName(bumpMapPath);
            bumpMapFbxTexture.SetTextureUse(FbxTexture.ETextureUse.eBumpNormalMap);
            bumpMapFbxTexture.SetMappingType(FbxTexture.EMappingType.eUV);
            bumpMapFbxTexture.SetMaterialUse(FbxFileTexture.EMaterialUse.eModelMaterial);
            bumpMapFbxTexture.SetSwapUV(false);
            bumpMapFbxTexture.SetTranslation(0, 0);
            bumpMapFbxTexture.SetScale(1, 1);
            bumpMapFbxTexture.SetRotation(0, 0);
            bumpMapFbxTexture.UVSet.Set("UVs");
            fbxMaterial.NormalMap.ConnectSrcObject(bumpMapFbxTexture);
        }

        return fbxMaterial;
    }

    private static void ExportFBXLights(FbxScene scene, GameObject[] lightGameObjects)
    {
        Light[] lightObjs = lightGameObjects.Select((x) => x.GetComponent<Light>()).ToArray();

        FbxNode lightGroupNode = FbxNode.Create(scene, "Lights");
        foreach (Light lightObj in lightObjs)
        {
            FbxLight fbxLight = ExportFBXLight(scene, lightObj);
            FbxNode lightNode = FbxNode.Create(scene, lightObj.name + "_node");
            lightNode.SetNodeAttribute(fbxLight);

            // ���ù�Դλ�ã�
            Vector3 pos = lightObj.transform.position * DISTANCE_SCALER;
            lightNode.LclTranslation.Set(new FbxDouble3(pos.x,pos.y,pos.z));
            lightNode.LclRotation.Set(new FbxDouble3(lightObj.transform.eulerAngles.x,
                                                     lightObj.transform.eulerAngles.y,
                                                     lightObj.transform.eulerAngles.z));
            lightNode.LclScaling.Set(new FbxDouble3(lightObj.transform.localScale.x,
                                                    lightObj.transform.localScale.y,
                                                    lightObj.transform.localScale.z));

            lightGroupNode.AddChild(lightNode);
        }

        scene.GetRootNode().AddChild(lightGroupNode);
    }

    private static FbxLight ExportFBXLight(FbxScene scene, Light lightObj)
    {
        Log("Export Light: " + lightObj.gameObject.name + ", type: " + lightObj.type.ToString(), 1);
        Dictionary<LightType, FbxLight.EType> lightTypeMap = new Dictionary<LightType, FbxLight.EType>() {
            {LightType.Directional,  FbxLight.EType.eDirectional}, {LightType.Point, FbxLight.EType.ePoint},
            {LightType.Spot, FbxLight.EType.eSpot }, {LightType.Area, FbxLight.EType.eArea}
        };
        FbxLight fbxLight = FbxLight.Create(scene, lightObj.name);
        if (lightTypeMap.TryGetValue(lightObj.type, out FbxLight.EType fbxLightType))
        {
            fbxLight.LightType.Set(fbxLightType);
        }
        else
        {
            fbxLight.LightType.Set(FbxLight.EType.ePoint);
        }
        fbxLight.Intensity.Set(lightObj.intensity * (lightObj.type == LightType.Directional ? 1 : INTENSITY_SCALER));
        fbxLight.Color.Set(ExportFBXColorConversion(lightObj.color));
        fbxLight.CastShadows.Set(true);
        fbxLight.ShadowColor.Set(new FbxColor(0, 0, 0));
        return fbxLight;
    }

    private static FbxColor ExportFBXColorConversion(Color color)
    {
        if (color == null)
        {
            return new FbxColor(0, 0, 0);
        }
        return new FbxColor(color.r, color.g, color.b, color.a);
    }

    private static Texture2D ExportFBXGetUncompressedTexture(Texture2D tex, bool isNormalMap = false, bool noAlpha = true)
    {
        Log("Handling Texture: " +  tex.name, 4);
        var format = tex.format;
        bool isCompressed = ExportFBXTextureFormatIsCompressed(format);
        if (!isCompressed)
        {
            return tex;
        }
        Texture2D uncompressed = new Texture2D(tex.width, tex.height, noAlpha ? TextureFormat.RGB24 : TextureFormat.RGBA32, false);
        uncompressed.name = tex.name;
        Color[] pixels = tex.GetPixels();
        if (isNormalMap)
        {
            for(int i = 0; i<pixels.Length; ++i)
            {
                Color c = pixels[i];
                float x = c.a, y = c.g;
                x = x * 2 - 1;
                y = y * 2 - 1;
                float z = Mathf.Sqrt(1 - x * x - y * y);
                pixels[i] = new Color((x+1)/2, (y+1)/2, (z+1)/2, 1);
            }
        }
        uncompressed.SetPixels(pixels);
        uncompressed.Apply();
        return uncompressed;
    }

    private static bool ExportFBXTextureFormatIsCompressed(TextureFormat format)
    {
        TextureFormat[] uncompressed =
        {
            TextureFormat.Alpha8, TextureFormat.ARGB4444, TextureFormat.RGB24, TextureFormat.RGBA32, TextureFormat.ARGB32, TextureFormat.RGB565,
            TextureFormat.R16, TextureFormat.RGBA4444, TextureFormat.BGRA32, TextureFormat.RHalf, TextureFormat.RGHalf, TextureFormat.RGBAHalf, TextureFormat.RFloat,
            TextureFormat.RGFloat, TextureFormat.RGBAFloat, TextureFormat.YUY2, TextureFormat.RGB9e5Float, TextureFormat.RG16, TextureFormat.R8,
            TextureFormat.RG32, TextureFormat.RGB48, TextureFormat.RGBA64, 
        };
        foreach (var uc in uncompressed)
        {
            if(uc == format)
            {
                return false;
            }
        }
        return true;
    }
}
