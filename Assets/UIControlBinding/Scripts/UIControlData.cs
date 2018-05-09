﻿/*
    URL: https://github.com/easy66/UIControlBinding
    使用方法:
    UE: 将此脚本添加到UI根节点，与程序协商好需要绑定的控件及其变量名后，将需要绑定的控件拖到脚本上
    程序: 点此脚本右上角的齿轮，点 "复制代码到剪贴板" 按钮

    UIManager 加载示例：
    `` C#
        UIA uiA = new UIA();
        GameObject prefab = Resources.Load<GameObject>("UI/UIA"); // you can get ui config from config file
        GameObject go = Instantiate(prefab);
        UIControlData ctrlData = go.GetComponent<UIControlData>();
        if(ctrlData != null)
        {
            ctrlData.BindAllFields(uiA);
        }
    ``

 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SDGame.UITools
{
    /// <summary>
    /// 单个控件数据
    /// </summary>
    [Serializable]
    public class CtrlItemData
    {
        public string                       name = string.Empty;
        [HideInInspector]
        public string                       type = string.Empty;
        public UnityEngine.Object[]         targets = new UnityEngine.Object[1];
    }

    /// <summary>
    /// 单个子UI数据
    /// </summary>
    [Serializable]
    public class SubUIItemData
    {
        public string           name                = string.Empty;
        public UIControlData    subUIData           = null;
    }

    /// <summary>
    /// 当前UI所有的绑定数据以及子UI指定
    /// </summary>
    [DisallowMultipleComponent]
    public class UIControlData : MonoBehaviour
    {
        /// <summary>
        /// 所有绑定的组件，不允许重名
        /// </summary>
        public List<CtrlItemData>        ctrlItemDatas;
        /// <summary>
        /// 子UI数据
        /// </summary>
        public List<SubUIItemData>       subUIItemDatas;

        /// <summary>
        /// 已知类型列表，如果以后有自定义类型可以加到这里并同时修改 FindCorrectComponent 方法
        /// </summary>
        private static Dictionary<string, Type> _typeMap = new Dictionary<string, Type>()
        {
            { "Text", typeof(Text)},
            { "RawImage", typeof(RawImage)},
            { "Button", typeof(Button)},
            { "Toggle", typeof(Toggle)},
            { "Slider", typeof(Slider)},
            { "Scrollbar", typeof(Scrollbar)},
            { "Dropdown", typeof(Dropdown)},
            { "InputField", typeof(InputField)},
            { "Canvas", typeof(Canvas)},
            { "ScrollRect", typeof(ScrollRect)},
            { "Image", typeof(Image)},
            { "RectTransform", typeof(RectTransform)},
            { "Transform", typeof(Transform)},
        };

        /// <summary>
        /// 将当前数据绑定到某窗口类实例的字段，UI 加载后必须被执行
        /// </summary>
        /// <param name="window"></param>
        public void BindAllFields(IWindow window)
        {
            if (window == null)
                return;

            FieldInfo[] fis = window.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            for(int i = 0, imax = fis.Length; i < imax; i++)
            {
                FieldInfo fi = fis[i];
            
                if (fi.GetCustomAttributes(typeof(ControlBindingAttribute), false).Length != 0)
                    BindCtrl(window, fi);
                else if (fi.GetCustomAttributes(typeof(SubUIBindingAttribute), false).Length != 0)
                    BindSubUI(window, fi);
            }
        }

        private void BindCtrl(IWindow window, FieldInfo fi)
        {
            int itemIdx = GetCtrlIndex(fi.Name);
            if (itemIdx == -1)
            {
                Debug.LogErrorFormat("can not find binding control of var [{0}]", fi.Name);
                return;
            }

            var objs = ctrlItemDatas[itemIdx];
            Type objType;
            if (!_typeMap.TryGetValue(objs.type, out objType))
                return;

            Type fieldType = fi.FieldType;
            if (fieldType.IsArray)
            {
                Array arrObj = Array.CreateInstance(objType, objs.targets.Length);

                // 给数组元素设置数据
                for (int j = 0, jmax = objs.targets.Length; j < jmax; j++)
                {
                    arrObj.SetValue(objs.targets[j], j);
                }
                fi.SetValue(window, arrObj);
            }
            else
            {
                UnityEngine.Object component = GetComponent(itemIdx);
                fi.SetValue(window, component);
            }
        }

        private void BindSubUI(IWindow window, FieldInfo fi)
        {
            int subUIIdx = GetSubUIIndex(fi.Name);
            if(subUIIdx == -1)
            {
                Debug.LogErrorFormat("can not find binding subUI of var [{0}]", fi.Name);
                return;
            }

            fi.SetValue(window, subUIItemDatas[subUIIdx].subUIData);
        }

    #region Get,不建议使用

        /// <summary>
        /// 找到指定名称的第一个组件, 不存在返回 null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public T GetComponent<T>(string name) where T : Component
        {
            int idx = GetCtrlIndex(name);
            if (idx == -1)
                return null;

            var targets = ctrlItemDatas[idx].targets;
            if (targets.Length == 0)
                return null;

            return targets[0] as T;
        }

        public new UnityEngine.Object GetComponent(string name)
        {
            int idx = GetCtrlIndex(name);
            if (idx == -1)
                return null;

            var targets = ctrlItemDatas[idx].targets;
            if (targets.Length == 0)
                return null;

            return targets[0];
        }

        public UnityEngine.Object GetComponent(int idx)
        {
            if (idx == -1 || idx >= ctrlItemDatas.Count)
                return null;

            var targets = ctrlItemDatas[idx].targets;
            if (targets.Length == 0)
                return null;

            return targets[0];
        }

        public UnityEngine.Object[] GetComponents(string name)
        {
            int idx = GetCtrlIndex(name);
            if (idx == -1)
                return null;

            return ctrlItemDatas[idx].targets;
        }

        public UnityEngine.Object[] GetComponents(int idx)
        {
            if (idx == -1 || idx >= ctrlItemDatas.Count)
                return null;

            return ctrlItemDatas[idx].targets;
        }



        private int  GetCtrlIndex(string name)
        {
            for (int i = 0, imax = ctrlItemDatas.Count; i < imax; i++)
            {
                CtrlItemData item = ctrlItemDatas[i];
                if (item.name == name)
                    return i;
            }
            return -1;
        }

        private int GetSubUIIndex(string name)
        {
            for(int i = 0, imax = subUIItemDatas.Count; i < imax; i++)
            {
                SubUIItemData item = subUIItemDatas[i];
                if (item.name == name)
                    return i;
            }
            return -1;
        }

    #endregion

    #region For Editor
    #if UNITY_EDITOR

        public bool dataHasChanged = false;

        public bool CorrectComponents()
        {
            bool isOK = true;
            for(int i = 0, imax = ctrlItemDatas.Count; i < imax; i++)
            {
                if (string.IsNullOrEmpty(ctrlItemDatas[i].name)) // TODO Check if is a valid varible name
                {
                    Debug.LogErrorFormat("第 {0} 个控件没有名字，请修正", i + 1);
                    return false;
                }

                for (int j = ctrlItemDatas.Count - 1; j >= 0; j--)
                {
                    if(ctrlItemDatas[i].name == ctrlItemDatas[j].name && i != j)
                    {
                        Debug.LogErrorFormat("控件名字 [{0}] 第 {1} 项与第 {2} 项重复，请修正", ctrlItemDatas[i].name, i + 1, j + 1);
                        return false;
                    }
                }
            }

            isOK = ReplaceTargetsToUIComponent();
            if(isOK)
                Debug.LogFormat("[{0}]控件绑定修正完毕", gameObject.name);

            return isOK;
        }

        /// <summary>
        /// 由于自动拖上去的对象永远都是 GameObject，所以我们需要把它修正为正确的对象类型
        /// </summary>
        private bool ReplaceTargetsToUIComponent()
        {
            for (int i = 0, imax = ctrlItemDatas.Count; i < imax; i++)
            {
                var objs = ctrlItemDatas[i].targets;
                Type type = null;
                for(int j = 0, jmax = objs.Length; j < jmax; j++)
                {
                    if(objs[j] == null)
                    {
                        Debug.LogErrorFormat("控件名字 [{0}] 第 {1} 项为空，请修正", ctrlItemDatas[i].name, j + 1);
                        return false;
                    }

                    GameObject go = objs[j] as GameObject;
                    if (go == null)
                        go = (objs[j] as Component).gameObject;

                    // 必须拖当前 Prefab 下的控件
                    if (!IsInCurrentPrefab((go.transform).transform))
                    {
                        Debug.LogErrorFormat("控件名字 [{0}] 第 {1} 项不是当前 Prefab 下的控件，请修正", ctrlItemDatas[i].name, j + 1);
                        return false;
                    }

                    var correctComponent = FindCorrectComponent(go);
                    if (type == null)
                        type = correctComponent.GetType();
                    else if(type != correctComponent.GetType())
                    {
                        Debug.LogErrorFormat("控件名字 [{0}] 第 {1} 项与第 1 项的类型不同，请修正", ctrlItemDatas[i].name, j + 1);
                        return false;
                    }

                    if (objs[j] != correctComponent)
                        dataHasChanged = true;

                    objs[j] = correctComponent;
                }

                ctrlItemDatas[i] = new CtrlItemData() { name = ctrlItemDatas[i].name, type = type.Name, targets = objs };
            }
            return true;
        }

        private bool IsInCurrentPrefab(Transform t)
        {
            do
            {
                if (t == transform)
                    return true;
                t = t.parent;
            } while (t != null);
            return false;
        }

        private UnityEngine.Object FindCorrectComponent(GameObject go)
        {
            List<Component> components = new List<Component>();
            go.GetComponents(components);

            Component newComp = null;

            for (int i = 0, imax = components.Count; i < imax; i++)
            {
                Component tmp = components[i];
            
                Type type = tmp.GetType();

                if (type == typeof(Text))
                    newComp = go.GetComponent<Text>();
                else if (type == typeof(RawImage))
                    newComp = go.GetComponent<RawImage>();
                else if (type == typeof(Button))
                    newComp = go.GetComponent<Button>();
                else if (type == typeof(Toggle))
                    newComp = go.GetComponent<Toggle>();
                else if (type == typeof(Slider))
                    newComp = go.GetComponent<Slider>();
                else if (type == typeof(Scrollbar))
                    newComp = go.GetComponent<Scrollbar>();
                else if (type == typeof(Dropdown))
                    newComp = go.GetComponent<Dropdown>();
                else if (type == typeof(InputField))
                    newComp = go.GetComponent<InputField>();
                else if (type == typeof(Canvas))
                    newComp = go.GetComponent<Canvas>();
                //else if (type == typeof(Panel))
                //    newComp = go.GetComponent<Panel>();
                else if (type == typeof(ScrollRect))
                    newComp = go.GetComponent<ScrollRect>();
                else if (type == typeof(Image))
                    newComp = go.GetComponent<Image>();

                // 很多组件上都有 Image，因此即使找到 Image 也不停止
                if (newComp != null && type != typeof(Image))
                    break;
            }

            if(newComp == null)
            {
                newComp = go.GetComponent<RectTransform>();
                if (newComp == null)
                    newComp = go.GetComponent<Transform>();
            }

            return newComp;
        }

    

        [ContextMenu("复制代码到剪贴板(Private)")]
        public void CopyCodeToClipBoardPrivate()
        {
            CopyCodeToClipBoardImpl(false);
        }

        [ContextMenu("复制代码到剪贴板(Public)")]
        public void CopyCodeToClipBoardPublic()
        {
            CopyCodeToClipBoardImpl(true);
        }

        private void CopyCodeToClipBoardImpl(bool isPublic)
        {
            string strVarAcc = isPublic ? "public" : "private";

            StringBuilder sb = new StringBuilder(1024);
            sb.Append("#region 控件绑定变量声明，自动生成请勿手改\r\n");

            foreach (var ctrl in ctrlItemDatas)
            {
                if (ctrl.targets.Length == 0)
                    continue;

                if (ctrl.targets.Length == 1)
                    sb.AppendFormat("\t\t[ControlBinding]\r\n\t\t{0} {1} {2};\r\n", strVarAcc, ctrl.type, ctrl.name);
                else
                    sb.AppendFormat("\t\t[ControlBinding]\r\n\t\t{0} {1}[] {2};\r\n", strVarAcc, ctrl.type, ctrl.name);
            }

            sb.AppendFormat("\r\n");
            foreach(var subUI in subUIItemDatas)
            {
                sb.AppendFormat("\t\t[SubUIBinding]\r\n\t\t{0} UIControlData {1};\r\n", strVarAcc, subUI.name);
            }
            sb.Append("#endregion\r\n\r\n");

            GUIUtility.systemCopyBuffer = sb.ToString();
        }



    #endif
        #endregion
    }

}
