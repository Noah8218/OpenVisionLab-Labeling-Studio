using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace MvcVisionSystem._3._Communication.TCP
{
    public static class LearningProtocol
    {
        public const string PacketSeparator = "\n\n";

        public static byte[] BuildTrainingPacket(
            string command,
            string imgSize,
            string batch,
            string epoch,
            string cfg,
            string weight,
            string dataYaml,
            string model = "yolov5",
            string task = "detect")
        {
            var request = new YoloTrainingRequest
            {
                imgSize = imgSize ?? "",
                batch = batch ?? "",
                epoch = epoch ?? "",
                cfg = NormalizeProtocolPath(cfg),
                weight = NormalizeProtocolPath(weight),
                dataYaml = NormalizeProtocolPath(dataYaml),
                model = string.IsNullOrWhiteSpace(model) ? "yolov5" : model,
                task = string.IsNullOrWhiteSpace(task) ? "detect" : task
            };

            string json = JsonConvert.SerializeObject(request);
            return BuildPacket(command, Encoding.UTF8.GetBytes(json));
        }

        public static byte[] BuildImagePacket(string command, Image image)
        {
            if (image == null)
            {
                return Array.Empty<byte>();
            }

            return BuildPacket(command, ImageToPngBytes(image));
        }

        public static byte[] BuildHealthCheckPacket(string requestId = "")
        {
            return BuildJsonLinePacket(new PythonWorkerRequest
            {
                Type = "HealthCheck",
                RequestId = FirstNonEmpty(requestId, Guid.NewGuid().ToString("N"))
            });
        }

        public static byte[] BuildModelStatusPacket(string requestId = "", bool ensureLoaded = false)
        {
            return BuildJsonLinePacket(new PythonModelStatusRequest
            {
                Type = "ModelStatus",
                RequestId = FirstNonEmpty(requestId, Guid.NewGuid().ToString("N")),
                Load = ensureLoaded,
                EnsureLoaded = ensureLoaded
            });
        }

        public static byte[] BuildDetectImagePacket(
            string requestId,
            string imageId,
            string imagePath,
            float confidence,
            string model = "yolov5")
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return Array.Empty<byte>();
            }

            return BuildJsonLinePacket(new DetectImageRequest
            {
                RequestId = FirstNonEmpty(requestId, Guid.NewGuid().ToString("N")),
                ImageId = imageId ?? string.Empty,
                ImagePath = imagePath,
                Confidence = confidence,
                Model = string.IsNullOrWhiteSpace(model) ? "yolov5" : model
            });
        }

        public static byte[] BuildJsonLinePacket(object request)
        {
            if (request == null)
            {
                return Array.Empty<byte>();
            }

            string json = JsonConvert.SerializeObject(
                request,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                });
            return Encoding.UTF8.GetBytes(json + "\n");
        }

        public static byte[] BuildPacket(string command, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return Array.Empty<byte>();
            }

            byte[] commandData = Encoding.ASCII.GetBytes(command);
            byte[] separator = Encoding.ASCII.GetBytes(PacketSeparator);
            return Combine(commandData, separator, payload ?? Array.Empty<byte>());
        }

        public static string NormalizeProtocolPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "" : path.Replace("\\", "/");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static byte[] ImageToPngBytes(Image image)
        {
            using (var stream = new MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        private static byte[] Combine(params byte[][] arrays)
        {
            int length = 0;
            foreach (byte[] array in arrays)
            {
                length += array?.Length ?? 0;
            }

            byte[] result = new byte[length];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                if (array == null || array.Length == 0)
                {
                    continue;
                }

                Buffer.BlockCopy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }

            return result;
        }
    }

    public sealed class YoloTrainingRequest
    {
        public string imgSize { get; set; } = "";
        public string batch { get; set; } = "";
        public string epoch { get; set; } = "";
        public string cfg { get; set; } = "";
        public string weight { get; set; } = "";
        public string dataYaml { get; set; } = "";
        public string model { get; set; } = "yolov5";
        public string task { get; set; } = "detect";
    }

    public class PythonWorkerRequest
    {
        public string Type { get; set; } = "";
        public int Version { get; set; } = 1;
        public string RequestId { get; set; } = "";
    }

    public sealed class PythonModelStatusRequest : PythonWorkerRequest
    {
        public bool Load { get; set; }
        public bool EnsureLoaded { get; set; }
    }

    public sealed class DetectImageRequest : PythonWorkerRequest
    {
        public DetectImageRequest()
        {
            Type = "DetectImage";
        }

        public string ImageId { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public float Confidence { get; set; }
        public string Model { get; set; } = "yolov5";
    }
}
