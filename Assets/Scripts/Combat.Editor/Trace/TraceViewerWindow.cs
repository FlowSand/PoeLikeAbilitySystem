using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Combat.Runtime.Trace;
using Combat.Editor.GraphAuthoring;
using Combat.Runtime.GraphRuntime;
using UnityEditor;
using UnityEngine;

namespace Combat.Editor.Trace
{
    public class TraceViewerWindow : EditorWindow
    {
        // State
        [SerializeField] private ExecutionTrace _currentTrace;
        [SerializeField] private string _traceFilePath;
        [SerializeField] private int _selectedOpIndex = -1;

        // UI State
        private Vector2 _scrollPosition;
        private bool _showSlotDetails = false;

        // Colors
        private static readonly Color ExecutionBarColor = new Color(0.2f, 0.8f, 0.2f, 0.7f);
        private static readonly Color SelectedColor = Color.cyan;
        private static readonly Color AlternateRowColor = new Color(0.9f, 0.9f, 0.9f);

        [MenuItem("Window/Combat/Trace Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<TraceViewerWindow>();
            window.titleContent = new GUIContent("Trace Viewer");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_currentTrace == null)
            {
                EditorGUILayout.HelpBox(
                    "No trace loaded. Click 'Load Trace' to open a trace file.",
                    MessageType.Info
                );
                return;
            }

            DrawMetadataPanel();
            GUILayout.Space(10);
            DrawOpExecutionList();
            GUILayout.Space(10);
            DrawCommandList();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Load Trace", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                LoadTrace();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                if (!string.IsNullOrEmpty(_traceFilePath))
                {
                    LoadTraceFromPath(_traceFilePath);
                }
            }

            EditorGUI.BeginDisabledGroup(_currentTrace == null);
            if (GUILayout.Button("Highlight in Graph", EditorStyles.toolbarButton, GUILayout.Width(150)))
            {
                HighlightInGraph();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMetadataPanel()
        {
            EditorGUILayout.LabelField("Trace Metadata", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Event Type: {_currentTrace.eventType}");
            EditorGUILayout.LabelField($"Root Event ID: {_currentTrace.rootEventId}  |  Trigger Depth: {_currentTrace.triggerDepth}");

            float milliseconds = _currentTrace.totalExecutionMicroseconds / 1000.0f;
            EditorGUILayout.LabelField($"Total Time: {_currentTrace.totalExecutionMicroseconds} μs ({milliseconds:F2} ms)");

            EditorGUILayout.LabelField($"Ops Executed: {_currentTrace.totalOpsExecuted}  |  Commands Emitted: {_currentTrace.totalCommandsEmitted}");
            EditorGUILayout.EndVertical();
        }

        private void DrawOpExecutionList()
        {
            EditorGUILayout.LabelField("Op Execution Timeline", EditorStyles.boldLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(250));

            long maxTime = _currentTrace.opExecutions.Count > 0
                ? _currentTrace.opExecutions.Max(op => op.microseconds)
                : 1;

            for (int i = 0; i < _currentTrace.opExecutions.Count; i++)
            {
                var opExec = _currentTrace.opExecutions[i];
                bool isSelected = i == _selectedOpIndex;

                // Background color
                Color bgColor = isSelected ? SelectedColor : (i % 2 == 0 ? Color.white : AlternateRowColor);
                GUI.backgroundColor = bgColor;

                EditorGUILayout.BeginHorizontal("box", GUILayout.Height(24));

                // Op button (clickable)
                if (GUILayout.Button($"[{opExec.opIndex}] {opExec.opCode}",
                    GUILayout.Width(200), GUILayout.Height(20)))
                {
                    _selectedOpIndex = i;
                }

                // Timing label
                EditorGUILayout.LabelField($"{opExec.microseconds} μs", GUILayout.Width(100));

                // Timing bar
                float barWidth = (opExec.microseconds / (float)maxTime) * 200f;
                Rect barRect = GUILayoutUtility.GetRect(barWidth, 16, GUILayout.Width(200));
                EditorGUI.DrawRect(barRect, ExecutionBarColor);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCommandList()
        {
            EditorGUILayout.LabelField($"Commands Emitted ({_currentTrace.commands.Count})", EditorStyles.boldLabel);

            if (_currentTrace.commands.Count == 0)
            {
                EditorGUILayout.HelpBox("No commands emitted", MessageType.None);
                return;
            }

            foreach (var cmd in _currentTrace.commands)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"[Op {cmd.emittedAtOpIndex}] {cmd.commandType}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(cmd.commandData, EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private void LoadTrace()
        {
            string tracesDir = TraceExporter.GetTracesDirectory();
            string path = EditorUtility.OpenFilePanel("Load Execution Trace", tracesDir, "json");

            if (!string.IsNullOrEmpty(path))
            {
                LoadTraceFromPath(path);
            }
        }

        private void LoadTraceFromPath(string path)
        {
            try
            {
                _currentTrace = TraceExporter.ImportFromJson(path);
                _traceFilePath = path;
                _selectedOpIndex = -1;
                Debug.Log($"[TraceViewer] Loaded trace: {Path.GetFileName(path)}");
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to load trace: {ex.Message}", "OK");
            }
        }

        private void HighlightInGraph()
        {
            if (_currentTrace == null)
                return;

            try
            {
                // 1. Find EffectGraphAsset by sourceGraphId (GUID)
                string graphPath = AssetDatabase.GUIDToAssetPath(_currentTrace.sourceGraphId);
                if (string.IsNullOrEmpty(graphPath))
                {
                    EditorUtility.DisplayDialog("Error",
                        $"Cannot find graph with GUID: {_currentTrace.sourceGraphId}", "OK");
                    return;
                }

                var graphAsset = AssetDatabase.LoadAssetAtPath<EffectGraphAsset>(graphPath);
                if (graphAsset == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        $"Failed to load graph at: {graphPath}", "OK");
                    return;
                }

                // 2. Find ExecPlanAsset
                string execPlanPath = $"Assets/Generated/ExecPlans/{graphAsset.name}_ExecPlan.asset";
                var execPlanAsset = AssetDatabase.LoadAssetAtPath<ExecPlanAsset>(execPlanPath);

                if (execPlanAsset == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        $"Cannot find ExecPlanAsset at: {execPlanPath}", "OK");
                    return;
                }

                // 3. Build executed node set
                var executedNodeIds = new HashSet<string>();
                foreach (var opExec in _currentTrace.opExecutions)
                {
                    if (opExec.opIndex < execPlanAsset.OpToNodeId.Length)
                    {
                        string nodeId = execPlanAsset.OpToNodeId[opExec.opIndex];
                        if (!string.IsNullOrEmpty(nodeId))
                        {
                            executedNodeIds.Add(nodeId);
                        }
                    }
                }

                // 4. Create highlight data
                var highlightData = new TraceHighlightData
                {
                    executedNodeIds = executedNodeIds.ToList(),
                    opExecutions = _currentTrace.opExecutions.ToList(),
                    opToNodeIdMapping = execPlanAsset.OpToNodeId
                };

                // 5. Apply highlighting
                NodeHighlighter.ApplyHighlightToGraph(graphAsset, highlightData);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to highlight graph: {ex.Message}", "OK");
            }
        }
    }
}
