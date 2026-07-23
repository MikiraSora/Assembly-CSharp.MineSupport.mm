using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MineSupportBundleBuilder
{
    private const string BundleName = "minevisuals";
    private const string Version = "mine-visual-v1";

    public static void Build()
    {
        const string shaderPath = "Assets/MineSpriteEffect.shader";
        const string materialPath = "Assets/MineSpriteMaterial.mat";
        const string versionPath = "Assets/MineSupportVersion.txt";

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        if (shader == null)
            throw new InvalidOperationException("MineSpriteEffect.shader could not be imported");

        var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (existingMaterial != null)
            AssetDatabase.DeleteAsset(materialPath);

        var material = new Material(shader)
        {
            name = "MineSpriteMaterial"
        };
        material.SetFloat("_GrayFloor", 0.58f);
        material.SetFloat("_GrayCeiling", 1f);
        material.SetFloat("_HatchScale", 9f);
        material.SetFloat("_HatchStrength", 0.72f);
        material.SetFloat("_HatchDark", 0.18f);
        material.SetFloat("_OutlineStrength", 0.85f);
        AssetDatabase.CreateAsset(material, materialPath);

        File.WriteAllText(versionPath, Version, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(versionPath, ImportAssetOptions.ForceSynchronousImport);

        SetBundleName(materialPath);
        SetBundleName(versionPath);
        AssetDatabase.SaveAssets();

        var outputPath = Environment.GetEnvironmentVariable("MINE_BUNDLE_OUTPUT");
        if (string.IsNullOrEmpty(outputPath))
            outputPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build");
        Directory.CreateDirectory(outputPath);

        var manifest = BuildPipeline.BuildAssetBundles(
            outputPath,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64);
        if (manifest == null)
            throw new InvalidOperationException("BuildPipeline.BuildAssetBundles returned null");

        var bundlePath = Path.Combine(outputPath, BundleName);
        var builtBundle = AssetBundle.LoadFromFile(bundlePath);
        if (builtBundle == null)
            throw new InvalidOperationException("Built MineSupport bundle could not be reopened");
        try
        {
            var builtVersion = builtBundle.LoadAsset<TextAsset>("MineSupportVersion");
            var builtMaterial = builtBundle.LoadAsset<Material>("MineSpriteMaterial");
            if (builtVersion == null || builtVersion.text != Version || builtMaterial == null || builtMaterial.shader == null)
                throw new InvalidOperationException("Built MineSupport bundle failed runtime-name validation");
        }
        finally
        {
            builtBundle.Unload(true);
        }

        Debug.Log("MineSupport bundle built and validated: " + bundlePath);
    }

    private static void SetBundleName(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
            throw new InvalidOperationException("AssetImporter unavailable: " + assetPath);

        importer.assetBundleName = BundleName;
        importer.SaveAndReimport();
    }
}
