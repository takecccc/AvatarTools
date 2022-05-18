using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace KurotoriTools
{
    public enum LogType
    {
        LOG,
        WARNING,
        ERROR,
    };


    public class LogWindow
    {
        private Vector2 scrollPosition = new Vector2();
        private bool displayNormalLog = false;
        private bool displayWarningLog = true;
        private bool displayErrorLog = true;

        public LogWindow()
        {
        }

        public void DrawLogWindow()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField("LOG");

            using (new EditorGUILayout.HorizontalScope())
            {


                using (new EditorGUILayout.HorizontalScope())
                {
                    displayNormalLog = GUILayout.Toggle(displayNormalLog, "LOG", EditorStyles.miniButtonLeft);
                    displayWarningLog = GUILayout.Toggle(displayWarningLog, "WARNING", EditorStyles.miniButtonMid);
                    displayErrorLog = GUILayout.Toggle(displayErrorLog, "ERROR", EditorStyles.miniButtonRight);
                }
                if (GUILayout.Button("COPY"))
                {
                    LogSystem.SystemCopyLogText();
                }
                if (GUILayout.Button("CLEAR"))
                {
                    LogSystem.ClearLog();
                }
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.skin.box);
            {
                foreach (var log in LogSystem.LogList)
                {
                    GUIStyle style = new GUIStyle();
                    style.wordWrap = true;
                    style.richText = true;

                    bool display = false;

                    switch (log.tag)
                    {
                        case "ERROR":
                            style.normal.textColor = Color.red;
                            display = displayErrorLog;
                            break;
                        case "WARNING":
                            if (EditorGUIUtility.isProSkin)
                            {
                                style.normal.textColor = Color.cyan;
                            }
                            else
                            {
                                style.normal.textColor = Color.blue;
                            }

                            display = displayWarningLog;
                            break;
                        default:
                            if (EditorGUIUtility.isProSkin)
                            {
                                style.normal.textColor = Color.white;
                            }
                            else
                            {
                                style.normal.textColor = Color.black;
                            }
                            display = displayNormalLog;
                            break;
                    }

                    if (display)
                        EditorGUILayout.LabelField(string.Format("<b>[{0}]:</b>\n{1}\n", log.tag, log.context), style);
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }
    }

    public static class LogSystem
    {
        public struct Log
        {
            public string tag;
            public string context;
        }

        public static IReadOnlyList<Log> LogList {get{ return LogStrage.instance.logList.AsReadOnly(); }}

        private static List<ILogUpdateObserver> observers = new List<ILogUpdateObserver>();

        public static void AddObserver(ILogUpdateObserver observer)
        {
            if(!observers.Contains(observer))
                observers.Add(observer);
        }

        public static void DeleteObserber(ILogUpdateObserver observer)
        {
            observers.Remove(observer);
        }

        private static void NortifyAll()
        {
            foreach(var observer in observers)
            {
                observer.OnUpdateLog();
            }
        }

        public static void AddLog(string tag, string context)
        {
            var instance = LogStrage.instance;

            Log log;
            log.tag = tag;
            log.context = context;

            instance.logList.Add(log);
            NortifyAll();
        }

        public static string CreateLogText()
        {
            var logList = LogStrage.instance.logList;

            string output = "";

            foreach(var log in logList)
            {
                output += string.Format("[{0}]:{1}\n", log.tag, log.context);
            }

            return output;
        }

        public static void SystemCopyLogText()
        {
            EditorGUIUtility.systemCopyBuffer = CreateLogText(); 
        }

        public static void ClearLog()
        {
            Debug.Log("LogSystem : ログをクリアしました");
            LogStrage.instance.logList.Clear();
            NortifyAll();
        }

        public interface ILogUpdateObserver
        {
            void OnUpdateLog();
        }

        private class LogStrage : ScriptableSingleton<LogStrage>
        {
            public List<Log> logList = new List<Log>();
            
        }
    }
}