using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace NERA.Editor
{
    public sealed class VolumetricNoiseGeneratorWindow : EditorWindow
    {
        private const string ComputeShaderPath =
            "Assets/_Project/NERA/Shaders/Editor/" +
            "VolumetricNoiseGenerator.compute";
        private const string DefaultMaterialPath =
            "Assets/Shaders/Custom_VolumetricFog.mat";
        private const string FogTextureProperty = "_FogNoise";
        private const int MaximumLayerCount = 16;

        [SerializeField] private TextureSize textureSize = TextureSize.Size128;
        [SerializeField] private int seed = 1337;
        [SerializeField] private bool generateMipMaps;
        [SerializeField] private float outputMinimum;
        [SerializeField] private float outputMaximum = 1f;
        [SerializeField] private bool invertOutput;
        [SerializeField] private string outputPath =
            "Assets/_Project/NERA/Art/VFX/Noise/VolumetricFogNoise.asset";
        [SerializeField] private Material targetMaterial;
        [SerializeField] private bool assignToMaterial = true;
        [SerializeField] private List<NoiseLayer> layers =
            new List<NoiseLayer>();
        [SerializeField] private PreviewAxis previewAxis = PreviewAxis.Z;
        [SerializeField] private float previewPosition = 0.5f;
        [SerializeField] private Texture3D previewTexture;
        [SerializeField] private Vector2 scrollPosition;

        private ComputeShader computeShader;
        private RenderTexture previewRenderTexture;
        private bool previewDirty = true;
        private string statusMessage =
            "Настройте слои и нажмите «Сгенерировать».";

        private enum TextureSize
        {
            Size32 = 32,
            Size64 = 64,
            Size128 = 128,
            Size256 = 256,
            Size512 = 512
        }

        private enum NoiseType
        {
            Perlin3D,
            Worley3D,
            PerlinWorley3D
        }

        private enum BlendMode
        {
            Replace,
            Add,
            Subtract,
            Multiply,
            Maximum,
            Minimum
        }

        private enum PreviewAxis
        {
            X,
            Y,
            Z
        }

        [Serializable]
        private sealed class NoiseLayer
        {
            public string displayName = "Noise layer";
            public bool enabled = true;
            public bool expanded = true;
            public NoiseType noiseType = NoiseType.Perlin3D;
            public BlendMode blendMode = BlendMode.Add;
            public Vector3 offset;
            public float frequency = 3f;
            public int octaveCount = 4;
            public float persistence = 0.5f;
            public float lacunarity = 2f;
            public float opacity = 1f;
            public float coverage = 0.48f;
            public float smoothness = 0.18f;
            public bool invert;

            public NoiseLayer Clone()
            {
                return (NoiseLayer)MemberwiseClone();
            }
        }

        private struct GpuNoiseLayer
        {
            public const int Stride = sizeof(float) * 16;

            public Vector4 OffsetFrequency;
            public Vector4 Fractal;
            public Vector4 Shaping;
            public Vector4 Flags;

            public GpuNoiseLayer(NoiseLayer layer)
            {
                OffsetFrequency = new Vector4(
                    layer.offset.x,
                    layer.offset.y,
                    layer.offset.z,
                    Mathf.Max(1f, layer.frequency));
                Fractal = new Vector4(
                    Mathf.Clamp01(layer.persistence),
                    Mathf.Max(1f, layer.lacunarity),
                    Mathf.Clamp01(layer.opacity),
                    Mathf.Clamp01(layer.coverage));
                Shaping = new Vector4(
                    Mathf.Max(0.0001f, layer.smoothness),
                    Mathf.Clamp(layer.octaveCount, 1, 8),
                    (int)layer.noiseType,
                    (int)layer.blendMode);
                Flags = new Vector4(layer.invert ? 1f : 0f, 0f, 0f, 0f);
            }
        }

        [MenuItem("NERA/Graphics/Volumetric Noise Generator")]
        public static void Open()
        {
            VolumetricNoiseGeneratorWindow window =
                GetWindow<VolumetricNoiseGeneratorWindow>(
                    "3D Noise Generator");
            window.minSize = new Vector2(430f, 620f);
        }

        private void OnEnable()
        {
            LoadComputeShader();
            if (layers == null || layers.Count == 0)
                ApplyCloudPreset(false);

            if (targetMaterial == null)
            {
                targetMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        DefaultMaterialPath);
            }

            if (previewTexture == null &&
                targetMaterial != null &&
                targetMaterial.HasProperty(FogTextureProperty))
            {
                previewTexture =
                    targetMaterial.GetTexture(FogTextureProperty) as Texture3D;
            }

            previewDirty = true;
        }

        private void OnDisable()
        {
            ReleasePreview();
        }

        private void OnGUI()
        {
            DrawHeader();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawOutputSettings();
            EditorGUILayout.Space(8f);
            DrawLayers();
            EditorGUILayout.Space(8f);
            DrawDestinationSettings();
            EditorGUILayout.Space(8f);
            DrawGenerateButton();
            EditorGUILayout.Space(8f);
            DrawPreview();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField(
                "Генератор объёмного шума",
                new GUIStyle(EditorStyles.largeLabel)
                {
                    fontStyle = FontStyle.Bold
                });
            EditorGUILayout.HelpBox(
                "Создаёт бесшовную одноканальную Texture3D (R8) для " +
                "Custom_VolumetricFog. Perlin формирует крупные массы, " +
                "Worley удобно вычитать для облачных разрывов.",
                MessageType.Info);

            if (computeShader == null)
            {
                EditorGUILayout.HelpBox(
                    $"Compute shader не найден: {ComputeShaderPath}",
                    MessageType.Error);
                if (GUILayout.Button("Повторить поиск compute shader"))
                    LoadComputeShader();
            }
        }

        private void DrawOutputSettings()
        {
            EditorGUILayout.LabelField(
                "Объём",
                EditorStyles.boldLabel);
            textureSize = (TextureSize)EditorGUILayout.EnumPopup(
                "Размер",
                textureSize);
            seed = EditorGUILayout.IntField("Seed", seed);
            generateMipMaps = EditorGUILayout.Toggle(
                new GUIContent(
                    "Mip Maps",
                    "В текущем fog shader используется LOD 0, поэтому " +
                    "обычно mip maps не нужны."),
                generateMipMaps);

            using (new EditorGUILayout.HorizontalScope())
            {
                outputMinimum = EditorGUILayout.Slider(
                    "Output Min",
                    outputMinimum,
                    0f,
                    1f);
                outputMaximum = EditorGUILayout.Slider(
                    "Max",
                    outputMaximum,
                    0f,
                    1f);
            }

            if (outputMaximum <= outputMinimum)
                outputMaximum = Mathf.Min(1f, outputMinimum + 0.001f);

            invertOutput = EditorGUILayout.Toggle(
                "Инвертировать результат",
                invertOutput);

            int size = (int)textureSize;
            double voxelMegabytes =
                (double)size * size * size / (1024d * 1024d);
            double assetMegabytes = generateMipMaps
                ? voxelMegabytes * 4d / 3d
                : voxelMegabytes;
            EditorGUILayout.LabelField(
                $"R8 asset: примерно {assetMegabytes:0.#} MB",
                EditorStyles.miniLabel);

            if (size >= 256)
            {
                EditorGUILayout.HelpBox(
                    "256³/512³ требуют заметный объём GPU и RAM. Для " +
                    "итераций начните со 128³, затем сделайте финальную " +
                    "генерацию в большем размере.",
                    MessageType.Warning);
            }
        }

        private void DrawLayers()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"Слои ({layers.Count}/{MaximumLayerCount})",
                    EditorStyles.boldLabel);
                if (GUILayout.Button("Cloud preset", GUILayout.Width(100f)))
                    ApplyCloudPreset(true);
                using (new EditorGUI.DisabledScope(
                           layers.Count >= MaximumLayerCount))
                {
                    if (GUILayout.Button("+", GUILayout.Width(28f)))
                        AddLayer();
                }
            }

            int removeIndex = -1;
            int duplicateIndex = -1;
            int moveFrom = -1;
            int moveTo = -1;

            for (int index = 0; index < layers.Count; index++)
            {
                NoiseLayer layer = layers[index];
                using (new EditorGUILayout.VerticalScope(
                           EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        layer.enabled = EditorGUILayout.Toggle(
                            layer.enabled,
                            GUILayout.Width(18f));
                        layer.expanded = EditorGUILayout.Foldout(
                            layer.expanded,
                            string.IsNullOrWhiteSpace(layer.displayName)
                                ? $"Layer {index + 1}"
                                : layer.displayName,
                            true);

                        using (new EditorGUI.DisabledScope(index == 0))
                        {
                            if (GUILayout.Button("▲", GUILayout.Width(26f)))
                            {
                                moveFrom = index;
                                moveTo = index - 1;
                            }
                        }

                        using (new EditorGUI.DisabledScope(
                                   index == layers.Count - 1))
                        {
                            if (GUILayout.Button("▼", GUILayout.Width(26f)))
                            {
                                moveFrom = index;
                                moveTo = index + 1;
                            }
                        }

                        using (new EditorGUI.DisabledScope(
                                   layers.Count >= MaximumLayerCount))
                        {
                            if (GUILayout.Button("⧉", GUILayout.Width(26f)))
                                duplicateIndex = index;
                        }

                        if (GUILayout.Button("×", GUILayout.Width(26f)))
                            removeIndex = index;
                    }

                    if (layer.expanded)
                        DrawLayerSettings(layer);
                }
            }

            if (moveFrom >= 0)
            {
                NoiseLayer moved = layers[moveFrom];
                layers.RemoveAt(moveFrom);
                layers.Insert(moveTo, moved);
            }
            else if (duplicateIndex >= 0)
            {
                NoiseLayer duplicate = layers[duplicateIndex].Clone();
                duplicate.displayName += " Copy";
                layers.Insert(duplicateIndex + 1, duplicate);
            }
            else if (removeIndex >= 0)
            {
                layers.RemoveAt(removeIndex);
            }
        }

        private static void DrawLayerSettings(NoiseLayer layer)
        {
            EditorGUI.indentLevel++;
            layer.displayName = EditorGUILayout.TextField(
                "Название",
                layer.displayName);
            layer.noiseType = (NoiseType)EditorGUILayout.EnumPopup(
                "Тип",
                layer.noiseType);
            layer.blendMode = (BlendMode)EditorGUILayout.EnumPopup(
                "Смешивание",
                layer.blendMode);
            layer.offset = EditorGUILayout.Vector3Field(
                "Offset",
                layer.offset);
            layer.frequency = EditorGUILayout.Slider(
                new GUIContent(
                    "Scale",
                    "Количество повторяющихся ячеек в объёме. Значение " +
                    "округляется до целого для бесшовности."),
                layer.frequency,
                1f,
                32f);
            layer.octaveCount = EditorGUILayout.IntSlider(
                "Octaves",
                layer.octaveCount,
                1,
                8);
            layer.persistence = EditorGUILayout.Slider(
                "Persistence",
                layer.persistence,
                0f,
                1f);
            layer.lacunarity = EditorGUILayout.Slider(
                "Lacunarity",
                layer.lacunarity,
                1f,
                4f);
            layer.opacity = EditorGUILayout.Slider(
                "Opacity",
                layer.opacity,
                0f,
                1f);
            layer.coverage = EditorGUILayout.Slider(
                "Coverage",
                layer.coverage,
                0f,
                1f);
            layer.smoothness = EditorGUILayout.Slider(
                "Smoothness",
                layer.smoothness,
                0.001f,
                0.5f);
            layer.invert = EditorGUILayout.Toggle("Invert", layer.invert);
            EditorGUI.indentLevel--;
        }

        private void DrawDestinationSettings()
        {
            EditorGUILayout.LabelField(
                "Сохранение и подключение",
                EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                outputPath = EditorGUILayout.TextField(
                    "Asset path",
                    outputPath);
                if (GUILayout.Button("…", GUILayout.Width(30f)))
                {
                    string chosenPath = EditorUtility.SaveFilePanelInProject(
                        "Сохранить 3D noise texture",
                        Path.GetFileNameWithoutExtension(outputPath),
                        "asset",
                        "Выберите путь для Texture3D asset.");
                    if (!string.IsNullOrEmpty(chosenPath))
                        outputPath = chosenPath;
                }
            }

            targetMaterial = (Material)EditorGUILayout.ObjectField(
                "Fog Material",
                targetMaterial,
                typeof(Material),
                false);
            assignToMaterial = EditorGUILayout.Toggle(
                $"Назначить в {FogTextureProperty}",
                assignToMaterial);

            if (targetMaterial != null &&
                !targetMaterial.HasProperty(FogTextureProperty))
            {
                EditorGUILayout.HelpBox(
                    $"Материал не содержит свойство {FogTextureProperty}.",
                    MessageType.Warning);
            }
        }

        private void DrawGenerateButton()
        {
            bool canGenerate =
                computeShader != null &&
                SystemInfo.supportsComputeShaders &&
                SystemInfo.supportsAsyncGPUReadback &&
                layers.Any(layer => layer.enabled);

            using (new EditorGUI.DisabledScope(!canGenerate))
            {
                if (GUILayout.Button(
                        "Сгенерировать и сохранить Texture3D",
                        GUILayout.Height(38f)))
                {
                    GenerateAndSave();
                }
            }

            if (!SystemInfo.supportsComputeShaders ||
                !SystemInfo.supportsAsyncGPUReadback)
            {
                EditorGUILayout.HelpBox(
                    "Для генерации нужны Compute Shaders и Async GPU " +
                    "Readback на текущем graphics device.",
                    MessageType.Error);
            }

            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField(
                "Preview среза",
                EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            previewTexture = (Texture3D)EditorGUILayout.ObjectField(
                "Texture3D",
                previewTexture,
                typeof(Texture3D),
                false);
            previewAxis = (PreviewAxis)EditorGUILayout.EnumPopup(
                "Ось",
                previewAxis);
            previewPosition = EditorGUILayout.Slider(
                "Позиция",
                previewPosition,
                0f,
                1f);
            if (EditorGUI.EndChangeCheck())
                previewDirty = true;

            if (previewTexture == null || computeShader == null)
                return;

            if (previewDirty && Event.current.type == EventType.Repaint)
                UpdatePreview();

            Rect previewRect = GUILayoutUtility.GetAspectRect(
                1f,
                GUILayout.MaxWidth(512f));
            if (previewRenderTexture != null)
            {
                EditorGUI.DrawPreviewTexture(
                    previewRect,
                    previewRenderTexture,
                    null,
                    ScaleMode.ScaleToFit);
            }
        }

        private void GenerateAndSave()
        {
            string validatedPath;
            try
            {
                validatedPath = ValidateAssetPath(outputPath);
            }
            catch (ArgumentException exception)
            {
                EditorUtility.DisplayDialog(
                    "Некорректный путь",
                    exception.Message,
                    "OK");
                return;
            }

            UnityEngine.Object existingObject =
                AssetDatabase.LoadMainAssetAtPath(validatedPath);
            if (existingObject != null &&
                !EditorUtility.DisplayDialog(
                    "Перезаписать Texture3D?",
                    $"Asset уже существует:\n{validatedPath}\n\n" +
                    "GUID будет сохранён, если это Texture3D.",
                    "Перезаписать",
                    "Отмена"))
            {
                return;
            }

            int size = (int)textureSize;
            if (size >= 512 &&
                !EditorUtility.DisplayDialog(
                    "Большой объём 512³",
                    "Генерация 512³ временно использует сотни MB памяти " +
                    "и может занять заметное время. Продолжить?",
                    "Продолжить",
                    "Отмена"))
            {
                return;
            }

            List<NoiseLayer> enabledLayers =
                layers.Where(layer => layer.enabled).ToList();
            GpuNoiseLayer[] gpuLayers = enabledLayers
                .Select(layer => new GpuNoiseLayer(layer))
                .ToArray();
            RenderTexture volume = null;
            ComputeBuffer layerBuffer = null;
            Texture3D generatedTexture = null;
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Volumetric Noise",
                    "Генерация объёма на GPU…",
                    0.15f);

                volume = CreateVolumeRenderTexture(size);
                layerBuffer = new ComputeBuffer(
                    gpuLayers.Length,
                    GpuNoiseLayer.Stride,
                    ComputeBufferType.Structured);
                layerBuffer.SetData(gpuLayers);

                int kernel = computeShader.FindKernel("GenerateVolume");
                computeShader.SetInt("_Size", size);
                computeShader.SetInt("_LayerCount", gpuLayers.Length);
                computeShader.SetInt("_Seed", seed);
                computeShader.SetFloat("_OutputMin", outputMinimum);
                computeShader.SetFloat("_OutputMax", outputMaximum);
                computeShader.SetInt(
                    "_InvertOutput",
                    invertOutput ? 1 : 0);
                computeShader.SetBuffer(kernel, "_Layers", layerBuffer);
                computeShader.SetTexture(kernel, "_Result", volume);
                int groups = Mathf.CeilToInt(size / 4f);
                computeShader.Dispatch(kernel, groups, groups, groups);

                EditorUtility.DisplayProgressBar(
                    "Volumetric Noise",
                    "Чтение полного R8 объёма с GPU…",
                    0.55f);
                AsyncGPUReadbackRequest request =
                    AsyncGPUReadback.Request(
                        volume,
                        0,
                        0,
                        size,
                        0,
                        size,
                        0,
                        size,
                        TextureFormat.R8);
                request.WaitForCompletion();
                if (request.hasError)
                {
                    throw new InvalidOperationException(
                        "Async GPU readback завершился с ошибкой.");
                }

                int expectedLength = checked(size * size * size);
                if (request.layerCount != size)
                {
                    throw new InvalidOperationException(
                        "GPU вернул неожиданное число Z-слоёв: " +
                        $"{request.layerCount}, ожидалось {size}.");
                }

                int sliceLength = checked(size * size);
                byte[] voxelData = new byte[expectedLength];
                for (int layer = 0; layer < request.layerCount; layer++)
                {
                    var sliceData = request.GetData<byte>(layer);
                    if (sliceData.Length != sliceLength)
                    {
                        throw new InvalidOperationException(
                            $"Z-слой {layer} содержит {sliceData.Length} " +
                            $"байт, ожидалось {sliceLength}.");
                    }

                    Unity.Collections.NativeArray<byte>.Copy(
                        sliceData,
                        0,
                        voxelData,
                        layer * sliceLength,
                        sliceLength);
                }

                EditorUtility.DisplayProgressBar(
                    "Volumetric Noise",
                    "Создание Texture3D asset…",
                    0.75f);
                generatedTexture = new Texture3D(
                    size,
                    size,
                    size,
                    TextureFormat.R8,
                    generateMipMaps)
                {
                    name = Path.GetFileNameWithoutExtension(validatedPath),
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    anisoLevel = 0
                };
                generatedTexture.SetPixelData(voxelData, 0);
                generatedTexture.Apply(generateMipMaps, false);

                EnsureAssetFolder(validatedPath);
                Texture3D savedTexture = SaveTexture(
                    generatedTexture,
                    existingObject,
                    validatedPath);
                generatedTexture = null;

                if (assignToMaterial && targetMaterial != null)
                    AssignToFogMaterial(savedTexture);

                AssetDatabase.SaveAssets();
                previewTexture = savedTexture;
                previewDirty = true;
                Selection.activeObject = savedTexture;
                EditorGUIUtility.PingObject(savedTexture);

                stopwatch.Stop();
                statusMessage =
                    $"Готово: {size}³ R8, {enabledLayers.Count} слоёв, " +
                    $"{stopwatch.Elapsed.TotalSeconds:0.0} с.\n" +
                    validatedPath;
                Debug.Log(
                    $"Generated volumetric noise: {validatedPath} " +
                    $"({size}³ R8, {enabledLayers.Count} layers).",
                    savedTexture);
            }
            catch (Exception exception)
            {
                statusMessage = "Ошибка генерации: " + exception.Message;
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Ошибка генерации 3D noise",
                    exception.Message,
                    "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                layerBuffer?.Release();
                if (volume != null)
                {
                    volume.Release();
                    DestroyImmediate(volume);
                }

                if (generatedTexture != null)
                    DestroyImmediate(generatedTexture);
            }
        }

        private static RenderTexture CreateVolumeRenderTexture(int size)
        {
            RenderTexture texture = new RenderTexture(
                size,
                size,
                0,
                RenderTextureFormat.RHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "Volumetric Noise Working Volume",
                dimension = TextureDimension.Tex3D,
                volumeDepth = size,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear
            };

            if (!texture.Create())
            {
                DestroyImmediate(texture);
                throw new InvalidOperationException(
                    "Не удалось создать 3D RenderTexture формата RHalf.");
            }

            return texture;
        }

        private static Texture3D SaveTexture(
            Texture3D generatedTexture,
            UnityEngine.Object existingObject,
            string assetPath)
        {
            if (existingObject is Texture3D existingTexture)
            {
                EditorUtility.CopySerialized(
                    generatedTexture,
                    existingTexture);
                existingTexture.name =
                    Path.GetFileNameWithoutExtension(assetPath);
                EditorUtility.SetDirty(existingTexture);
                DestroyImmediate(generatedTexture);
                return existingTexture;
            }

            if (existingObject != null &&
                !AssetDatabase.DeleteAsset(assetPath))
            {
                throw new InvalidOperationException(
                    $"Не удалось заменить asset: {assetPath}");
            }

            AssetDatabase.CreateAsset(generatedTexture, assetPath);
            return generatedTexture;
        }

        private void AssignToFogMaterial(Texture3D texture)
        {
            if (!targetMaterial.HasProperty(FogTextureProperty))
            {
                Debug.LogWarning(
                    $"Material '{targetMaterial.name}' does not contain " +
                    $"{FogTextureProperty}; texture was saved but not " +
                    "assigned.",
                    targetMaterial);
                return;
            }

            Undo.RecordObject(targetMaterial, "Assign volumetric noise");
            targetMaterial.SetTexture(FogTextureProperty, texture);
            EditorUtility.SetDirty(targetMaterial);
        }

        private void UpdatePreview()
        {
            previewDirty = false;
            ReleasePreview();
            if (previewTexture == null || computeShader == null)
                return;

            int previewSize = Mathf.Min(
                512,
                Mathf.Max(previewTexture.width, 32));
            previewRenderTexture = new RenderTexture(
                previewSize,
                previewSize,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                name = "Volumetric Noise Slice Preview",
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            previewRenderTexture.Create();

            int kernel = computeShader.FindKernel("ExtractSlice");
            int sourceSize = previewTexture.width;
            computeShader.SetInt("_SourceSize", sourceSize);
            computeShader.SetInt("_PreviewSize", previewSize);
            computeShader.SetInt("_PreviewAxis", (int)previewAxis);
            computeShader.SetInt(
                "_PreviewSlice",
                Mathf.RoundToInt((sourceSize - 1) * previewPosition));
            computeShader.SetTexture(kernel, "_Source", previewTexture);
            computeShader.SetTexture(
                kernel,
                "_Preview",
                previewRenderTexture);
            int groups = Mathf.CeilToInt(previewSize / 8f);
            computeShader.Dispatch(kernel, groups, groups, 1);
        }

        private void ReleasePreview()
        {
            if (previewRenderTexture == null)
                return;

            previewRenderTexture.Release();
            DestroyImmediate(previewRenderTexture);
            previewRenderTexture = null;
        }

        private void AddLayer()
        {
            layers.Add(new NoiseLayer
            {
                displayName = $"Layer {layers.Count + 1}",
                blendMode = layers.Count == 0
                    ? BlendMode.Replace
                    : BlendMode.Add
            });
        }

        private void ApplyCloudPreset(bool askForConfirmation)
        {
            if (askForConfirmation &&
                layers.Count > 0 &&
                !EditorUtility.DisplayDialog(
                    "Применить Cloud preset?",
                    "Текущие слои будут заменены.",
                    "Применить",
                    "Отмена"))
            {
                return;
            }

            layers = new List<NoiseLayer>
            {
                new NoiseLayer
                {
                    displayName = "Base Perlin",
                    noiseType = NoiseType.Perlin3D,
                    blendMode = BlendMode.Replace,
                    frequency = 3f,
                    octaveCount = 5,
                    persistence = 0.52f,
                    lacunarity = 2f,
                    opacity = 1f,
                    coverage = 0.42f,
                    smoothness = 0.22f
                },
                new NoiseLayer
                {
                    displayName = "Worley Erosion",
                    noiseType = NoiseType.Worley3D,
                    blendMode = BlendMode.Subtract,
                    offset = new Vector3(11.3f, 27.1f, 5.7f),
                    frequency = 7f,
                    octaveCount = 3,
                    persistence = 0.5f,
                    lacunarity = 2f,
                    opacity = 0.38f,
                    coverage = 0.5f,
                    smoothness = 0.2f
                }
            };
        }

        private void LoadComputeShader()
        {
            computeShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                ComputeShaderPath);
            previewDirty = true;
        }

        private static string ValidateAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Укажите путь сохранения.");

            string normalized = path.Trim().Replace('\\', '/');
            if (!normalized.StartsWith(
                    "Assets/",
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Texture3D должна сохраняться внутри папки Assets.");
            }

            if (!normalized.EndsWith(
                    ".asset",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalized += ".asset";
            }

            return normalized;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)
                ?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) ||
                AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] segments = folder.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
