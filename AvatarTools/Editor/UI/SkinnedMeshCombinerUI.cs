using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEditor;

namespace KurotoriTools
{
    public class SkinnedMeshCombinerUI : EditorWindow
    {
        private GameObject targetAvatar;
        private string saveFolder = "ATExportMesh";
        private LogWindow logWindow;

        [MenuItem("AvatarTools/SkinnedMeshCombiner")]
        static void Open()
        {
            var window = EditorWindow.GetWindow<SkinnedMeshCombinerUI>("SkinnedMeshCombiner");
            window.Setup();

        }

        private void Setup()
        {
            logWindow = new LogWindow();
        }

        private bool Check()
        {
            var skinMeshCombiner = new SkinnedMeshCombinerCore(targetAvatar);

            return skinMeshCombiner.Check();
        }

        private void Combine()
        {
            var skinMeshCombiner = new SkinnedMeshCombinerCore(targetAvatar);
            skinMeshCombiner.Combine(saveFolder);
        }

        private void OnGUI()
        {
            KurotoriUtility.GUITitle("Skinned Mesh Combiner");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Save Folder");
                saveFolder = EditorGUILayout.TextField(saveFolder);
            }

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("TargetObject");
            targetAvatar = EditorGUILayout.ObjectField(targetAvatar, typeof(GameObject), true) as GameObject;
            GUILayout.EndHorizontal();

            EditorGUI.BeginDisabledGroup(!targetAvatar);
            if (GUILayout.Button("Combine!"))
            {
                if (!Check()) return;
                Combine();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            if(logWindow == null)
            {
                logWindow = new LogWindow();
            }

            logWindow.DrawLogWindow();
        }
    }
}
