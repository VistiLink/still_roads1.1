using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PSXStyleShader
{
    public sealed class PSXDownscaleDitherFeature : ScriptableRendererFeature
    {
        [Serializable]
        private sealed class Settings
        {
            [SerializeField] private Shader _shader;
            [SerializeField] private Vector2Int _targetResolution = new Vector2Int(320, 240);
            [SerializeField, Range(1f, 8f)] private float _colorBits = 5f;
            [SerializeField, Range(0f, 1f)] private float _ditherStrength = 0.8f;
            [SerializeField] private RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            public Shader Shader => _shader;
            public Vector2Int TargetResolution => _targetResolution;
            public float ColorBits => _colorBits;
            public float DitherStrength => _ditherStrength;
            public RenderPassEvent RenderPassEvent => _renderPassEvent;
        }

        [SerializeField] private Settings _settings = new Settings();

        private Material _material;
        private Pass _pass;

        public override void Create()
        {
            Shader shader = _settings.Shader;
            if (shader == null || !shader.isSupported)
            {
                DestroyMaterial();
                _pass = null;
                return;
            }

            if (_material == null || _material.shader != shader)
            {
                DestroyMaterial();
                _material = CoreUtils.CreateEngineMaterial(shader);
            }

            if (_pass == null)
            {
                _pass = new Pass(_material);
            }

            _pass.renderPassEvent = _settings.RenderPassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pass == null || _material == null)
            {
                return;
            }

            if (renderingData.cameraData.isPreviewCamera)
            {
                return;
            }

            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
            {
                return;
            }

            _pass.SetSettings(_settings.TargetResolution, _settings.ColorBits, _settings.DitherStrength);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (_pass != null)
            {
                _pass.Dispose();
                _pass = null;
            }

            DestroyMaterial();
        }

        private void DestroyMaterial()
        {
            if (_material == null)
            {
                return;
            }

            CoreUtils.Destroy(_material);
            _material = null;
        }

        private sealed class Pass : ScriptableRenderPass
        {
            private static readonly int TargetResolutionId = Shader.PropertyToID("_TargetResolution");
            private static readonly int UseScreenResolutionId = Shader.PropertyToID("_UseScreenResolution");
            private static readonly int ColorBitsId = Shader.PropertyToID("_ColorBits");
            private static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");

            private readonly Material _material;

            private RTHandle _cameraColor;
            private RTHandle _tempRt;

            private Vector2Int _targetResolution;
            private float _colorBits;
            private float _ditherStrength;

            public Pass(Material material)
            {
                _material = material;
                ConfigureInput(ScriptableRenderPassInput.Color);
            }

            public void SetSettings(Vector2Int targetResolution, float colorBits, float ditherStrength)
            {
                _targetResolution = targetResolution;
                _colorBits = colorBits;
                _ditherStrength = ditherStrength;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _cameraColor = renderingData.cameraData.renderer.cameraColorTargetHandle;

                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(
                    ref _tempRt,
                    desc,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    false,
                    1,
                    0f,
                    "_PSX_DitherTemp"
                );
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_material == null || _cameraColor == null || _tempRt == null)
                {
                    return;
                }

                int w = Mathf.Max(1, _targetResolution.x);
                int h = Mathf.Max(1, _targetResolution.y);

                _material.SetVector(TargetResolutionId, new Vector4(w, h, 0f, 0f));
                _material.SetFloat(UseScreenResolutionId, 0f);
                _material.SetFloat(ColorBitsId, Mathf.Clamp(_colorBits, 1f, 8f));
                _material.SetFloat(DitherStrengthId, Mathf.Clamp01(_ditherStrength));

                CommandBuffer cmd = CommandBufferPool.Get("PSX Downscale Dither");

                cmd.Blit(_cameraColor.nameID, _tempRt.nameID, _material, 0);
                cmd.Blit(_tempRt.nameID, _cameraColor.nameID);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                if (_tempRt != null)
                {
                    _tempRt.Release();
                    _tempRt = null;
                }
            }
        }
    }
}
