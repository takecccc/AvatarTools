using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;

namespace KurotoriTools
{
    using BonePathList = Dictionary<string, Transform>;

    /// <summary>
    /// BaseAvatarとAvatarPartsを結合しAssembledAvatarを生成するクラス
    /// </summary>
    public class AvatarAssemblerCore
    {
        private GameObject baseAvatar;
        private List<GameObject> avatarParts;

        private List<IComponentRemapper> preRemapperList;
        private List<IComponentRemapper> remapperList;
        
        static private Vector3 SPAWN_POINT = new Vector3(2.0f, 0.0f, 0.0f);

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="_baseAvatar">ベースとなるアバター</param>
        /// <param name="_avatarParts">パーツとなるアバターのリスト</param>
        public AvatarAssemblerCore(GameObject _baseAvatar, List<GameObject> _avatarParts)
        {
            baseAvatar = _baseAvatar;
            avatarParts = _avatarParts;

            preRemapperList = new List<IComponentRemapper>();
            remapperList = new List<IComponentRemapper>();
        }

        /// <summary>
        /// 結合可能かチェックする
        /// </summary>
        public bool CheckAssembleable()
        {
            KurotoriUtility.OutputLog(LogType.LOG, "アセットチェック開始");
            if (KurotoriUtility.GetTypeByClassName("DynamicBone") == null)
            {
                KurotoriUtility.OutputLog(LogType.WARNING, "DynamicBone コンポーネントは存在しません。DynamicBoneを使用する場合はインポートしてください。");
            }
            if (KurotoriUtility.GetTypeByClassName("DynamicBoneColliderBase") == null)
            {
                KurotoriUtility.OutputLog(LogType.WARNING, "DynamicBoneColliderBase コンポーネントは存在しません。DynamicBoneを使用する場合はインポートしてください。");
            }
            if (KurotoriUtility.GetTypeByClassName("DynamicBoneCollider") == null)
            {
                KurotoriUtility.OutputLog(LogType.WARNING, "DynamicBoneCollider コンポーネントは存在しません。DynamicBoneを使用する場合はインポートしてください。");
            }
            KurotoriUtility.OutputLog(LogType.LOG, "アセットチェック完了");


            KurotoriUtility.OutputLog(LogType.LOG, "データチェック開始");
            KurotoriUtility.OutputLog(LogType.LOG, "ベースモデルチェック開始");
            if (!CheckBaseAvatar()) return false;
            
            KurotoriUtility.OutputLog(LogType.LOG, "ベースモデルチェック完了");
            var baseRootBone = GetRootBone(baseAvatar);

            KurotoriUtility.OutputLog(LogType.LOG, "結合モデルチェック開始");
            if (!CheckAvatarParts(baseRootBone)) return false;
            KurotoriUtility.OutputLog(LogType.LOG, "結合モデルチェック完了");

            return true;
        }

        private Transform GetRootBone(GameObject gameObject)
        {
            var animator = gameObject.GetComponent<Animator>();
            var rootBone = animator.GetBoneTransform(HumanBodyBones.Hips);

            return rootBone;
        }

        /// <summary>
        /// BaseAvatarの整合性チェック
        /// </summary>
        /// <returns></returns>
        private bool CheckBaseAvatar()
        {
            var baseAvatarAnimator = baseAvatar.GetComponent<Animator>();
            if(!baseAvatarAnimator)
            {
                KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0}はモデルデータではありません。", baseAvatar.name));
                return false;
            }

            var avatar = baseAvatarAnimator.avatar;
            if(avatar == null || !avatar.isHuman || !avatar.isValid)
            {
                KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0}はHumanoidAvatarではありません。", baseAvatar.name));
                return false;
            }

