using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace KurotoriTools
{
    public class AvatarAssemblerUI : EditorWindow
    {
        private GameObject baseAvatar;
        private List<GameObject> avatarParts = new List<GameObject>();
        private LogWindow logWindow;


        [MenuItem("AvatarTools/AvatarAssembler")]
        static void Open()
        {
            var window = EditorWindow.GetWindow<AvatarAssemblerUI>("AvatarAssembler");
            window.Setup();
            
        }

        private void Setup()
        {
            logWindow = new LogWindow();
        }

        private void Check()
        {
            var avatarAssembler = new AvatarAssemblerCore(baseAvatar, avatarParts);

            if(avatarAssembler.CheckAssembleable())
            {
                LogSystem.AddLog("CHECK", "結合可能です");
            }
            else
            {
                LogSystem.AddLog("ERROR", "このモデル同士は結合できません");
            }
        }

        private void Assemble()
        {
            var avatarAssembler = new AvatarAssemblerCore(baseAvatar, avatarParts);
            avatarAssembler.CreateAssembledAvatar(baseAvatar.name + "_Assemble");
            Debug.Log("Create");
        }

        private void OnGUI()
        {
            KurotoriUtility.GUITitle("Avatar Assembler");
            
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("BaseObject");
            baseAvatar = EditorGUILayout.ObjectField(baseAvatar, typeof(GameObject), true) as GameObject;
            GUILayout.EndHorizontal();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("CombineObjects");
                if (GUILayout.Button("+"))
                {
                    avatarParts.Add(null);
                }
                EditorGUI.BeginDisabledGroup(!avatarParts.Any());
                if (GUILayout.Button("-"))
                {
                    avatarParts.Remove(avatarParts.Last());
                }
                EditorGUI.EndDisabledGroup();
            }

            for (int i = 0; i < avatarParts.Count(); ++i)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("CombineObject" + (i + 1));
                    avatarParts[i] = EditorGUILayout.ObjectField(avatarParts[i], typeof(GameObject), true) as GameObject;
                }
            }

            EditorGUI.BeginDisabledGroup(!CheckCombineObjects() || !baseAvatar);
            if (GUILayout.Button("Check"))
            {
                Check();
            }
            if (GUILayout.Button("Assemble!"))
            {
                Assemble();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            if (logWindow == null)
            {
                logWindow = new LogWindow();
            }

            logWindow.DrawLogWindow();
        }

        private void OnDisable()
        {

        }

        private bool CheckCombineObjects()
        {
            if (!avatarParts.Any()) return false;

            foreach (var combineObject in avatarParts)
            {
                if (!combineObject) return false;
            }

            return true;
        }
    }
}
