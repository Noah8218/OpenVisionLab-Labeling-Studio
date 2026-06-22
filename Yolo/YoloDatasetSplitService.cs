using System;
using System.Collections.Generic;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetSplitService
    {
        public const string TrainMode = "train";
        public const string ValidMode = "valid";
        public const string TestMode = "test";

        public static IReadOnlyList<string> SelectModesForImage(string imageName, YoloDatasetSettings settings)
        {
            int validationPercent = Math.Clamp(settings?.ValidationPercent ?? 20, 0, 100);
            int testPercent = Math.Clamp(settings?.TestPercent ?? 0, 0, 100);
            int reservedPercent = Math.Clamp(validationPercent + testPercent, 0, 100);
            if (reservedPercent <= 0)
            {
                return new[] { TrainMode };
            }

            if (testPercent >= 100)
            {
                return new[] { TestMode };
            }

            if (validationPercent >= 100)
            {
                return new[] { ValidMode };
            }

            uint bucket = StableBucket(imageName, settings?.SplitSeed ?? 17);
            if (bucket < testPercent)
            {
                return new[] { TestMode };
            }

            return bucket < reservedPercent
                ? new[] { ValidMode }
                : new[] { TrainMode };
        }

        private static uint StableBucket(string value, int seed)
        {
            unchecked
            {
                uint hash = 2166136261;
                string key = (value ?? string.Empty).Trim().ToLowerInvariant();
                foreach (char ch in key)
                {
                    hash ^= ch;
                    hash *= 16777619;
                }

                hash ^= (uint)seed;
                hash *= 16777619;
                return hash % 100;
            }
        }
    }
}