            if(!baseAvatar.HasComponentInChildren<SkinnedMeshRenderer>())
            {
                KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0}はSkinnedMeshRendererがありません。", baseAvatar.name));
                return false;
            }

            var baseRootBone = baseAvatarAnimator.GetBoneTransform(HumanBodyBones.Hips);
            
            if(CheckSamePathBoneExist(baseRootBone))
            {
                KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0}は同一階層同一名のオブジェクトが含まれています", baseAvatar.name));
                return false;
            }

            return true;
        }

        /// <summary>
        /// AvatarPartsの整合性チェック
        /// </summary>
        /// <param name="baseRootBone">BaseAvatarのRootBone</param>
        /// <returns></returns>
        private bool CheckAvatarParts(Transform baseRootBone)
        {
            BonePathList baseBonePathList;
            KurotoriUtility.CreateBonePathList(baseRootBone, out baseBonePathList);

            foreach(var parts in avatarParts)
            {
                if (!parts.HasComponent<Animator>())
                {
                    KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0}はモデルデータではありません。", parts.name));
                    return false;
                }

                if (!parts.HasComponentInChildren<SkinnedMeshRenderer>())
                {
                    KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0}にSkinnedMeshRendererが存在しません。", parts.name));
                    return false;
                }
                var partsRootBone = KurotoriUtility.FindSameNameObjectInChildren(parts.transform, baseRootBone.name);

                if(!partsRootBone)
                {
                    KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0} に {1}が見つかりませんでした。{0} はBaseBoneと互換がありません。", parts.name, baseRootBone.name));
                    return false;
                }

                BonePathList partsPathList;
                if(!KurotoriUtility.CreateBonePathList(partsRootBone, out partsPathList))
                {
                    KurotoriUtility.OutputLog(LogType.ERROR, string.Format("{0} には同じ階層、同じ名前のオブジェクトが複数含まれています。", parts.name));
                    return false;
                }

                // localTransformのチェック
                {
                    //if(!IsSameLocalTransfrom(partsRootBone.parent, baseRootBone.parent))
                    //{
                    //    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("{0} のボーン {1} はlocalTransformが異なるため、正しく結合されない可能性があります。", parts.name, partsRootBone.parent.name));
                    //}

                    CheckBoneTransformIsSame(baseRootBone.parent, partsRootBone.parent, parts.name);

                    //CheckSameBoneTransform(baseBonePathList, partsPathList, parts.name);
                }

                // コンポーネントチェック
                {
                    CheckComponentRemappable(partsPathList, parts.name);
                }

                {
                    List<Transform> childrenList;
                    parts.transform.GetChildrenAndSelfList(out childrenList);

                    int i = 0;
                    foreach (Transform child in childrenList)
                    {
                        // 結合対象でないオブジェクトの場合
                        if (IsNonAssembleTarget(child, partsRootBone))
                        {
                            var components = child.GetComponents<Component>();
                            foreach (var component in components)
                            {
                                if (component.GetType() != typeof(Transform))
                                {
                                    if (i == 0 && component.GetType() == typeof(Animator)) continue;
                                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("{0} の {1} に {2} コンポーネントが設定されていますが、結合後には反映されません。コンポーネントは{3}以下に設定してください。", parts.name, child.name, component.GetType().Name, partsRootBone.name));
                                    
                                }
                            }
                        }
                    }
                }

            }

            return true;
        }
        
        /// <summary>
        /// 対象となるボーンのローカル姿勢が同じかチェック
        /// </summary>
        /// <param name="baseModel"></param>
        /// <param name="partsModel"></param>
        /// <returns></returns>
        private bool CheckSameBoneTransform(BonePathList baseModel, BonePathList partsModel, string partsName)
        {
            bool isSame = true;
            foreach(var bone in baseModel)
            {
                var baseBone = bone.Value;
                var baseBonePath = bone.Key;

                Transform partsBone;
                if (partsModel.TryGetValue(baseBonePath, out partsBone))
                {
                    //if (!IsSameLocalTransfrom(baseBone, partsBone))
                    //{
                    //    // サイズ差チェック
                    //    var sXRate = partsBone.localScale.x / baseBone.localScale.x;
                    //    var sYRate = partsBone.localScale.y / baseBone.localScale.y;
                    //    var sZRate = partsBone.localScale.z / baseBone.localScale.z;

                    //    // 位置差チェック
                    //    var pDiff = (partsBone.localPosition - baseBone.localPosition).magnitude;

                    //    // 回転差チェック
                    //    var rDiff = Quaternion.Angle(partsBone.localRotation, baseBone.localRotation);

                    //    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("{0} のボーン {1} はlocalTransformが異なるため、正しく結合されない可能性があります。\n スケール差 : ({2},{3},{4}) \n 位置差 : {5} \n 回転差 : {6} °"
                    //        , partsName, baseBone.name, sXRate, sYRate, sZRate, pDiff, rDiff));
                    //    isSame = false;
                    //}
                    isSame = CheckBoneTransformIsSame(baseBone, partsBone, partsName);
                }
            }

            return isSame;
        }

        /// <summary>
        /// 対象となるボーンのローカル姿勢が同じかチェック
        /// </summary>
        /// <param name="baseModel"></param>
        /// <param name="partsModel"></param>
        /// <returns></returns>
        private bool CheckBoneTransformIsSame(Transform baseBone, Transform partsBone, string partsName)
        {
            if (!IsSameLocalTransfrom(baseBone, partsBone))
            {
                // サイズ差チェック
                var sXRate = Mathf.Abs(partsBone.localScale.x - baseBone.localScale.x);
                var sYRate = Mathf.Abs(partsBone.localScale.y - baseBone.localScale.y);
                var sZRate = Mathf.Abs(partsBone.localScale.z - baseBone.localScale.z);

                // 位置差チェック
                var pDiff = (partsBone.localPosition - baseBone.localPosition).magnitude;

                // 回転差チェック
                var rDiff = Quaternion.Angle(partsBone.localRotation, baseBone.localRotation);

                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("{0} のボーン {1} はlocalTransformが異なるため、正しく結合されない可能性があります。\n スケール差 : ({2:0.000},{3:0.000},{4:0.000}) \n 位置差 : {5:0.000} \n 回転差 : {6:0.000} °"
                    , partsName, baseBone.name, sXRate, sYRate, sZRate, pDiff, rDiff));
                return false;
            }

            return true;
        }

        /// <summary>
        /// 再マップをサポートしているコンポーネントかチェック
        /// </summary>
        /// <param name="partsModel"></param>
        /// <param name="partsName"></param>
        /// <returns></returns>
        private bool CheckComponentRemappable(BonePathList partsModel, string partsName)
        {
            bool isRemappable = true;
            foreach (var bone in partsModel)
            {
                var parts = bone.Value.gameObject;

                foreach (var component in parts.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        isRemappable = false;
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("{0} の {1} で Missing Script があります。結合後は手動で削除してください。", partsName, parts.name));
                    }
                    else if (!IsRemappableComponent(component))
                    {
                        isRemappable = false;

                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("{0} の {1} にアタッチされているコンポーネント {2} は本ツールではサポートされていません。依存関係が正しくなくなる可能性があります。", partsName, parts.name, component.GetType()));
                    }
                }

            }

            return isRemappable;
        }

        private bool IsSameLocalTransfrom(Transform a, Transform b)
        {
            return (
                a.localPosition == b.localPosition &&
                a.localRotation == b.localRotation &&
                a.localScale == b.localScale);
        }

        private bool IsNonAssembleTarget(Transform target, Transform rootBone)
        {
            return
                !target.IsSelfOrChildOf(rootBone) &&                    // ボーン結合対象ではない階層のオブジェクトかつ
                !target.gameObject.HasComponent<SkinnedMeshRenderer>(); // SkinnedMeshRendererを持っていない。
        }

        /// <summary>
        /// AssembledAvatarを生成する。
        /// </summary>
        /// <param name="name">生成されるAssmebledAvatarの名前</param>
        public void CreateAssembledAvatar(string name)
        {
            if (!CheckAssembleable()) return;

            KurotoriUtility.OutputLog(LogType.LOG, "モデル結合開始");

            GameObject assembledAvatar = Object.Instantiate(baseAvatar, SPAWN_POINT, Quaternion.identity);
            assembledAvatar.name = name;

            KurotoriUtility.OutputLog(LogType.LOG, "モデルを複製");
            
            CopyAvatarParts(assembledAvatar);
        }

        private string GetParentPath(KeyValuePair<string, Transform> kvp)
        {
            return kvp.Key.Substring(0, kvp.Key.Length - (kvp.Value.name.Length + 1));
        }

        private void AddComponentRemapper(GameObject target, GameObject partsBone, BonePathInfo partsInfo)
        {
            foreach (var component in partsBone.GetComponents<Component>())
            {
                var remapper = ComponentRemapperFactory.CreateComponentRemapper(component, target, partsInfo);

                if (remapper != null)
                {
                    if (remapper.GetType() == typeof(DynamicBoneColliderRemapper))
                    {
                        //DynamicBoneColliderのみ先にマッピングを行うようにする（参照切れ防止）
                        preRemapperList.Add(remapper);
                    }
                    else
                    {
                        remapperList.Add(remapper);
                    }
                }
            }
        }

        /// <summary>
        /// このツールで再マップをサポートしているコンポーネントか
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        private bool IsRemappableComponent(Component component)
        {
            if (component == null) return false;

            var type = component.GetType();

            bool isRemappable =
                type == typeof(AimConstraint) ||
                type == typeof(LookAtConstraint) ||
                type == typeof(ParentConstraint) ||
                type == typeof(PositionConstraint) ||
                type == typeof(RotationConstraint) ||
                type == typeof(ScaleConstraint) ||
                type == typeof(BoxCollider) ||
                type == typeof(SphereCollider) ||
                type == typeof(CapsuleCollider) ||
                type == typeof(MeshCollider) ||
                type == typeof(Transform) ||
                type.Name.Equals("DynamicBone") ||
                type.Name.Equals("DynamicBoneCollider");

            return isRemappable;
        }

        private void DestroyReGenerationComponents(GameObject gameObject)
        {
            foreach(var component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                
                if (
                    type == typeof(AimConstraint) ||
                    type == typeof(LookAtConstraint) ||
                    type == typeof(ParentConstraintRemapper) ||
                    type == typeof(PositionConstraintRemapper) ||
                    type == typeof(RotationConstraintRemapper) ||
                    type == typeof(ScaleConstraintRemapper) ||
                    type.Name.Equals("DynamicBone") ||
                    type.Name.Equals("DynamicBoneColliderBase")
                    )
                {
                    GameObject.DestroyImmediate(component);
                }
            }
        }

        /// <summary>
        /// AvatarPartsをAssembledAvatarにコピーし、ComponentRemapperを追加する。
        /// </summary>
        /// <param name="assembledPathList"></param>
        /// <param name="assembledRootBone"></param>
        private void CopyAvatarParts(GameObject assembledAvatar)
        {
            var assembledRootBone = GetRootBone(assembledAvatar);

            // BonePathListの生成
            BonePathList assembledPathList;
            KurotoriUtility.CreateBonePathList(assembledRootBone, out assembledPathList);

            BonePathInfo assembledInfo;
            assembledInfo.rootBone = assembledRootBone;
            assembledInfo.pathList = assembledPathList;
            
            foreach (var parts in avatarParts)
            {
                KurotoriUtility.OutputLog(LogType.LOG, string.Format("{0}の結合開始", parts.name));

                var partsRootBone = KurotoriUtility.FindSameNameObjectInChildren(parts.transform, assembledRootBone.name);

                BonePathList partsBonePathList;
                KurotoriUtility.CreateBonePathList(partsRootBone, out partsBonePathList);

                BonePathInfo partsInfo;
                partsInfo.rootBone = partsRootBone;
                partsInfo.pathList = partsBonePathList;

                foreach (KeyValuePair<string, Transform> kvp in partsBonePathList)
                {
                    string partsBonePath = kvp.Key;
                    Transform partsBone = kvp.Value;

                    KurotoriUtility.OutputLog(LogType.LOG, string.Format("ボーンの結合:{0}", partsBonePath));

                    Transform sameBone;
                    bool existSameBone = assembledPathList.TryGetValue(partsBonePath, out sameBone);
                    if (existSameBone)
                    {
                        
                        AddComponentRemapper(sameBone.gameObject, partsBone.gameObject, partsInfo);
                    }
                    else
                    {
                        var parentPath = GetParentPath(kvp);
                        Transform targetParent;
                        bool existParent = assembledPathList.TryGetValue(parentPath, out targetParent);

                        if(existParent)
                        {
                            // オブジェクトを追加する
                            var boneObject = Object.Instantiate(partsBone.gameObject, targetParent);
                            boneObject.name = partsBone.name;

                            KurotoriUtility.DestroyChildren(boneObject);
                            DestroyReGenerationComponents(boneObject);

                            // ローカルトランスフォームをそろえる
                            boneObject.transform.localPosition = partsBone.localPosition;
                            boneObject.transform.localRotation = partsBone.localRotation;
                            boneObject.transform.localScale = partsBone.localScale;

                            // 結合モデルのBonePathListに追加
                            assembledPathList.Add(partsBonePath, boneObject.transform);

                            AddComponentRemapper(boneObject, partsBone.gameObject, partsInfo);
                        }
                        else
                        {
                            // 親が存在しなかった場合
                            KurotoriUtility.OutputLog(LogType.ERROR, string.Format("\"{0}\"の\"{1}\"ボーンの親\"{2}\"はベースオブジェクトに存在しません。同じ階層のオブジェクトを結合してください。", partsBone.name, partsBone.name, parentPath));
                        }
                    }
                }

                KurotoriUtility.OutputLog(LogType.LOG, "コンポーネントの再マップ開始");

                ComponentRemapping(assembledPathList, assembledRootBone);

                ClearRemapper();

                List<SkinnedMeshRenderer> newAssembledMeshList;
                CopyMesh(assembledAvatar, parts, assembledInfo,partsInfo, out newAssembledMeshList);

                // Anchor Overideの書き換え
                AnchorOverrideRemapping(assembledAvatar, newAssembledMeshList);

                KurotoriUtility.OutputLog(LogType.LOG, string.Format("{0}の結合完了", parts.name));
            }
        }

        /// <summary>
        /// コンポーネントの再設定
        /// </summary>
        /// <param name="assmebledPathList"></param>
        /// <param name="rootBone"></param>
        private void ComponentRemapping(BonePathList assmebledPathList, Transform rootBone)
        {
            BonePathInfo info;

            info.rootBone = rootBone;
            info.pathList = assmebledPathList;

            foreach(var preRemapper in preRemapperList)
            {
                preRemapper.Remap(info);
            }

            foreach (var remapper in remapperList)
            {
                remapper.Remap(info);
            }
        }

        private void ClearRemapper()
        {
            preRemapperList.Clear();
            remapperList.Clear();
        }

        private void ClothComponentRemapping(Cloth cloth, BonePathList dic, Transform targetBaseRoot)
        {
            {   // Cupsule Colliders の参照書き換え
                var capusuleColliders = cloth.capsuleColliders;
                List<CapsuleCollider> newList = new List<CapsuleCollider>();

                foreach (var capusuleCollider in capusuleColliders)
                {
                    if (capusuleCollider == null) continue;

                    var capusuleColliderPath = KurotoriUtility.GetBonePath(targetBaseRoot, capusuleCollider.transform);
                    Transform sameObject;
                    bool existSameObject = dic.TryGetValue(capusuleColliderPath, out sameObject);

                    if (existSameObject)
                    {
                        newList.Add(sameObject.GetComponent<CapsuleCollider>());
                    }
                }

                cloth.capsuleColliders = newList.ToArray();
            }

            {   // Sphere Colliders の参照書き換え
                var clothSphereColliderPairs = cloth.sphereColliders;

                List<ClothSphereColliderPair> newSphereColliderList = new List<ClothSphereColliderPair>();

                foreach (var clothSphereColliderPair in clothSphereColliderPairs)
                {
                    var newClothSphereColliderPair = new ClothSphereColliderPair();

                    var first = clothSphereColliderPair.first;
                    if (first != null)
                    {
                        var sphereColliderPath = KurotoriUtility.GetBonePath(targetBaseRoot, first.transform);
                        Transform sameObject;
                        bool existSameObject = dic.TryGetValue(sphereColliderPath, out sameObject);

                        if (existSameObject)
                        {
                            newClothSphereColliderPair.first = sameObject.GetComponent<SphereCollider>();
                        }
                    }

                    var second = clothSphereColliderPair.second;
                    if (second != null)
                    {
                        var sphereColliderPath = KurotoriUtility.GetBonePath(targetBaseRoot, second.transform);
                        Transform sameObject;
                        bool existSameObject = dic.TryGetValue(sphereColliderPath, out sameObject);

                        if (existSameObject)
                        {
                            newClothSphereColliderPair.second = sameObject.GetComponent<SphereCollider>();
                        }
                    }

                    newSphereColliderList.Add(newClothSphereColliderPair);
                }

                cloth.sphereColliders = newSphereColliderList.ToArray();
            }
        }

        private void CopyMesh(GameObject assembledObject, GameObject parts, BonePathInfo assembledInfo, BonePathInfo partsInfo, out List<SkinnedMeshRenderer> newAssembledMeshList)
        {
            KurotoriUtility.OutputLog(LogType.LOG, "メッシュデータの結合開始");

            var partsMeshs = parts.GetComponentsInChildren<SkinnedMeshRenderer>();

            newAssembledMeshList = new List<SkinnedMeshRenderer>();

            foreach(var mesh in partsMeshs)
            {
                KurotoriUtility.OutputLog(LogType.LOG, string.Format("メッシュ{0}の結合開始", mesh.name));

                var assembledMeshObject = GameObject.Instantiate(mesh.gameObject);
                assembledMeshObject.name = mesh.gameObject.name;
                assembledMeshObject.transform.SetParent(assembledObject.transform);

                assembledMeshObject.transform.localPosition = mesh.gameObject.transform.localPosition;
                assembledMeshObject.transform.localRotation = mesh.gameObject.transform.localRotation;
                assembledMeshObject.transform.localScale    = mesh.gameObject.transform.localScale;

                var assembledSkinMesh = assembledMeshObject.GetComponent<SkinnedMeshRenderer>();
                assembledSkinMesh.sharedMesh = mesh.sharedMesh;

                // UpdateBoneList
                {
                    int i = 0;
                    List<Transform> bones = new List<Transform>();

                    foreach(var bone in assembledSkinMesh.bones)
                    {
                        var targetPath = KurotoriUtility.GetBonePath(partsInfo.rootBone, bone);

                        Transform targetBone;
                        bool existTargetBone = assembledInfo.pathList.TryGetValue(targetPath, out targetBone);

                        if(!existTargetBone)
                        {
                            KurotoriUtility.OutputLog(LogType.ERROR, string.Format("結合オブジェクト {0} のメッシュ {1} に不正なボーン {2} が含まれています。結合モデルの生成に失敗しました。", assembledObject.name, assembledSkinMesh.name, bone.name));
                            return;
                        }

                        bones.Add(targetBone);
                        ++i;
                    }

                    assembledSkinMesh.bones = bones.ToArray();
                }

                // ルートボーンの参照切り替え
                if(mesh.rootBone != null)
                {
                    var meshRootBonePath = KurotoriUtility.GetBonePath(partsInfo.rootBone, mesh.rootBone);
                    Transform newRootBone;
                    bool existRootBone = assembledInfo.pathList.TryGetValue(meshRootBonePath, out newRootBone);

                    if (existRootBone)
                    {
                        assembledSkinMesh.rootBone = newRootBone;
                    }
                }

                // Clothコンポーネントの参照書き換え
                var cloth = assembledMeshObject.GetComponent<Cloth>();
                if(cloth)
                {
                    ClothComponentRemapping(cloth, assembledInfo.pathList, partsInfo.rootBone);
                }

                newAssembledMeshList.Add(assembledSkinMesh);
            }
        }

        private void AnchorOverrideRemapping(GameObject assembledObject, List<SkinnedMeshRenderer> newAssembledMeshList)
        {
            BonePathList pathList;
            KurotoriUtility.CreateBonePathList(assembledObject.transform, out pathList);

            foreach(var mesh in newAssembledMeshList)
            {
                var anchor = mesh.probeAnchor;

                if (anchor == null) continue;


                var targetPath = KurotoriUtility.GetBonePath(assembledObject.transform, anchor);
                Transform targetBone;
                bool existTargetBone = pathList.TryGetValue(targetPath, out targetBone);
                if(!existTargetBone)
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("メッシュ:{0} のprobeAncher: {1}が見つかりませんでした。見た目が正しくない可能性があります。", mesh.name, targetPath));
                }

                mesh.probeAnchor = targetBone;
            }
        }

        /// <summary>
        /// 同じ階層構造になるオブジェクトが存在するかチェックする
        /// </summary>
        /// <param name="srcObject"></param>
        /// <returns></returns>
        private bool CheckSamePathBoneExist(Transform srcObject)
        {
            BonePathList bonePathList;
            return !(KurotoriUtility.CreateBonePathList(srcObject, out bonePathList));
        }

    }
}