using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MvcVisionSystem
{
    public static class HeadlessRuntimeCommandService
    {
        public const string EnvironmentSelfTestArgument = "--environment-self-test";
        public const string JsonArgument = "--json";
        public const int SuccessExitCode = 0;
        public const int FailedChecksExitCode = 2;
        public const int InvalidArgumentsExitCode = 64;
        public const int InternalErrorExitCode = 70;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static bool TryExecute(
            string[] args,
            TextWriter standardOutput,
            out int exitCode)
        {
            args ??= Array.Empty<string>();
            bool requested = args.Any(argument =>
                string.Equals(argument, EnvironmentSelfTestArgument, StringComparison.OrdinalIgnoreCase));
            if (!requested)
            {
                exitCode = SuccessExitCode;
                return false;
            }

            standardOutput ??= TextWriter.Null;
            bool validArguments = args.Length == 2
                && args.Count(argument => string.Equals(
                    argument,
                    EnvironmentSelfTestArgument,
                    StringComparison.OrdinalIgnoreCase)) == 1
                && args.Count(argument => string.Equals(
                    argument,
                    JsonArgument,
                    StringComparison.OrdinalIgnoreCase)) == 1;

            if (!validArguments)
            {
                exitCode = InvalidArgumentsExitCode;
                WriteJson(
                    standardOutput,
                    new
                    {
                        schemaVersion = 1,
                        command = "environment-self-test",
                        mode = "read-only",
                        status = "error",
                        exitCode,
                        durableWrites = false,
                        error = new
                        {
                            code = "invalid-arguments",
                            message = "Use: --environment-self-test --json"
                        }
                    });
                return true;
            }

            try
            {
                var service = new RuntimeDiagnosticsService();
                RuntimeSelfTestResult result = service.RunReadOnlySelfTest();
                exitCode = result.FailedCount > 0
                    ? FailedChecksExitCode
                    : SuccessExitCode;
                WriteJson(
                    standardOutput,
                    new
                    {
                        schemaVersion = 1,
                        command = "environment-self-test",
                        mode = "read-only",
                        status = result.OverallStatus,
                        exitCode,
                        durableWrites = false,
                        checkedAt = result.CheckedAt,
                        product = result.Product,
                        productVersion = result.ProductVersion,
                        operatingSystem = result.OperatingSystem,
                        framework = result.Framework,
                        processArchitecture = result.ProcessArchitecture,
                        counts = new
                        {
                            passed = result.PassedCount,
                            warning = result.WarningCount,
                            failed = result.FailedCount
                        },
                        checks = result.Checks.Select(check => new
                        {
                            name = check.Name,
                            status = check.Status,
                            detail = check.Detail
                        }).ToArray()
                    });
            }
            catch (Exception ex)
            {
                exitCode = InternalErrorExitCode;
                WriteJson(
                    standardOutput,
                    new
                    {
                        schemaVersion = 1,
                        command = "environment-self-test",
                        mode = "read-only",
                        status = "error",
                        exitCode,
                        durableWrites = false,
                        error = new
                        {
                            code = "internal-error",
                            exceptionType = ex.GetType().Name
                        }
                    });
            }

            return true;
        }

        private static void WriteJson(TextWriter standardOutput, object value)
        {
            standardOutput.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            standardOutput.Flush();
        }
    }

    [Obsolete("Use HeadlessRuntimeCommandService.", false)]
    public static class WpfHeadlessRuntimeCommandService
    {
        public const string EnvironmentSelfTestArgument = HeadlessRuntimeCommandService.EnvironmentSelfTestArgument;
        public const string JsonArgument = HeadlessRuntimeCommandService.JsonArgument;
        public const int SuccessExitCode = HeadlessRuntimeCommandService.SuccessExitCode;
        public const int FailedChecksExitCode = HeadlessRuntimeCommandService.FailedChecksExitCode;
        public const int InvalidArgumentsExitCode = HeadlessRuntimeCommandService.InvalidArgumentsExitCode;
        public const int InternalErrorExitCode = HeadlessRuntimeCommandService.InternalErrorExitCode;

        public static bool TryExecute(string[] args, TextWriter standardOutput, out int exitCode)
            => HeadlessRuntimeCommandService.TryExecute(args, standardOutput, out exitCode);
    }
}
