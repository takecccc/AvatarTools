using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

#if VRC_SDK_VRCSDK3

using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDK3.Dynamics.Contact.Components;

#endif

namespace KurotoriTools
{
    using BonePathList = Dictionary<string, Transform>;

    public struct BonePathInfo
    {
        public BonePathList pathList;
        public Transform rootBone;
    };

    public interface IComponentRemapper
    {

        void Remap(BonePathInfo assembledPathInfo);
    }

    public static class ComponentRemapperFactory
    {
        public static IComponentRemapper CreateComponentRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            if (component == null) return null;

            var type = component.GetType();
            
            if (type == typeof(AimConstraint))
            {
                return new AimConstraintReMapper(component, target, partsPathInfo);
            }
            if (type == typeof(LookAtConstraint))
            {
                return new LookAtConstraintReMapper(component, target, partsPathInfo);
            }
            if (type == typeof(ParentConstraint))
            {
                return new ParentConstraintRemapper(component, target, partsPathInfo);
            }
            if (type == typeof(PositionConstraint))
            {
                return new PositionConstraintRemapper(component, target, partsPathInfo);
            }
            if (type == typeof(RotationConstraint))
            {
                return new RotationConstraintRemapper(component, target, partsPathInfo);
            }
            if (type == typeof(ScaleConstraint))
            {
                return new ScaleConstraintRemapper(component, target, partsPathInfo);
            }

            if (type.Name.Equals("DynamicBone"))
            {
                return new DynamicBoneRemapper(component, target, partsPathInfo);
            }

            if (type.Name.Equals("DynamicBoneColliderBase"))
            {
                return new DynamicBoneColliderRemapper(component, target,partsPathInfo);
            }

#if VRC_SDK_VRCSDK3
            if(type == typeof(VRCPhysBone))
            {
                return new PhysBoneRemapper(component, target, partsPathInfo);
            }

            if(type == typeof(VRCPhysBoneCollider))
            {
                return new PhysBoneColliderRemapper(component, target, partsPathInfo);
            }

            if(type == typeof(VRCContactReceiver))
            {
                return new ContactReceiverRemapper(component, target, partsPathInfo);
            }

            if(type == typeof(VRCContactSender))
            {
                return new ContactSenderRemapper(component, target, partsPathInfo);
            }
#endif

