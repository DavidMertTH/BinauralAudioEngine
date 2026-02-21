using Code.Renderer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Editor
{
    /// <summary>
    /// Gets rid of the FPS obliterating volume gauge Unity displays on scripts implementing <c>OnAudioFilterRead</c>
    /// </summary>
    [CustomEditor(typeof(BinauralAudioFilter))]
    public class BinauralAudioFilter_Editor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var inspector = new VisualElement();
            InspectorElement.FillDefaultInspector(inspector, serializedObject, this);
            return inspector;
        }
    }
}
