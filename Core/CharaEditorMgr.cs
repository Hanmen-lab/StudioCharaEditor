using AIChara;
using BepInEx;
using KKAPI.Studio;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using UnityEngine;
using CharaCustom;
using HarmonyLib;

namespace StudioCharaEditor
{
    class CharaEditorMgr : MonoBehaviour
    {
        public CharaEditorUI gui;
        public Dictionary<OCIChar, CharaEditorController> charaEditorCtrlDict = new Dictionary<OCIChar, CharaEditorController>();
        public Dictionary<string, Dictionary<string, string>> charaEditorLocalizeDict = new Dictionary<string, Dictionary<string, string>>();
        private const int HouseKeepingFrameInterval = 30;
        private int housekeepingCooldown;

        public static CharaEditorMgr Instance { get; private set; }

        public static CharaEditorMgr Install(GameObject container)
        {
            if (CharaEditorMgr.Instance == null)
            {
                CharaEditorMgr.Instance = container.AddComponent<CharaEditorMgr>();
            }
            return CharaEditorMgr.Instance;
        }

        public bool VisibleGUI
        {
            get => gui.VisibleGUI;
            set => gui.VisibleGUI = value;
        }

        private void Awake()
        {
        }

        private void Start()
        {
            StartCoroutine(LoadingCo());
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        //[Warning: Unity Log] OnLevelWasLoaded was found on ConsolePlugin
        //This message has been deprecated and will be removed in a later version of Unity.
        //Add a delegate to SceneManager.sceneLoaded instead to get notifications after scene loading has completed
        private IEnumerator LoadingCo()
        {
            yield return new WaitUntil(() => StudioAPI.StudioLoaded);
            // Wait until fully loaded
            yield return null;

            // start ui
            gui = new GameObject("GUI").AddComponent<CharaEditorUI>();
            gui.transform.parent = base.transform;
            gui.VisibleGUI = false;
            PluginTimelineCompatibility.PopulateTimeline();
            //Console.WriteLine("StudioCharaEditor CharaEditorMgr Started.");

            // check extra plugins
        }

        public void ResetGUI()
        {
            gui.ResetGui();
        }

        public void RunAfterFrames(int frameCount, Action action)
        {
            if (action == null)
            {
                return;
            }

            StartCoroutine(RunAfterFramesCo(frameCount, action));
        }

        private IEnumerator RunAfterFramesCo(int frameCount, Action action)
        {
            for (int i = 0; i < frameCount; i++)
            {
                yield return null;
            }

            action();
        }

        public void HouseKeeping(bool isVisible)
        {
            if (!isVisible)
            {
                housekeepingCooldown = 0;
                return;
            }

            if (housekeepingCooldown > 0)
            {
                housekeepingCooldown--;
                return;
            }
            housekeepingCooldown = HouseKeepingFrameInterval;

            // release deleted controller
            OCIChar deletedChar = null;
            foreach (OCIChar ociChar in charaEditorCtrlDict.Keys)
            {
                if (ociChar.charInfo == null)
                {
                    deletedChar = ociChar;
                    break;
                }
            }

            if (deletedChar != null)
            {
                Console.WriteLine("Remove controller for deleted chara");
                charaEditorCtrlDict.Remove(deletedChar);
                return;
            }

            // housekeeping for controller
            foreach (var ctrl in charaEditorCtrlDict.Values)
            {
                ctrl.RefreshAccessoriesListIfExpired();
            }
        }

        public CharaEditorController GetEditorController(OCIChar ociTarget)
        {
            if (ociTarget == null)
            {
                return null;
            }
            if (!charaEditorCtrlDict.ContainsKey(ociTarget))
            {
                charaEditorCtrlDict[ociTarget] = new CharaEditorController(ociTarget);
                charaEditorCtrlDict[ociTarget].Initialize();
            }
            return charaEditorCtrlDict[ociTarget];
        }

        public CharaEditorController GetEditorController(ChaControl chaCtrl)
        {
            foreach (OCIChar ociChar in charaEditorCtrlDict.Keys)
            {
                if (ociChar.charInfo == chaCtrl)
                {
                    return charaEditorCtrlDict[ociChar];
                }
            }
            return null;
        }

        public void RefreshAllControllerFileData()
        {
            foreach (var ctrl in charaEditorCtrlDict.Values)
            {
                ctrl.InitFileData();
            }
        }

        public void ReloadDictionary()
        {
            LoadExtendSetting();
            if (gui != null)
            {
                gui.curLocalizationDict = AssignLocalizeDict();
            }
        }

        public void LoadExtendSetting()
        {
            try
            {
                string xmlFilename = Path.Combine(GetDllPath(), "HS2StudioCharaEditor.xml");
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load(xmlFilename);

                XmlNode rootNode = xDoc.DocumentElement;
                if (!rootNode.Name.Equals("StudioCharaEditorSetting"))
                {
                    throw new Exception("Root element missed!?");
                }

                charaEditorLocalizeDict.Clear();
                foreach (XmlNode sNode in rootNode.ChildNodes)
                {
                    if (sNode.Name.Equals("LocalizeDictionary"))
                    {
                        string dicName;
                        XmlAttribute attr = (XmlAttribute)sNode.Attributes.GetNamedItem("language");
                        if (attr == null || string.IsNullOrWhiteSpace(attr.Value))
                            dicName = "default";
                        else
                            dicName = attr.Value;
                        charaEditorLocalizeDict[dicName] = new Dictionary<string, string>();

                        foreach (XmlNode ssNode in sNode.ChildNodes)
                        {
                            if (ssNode.Name.Equals("DictPair"))
                            {
                                XmlAttribute srcAttr = (XmlAttribute)ssNode.Attributes.GetNamedItem("source");
                                if (srcAttr == null || string.IsNullOrWhiteSpace(srcAttr.Value))
                                    continue;
                                string srcText = srcAttr.Value;
                                string toText = ssNode.InnerText;
                                charaEditorLocalizeDict[dicName][srcText] = toText;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        public void SaveExtendSetting()
        {
            try
            {
                string xmlFilename = Path.Combine(GetDllPath(), "HS2StudioCharaEditor.xml");
                XmlDocument xDoc = new XmlDocument();

                XmlElement rootNode = xDoc.CreateElement("StudioCharaEditorSetting");
                xDoc.AppendChild(rootNode);

                if (charaEditorLocalizeDict != null && charaEditorLocalizeDict.Count > 0)
                {
                    foreach (string dicName in charaEditorLocalizeDict.Keys)
                    {
                        XmlElement dicRoot = xDoc.CreateElement("LocalizeDictionary");
                        dicRoot.SetAttribute("language", dicName);
                        rootNode.AppendChild(dicRoot);

                        foreach (string srcText in charaEditorLocalizeDict[dicName].Keys)
                        {
                            XmlElement dicItem = xDoc.CreateElement("DictPair");
                            dicItem.SetAttribute("source", srcText);
                            dicItem.InnerText = charaEditorLocalizeDict[dicName][srcText];
                            dicRoot.AppendChild(dicItem);
                        }
                    }
                }

                xDoc.Save(xmlFilename);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        private Dictionary<string, string> AssignLocalizeDict()
        {
            string tgtDicName = ResolveLocalizationDictionaryName(
                StudioCharaEditor.UILanguage.Value);

            if (charaEditorLocalizeDict != null)
            {
                if (charaEditorLocalizeDict.ContainsKey(tgtDicName))
                {
                    Dictionary<string, string> selected = charaEditorLocalizeDict[tgtDicName];
                    AddBuiltInMainGameTranslations(selected, tgtDicName);
                    return selected;
                }
                else if (charaEditorLocalizeDict.ContainsKey("default"))
                {
                    return charaEditorLocalizeDict["default"];
                }
            }
            return null;
        }

        private static void AddBuiltInMainGameTranslations(
            Dictionary<string, string> dictionary,
            string language)
        {
            if (dictionary == null)
            {
                return;
            }

            Dictionary<string, string> additions = null;
            if (string.Equals(language, "chinese", StringComparison.OrdinalIgnoreCase))
            {
                additions = new Dictionary<string, string>
                {
                    ["Status"] = "状态",
                    ["Plugin settings"] = "插件设置",
                    ["Look"] = "视线",
                    ["Camera"] = "相机",
                    ["Front"] = "前方",
                    ["Play Pose"] = "播放姿势",
                    ["Neck"] = "颈部",
                    ["Pose"] = "姿势",
                    ["Eyebrows"] = "眉毛",
                    ["Eyes"] = "眼睛",
                    ["Mouth"] = "嘴部",
                    ["Save with translated names"] = "使用翻译名称保存",
                    ["Lock Cameralight"] = "锁定相机灯光",
                    ["Advanced BoneMod window"] = "高级骨骼修改窗口",
                    ["Blendshape Creator"] = "混合形状创建器",
                    ["Coordinate Visibility Rules"] = "坐标可见性规则",
                    ["Toggle Backlight"] = "切换背光",
                    ["Show height measure bar"] = "显示身高测量条",
                    ["Toggle Blinking"] = "切换眨眼",
                    ["Split XYZ scale sliders"] = "拆分 XYZ 缩放滑块",
                    ["Search"] = "搜索",
                    ["Reset"] = "重置",
                    ["Random"] = "随机",
                    ["Type"] = "类型",
                    ["Color"] = "颜色",
                    ["Folders"] = "文件夹",
                    ["Create"] = "创建",
                    ["Create folder"] = "创建文件夹",
                    ["Rename"] = "重命名",
                    ["Close"] = "关闭",
                    ["Clear items"] = "清空项目",
                    ["Delete folder"] = "删除文件夹",
                    ["Add favorite"] = "添加收藏",
                    ["Remove favorite"] = "移除收藏",
                    ["System"] = "系统",
                    ["Settings"] = "设置",
                    ["Use Mouse Wheel in Sliders"] = "使用鼠标滚轮调整滑块",
                    ["UI Scale"] = "界面缩放",
                    ["Load"] = "加载",
                    ["Delete"] = "删除",
                    ["Save"] = "保存",
                    ["Save New"] = "另存为新卡",
                    ["Overwrite coordinate"] = "覆盖服装卡",
                    ["Overwrite Thumbnail"] = "覆盖缩略图",
                    ["Keep Thumbnail"] = "保留缩略图",
                    ["No coordinate selected"] = "未选择服装卡",
                    ["Refresh"] = "刷新",
                    ["Newest"] = "最新",
                    ["Select clothes folder"] = "选择服装文件夹",
                    ["Load Clothing"] = "加载服装",
                    ["Load Accessories"] = "加载饰品",
                    ["Load All"] = "全部加载",
                    ["Apply"] = "应用",
                    ["Cancel"] = "取消",
                    ["Confirm Delete"] = "确认删除"
                };
            }
            else if (string.Equals(language, "日本語", StringComparison.OrdinalIgnoreCase))
            {
                additions = new Dictionary<string, string>
                {
                    ["Status"] = "ステータス",
                    ["Plugin settings"] = "プラグイン設定",
                    ["Look"] = "視線",
                    ["Camera"] = "カメラ",
                    ["Front"] = "正面",
                    ["Play Pose"] = "ポーズ再生",
                    ["Neck"] = "首",
                    ["Pose"] = "ポーズ",
                    ["Eyebrows"] = "眉",
                    ["Eyes"] = "目",
                    ["Mouth"] = "口",
                    ["Save with translated names"] = "翻訳名で保存",
                    ["Lock Cameralight"] = "カメラライトを固定",
                    ["Advanced BoneMod window"] = "Advanced BoneMod ウィンドウ",
                    ["Blendshape Creator"] = "ブレンドシェイプ作成",
                    ["Coordinate Visibility Rules"] = "コーディネート表示ルール",
                    ["Toggle Backlight"] = "バックライト切替",
                    ["Show height measure bar"] = "身長測定バーを表示",
                    ["Toggle Blinking"] = "まばたき切替",
                    ["Split XYZ scale sliders"] = "XYZスケールを分割",
                    ["Search"] = "検索",
                    ["Reset"] = "リセット",
                    ["Random"] = "ランダム",
                    ["Type"] = "種類",
                    ["Color"] = "色",
                    ["Folders"] = "フォルダー",
                    ["Create"] = "作成",
                    ["Create folder"] = "フォルダー作成",
                    ["Rename"] = "名前変更",
                    ["Close"] = "閉じる",
                    ["Clear items"] = "項目をクリア",
                    ["Delete folder"] = "フォルダー削除",
                    ["Add favorite"] = "お気に入りに追加",
                    ["Remove favorite"] = "お気に入りから削除",
                    ["System"] = "システム",
                    ["Settings"] = "設定",
                    ["Use Mouse Wheel in Sliders"] = "マウスホイールでスライダーを操作",
                    ["UI Scale"] = "UIスケール",
                    ["Load"] = "読込",
                    ["Delete"] = "削除",
                    ["Save"] = "保存",
                    ["Save New"] = "新規保存",
                    ["Overwrite coordinate"] = "コーディネートを上書き",
                    ["Overwrite Thumbnail"] = "サムネイルを更新",
                    ["Keep Thumbnail"] = "サムネイルを維持",
                    ["No coordinate selected"] = "コーディネートが選択されていません",
                    ["Refresh"] = "更新",
                    ["Newest"] = "新しい順",
                    ["Select clothes folder"] = "服フォルダーを選択",
                    ["Load Clothing"] = "服を読込",
                    ["Load Accessories"] = "アクセサリーを読込",
                    ["Load All"] = "すべて読込",
                    ["Apply"] = "適用",
                    ["Cancel"] = "キャンセル",
                    ["Confirm Delete"] = "削除確認"
                };
            }

            if (additions == null)
            {
                return;
            }
            foreach (KeyValuePair<string, string> pair in additions)
            {
                if (!dictionary.TryGetValue(pair.Key, out string existing) ||
                    string.IsNullOrWhiteSpace(existing))
                {
                    dictionary[pair.Key] = pair.Value;
                }
            }
        }

        private static string ResolveLocalizationDictionaryName(string configuredName)
        {
            if (!string.IsNullOrWhiteSpace(configuredName) &&
                !string.Equals(configuredName, "default", StringComparison.OrdinalIgnoreCase))
            {
                return configuredName;
            }

            try
            {
                string configPath = Path.Combine(Paths.ConfigPath, "AutoTranslatorConfig.ini");
                if (!File.Exists(configPath))
                {
                    return "default";
                }

                foreach (string rawLine in File.ReadLines(configPath))
                {
                    string line = rawLine?.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith(";"))
                    {
                        continue;
                    }
                    if (!line.StartsWith("Language=", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string language = line.Substring("Language=".Length).Trim();
                    if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ||
                        language.IndexOf("chinese", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "chinese";
                    }
                    if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ||
                        language.IndexOf("japanese", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "日本語";
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogDebug(
                    "Could not detect AutoTranslator language: " + ex.Message);
            }

            return "default";
        }

        static public string GetDllPath()
        {
            //string dllPath = Path.GetDirectoryName(new Uri(this.GetType().Assembly.CodeBase).AbsolutePath);
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            string dllPath = Path.GetDirectoryName(path);
            return dllPath;
        }

        static public string GetExportCharaPath(byte sex)
        {
            string exportPath = StudioCharaEditor.CharaExportPath.Value;
            string defPath = Path.Combine(Paths.GameRootPath, sex == 0 ? "UserData\\chara\\male" : "UserData\\chara\\female");
            if (exportPath.Contains(StudioCharaEditor.DefaultPathMacro))
            {
                exportPath = exportPath.Replace(StudioCharaEditor.DefaultPathMacro, defPath);
            }
            return exportPath;
        }

        static public string GetExportCoordPath(byte sex)
        {
            string exportPath = StudioCharaEditor.CoordExportPath.Value;
            string defPath = Path.Combine(Paths.GameRootPath, sex == 0 ? "UserData\\coordinate\\male" : "UserData\\coordinate\\female");
            if (exportPath.Contains(StudioCharaEditor.DefaultCoordMacro))
            {
                exportPath = exportPath.Replace(StudioCharaEditor.DefaultCoordMacro, defPath);
            }
            return exportPath;
        }

        static public bool SetCustomBase(ChaControl chaCtrl)
        {
            // check and init CustomBase
            if (Singleton<CustomBase>.Instance == null)
            {
                try
                {
                    CustomBase dummyCustomBase = CharaEditorMgr.Instance.gameObject.AddComponent<CustomBase>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("This is an expected exception for creating a CustomBase in studio: " + ex.Message);
                }

                // re-check
                if (Singleton<CustomBase>.Instance == null)
                {
                    StudioCharaEditor.Logger.LogError("Fail to create CustomBase.");
                    return false;
                }
            }

            try
            {
                Singleton<CustomBase>.Instance.chaCtrl = chaCtrl;
                return true;
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger.LogError("Fail to set CustomBase.chaCtrl: " + ex.Message);
                return false;
            }
        }

        static public bool SetMakerApiInsideMaker(bool insideMaker)
        {
            try
            {
                //KKAPI.Maker.MakerAPI.InsideMaker = insideMaker;
                Traverse makerApiCls = Traverse.CreateWithType("KKAPI.Maker.MakerAPI, HS2API");
                Traverse insideMakerField = makerApiCls.Field("_insideMaker");
                if (!insideMakerField.FieldExists())
                {
                    Console.WriteLine("_insideMaker not found in KKAPI.Maker.MakerAPI");
                    return false;
                }
                insideMakerField.SetValue(insideMaker);
                Console.WriteLine("Set KKAPI.Maker.MakerAPI._insideMaker = {0}", insideMaker);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("This is an expected exception for set MakerAPI.InsiderMaker in studio: " + ex.Message);
                return false;
            }
        }
    }
}
