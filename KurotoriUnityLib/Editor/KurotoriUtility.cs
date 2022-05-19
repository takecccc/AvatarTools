using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;



namespace KurotoriTools
{

    public static class KurotoriUtility
    {
        /// <summary>
        /// 指定したTransform以下の階層構造とボーンのTransformのリストを作成する。
        /// 同一階層の同じ名前のオブジェクトを発見した場合、そこで切り上げてFalseを返す。
        /// </summary>
        /// <param name="srcObject">BonePathListを生成したい親Transform</param>
        /// <param name="bonePathList">出力されるBonePathList</param>
        /// <returns>同一階層の同じ名前のオブジェクトを発見した場合Falseを返す</returns>
        public static bool CreateBonePathList(Transform srcObject, out Dictionary<string, Transform> bonePathList)
        {
            bonePathList = new Dictionary<string, Transform>();

            string path = "/" + srcObject.name;

            bonePathList.Add(path, srcObject);

            Transform children = srcObject.GetComponentInChildren<Transform>();

            if (children.childCount > 0)
            {
                foreach (Transform child in children)
                {
                    if(!CreateChildBonePath(path, child, ref bonePathList))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        
        private static bool CreateChildBonePath(string path, Transform srcObj, ref Dictionary<string, Transform> bonePathList)
        {
            string newPath = path + "/" + srcObj.name;

            if (bonePathList.ContainsKey(newPath)) return false;

            bonePathList.Add(newPath, srcObj);

            Transform children = srcObj.GetComponentInChildren<Transform>();
            if (children.childCount > 0)
            {
                foreach (Transform child in children)
                {
                    if (!CreateChildBonePath(newPath, child, ref bonePathList))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// BonePathListをString表示するためのユーティリティ
        /// </summary>
        /// <param name="bonePathList"></param>
        /// <returns></returns>
        public static string BonePathListString(Dictionary<string, Transform> bonePathList)
        {
            string output = "";
            foreach (var bonePath in bonePathList)
            {
                output += string.Format("{0}:{1}\n", bonePath.Key, bonePath.Value.name);
            }
            return output;
        }

        /// <summary>
        /// 子オブジェクトを削除する
        /// </summary>
        /// <param name="gameObject"></param>
        public static void DestroyChildren(GameObject gameObject)
        {
            var children = new List<Transform>();
            foreach(Transform child in gameObject.transform)
            {
                children.Add(child);
            }

            foreach(var child in children)
            {
                GameObject.DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// 同名のTransformをsrc以下から探す
        /// </summary>
        /// <param name="src">検索対象</param>
        /// <param name="name">検索する名前</param>
        /// <returns>同名のTransform、ない場合はnullを返す</returns>
        public static Transform FindSameNameObjectInChildren(Transform src, string name)
        {
            if (src.name == name)
            {
                return src;
            }
            else
            {
                var children = src.GetComponentInChildren<Transform>();
                if(children.childCount == 0)
                {
                    return null;
                }
                else
                {
                    Transform result = null;
                    foreach(Transform child in children)
                    {
                        result = FindSameNameObjectInChildren(child, name);
                        if (result != null) break;
                    }
                    return result;
                }
            }
        }
        
        /// <summary>
        /// 自身とその子供のリストを取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="list"></param>
        public static void GetChildrenAndSelfList(this Transform self, out List<Transform> list)
        {
            list = new List<Transform>();

            list.Add(self);

            Transform children = self.GetComponentInChildren<Transform>();
            if (children.childCount > 0)
            {
                foreach (Transform child in children)
                {
                    child.GetChildrenList(ref list);
                }
            }
            
        }

        /// <summary>
        /// BonePathを取得する。rootBoneがtargetの親でない場合は空文字を返す。
        /// </summary>
        /// <param name="rootBone">ルートボーン</param>
        /// <param name="target">対象</param>
        /// <returns>BonePath</returns>
        public static string GetBonePath(Transform rootBone, Transform target)
        {
            var targetTmp = target;
            string path = "";
            while(true)
            {
                path = "/" + targetTmp.name + path;

                if (rootBone.Equals(targetTmp))
                {
                    break;
                }

                targetTmp = targetTmp.parent;

                if (!targetTmp) return "";
            }

            return path;
        }

        /// <summary>
        /// 子供のリストを取得する
        /// </summary>
        /// <param name="self"></param>
        /// <param name="list"></param>
        private static void GetChildrenList(this Transform self, ref List<Transform> list)
        {
            list.Add(self);

            Transform children = self.GetComponentInChildren<Transform>();

            if (children.childCount == 0)
            {
                return;
            }
            foreach (Transform child in children)
            {
                child.GetChildrenList(ref list);
            }
        }

        /// <summary>
        /// 指定されたtransformが自身、または子供かどうか
        /// </summary>
        /// <param name="self"></param>
        /// <param name="transform">対象のTransform</param>
        /// <returns></returns>
        public static bool IsSelfOrChildOf(this Transform self, Transform transform)
        {
            return self.IsChildOf(transform) || self == transform;
        }

        /// <summary>
        /// ログ出力ユーティリティ
        /// </summary>
        /// <param name="type"></param>
        /// <param name="log"></param>
        public static void OutputLog(LogType type, string log)
        {
            //Debug.Log(LogUtility.CreateLog(type, log));

            LogSystem.AddLog(type.ToString(), log);
        }

        /// <summary>
        /// 指定されたコンポーネントがアタッチされているかどうかを返します
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool HasComponent<T>(this GameObject self) where T : Component
        {
            return self.GetComponent<T>() != null;
        }

        /// <summary>
        /// 指定されたコンポーネントがアタッチされているかどうかを返します
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool HasComponentInChildren<T>(this GameObject self) where T : Component
        {
            return self.GetComponentInChildren<T>() != null;
        }

        /// <summary>
        /// UnityではType.GetTypeでクラスの型を名前で取得できないのでその代用
        /// </summary>
        /// <param name="className">クラス名</param>
        /// <returns>クラスの型</returns>
        public static System.Type GetTypeByClassName(string className)
        {
            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (type.Name == className)
                    {
                        return type;
                    }
                }
            }

            Debug.LogWarning("GetTypeByClassName: " + className + " は存在しません");
            return null;
        }

        /// <summary>
        /// UnityではType.GetTypeでクラスの型を名前で取得できないのでその代用（ネームスペース込み）
        /// </summary>
        /// <param name="namespaceName">名前空間名</param>
        /// <param name="className">クラス名</param>
        /// <returns>クラスの型</returns>
        public static System.Type GetTypeByClassNameWithNamespace(string namespaceName,string className)
        {
            foreach (Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (System.Type type in assembly.GetTypes())
                {
                    if (type.Namespace != null && type.Namespace.Equals(namespaceName) && type.Name.Equals(className))
                    {
                        return type;
                    }
                }
            }

            Debug.LogWarning("GetTypeByClassName: " + namespaceName+ "." + className + " は存在しません");
            return null;
        }

        /// <summary>
        /// リフレクションを使用しコンポーネントから指定したフィールドに値をセットする
        /// </summary>
        /// <param name="component"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <param name="type"></param>
        public static void SetFieldValueByName(Component component, string fieldName, object value, System.Type type)
        {
            var field = type.GetField(fieldName);
            field.SetValue(component, value);
        }

        /// <summary>
        /// リフレクションを使用しコンポーネントから指定したフィールドの値を取得する
        /// </summary>
        /// <param name="component"></param>
        /// <param name="fieldName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object GetFieldValueByName(Component component, string fieldName, System.Type type)
        {
            var field = type.GetField(fieldName);
            return field?.GetValue(component);
        }

        /// <summary>
        /// リフレクションを使用しコンポーネントからコンポーネントへ指定したフィールドに値をコピーする
        /// </summary>
        /// <param name="fieldName"></param>
        /// <param name="src"></param>
        /// <param name="dst"></param>
        /// <param name="type"></param>
        public static void CopyFieldValue(string fieldName, Component src, Component dst, System.Type type)
        {
            SetFieldValueByName(
                dst,
                fieldName,
                GetFieldValueByName(src, fieldName, type),
                type);
        }

        /// <summary>
        /// リフレクションを使用しリストオブジェクトを取得する
        /// </summary>
        /// <param name="list"></param>
        /// <returns></returns>
        public static List<object> GetListPropertyByName(object list)
        {
            var output = new List<object>();

            var listType = list.GetType();
            var countMethod = listType.GetProperty("Count");

            int count = (int)countMethod.GetValue(list, null);
            for (int i = 0; i < count; i++)
            {
                var indexer = listType.GetProperty("Item");
                var value = indexer.GetValue(list, new object[] { i });

                if (value != null)
                {
                    output.Add(value);
                }
            }

            return output;
        }

        /// <summary>
        /// リフレクションを使って、ターゲットのリストを入れ替える
        /// </summary>
        /// <param name="target"></param>
        /// <param name="list"></param>
        public static void ReplaceListPropetyByName(object target, List<object> list)
        {
            var targetType = target.GetType();
            var clearMethod = targetType.GetMethod("Clear");
            var addMethod = targetType.GetMethod("Add");

            clearMethod.Invoke(target, null);

            foreach(var item in list)
            {
                addMethod.Invoke(target, new object[] { item });
            }
        }

        public static AnimationCurve CopyAnimationCurve(AnimationCurve curve)
        {
            var newCurve = new AnimationCurve();

            foreach(var key in curve.keys)
            {
                newCurve.AddKey(key);
            }

            newCurve.postWrapMode = curve.postWrapMode;
            newCurve.preWrapMode = curve.preWrapMode;

            return newCurve;
        }

        public static void GUITitle(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            GUIStyle titleStyle = new GUIStyle();
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 16;
            titleStyle.fontStyle = FontStyle.Bold;
            if(EditorGUIUtility.isProSkin)
            {
                titleStyle.normal.textColor = Color.white;
            }
            else
            {
                titleStyle.normal.textColor = Color.black;
            }
            EditorGUILayout.LabelField(title, titleStyle);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
        }
    }
}
