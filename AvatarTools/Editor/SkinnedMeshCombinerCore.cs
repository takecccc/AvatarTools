using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace KurotoriTools
{
    public class SkinnedMeshCombinerCore
    {
        public static int UV_CHANNEL_NUM = 8;
        private GameObject targetModel;

        private struct BlendShape
        {
            public string name;
            public int vertexOffset;
            public List<Vector3> deltaVerticies;
            public List<Vector3> deltaNormals;
            public List<Vector3> deltaTangents;

            public BlendShape(string name, List<Vector3> verticies, List<Vector3> normals, List<Vector3> tangents, int vertexOffset)
            {
                this.name = name;
                this.vertexOffset = vertexOffset;
                deltaVerticies = verticies;
                deltaNormals = normals;
                deltaTangents = tangents;
            }
        };

        private class SkinMeshInfo
        {
            public List<Material> materialList = new List<Material>();
            public List<Vector3> verticesList = new List<Vector3>(); // 頂点リスト
            public List<Vector3> normalsList = new List<Vector3>(); // ノーマルリスト
            public List<Vector4> tangentsList = new List<Vector4>(); // タンジェントリスト
            public List<Color> colorList = new List<Color>(); // 頂点カラーリスト
            public List<Vector2>[] uvList = new List<Vector2>[UV_CHANNEL_NUM]; // UVリスト
            public List<BlendShape> blendShapesList = new List<BlendShape>(); // ブレンドシェイプリスト
            public List<List<int>> subMeshList = new List<List<int>>(); // サブメッシュ(三角形)リスト
            public List<BoneWeight> boneWeightsList = new List<BoneWeight>(); // ボーンウェイトリスト

            public bool[] uvExist = new bool[UV_CHANNEL_NUM];
            public bool colorExist = false;

            public SkinMeshInfo()
            {
                for (int c = 0; c < UV_CHANNEL_NUM; c++)
                {
                    uvList[c] = new List<Vector2>();
                    uvExist[c] = false;
                }
            }

            public void SetExistFlag(IEnumerable<SkinnedMeshRenderer> meshRenderers)
            {
                foreach (var skinMesh in meshRenderers)
                {
                    // 事前にUVチャンネルと頂点カラー有無をチェックする。
                    var mesh = skinMesh.sharedMesh;
                    uvExist[0] = uvExist[0] || (mesh.uv.Length == mesh.vertexCount);
                    uvExist[1] = uvExist[1] || (mesh.uv2.Length == mesh.vertexCount);
                    uvExist[2] = uvExist[2] || (mesh.uv3.Length == mesh.vertexCount);
                    uvExist[3] = uvExist[3] || (mesh.uv4.Length == mesh.vertexCount);
                    uvExist[4] = uvExist[4] || (mesh.uv5.Length == mesh.vertexCount);
                    uvExist[5] = uvExist[5] || (mesh.uv6.Length == mesh.vertexCount);
                    uvExist[6] = uvExist[6] || (mesh.uv7.Length == mesh.vertexCount);
                    uvExist[7] = uvExist[7] || (mesh.uv8.Length == mesh.vertexCount);

                    colorExist = colorExist || (mesh.colors.Length == mesh.vertexCount);
                }
            }
        }

        public SkinnedMeshCombinerCore(GameObject targetModel)
        {
            this.targetModel = targetModel;
        }

        public bool Check()
        {
            if (!targetModel.HasComponentInChildren<SkinnedMeshRenderer>())
            {
                KurotoriUtility.OutputLog(LogType.ERROR, "対象のオブジェクトにSkinnedMeshRendererが存在しません。");
                //EditorUtility.DisplayDialog("ERROR", "対象のオブジェクトにSkinnedMeshRendererが存在しません。", "OK");
                return false;
            }
            return true;
        }

        public void Combine(string saveFolder)
        {
            KurotoriUtility.OutputLog(LogType.LOG, "結合開始");

            GameObject exportObject = Object.Instantiate(targetModel, Vector3.zero, Quaternion.identity);
            SkinnedMeshRenderer baseMesh = exportObject.GetComponentInChildren<SkinnedMeshRenderer>();
            
            
            var baseAnimator = exportObject.GetComponent<Animator>();
            Transform baseRootBone = baseAnimator.GetBoneTransform(HumanBodyBones.Hips);

            Transform rootBone = baseRootBone;

            List<Transform> boneList;
            baseRootBone.GetChildrenAndSelfList(out boneList);

            List<Matrix4x4> bindPoseList;
            CreateBindPoseList(boneList, exportObject.transform.localToWorldMatrix, out bindPoseList);

            var skinMeshRendererList = exportObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            ExtrudeClothMesh(ref skinMeshRendererList);

            SkinMeshInfo skinMeshInfo = new SkinMeshInfo();
            skinMeshInfo.SetExistFlag(skinMeshRendererList);
            

            foreach(var skinMeshRenderer in skinMeshRendererList)
            {
                var srcMesh = skinMeshRenderer.sharedMesh;

                Dictionary<int, int> boneIndexMatchDictionary;
                CreateBoneIndexMatchDictionary(skinMeshRenderer, boneList, out boneIndexMatchDictionary);

                Dictionary<int, int> materialIndexMatchDictionary;
                UpdateMaterialList(skinMeshRenderer, ref skinMeshInfo.materialList, ref skinMeshInfo.subMeshList, out materialIndexMatchDictionary);

                var originalScale = skinMeshRenderer.transform.localScale;
                var originalRotation = skinMeshRenderer.transform.localRotation;

                // BakeMeshに備えて位置姿勢を初期値に（こうするとバインド時にずれない）
                skinMeshRenderer.transform.localPosition = Vector3.zero;
                skinMeshRenderer.transform.localScale = Vector3.one;
                skinMeshRenderer.transform.localRotation = Quaternion.identity;

                // バインドポーズ適用
                var bakedMesh = new Mesh();
                skinMeshRenderer.BakeMesh(bakedMesh);


                int indexOffset = skinMeshInfo.verticesList.Count();

                // メッシュコピー
                CopyMesh(indexOffset, srcMesh, bakedMesh, ref skinMeshInfo.verticesList, ref skinMeshInfo.normalsList, ref skinMeshInfo.tangentsList, ref skinMeshInfo.colorList, ref skinMeshInfo.uvList, skinMeshInfo.colorExist, skinMeshInfo.uvExist);

                CopyBlendShape(ref skinMeshInfo.blendShapesList, indexOffset, srcMesh, originalRotation, originalScale);

                CopyTriangleList(ref skinMeshInfo.subMeshList, indexOffset, srcMesh, materialIndexMatchDictionary);

                CopyBoneWeight(ref skinMeshInfo.boneWeightsList, srcMesh, boneIndexMatchDictionary);

                // 参照元のオブジェクトを削除
                GameObject.DestroyImmediate(skinMeshRenderer.gameObject);
            }

            var newMeshObject = GenerateNewSkinnedMeshObject(exportObject.transform, skinMeshInfo, boneList, bindPoseList, rootBone);
            var newMeshRenderer = newMeshObject.GetComponent<SkinnedMeshRenderer>();
            

            RemapVRCVisemeSkinMesh(exportObject, newMeshRenderer);

            SaveMeshAsset(saveFolder, targetModel.name + "_mesh", newMeshRenderer.sharedMesh);

        }

        private void CreateBoneList(GameObject target, out List<Transform> list)
        {
            var baseAnimator = target.GetComponent<Animator>();
            Transform baseRootBone = baseAnimator.GetBoneTransform(HumanBodyBones.Hips);

            baseRootBone.GetChildrenAndSelfList(out list);

        }

        private void CreateBindPoseList(IEnumerable<Transform> bones, Matrix4x4 localToWroldMatrix, out List<Matrix4x4> bindPoseList)
        {
            bindPoseList = new List<Matrix4x4>();
            foreach(var bone in bones)
            {
                var bindPose = bone.worldToLocalMatrix * localToWroldMatrix;
                bindPoseList.Add(bindPose);
            }
        }

        private void ExtrudeClothMesh(ref SkinnedMeshRenderer[] skinnedMeshes)
        {
            skinnedMeshes = skinnedMeshes.Where(item => item.gameObject.GetComponent<Cloth>() == null).ToArray();
        }

        private void CreateBoneIndexMatchDictionary(SkinnedMeshRenderer skinnedMesh, IEnumerable<Transform> boneList, out Dictionary<int, int> dic)
        {
            dic = new Dictionary<int, int>();
            for(int i = 0; i < skinnedMesh.bones.Length; ++i)
            {
                Transform srcBone = skinnedMesh.bones[i];
                if(srcBone == null)
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("メッシュ {0} の{1}番目のボーンの参照が切れています。ボーンアニメーションに影響がある可能性があります。", skinnedMesh.name, i));
                    continue;
                }
                var dstBone = boneList.Select((trans, index) => new { trans, index }).Where(e => string.Equals(e.trans.name, srcBone.name)).First();
                dic.Add(i, dstBone.index);
            }
        }

        private void UpdateMaterialList(SkinnedMeshRenderer skinnedMesh, ref List<Material> materialList, ref List<List<int>> subMeshList, out Dictionary<int, int> materialIndexMatchDictionary)
        {
            materialIndexMatchDictionary = new Dictionary<int, int>();

            for (int i = 0; i < skinnedMesh.sharedMaterials.Length; ++i)
            {
                Material srcMaterial = skinnedMesh.sharedMaterials[i];
                var targetMat = materialList.Select((mat, index) => new { mat, index }).Where(e => e.mat.Equals(srcMaterial));

                if (!targetMat.Any())
                {
                    // 同一マテリアルが存在しない場合
                    // マテリアルリストに追加
                    materialList.Add(srcMaterial);

                    // サブメッシュリストに新たなサブメッシュを作成する
                    subMeshList.Add(new List<int>());

                    // Srcマテリアルのインデックスと結合メッシュオブジェクトのマテリアルのインデックスの対応
                    materialIndexMatchDictionary.Add(i, materialList.Count() - 1);

                }
                else
                {
                    // 同一マテリアルが存在する場合
                    materialIndexMatchDictionary.Add(i, targetMat.First().index);
                }
            }
        }

        private void SetUVList(int channel, in Mesh srcMesh, bool[] uvExist, List<Vector2> outputUVsList)
        {
            Vector2[] uvList;
            switch (channel)
            {
                case 0:
                    uvList = srcMesh.uv;
                    break;
                case 1:
                    uvList = srcMesh.uv2;
                    break;
                case 2:
                    uvList = srcMesh.uv3;
                    break;
                case 3:
                    uvList = srcMesh.uv4;
                    break;
                case 4:
                    uvList = srcMesh.uv5;
                    break;
                case 5:
                    uvList = srcMesh.uv6;
                    break;
                case 6:
                    uvList = srcMesh.uv7;
                    break;
                case 7:
                    uvList = srcMesh.uv8;
                    break;
                default:
                    return;
            }

            if (uvExist[channel])
            {
                if (uvList.Length == srcMesh.vertexCount)
                {
                    foreach (var uv in uvList)
                    {
                        outputUVsList.Add(uv);
                    }
                }
                else
                {
                    for (int i = 0; i < srcMesh.vertexCount; ++i)
                    {
                        outputUVsList.Add(Vector2.zero);
                    }
                }
            }
        }
        
        private void CopyMesh(in int indexOffset, in Mesh srcMesh,in Mesh bakedMesh,ref List<Vector3> verticesList, ref List<Vector3> normalsList, ref List<Vector4> tangentsList, ref List<Color> colorList, ref List<Vector2>[] uvList, in bool colorExist, in bool[] uvExist)
        {

            foreach(var vertex in bakedMesh.vertices)
            {
                verticesList.Add(vertex);
            }

            foreach (var normal in bakedMesh.normals)
            {
                normalsList.Add(normal);
            }

            bakedMesh.RecalculateTangents();

            foreach (var tangent in bakedMesh.tangents)
            {
                tangentsList.Add(tangent);
            }

            if (colorExist)
            {
                if (bakedMesh.colors.Length == bakedMesh.vertexCount)
                {
                    foreach (var color in bakedMesh.colors)
                    {
                        colorList.Add(color);
                    }
                }
                else
                {
                    for (int i = 0; i < bakedMesh.vertexCount; i++)
                    {
                        colorList.Add(Color.white);
                    }
                }
            }

            // UVチャンネルのコピー
            for (int i = 0; i < UV_CHANNEL_NUM; i++)
            {
                SetUVList(i, srcMesh, uvExist, uvList[i]);
            }
        }

        private void CopyBlendShape(ref List<BlendShape> blendShapesList, in int indexOffset, in Mesh srcMesh,in Quaternion originalRotation, in Vector3 originalScale)
        {
            for (int i = 0; i < srcMesh.blendShapeCount; ++i)
            {
                int vertexNum = srcMesh.vertexCount;

                Vector3[] deltaVartices = new Vector3[vertexNum];
                Vector3[] deltaNormals = new Vector3[vertexNum];
                Vector3[] deltaTangets = new Vector3[vertexNum];

                srcMesh.GetBlendShapeFrameVertices(i, 0, deltaVartices, deltaNormals, deltaTangets);

                var RSmatrix = Matrix4x4.TRS(Vector3.zero, originalRotation, originalScale);

                // バインドポーズ適用時の回転、拡大縮小の補正
                for (int j = 0; j < deltaVartices.Length; j++)
                {
                    deltaVartices[j] = RSmatrix.MultiplyPoint3x4(deltaVartices[j]);
                    deltaNormals[j] = RSmatrix.MultiplyPoint3x4(deltaNormals[j]);
                    deltaTangets[j] = RSmatrix.MultiplyPoint3x4(deltaTangets[j]);
                }

                string blendShapeName = srcMesh.GetBlendShapeName(i);
                BlendShape blendShape = new BlendShape(blendShapeName, deltaVartices.ToList(), deltaNormals.ToList(), deltaTangets.ToList(), indexOffset);
                blendShapesList.Add(blendShape);
            }
        }

        private void CopyTriangleList(ref List<List<int>> subMeshList,in int indexOffset,in Mesh srcMesh, in Dictionary<int,int> materialIndexMatchDictionary)
        {
            for (int subMeshIndex = 0; subMeshIndex < srcMesh.subMeshCount; ++subMeshIndex)
            {
                var triangles = srcMesh.GetTriangles(subMeshIndex);
                foreach (var triangle in triangles)
                {
                    subMeshList[materialIndexMatchDictionary[subMeshIndex]].Add(triangle + indexOffset);
                }
            }
        }

        private void CopyBoneWeight(ref List<BoneWeight> boneWeightsList, in Mesh srcMesh, in Dictionary<int, int> boneIndexMatchDictionary)
        {
            foreach (var boneWeight in srcMesh.boneWeights)
            {
                BoneWeight weight = new BoneWeight();
                if(boneIndexMatchDictionary.ContainsKey(boneWeight.boneIndex0))
                {
                    weight.boneIndex0 = boneIndexMatchDictionary[boneWeight.boneIndex0];
                    weight.weight0 = boneWeight.weight0;
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, "存在しないボーンウェイトがありました。出力されるモデルはボーンウェイトが壊れた可能性があります。");
                    weight.boneIndex0 = 0;
                    weight.weight0 = 0;
                }

                if (boneIndexMatchDictionary.ContainsKey(boneWeight.boneIndex1))
                {
                    weight.boneIndex1 = boneIndexMatchDictionary[boneWeight.boneIndex1];
                    weight.weight1 = boneWeight.weight1;
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, "存在しないボーンウェイトがありました。出力されるモデルはボーンウェイトが壊れた可能性があります。");
                    weight.boneIndex1 = 0;
                    weight.weight1 = 0;
                }

                if (boneIndexMatchDictionary.ContainsKey(boneWeight.boneIndex2))
                {
                    weight.boneIndex2 = boneIndexMatchDictionary[boneWeight.boneIndex2];
                    weight.weight2 = boneWeight.weight2;
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, "存在しないボーンウェイトがありました。出力されるモデルはボーンウェイトが壊れた可能性があります。");
                    weight.boneIndex2 = 0;
                    weight.weight2 = 0;
                }
                if (boneIndexMatchDictionary.ContainsKey(boneWeight.boneIndex3))
                {
                    weight.boneIndex3 = boneIndexMatchDictionary[boneWeight.boneIndex3];
                    weight.weight3 = boneWeight.weight3;
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, "存在しないボーンウェイトがありました。出力されるモデルはボーンウェイトが壊れた可能性があります。");
                    weight.boneIndex3 = 0;
                    weight.weight3 = 0;
                }
                boneWeightsList.Add(weight);
            }
        }

        private GameObject GenerateNewSkinnedMeshObject(in Transform parent, in SkinMeshInfo skinMeshInfo, in List<Transform> boneList, in List<Matrix4x4> bindPoseList, in Transform rootBone)
        {
            GameObject generateMeshObject = new GameObject("Body");
            generateMeshObject.transform.parent = parent;
            generateMeshObject.transform.localPosition = Vector3.zero;

            SkinnedMeshRenderer combinedSkinMesh = generateMeshObject.AddComponent<SkinnedMeshRenderer>();
            Mesh combinedMesh = new Mesh();
            
            combinedSkinMesh.rootBone = rootBone;
            combinedMesh.RecalculateBounds(); // バウンズは再生成

            combinedMesh.SetVertices(skinMeshInfo.verticesList);
            combinedMesh.SetNormals(skinMeshInfo.normalsList);
            combinedMesh.SetTangents(skinMeshInfo.tangentsList);
            if (skinMeshInfo.colorExist)
            {
                combinedMesh.SetColors(skinMeshInfo.colorList);
            }
            for (int i = 0; i < UV_CHANNEL_NUM; i++)
            {
                combinedMesh.SetUVs(i, skinMeshInfo.uvList[i]);
            }

            string subMeshInfo = "";
            subMeshInfo += string.Format("sub mesh counts : {0}\n", skinMeshInfo.subMeshList.Count());

            combinedMesh.subMeshCount = skinMeshInfo.subMeshList.Count();
            for (int i = 0; i < skinMeshInfo.subMeshList.Count(); ++i)
            {
                combinedMesh.SetTriangles(skinMeshInfo.subMeshList[i].ToArray(), i);
                subMeshInfo += string.Format("submesh[{0}] : {1} vertices \n", i, skinMeshInfo.subMeshList[i].Count());
            }

            Debug.Log(subMeshInfo);

            // ブレンドシェイプの設定
            foreach (var blendShape in skinMeshInfo.blendShapesList)
            {
                int offset = blendShape.vertexOffset;
                int vertexSize = blendShape.deltaVerticies.Count();

                List<Vector3> deltaVerticies = new List<Vector3>();
                List<Vector3> deltaNormals = new List<Vector3>();
                List<Vector3> deltaTangents = new List<Vector3>();

                for (int i = 0; i < skinMeshInfo.verticesList.Count(); ++i)
                {
                    if (i >= offset && i < offset + vertexSize)
                    {
                        deltaVerticies.Add(blendShape.deltaVerticies[i - offset]);
                        deltaNormals.Add(blendShape.deltaNormals[i - offset]);
                        deltaTangents.Add(blendShape.deltaTangents[i - offset]);
                    }
                    else
                    {   // 動かないメッシュは0指定
                        deltaVerticies.Add(Vector3.zero);
                        deltaNormals.Add(Vector3.zero);
                        deltaTangents.Add(Vector3.zero);
                    }
                }

                combinedMesh.AddBlendShapeFrame(blendShape.name, 100, deltaVerticies.ToArray(), deltaNormals.ToArray(), deltaTangents.ToArray());

            }

            combinedMesh.boneWeights = skinMeshInfo.boneWeightsList.ToArray();
            combinedMesh.bindposes = bindPoseList.ToArray();
            combinedSkinMesh.sharedMesh = combinedMesh;
            combinedSkinMesh.sharedMaterials = skinMeshInfo.materialList.ToArray();
            combinedSkinMesh.bones = boneList.ToArray();

            return generateMeshObject;
        }

        private void RemapVRCVisemeSkinMesh(in GameObject vrcObject,in SkinnedMeshRenderer meshRenderer)
        {
            
            if(KurotoriUtility.GetTypeByClassNameWithNamespace("VRCSDK2","VRC_AvatarDescriptor") != null)
            {
                // SDK2
                KurotoriUtility.OutputLog(LogType.LOG, "VRCSDK2 を認識しました");
                var type = KurotoriUtility.GetTypeByClassNameWithNamespace("VRCSDK2","VRC_AvatarDescriptor");
                
                var avatarDescriptor = vrcObject.GetComponent(type);

                if(avatarDescriptor != null)
                    KurotoriUtility.SetFieldValueByName(avatarDescriptor, "VisemeSkinnedMesh", meshRenderer,type);
            }

            if (KurotoriUtility.GetTypeByClassName("VRCAvatarDescriptor") != null)
            {
                // SDK3
                KurotoriUtility.OutputLog(LogType.WARNING, "VRCSDK3 を認識しました。SDK3への対応はまだβです。");
                var type = KurotoriUtility.GetTypeByClassName("VRCAvatarDescriptor");

                var avatarDescriptor = vrcObject.GetComponent(type);

                if(avatarDescriptor != null)
                    KurotoriUtility.SetFieldValueByName(avatarDescriptor, "VisemeSkinnedMesh", meshRenderer, type);
            }


        }

        private void SaveMeshAsset(in string saveMeshFolder, in string filename, in Mesh mesh)
        {
            var savePath = Path.Combine("Assets", saveMeshFolder);
            if (!AssetDatabase.IsValidFolder(savePath))
            {
                AssetDatabase.CreateFolder("Assets", saveMeshFolder);
            }

            AssetDatabase.CreateAsset(mesh, Path.Combine(savePath, filename + ".asset"));
            AssetDatabase.SaveAssets();

            KurotoriUtility.OutputLog(LogType.LOG, string.Format("{0}をファイル出力しました。", filename + ".asset"));
        }
    }
}