            return null;
        }
    }

    public class DynamicBoneRemapper : IComponentRemapper
    {
        private Component component;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public DynamicBoneRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            this.component = component;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        

        public void Remap(BonePathInfo assembledPathInfo)
        {
            Type dynamicBoneType = KurotoriUtility.GetTypeByClassName("DynamicBone");
            Component assembledComponent = target.AddComponent(dynamicBoneType);

            KurotoriUtility.OutputLog(LogType.LOG, string.Format("DynamicBone:{0}のリマップ開始。", assembledComponent.gameObject.name));

            {   // ルートのマッピング
                KurotoriUtility.OutputLog(LogType.LOG, string.Format("Rootのリマップ開始。"));

                Transform root = (Transform)KurotoriUtility.GetFieldValueByName(component, "m_Root", dynamicBoneType);

                if (root == null)
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("DynamicBone:{0}のRootがnullです。", component.gameObject.name));
                }
                else
                {
                    var rootBonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, root);

                    if (string.IsNullOrEmpty(rootBonePath))
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("DynamicBone:{0}のRoot:{1}はベースアバター内に存在しません。", component.gameObject.name, root.name));
                    }
                    else
                    {
                        Transform newRoot;
                        if (assembledPathInfo.pathList.TryGetValue(rootBonePath, out newRoot))
                        {
                            KurotoriUtility.SetFieldValueByName(assembledComponent, "m_Root", newRoot, dynamicBoneType);
                        }
                        else
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("DynamicBone:{0}のRoot:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, rootBonePath));
                        }
                    }
                }
            }

            {   // コライダーのマッピング

                KurotoriUtility.OutputLog(LogType.LOG, string.Format("コライダーのリマップ開始。"));

                var colliders = KurotoriUtility.GetFieldValueByName(component, "m_Colliders", dynamicBoneType);
                var colliderList = KurotoriUtility.GetListPropertyByName(colliders);
                
                var colliderTransformList = new List<Transform>();

                foreach (var collider in colliderList)
                {
                    Type DBColliderType = collider.GetType();
                    var transform = (Transform)DBColliderType.GetProperty("transform").GetValue(collider);

                    var colliderBonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, transform);
                    Transform newColliderTransform;
                    if (assembledPathInfo.pathList.TryGetValue(colliderBonePath, out newColliderTransform))
                    {
                        colliderTransformList.Add(newColliderTransform);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ダイナミックボーン:{0}に含まれるコライダー:{1}は結合モデル内に存在しません。コライダーは結合モデル内のものを参照してください。", assembledComponent.gameObject.name, transform.name));
                    }
                }

                var dynamicBoneColliderType = KurotoriUtility.GetTypeByClassName("DynamicBoneColliderBase");
                if (dynamicBoneColliderType != null)
                {
                    Type openedType = typeof(List<>);
                    Type closedType = openedType.MakeGenericType(dynamicBoneColliderType);
                    object newDBColliderList = Activator.CreateInstance(closedType);
                    Type dbColliderListType = newDBColliderList.GetType();
                    var addMethod = dbColliderListType.GetMethod("Add");

                    foreach (var collider in colliderTransformList)
                    {
                        var colliderComponent = collider.gameObject.GetComponent(dynamicBoneColliderType);
                        addMethod.Invoke(newDBColliderList, new object[] { colliderComponent });
                    }

                    KurotoriUtility.SetFieldValueByName(assembledComponent, "m_Colliders", newDBColliderList, dynamicBoneType);
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.ERROR, "DynamicBoneColliderのリマップに失敗しました。最新のDynamicBoneを正しくインポートできていない可能性があります。");
                }
            }

            {   // 除外オブジェクトのマッピング
                KurotoriUtility.OutputLog(LogType.LOG, string.Format("除外オブジェクトのリマップ開始。"));

                var exclusions = KurotoriUtility.GetFieldValueByName(component, "m_Exclusions", dynamicBoneType);
                var exclusionsList = KurotoriUtility.GetListPropertyByName(exclusions);

                var newExclusionsList = new List<Transform>();

                foreach (var exclusion in exclusionsList)
                {
                    Transform exclusionTransform = exclusion as Transform;
                    
                    if(exclusionTransform != null)
                    {
                        var exclusionPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, (Transform)exclusion);
                        Transform newExclusionTransform;
                        if(assembledPathInfo.pathList.TryGetValue(exclusionPath, out newExclusionTransform))
                        {
                            newExclusionsList.Add(newExclusionTransform);
                        }
                        else
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("Dynamic Bone:{0} のExclusion、{1}が含まれていませんでした。", component.gameObject.name, exclusionPath));
                        }
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("Dynamic Bone:{0} のExclusionにNoneが含まれています。", component.gameObject.name));
                    }
                }

                KurotoriUtility.SetFieldValueByName(assembledComponent, "m_Exclusions", newExclusionsList,dynamicBoneType);
            }

            KurotoriUtility.OutputLog(LogType.LOG, string.Format("その他のパラメーターのコピー"));

            KurotoriUtility.CopyFieldValue("m_Damping",             component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_DampingDistrib",      component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_DistanceToObject",    component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_DistantDisable",      component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_Elasticity",          component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_ElasticityDistrib",   component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_EndLength",           component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_EndOffset",           component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_Force",               component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_FreezeAxis",          component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_Gravity",             component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_Inert",               component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_InertDistrib",        component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_Radius",              component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_RadiusDistrib",       component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_Stiffness",           component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_StiffnessDistrib",    component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_UpdateMode",          component, assembledComponent, dynamicBoneType);
            KurotoriUtility.CopyFieldValue("m_UpdateRate",          component, assembledComponent, dynamicBoneType);
        }
    }
    
    public class DynamicBoneColliderRemapper : IComponentRemapper
    {
        private Component component;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public DynamicBoneColliderRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            this.component = component;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("DynamicBoneCollider:{0}のリマップ開始", component.gameObject.name));

            Type dynamicBoneColliderType = KurotoriUtility.GetTypeByClassName("DynamicBoneColliderBase");
            if(dynamicBoneColliderType == null)
            {
                KurotoriUtility.OutputLog(LogType.ERROR, "DynamicBoneColliderBaseが見つかりません。最新のDynamicBoneを正しくインポートできていない可能性があります。");
            }

            Component assembledComponent = target.AddComponent(dynamicBoneColliderType);

            if (component.GetType() == KurotoriUtility.GetTypeByClassName("DynamicBoneCollider"))
            {
                KurotoriUtility.CopyFieldValue("m_Direction",   component, assembledComponent, dynamicBoneColliderType);
                KurotoriUtility.CopyFieldValue("m_Center",      component, assembledComponent, dynamicBoneColliderType);
                KurotoriUtility.CopyFieldValue("m_Bound",       component, assembledComponent, dynamicBoneColliderType);
                KurotoriUtility.CopyFieldValue("m_Radius",      component, assembledComponent, dynamicBoneColliderType);
                KurotoriUtility.CopyFieldValue("m_Height",      component, assembledComponent, dynamicBoneColliderType);
            }
        }
    }

