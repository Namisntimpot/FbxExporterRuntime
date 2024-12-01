using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FbxExporterTest : MonoBehaviour
{
    public GameObject[] gameObjectsToExport;
    public string fbxFileName = "test.fbx";
    public bool exportMaterial = true;
    public bool exportEmbedded = true;

    // Start is called before the first frame update
    void Start()
    {
        FbxExporterRuntime.EXPORT_EMBEDDED = exportEmbedded;
        FbxExporterRuntime.EXPORT_MATERIAL = exportMaterial;
        FbxExporterRuntime.ExportFbx(gameObjectsToExport, fbxFileName);
        Debug.Log("Export Finished.");
    }
}
