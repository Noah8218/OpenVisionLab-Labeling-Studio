using OpenVisionLab.ImageCanvas.Rendering;
using SharpGL;
using SharpGL.WinForms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    internal static class OpenGlRuntimeCapabilityProbe
    {
        private static readonly string[] RequiredExtensionFunctions =
        {
            "glGenFramebuffersEXT",
            "glBindFramebufferEXT",
            "glFramebufferTexture2DEXT",
            "glCheckFramebufferStatusEXT",
            "glDeleteFramebuffersEXT",
            "glGenRenderbuffersEXT",
            "glBindRenderbufferEXT",
            "glRenderbufferStorageEXT",
            "glFramebufferRenderbufferEXT",
            "glDeleteRenderbuffersEXT",
            "glGenerateMipmapEXT"
        };

        internal static RuntimeSelfTestCheck Probe(ImageCanvasControl imageViewer)
        {
            OpenGLControl control = imageViewer?.GetOpenGLControl();
            OpenGL gl = control?.OpenGL;
            if (control == null
                || !control.IsHandleCreated
                || gl?.RenderContextProvider == null
                || gl.RenderContextProvider.RenderContextHandle == IntPtr.Zero)
            {
                return new RuntimeSelfTestCheck(
                    RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                    "warning",
                    "이미지 뷰어 그래픽 컨텍스트가 아직 준비되지 않았습니다. 창을 연 뒤 환경 점검을 다시 실행하세요.");
            }

            try
            {
                gl.MakeCurrent();
                string vendor = Normalize(gl.Vendor);
                string renderer = Normalize(gl.Renderer);
                string version = Normalize(gl.Version);
                IReadOnlyList<string> missingFunctions = RequiredExtensionFunctions
                    .Where(functionName => !gl.IsExtensionFunctionSupported(functionName))
                    .ToArray();
                string identity = BuildIdentity(vendor, renderer, version);

                if (missingFunctions.Count > 0)
                {
                    return new RuntimeSelfTestCheck(
                        RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                        "fail",
                        "이미지 뷰어 사용 불가"
                        + identity
                        + " · 지원되지 않는 필수 기능: "
                        + string.Join(", ", missingFunctions)
                        + " · 지원되는 GPU 드라이버가 설치된 로컬 PC/VM 콘솔에서 다시 실행하세요.");
                }

                return new RuntimeSelfTestCheck(
                    RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                    "pass",
                    "이미지 뷰어 사용 가능"
                    + identity
                    + $" · 필수 framebuffer 함수 {RequiredExtensionFunctions.Length}개 확인");
            }
            catch (Exception ex)
            {
                return new RuntimeSelfTestCheck(
                    RuntimeDiagnosticsService.ViewerGraphicsCheckName,
                    "fail",
                    "이미지 뷰어 그래픽 점검 실패 · "
                    + ex.GetType().Name
                    + " · 지원되는 GPU 드라이버가 설치된 로컬 PC/VM 콘솔에서 다시 실행하세요.");
            }
        }

        private static string BuildIdentity(string vendor, string renderer, string version)
        {
            string[] values = { vendor, renderer, version };
            string identity = string.Join(
                " / ",
                values.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(identity)
                ? string.Empty
                : " · " + identity;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }
    }
}