#if VRC_SDK_VRCSDK3
    public class PhysBoneRemapper : IComponentRemapper
    {
        private VRCPhysBone component;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public PhysBoneRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            this.component = component as VRCPhysBone;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            VRCPhysBone assembledComponent = target.AddComponent<VRCPhysBone>();

            // ルートボーンのマッピング
            {
                Transform root = component.rootTransform;
                if (root != null)
                {
                    var rootBonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, root);
                    if (string.IsNullOrEmpty(rootBonePath))
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のRoot:{1}はベースアバター内に存在しません。", component.gameObject.name, root.name));
                    }
                    else
                    {
                        Transform newRoot;
                        if (assembledPathInfo.pathList.TryGetValue(rootBonePath, out newRoot))
                        {
                            assembledComponent.rootTransform = newRoot;
                        }
                        else
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のRoot:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, rootBonePath));
                        }
                    }
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のRootがnullです。", component.gameObject.name));
                }
            }

            // 無視オブジェクトのマッピング
            {
                List<Transform> newIgnoreTransformList = new List<Transform>();

                foreach (var ignoreTransform in component.ignoreTransforms)
                {
                    if (ignoreTransform == null)
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のIgnoreTransformにnullのものが存在します。", component.gameObject.name));
                    }
                    else
                    {
                        var bonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, ignoreTransform);
                        if (string.IsNullOrEmpty(bonePath))
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のIgnoreTransform:{1}はベースアバター内に存在しません。", component.gameObject.name, ignoreTransform.name));
                        }
                        else
                        {
                            Transform newTransform;
                            if (assembledPathInfo.pathList.TryGetValue(bonePath, out newTransform))
                            {
                                newIgnoreTransformList.Add(newTransform);
                            }
                            else
                            {
                                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のIgnoreTransform:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, bonePath));
                            }
                        }
                    }
                }

                assembledComponent.ignoreTransforms = newIgnoreTransformList;
            }

            // コライダーのマッピング
            {
                List<VRC.Dynamics.VRCPhysBoneColliderBase> newColliderList = new List<VRC.Dynamics.VRCPhysBoneColliderBase>();

                foreach (var collider in component.colliders)
                {
                    if (collider == null)
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のCollidersにnullのものが存在します。", component.gameObject.name));
                    }
                    else
                    {
                        var colliderTransform = collider.transform;
                        var bonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, colliderTransform);
                        if (string.IsNullOrEmpty(bonePath))
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のColliders:{1}はベースアバター内に存在しません。", component.gameObject.name, colliderTransform.name));
                        }
                        else
                        {
                            Transform newTransform;
                            if (assembledPathInfo.pathList.TryGetValue(bonePath, out newTransform))
                            {
                                var newPhysColliders = newTransform.gameObject.GetComponents<VRCPhysBoneCollider>();
                                if (newPhysColliders == null)
                                {
                                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のColliders:{1}にPhysBoneColliderコンポーネントが存在しませんでした。", component.gameObject.name, bonePath));
                                }
                                else if (newPhysColliders.Length > 1)
                                {
                                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のColliders:{1}にPhysBoneColliderコンポーネントが2つ以上存在します。このツールでは同一オブジェクトに同じ種類の複数コンポーネントは対応していません。", component.gameObject.name, bonePath));
                                }
                                else
                                {
                                    newColliderList.Add(newPhysColliders[0]);
                                }
                            }
                            else
                            {
                                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBone:{0}のColliders:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, bonePath));
                            }
                        }
                    }
                }
                assembledComponent.colliders = newColliderList;
            }

            assembledComponent.endpointPosition = component.endpointPosition;

            assembledComponent.multiChildType = component.multiChildType;

            assembledComponent.integrationType = component.integrationType;


            assembledComponent.pull = component.pull;
            assembledComponent.pullCurve = KurotoriUtility.CopyAnimationCurve(component.pullCurve);
            
            assembledComponent.spring = component.spring;
            assembledComponent.springCurve = KurotoriUtility.CopyAnimationCurve(component.springCurve);

            assembledComponent.stiffness = component.stiffness;
            assembledComponent.stiffnessCurve = KurotoriUtility.CopyAnimationCurve(component.stiffnessCurve);

            assembledComponent.immobileType = component.immobileType;
            assembledComponent.immobile = component.immobile;
            assembledComponent.immobileCurve = KurotoriUtility.CopyAnimationCurve(component.immobileCurve);
            
            assembledComponent.radius = component.radius;
            assembledComponent.radiusCurve = KurotoriUtility.CopyAnimationCurve(component.radiusCurve);

            assembledComponent.gravity = component.gravity;
            assembledComponent.gravityCurve = KurotoriUtility.CopyAnimationCurve(component.gravityCurve);

            assembledComponent.gravityFalloff = component.gravityFalloff;
            assembledComponent.gravityFalloffCurve = KurotoriUtility.CopyAnimationCurve(component.gravityFalloffCurve);

            assembledComponent.maxAngleX = component.maxAngleX;
            assembledComponent.maxAngleZ = component.maxAngleZ;
            assembledComponent.maxAngleXCurve = KurotoriUtility.CopyAnimationCurve(component.maxAngleXCurve);
            assembledComponent.maxAngleZCurve = KurotoriUtility.CopyAnimationCurve(component.maxAngleZCurve);
            assembledComponent.limitRotation = component.limitRotation;
            assembledComponent.limitRotationXCurve = KurotoriUtility.CopyAnimationCurve(component.limitRotationXCurve);
            assembledComponent.limitRotationYCurve = KurotoriUtility.CopyAnimationCurve(component.limitRotationYCurve);
            assembledComponent.limitRotationZCurve = KurotoriUtility.CopyAnimationCurve(component.limitRotationZCurve);

            assembledComponent.limitType = component.limitType;

            assembledComponent.allowCollision = component.allowCollision;


            assembledComponent.staticFreezeAxis = component.staticFreezeAxis;


            assembledComponent.allowGrabbing = component.allowGrabbing;
            assembledComponent.allowPosing = component.allowPosing;
            assembledComponent.grabMovement = component.grabMovement;
            assembledComponent.maxStretch = component.maxStretch;
            assembledComponent.maxStretchCurve = KurotoriUtility.CopyAnimationCurve(component.maxStretchCurve);
            assembledComponent.parameter = component.parameter;
            assembledComponent.isAnimated = component.isAnimated;

            assembledComponent.showGizmos = component.showGizmos;
            assembledComponent.boneOpacity = component.boneOpacity;
            assembledComponent.limitOpacity = component.limitOpacity;
        }
    }

    public class PhysBoneColliderRemapper :IComponentRemapper
    {
        private VRCPhysBoneCollider component;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public PhysBoneColliderRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            this.component = component as VRCPhysBoneCollider;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            var assembledComponent = target.AddComponent<VRCPhysBoneCollider>();

            // ルートボーンのマッピング
            {
                Transform root = component.rootTransform;
                if (root != null)
                {
                    var rootBonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, root);
                    if (string.IsNullOrEmpty(rootBonePath))
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBoneCollider:{0}のRoot:{1}はベースアバター内に存在しません。", component.gameObject.name, root.name));
                    }
                    else
                    {
                        Transform newRoot;
                        if (assembledPathInfo.pathList.TryGetValue(rootBonePath, out newRoot))
                        {
                            assembledComponent.rootTransform = newRoot;
                        }
                        else
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBoneCollider:{0}のRoot:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, rootBonePath));
                        }
                    }
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PhysBoneCollider:{0}のRootがnullです。", component.gameObject.name));
                }
            }

            assembledComponent.shapeType = component.shapeType;
            assembledComponent.radius = component.radius;
            assembledComponent.height = component.height;
            assembledComponent.rotation = component.rotation;
            assembledComponent.position = component.position;
            assembledComponent.insideBounds = component.insideBounds;
        }
    }

    public class ContactSenderRemapper : IComponentRemapper
    {
        private VRCContactSender component;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public ContactSenderRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            this.component = component as VRCContactSender;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            var assembledComponent = target.AddComponent<VRCContactSender>();

            // ルートボーンのマッピング
            {
                Transform root = component.rootTransform;
                if (root != null)
                {
                    var rootBonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, root);
                    if (string.IsNullOrEmpty(rootBonePath))
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("VRCContactSender:{0}のRoot:{1}はベースアバター内に存在しません。", component.gameObject.name, root.name));
                    }
                    else
                    {
                        Transform newRoot;
                        if (assembledPathInfo.pathList.TryGetValue(rootBonePath, out newRoot))
                        {
                            assembledComponent.rootTransform = newRoot;
                        }
                        else
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("VRCContactSender:{0}のRoot:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, rootBonePath));
                        }
                    }
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("VRCContactSender:{0}のRootがnullです。", component.gameObject.name));
                }
            }

            assembledComponent.shapeType = component.shapeType;
            assembledComponent.radius = component.radius;
            assembledComponent.height = component.height;
            assembledComponent.position = component.position;
            assembledComponent.rotation = component.rotation;

            List<string> newTags = new List<string>();

            foreach(var tag in component.collisionTags)
            {
                newTags.Add(tag);
            }

            assembledComponent.collisionTags = newTags;
        }
    }

    public class ContactReceiverRemapper : IComponentRemapper
    {
        private VRCContactReceiver component;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public ContactReceiverRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            this.component = component as VRCContactReceiver;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            var assembledComponent = target.AddComponent<VRCContactReceiver>();

            // ルートボーンのマッピング
            {
                Transform root = component.rootTransform;
                if (root != null)
                {
                    var rootBonePath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, root);
                    if (string.IsNullOrEmpty(rootBonePath))
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("VRCContactReceiver:{0}のRoot:{1}はベースアバター内に存在しません。", component.gameObject.name, root.name));
                    }
                    else
                    {
                        Transform newRoot;
                        if (assembledPathInfo.pathList.TryGetValue(rootBonePath, out newRoot))
                        {
                            assembledComponent.rootTransform = newRoot;
                        }
                        else
                        {
                            KurotoriUtility.OutputLog(LogType.WARNING, string.Format("VRCContactReceiver:{0}のRoot:{1}が結合後のアバターに存在しませんでした。", component.gameObject.name, rootBonePath));
                        }
                    }
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, string.Format("VRCContactReceiver:{0}のRootがnullです。", component.gameObject.name));
                }
            }

            assembledComponent.shapeType = component.shapeType;
            assembledComponent.radius = component.radius;
            assembledComponent.height = component.height;
            assembledComponent.position = component.position;
            assembledComponent.rotation = component.rotation;

            assembledComponent.allowSelf = component.allowSelf;
            assembledComponent.allowOthers = component.allowOthers;
            assembledComponent.localOnly = component.localOnly;
            
            List<string> newTags = new List<string>();

            foreach (var tag in component.collisionTags)
            {
                newTags.Add(tag);
            }

            assembledComponent.collisionTags = newTags;

            assembledComponent.receiverType = component.receiverType;
            assembledComponent.parameter = component.parameter;
            assembledComponent.minVelocity = component.minVelocity;

        }
    }
