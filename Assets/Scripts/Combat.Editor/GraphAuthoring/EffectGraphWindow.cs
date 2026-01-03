using GraphProcessor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Combat.Editor.GraphAuthoring
{
    /// <summary>
    /// Editor window for EffectGraphAsset.
    /// Opens when double-clicking an EffectGraphAsset in the Project window.
    /// </summary>
    public class EffectGraphWindow : BaseGraphWindow
    {
        /// <summary>
        /// Menu item to manually open the Effect Graph editor.
        /// </summary>
        [MenuItem("Window/Combat/Effect Graph Editor")]
        public static BaseGraphWindow OpenManual()
        {
            var graphWindow = CreateWindow<EffectGraphWindow>();
            graphWindow.Show();
            return graphWindow;
        }

        /// <summary>
        /// Asset callback: opens the graph when double-clicking an EffectGraphAsset.
        /// </summary>
        [OnOpenAsset(0)]
        public static bool OnOpenAsset(int instanceID, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceID) as EffectGraphAsset;
            if (asset != null)
            {
                var window = GetWindow<EffectGraphWindow>();
                window.InitializeGraph(asset);
                window.Show();
                return true;
            }
            return false;
        }

        protected override void InitializeWindow(BaseGraph graph)
        {
            titleContent = new GUIContent("Effect Graph");

            if (graphView == null)
            {
                graphView = new BaseGraphView(this);
            }

            rootView.Add(graphView);
        }
    }
}
