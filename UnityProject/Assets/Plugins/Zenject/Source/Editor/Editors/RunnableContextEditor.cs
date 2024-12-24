#if !ODIN_INSPECTOR

using UnityEditor;

namespace Zenject
{
    [NoReflectionBaking]
    public class RunnableContextEditor : ContextEditor
    {
        SerializedProperty _autoRun;
        private SerializedProperty autoFindContexts;

        public override void OnEnable()
        {
            base.OnEnable();

            _autoRun = serializedObject.FindProperty("_autoRun");
            autoFindContexts = serializedObject.FindProperty("autoFindContexts");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        protected override void OnGui()
        {
            base.OnGui();

            EditorGUILayout.PropertyField(_autoRun);
            EditorGUILayout.PropertyField(autoFindContexts);
            var script = (RunnableContext)target;
            script.OnValidate();
        }
    }
}


#endif