#endif

    public class AimConstraintReMapper : IComponentRemapper
    {
        private AimConstraint constraint;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public AimConstraintReMapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            constraint = component as AimConstraint;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("AimConstraint:{0}のリマップ開始", constraint.gameObject.name));

            if (target.HasComponent<AimConstraint>())
            {
                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ベースオブジェクトと結合オブジェクト（{0}）両方に同じAimConstraintがあります。同一のコンストレイントコンポーネントはコピーできません。ベースモデル側のパラメーターを採用します。", constraint.gameObject.name));
                return;
            }

            AimConstraint assembledComponent = target.AddComponent<AimConstraint>();

            assembledComponent.aimVector = constraint.aimVector;
            assembledComponent.locked = constraint.locked;
            assembledComponent.rotationAtRest = constraint.rotationAtRest;
            assembledComponent.rotationAxis = constraint.rotationAxis;
            assembledComponent.rotationOffset = constraint.rotationOffset;
            assembledComponent.upVector = constraint.upVector;
            assembledComponent.weight = constraint.weight;

            if (constraint.worldUpObject != null)
            {
                var worldUpObjectPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, constraint.worldUpObject);

                Transform assembledWorldUpObject;
                if (assembledPathInfo.pathList.TryGetValue(worldUpObjectPath, out assembledWorldUpObject))
                {
                    assembledComponent.worldUpObject = assembledWorldUpObject;
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, "AimConstraintのworldUpObjectの参照の書き換えに失敗しました。正しく結合できていない可能性があります。");
                }
            }

            assembledComponent.worldUpType = constraint.worldUpType;
            assembledComponent.worldUpVector = constraint.worldUpVector;

            for (int i = 0; i < constraint.sourceCount; ++i)
            {
                var constraintSource = constraint.GetSource(i);
                Transform sourceTransform = constraintSource.sourceTransform;

                if (sourceTransform != null)
                {
                    var srcTransPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, sourceTransform);
                    Transform sameObject;

                    if (assembledPathInfo.pathList.TryGetValue(srcTransPath, out sameObject))
                    {
                        ConstraintSource source = constraintSource;
                        source.sourceTransform = sameObject;
                        assembledComponent.AddSource(source);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("AimConstraint:{0}で指定されているオブジェクト:{1}は結合モデル内に存在しません。結合モデル内のものを参照してください。", assembledComponent.gameObject.name, srcTransPath));
                    }
                }
            }

            assembledComponent.constraintActive = constraint.constraintActive;
        }
    }

    public class LookAtConstraintReMapper : IComponentRemapper
    {
        private LookAtConstraint constraint;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public LookAtConstraintReMapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            constraint = component as LookAtConstraint;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("LookAtConstraint:{0}のリマップ開始", constraint.gameObject.name));

            if (target.HasComponent<LookAtConstraint>())
            {
                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ベースオブジェクトと結合オブジェクト（{0}）両方に同じLookAtConstraintがあります。同一のコンストレイントコンポーネントはコピーできません。ベースモデル側のパラメーターを採用します。", constraint.gameObject.name));
                return;
            }

            LookAtConstraint assembledComponent = target.AddComponent<LookAtConstraint>();

            assembledComponent.locked = constraint.locked;
            assembledComponent.roll = constraint.roll;
            assembledComponent.rotationAtRest = constraint.rotationAtRest;
            assembledComponent.rotationOffset = constraint.rotationOffset;
            assembledComponent.useUpObject = constraint.useUpObject;
            assembledComponent.weight = constraint.weight;

            if (constraint.worldUpObject != null)
            {
                var worldUpObjectPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, constraint.worldUpObject);

                Transform assembledWorldUpObject;
                if (assembledPathInfo.pathList.TryGetValue(worldUpObjectPath, out assembledWorldUpObject))
                {
                    assembledComponent.worldUpObject = assembledWorldUpObject;
                }
                else
                {
                    KurotoriUtility.OutputLog(LogType.WARNING, "LookAtConstraintのworldUpObjectの参照の書き換えに失敗しました。正しく結合できていない可能性があります。");
                }
            }

            for (int i = 0; i < constraint.sourceCount; ++i)
            {
                var constraintSource = constraint.GetSource(i);
                Transform sourceTransform = constraintSource.sourceTransform;

                if (sourceTransform != null)
                {
                    var srcTransPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, sourceTransform);
                    Transform sameObject;

                    if (assembledPathInfo.pathList.TryGetValue(srcTransPath, out sameObject))
                    {
                        ConstraintSource source = constraintSource;
                        source.sourceTransform = sameObject;
                        assembledComponent.AddSource(source);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("LookAtConstraint:{0}で指定されているオブジェクト:{1}は結合モデル内に存在しません。結合モデル内のものを参照してください。", assembledComponent.gameObject.name, srcTransPath));
                    }
                }
            }

            assembledComponent.constraintActive = constraint.constraintActive;
        }
    }

    public class ParentConstraintRemapper : IComponentRemapper
    {
        private ParentConstraint constraint;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public ParentConstraintRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            constraint = component as ParentConstraint;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("ParentConstraint:{0}のリマップ開始", constraint.gameObject.name));

            if (target.HasComponent<ParentConstraint>())
            {
                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ベースオブジェクトと結合オブジェクト（{0}）両方に同じParentConstraintがあります。同一のコンストレイントコンポーネントはコピーできません。ベースモデル側のパラメーターを採用します。", constraint.gameObject.name));
                return;
            }

            ParentConstraint assembledComponent = target.AddComponent<ParentConstraint>();

            assembledComponent.locked = constraint.locked;
            assembledComponent.rotationAtRest = constraint.rotationAtRest;
            assembledComponent.rotationAxis = constraint.rotationAxis;
            assembledComponent.rotationOffsets = constraint.rotationOffsets;
            assembledComponent.translationAtRest = constraint.translationAtRest;
            assembledComponent.translationAxis = constraint.translationAxis;
            assembledComponent.translationOffsets = constraint.translationOffsets;
            assembledComponent.weight = constraint.weight;

            for (int i = 0; i < constraint.sourceCount; ++i)
            {
                var constraintSource = constraint.GetSource(i);
                Transform sourceTransform = constraintSource.sourceTransform;

                if (sourceTransform != null)
                {
                    var srcTransPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, sourceTransform);
                    Transform sameObject;

                    if (assembledPathInfo.pathList.TryGetValue(srcTransPath, out sameObject))
                    {
                        ConstraintSource source = constraintSource;
                        source.sourceTransform = sameObject;
                        assembledComponent.AddSource(source);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ParentConstraint:{0}で指定されているオブジェクト:{1}は結合モデル内に存在しません。結合モデル内のものを参照してください。", assembledComponent.gameObject.name, srcTransPath));
                    }
                }
            }

            assembledComponent.constraintActive = constraint.constraintActive;
        }
    }

    public class PositionConstraintRemapper : IComponentRemapper
    {
        private PositionConstraint constraint;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public PositionConstraintRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            constraint = component as PositionConstraint;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("PositionConstraint:{0}のリマップ開始", constraint.gameObject.name));

            if (target.HasComponent<PositionConstraint>())
            {
                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ベースオブジェクトと結合オブジェクト（{0}）両方に同じPositionConstraintがあります。同一のコンストレイントコンポーネントはコピーできません。ベースモデル側のパラメーターを採用します。", constraint.gameObject.name));
                return;
            }

            PositionConstraint assembledComponent = target.AddComponent<PositionConstraint>();

            assembledComponent.locked = constraint.locked;
            assembledComponent.translationAtRest = constraint.translationAtRest;
            assembledComponent.translationAxis = constraint.translationAxis;
            assembledComponent.translationOffset = constraint.translationOffset;
            assembledComponent.weight = constraint.weight;

            for (int i = 0; i < constraint.sourceCount; ++i)
            {
                var constraintSource = constraint.GetSource(i);
                Transform sourceTransform = constraintSource.sourceTransform;

                if (sourceTransform != null)
                {
                    var srcTransPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, sourceTransform);
                    Transform sameObject;

                    if (assembledPathInfo.pathList.TryGetValue(srcTransPath, out sameObject))
                    {
                        ConstraintSource source = constraintSource;
                        source.sourceTransform = sameObject;
                        assembledComponent.AddSource(source);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("PositionConstraint:{0}で指定されているオブジェクト:{1}は結合モデル内に存在しません。結合モデル内のものを参照してください。", assembledComponent.gameObject.name, srcTransPath));
                    }
                }
            }

            assembledComponent.constraintActive = constraint.constraintActive;
        }
    }

    public class RotationConstraintRemapper : IComponentRemapper
    {
        private RotationConstraint constraint;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public RotationConstraintRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            constraint = component as RotationConstraint;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("RotationConstraint:{0}のリマップ開始", constraint.gameObject.name));
            
            if(target.HasComponent<RotationConstraint>())
            {
                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ベースオブジェクトと結合オブジェクト（{0}）両方に同じRotationConstraintがあります。同一のコンストレイントコンポーネントはコピーできません。ベースモデル側のパラメーターを採用します。", constraint.gameObject.name));
                return;
            }

            RotationConstraint assembledComponent = target.AddComponent<RotationConstraint>();

            assembledComponent.locked = constraint.locked;
            assembledComponent.rotationAtRest = constraint.rotationAtRest;
            assembledComponent.rotationAxis = constraint.rotationAxis;
            assembledComponent.rotationOffset = constraint.rotationOffset;
            assembledComponent.weight = constraint.weight;

            for (int i = 0; i < constraint.sourceCount; ++i)
            {
                var constraintSource = constraint.GetSource(i);
                Transform sourceTransform = constraintSource.sourceTransform;

                if (sourceTransform != null)
                {
                    var srcTransPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, sourceTransform);
                    Transform sameObject;

                    if (assembledPathInfo.pathList.TryGetValue(srcTransPath, out sameObject))
                    {
                        ConstraintSource source = constraintSource;
                        source.sourceTransform = sameObject;
                        assembledComponent.AddSource(source);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("RotationConstraint:{0}で指定されているオブジェクト:{1}は結合モデル内に存在しません。結合モデル内のものを参照してください。", assembledComponent.gameObject.name, srcTransPath));
                    }
                }
            }

            assembledComponent.constraintActive = constraint.constraintActive;
        }
    }

    public class ScaleConstraintRemapper : IComponentRemapper
    {
        private ScaleConstraint constraint;
        private GameObject target;
        private BonePathInfo partsPathInfo;

        public ScaleConstraintRemapper(Component component, GameObject target, BonePathInfo partsPathInfo)
        {
            constraint = component as ScaleConstraint;
            this.target = target;
            this.partsPathInfo = partsPathInfo;
        }

        public void Remap(BonePathInfo assembledPathInfo)
        {
            KurotoriUtility.OutputLog(LogType.LOG, string.Format("ScaleConstraint:{0}のリマップ開始", constraint.gameObject.name));

            if (target.HasComponent<ScaleConstraint>())
            {
                KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ベースオブジェクトと結合オブジェクト（{0}）両方に同じScaleConstraintがあります。同一のコンストレイントコンポーネントはコピーできません。ベースモデル側のパラメーターを採用します。", constraint.gameObject.name));
                return;
            }

            ScaleConstraint assembledComponent = target.AddComponent<ScaleConstraint>();

            assembledComponent.locked = constraint.locked;
            assembledComponent.scaleAtRest = constraint.scaleAtRest;
            assembledComponent.scalingAxis = constraint.scalingAxis;
            assembledComponent.scaleOffset = constraint.scaleOffset;
            assembledComponent.weight = constraint.weight;

            for (int i = 0; i < constraint.sourceCount; ++i)
            {
                var constraintSource = constraint.GetSource(i);
                Transform sourceTransform = constraintSource.sourceTransform;

                if (sourceTransform != null)
                {
                    var srcTransPath = KurotoriUtility.GetBonePath(partsPathInfo.rootBone, sourceTransform);
                    Transform sameObject;

                    if (assembledPathInfo.pathList.TryGetValue(srcTransPath, out sameObject))
                    {
                        ConstraintSource source = constraintSource;
                        source.sourceTransform = sameObject;
                        assembledComponent.AddSource(source);
                    }
                    else
                    {
                        KurotoriUtility.OutputLog(LogType.WARNING, string.Format("ScaleConstraint:{0}で指定されているオブジェクト:{1}は結合モデル内に存在しません。結合モデル内のものを参照してください。", assembledComponent.gameObject.name, srcTransPath));
                    }
                }
            }

            assembledComponent.constraintActive = constraint.constraintActive;
        }
    }


}
