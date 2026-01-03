using System.IO;
using UnityEngine;

namespace Combat.Runtime.Trace
{
    /// <summary>
    /// Utility for exporting and importing ExecutionTrace to/from JSON files.
    /// Traces are saved to Application.persistentDataPath/Traces/ directory.
    /// </summary>
    public static class TraceExporter
    {
        private const string TraceDirectoryName = "Traces";

        /// <summary>
        /// Export an ExecutionTrace to JSON file.
        /// </summary>
        /// <param name="trace">The trace to export</param>
        /// <param name="fileName">File name (without path). If null, auto-generates based on timestamp.</param>
        /// <returns>Full path to the exported file</returns>
        public static string ExportToJson(ExecutionTrace trace, string fileName = null)
        {
            if (trace == null)
            {
                Debug.LogError("[TraceExporter] Cannot export null trace");
                return null;
            }

            // Create traces directory if it doesn't exist
            string tracesPath = Path.Combine(Application.persistentDataPath, TraceDirectoryName);
            if (!Directory.Exists(tracesPath))
            {
                Directory.CreateDirectory(tracesPath);
            }

            // Generate file name if not provided
            if (string.IsNullOrEmpty(fileName))
            {
                fileName = $"trace_{trace.rootEventId}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
            }

            // Ensure .json extension
            if (!fileName.EndsWith(".json"))
            {
                fileName += ".json";
            }

            string fullPath = Path.Combine(tracesPath, fileName);

            // Serialize to JSON using Unity's JsonUtility
            string json = JsonUtility.ToJson(trace, prettyPrint: true);

            // Write to file
            File.WriteAllText(fullPath, json);

            Debug.Log($"[TraceExporter] Exported trace to {fullPath}");
            return fullPath;
        }

        /// <summary>
        /// Import an ExecutionTrace from JSON file.
        /// </summary>
        /// <param name="filePath">Full path to the JSON file</param>
        /// <returns>Deserialized ExecutionTrace, or null if failed</returns>
        public static ExecutionTrace ImportFromJson(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogError("[TraceExporter] Cannot import from null or empty file path");
                return null;
            }

            if (!File.Exists(filePath))
            {
                Debug.LogError($"[TraceExporter] File does not exist: {filePath}");
                return null;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                ExecutionTrace trace = JsonUtility.FromJson<ExecutionTrace>(json);

                Debug.Log($"[TraceExporter] Imported trace from {filePath} ({trace.opExecutions.Count} ops, {trace.commands.Count} commands)");
                return trace;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TraceExporter] Failed to import trace from {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the default traces directory path.
        /// </summary>
        public static string GetTracesDirectory()
        {
            return Path.Combine(Application.persistentDataPath, TraceDirectoryName);
        }
    }
}
