using System;
using System.Collections.Generic;
using System.IO;
using MineSupport;
using UnityEngine;

namespace MineSupport
{
    internal static class MineVisual
    {
        private const string PatchVersion = "mine-support-v1";
        private const string BundleVersion = "mine-visual-v1";
        private const string MaterialAssetName = "MineSpriteMaterial";
        private const string VersionAssetName = "MineSupportVersion";

        private static readonly Dictionary<object, VisualState> States =
            new Dictionary<object, VisualState>(ReferenceEqualityComparer<object>.Instance);

        private static AssetBundle bundle;
        private static Material mineMaterial;
        private static int availabilityState;
        private static string availabilityError;

        internal static bool EnsureAvailable(out string error)
        {
            if (availabilityState == 1)
            {
                error = string.Empty;
                return true;
            }

            if (availabilityState == -1)
            {
                error = availabilityError;
                return false;
            }

            var paths = GetBundlePaths();
            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (!File.Exists(path))
                    continue;

                AssetBundle loadedBundle = null;
                try
                {
                    loadedBundle = AssetBundle.LoadFromFile(path);
                    if (loadedBundle == null)
                        throw new InvalidOperationException("AssetBundle.LoadFromFile returned null");

                    var versionAsset = loadedBundle.LoadAsset<TextAsset>(VersionAssetName);
                    if (versionAsset == null || !string.Equals(versionAsset.text, BundleVersion, StringComparison.Ordinal))
                        throw new InvalidDataException("MineSupportVersion is missing or incompatible");

                    var loadedMaterial = loadedBundle.LoadAsset<Material>(MaterialAssetName);
                    if (loadedMaterial == null || loadedMaterial.shader == null || !loadedMaterial.shader.isSupported)
                        throw new InvalidDataException("MineSpriteMaterial or its shader is unavailable");

                    if (!loadedMaterial.HasProperty("_GrayFloor")
                        || !loadedMaterial.HasProperty("_GrayCeiling")
                        || !loadedMaterial.HasProperty("_HatchScale"))
                    {
                        throw new InvalidDataException("MineSpriteMaterial property validation failed");
                    }

                    bundle = loadedBundle;
                    mineMaterial = loadedMaterial;
                    availabilityState = 1;
                    error = string.Empty;
                    PatchLog.WriteLine(
                        $"[Mine] visual bundle loaded: {path}, patchVersion={PatchVersion}, resourceVersion={BundleVersion}");
                    return true;
                }
                catch (Exception exception)
                {
                    availabilityError =
                        $"path={path}, patchVersion={PatchVersion}, expectedResourceVersion={BundleVersion}, stage=load/validate, "
                        + $"{exception.GetType().Name}: {exception.Message}";
                    try
                    {
                        loadedBundle?.Unload(false);
                        bundle?.Unload(false);
                    }
                    catch
                    {
                    }

                    bundle = null;
                    mineMaterial = null;
                }
            }

            availabilityState = -1;
            availabilityError = availabilityError
                ?? $"paths={string.Join(";", paths)}, patchVersion={PatchVersion}, "
                    + $"expectedResourceVersion={BundleVersion}, stage=locate, "
                    + "FileNotFoundException: no MineSupport visual AssetBundle was found";
            error = availabilityError;
            return false;
        }

        internal static void Apply(MonoBehaviour note, bool mine, GameObject suppressedOverlay = null)
        {
            if (note == null)
                return;

            if (!mine)
            {
                Clear(note);
                return;
            }

            string error;
            if (!EnsureAvailable(out error))
                return;

            VisualState state;
            if (!States.TryGetValue(note, out state))
            {
                var renderers = note.GetComponentsInChildren<SpriteRenderer>(true);
                state = new VisualState(renderers, suppressedOverlay);
                States.Add(note, state);
            }

            state.Apply(mineMaterial);
        }

        internal static void Clear(MonoBehaviour note)
        {
            if (note == null)
                return;

            VisualState state;
            if (!States.TryGetValue(note, out state))
                return;

            state.Restore();
            States.Remove(note);
        }

        internal static void ClearAll()
        {
            foreach (var state in States.Values)
                state.Restore();
            States.Clear();
        }

        private static string[] GetBundlePaths()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            return new[]
            {
                Path.Combine(root, "BepInEx", "monomod", "MineSupport", "MineVisuals"),
                Path.Combine(root, "BepInEx", "monomod", "MineSupport", "minevisuals"),
                Path.Combine(root, "BepInEx", "monomod", "MineVisuals")
            };
        }

        private sealed class VisualState
        {
            private readonly SpriteRenderer[] renderers;
            private readonly Material[] originalMaterials;
            private readonly Color[] originalColors;
            private readonly bool[] originalEnabled;
            private readonly GameObject suppressedOverlay;
            private readonly bool originalOverlayActive;

            internal VisualState(SpriteRenderer[] renderers, GameObject suppressedOverlay)
            {
                this.renderers = renderers ?? Array.Empty<SpriteRenderer>();
                this.suppressedOverlay = suppressedOverlay;
                originalOverlayActive = suppressedOverlay != null && suppressedOverlay.activeSelf;
                originalMaterials = new Material[this.renderers.Length];
                originalColors = new Color[this.renderers.Length];
                originalEnabled = new bool[this.renderers.Length];
                for (var i = 0; i < this.renderers.Length; i++)
                {
                    var renderer = this.renderers[i];
                    if (renderer == null)
                        continue;

                    originalMaterials[i] = renderer.sharedMaterial;
                    originalColors[i] = renderer.color;
                    originalEnabled[i] = renderer.enabled;
                }
            }

            internal void Apply(Material material)
            {
                if (suppressedOverlay != null && suppressedOverlay.activeSelf)
                    suppressedOverlay.SetActive(false);

                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer != null && renderer.sharedMaterial != material)
                        renderer.sharedMaterial = material;
                }
            }

            internal void Restore()
            {
                for (var i = 0; i < renderers.Length; i++)
                {
                    var renderer = renderers[i];
                    if (renderer == null)
                        continue;

                    renderer.sharedMaterial = originalMaterials[i];
                    renderer.color = originalColors[i];
                    renderer.enabled = originalEnabled[i];
                }

                if (suppressedOverlay != null && suppressedOverlay.activeSelf != originalOverlayActive)
                    suppressedOverlay.SetActive(originalOverlayActive);
            }
        }
    }
}
