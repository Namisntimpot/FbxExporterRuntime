using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System.Linq;
using System;
using System.IO;

using Autodesk.Fbx;

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

    private static string GetTemporaryDir()
    {
        string tmpdir = Application.dataPath;
        tmpdir = Path.Combine(tmpdir, "temp_fbx_exporter");
        return tmpdir;
    }

    /// <summary>
    /// Export unity objects into a single fbx file.
    /// </summary>
    /// <param name="unityObjects"></param>
    /// <param name="fileName"></param>
    public static void ExportFbx(GameObject[] unityObjects, string fileName)
    {
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
                ExportFBXMeshes(scene, unityObjects.Where((x)=>x.GetComponent<MeshFilter>() != null).ToArray());

                // Export all Lights
                ExportFBXLights(scene, unityObjects.Where((x) => x.GetComponent<Light>() != null).ToArray());

                // Export the scene to the file.
                exporter.Export(scene);
            }
        }
    }

    private static void ExportFBXMeshes(FbxScene scene, GameObject[] meshGameObjects)
    {
        MeshFilter[] meshlist = meshGameObjects.Select((x) => x.GetComponent<MeshFilter>()).ToArray();
        foreach (MeshFilter meshFilter in meshlist)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null) { continue; }
            FbxNode fbxNode = FbxNode.Create(scene, meshFilter.name);

            // Set transformations
            Vector3 pos = meshFilter.transform.position * DISTANCE_SCALER;
            fbxNode.LclTranslation.Set(new FbxDouble3(pos.x, pos.y, pos.z));
            fbxNode.LclRotation.Set(new FbxDouble3(meshFilter.transform.eulerAngles.x,
                                                   meshFilter.transform.eulerAngles.y,
                                                   meshFilter.transform.eulerAngles.z));
            fbxNode.LclScaling.Set(new FbxDouble3(meshFilter.transform.localScale.x,
                                                  meshFilter.transform.localScale.y,
                                                  meshFilter.transform.localScale.z));

            // 导入几何数据
            FbxMesh fbxMesh = ExportFBXMeshGeometryFromGameObject(scene, meshFilter);

            fbxNode.SetNodeAttribute(fbxMesh);
            fbxNode.SetShadingMode(FbxNode.EShadingMode.eTextureShading);

            //FbxNode meshNode = FbxNode.Create(scene, fbxMesh.GetName());
            //meshNode.SetNodeAttribute(fbxMesh);
            //meshNode.SetShadingMode(FbxNode.EShadingMode.eTextureShading);

            // 导入材质和纹理数据
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
        Debug.Log($"Mesh: {unityMesh.name}, Vertices: {vertices.Length}, Triangles: {unityMesh.triangles.Length}");

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
        if (mesh.subMeshCount > 1 && mesh.subMeshCount == meshRenderer.sharedMaterials.Length)
        {
            // 处理subMesh
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
                    fbxLayerElementMaterial.GetIndexArray().Add(subMeshId);   // 设置哪个多边形用哪个材质
                }
            }
            fbxMesh.GetLayer(0).SetMaterials(fbxLayerElementMaterial);

            // 处理所有子材质
            foreach (Material mat in meshRenderer.sharedMaterials)
            {
                FbxSurfacePhong fbxMaterial = ExportFBXParseMaterial(fbxMesh, mat);
                fbxMesh.GetNode().AddMaterial(fbxMaterial);
            }
        }
        else
        {
            // 只有单个材质
            Material mat = meshRenderer.sharedMaterials[0];
            // 在layer中存下material的定义
            FbxLayerElementMaterial fbxLayerElementMaterial = FbxLayerElementMaterial.Create(fbxMesh, mat.name);
            fbxLayerElementMaterial.SetMappingMode(FbxLayerElement.EMappingMode.eAllSame);
            fbxLayerElementMaterial.SetReferenceMode(FbxLayerElement.EReferenceMode.eIndexToDirect);
            fbxLayerElementMaterial.GetIndexArray().Add(0);
            fbxMesh.GetLayer(0).SetMaterials(fbxLayerElementMaterial);

            // 将材质转换为fbx的材质.
            FbxSurfacePhong fbxMaterial = ExportFBXParseMaterial(fbxMesh, mat);
            fbxMesh.GetNode().AddMaterial(fbxMaterial);
        }
    }

    private static FbxSurfacePhong ExportFBXParseMaterial(FbxMesh fbxMesh, Material material)
    {
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


        // 绑定以下材质：_MainTex -> Diffuse; _BumpMap -> NormalMap;
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
                // 先保存为图片，再设置FbxFileTexture
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
            Debug.Log(bumpMap.GetPixel(0, 0));
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
        Dictionary<LightType, FbxLight.EType> lightTypeMap = new Dictionary<LightType, FbxLight.EType>();
        lightTypeMap[LightType.Directional] = FbxLight.EType.eDirectional;
        lightTypeMap[LightType.Point] = FbxLight.EType.ePoint;
        lightTypeMap[LightType.Spot] = FbxLight.EType.eSpot;
        lightTypeMap[LightType.Area] = FbxLight.EType.eArea;
        foreach (Light lightObj in lightObjs)
        {
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

            FbxNode lightNode = FbxNode.Create(scene, lightObj.name + "_node");
            lightNode.SetNodeAttribute(fbxLight);

            // 设置光源位置！
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
