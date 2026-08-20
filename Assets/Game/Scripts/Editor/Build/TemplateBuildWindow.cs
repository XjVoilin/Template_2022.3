using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Build
{
    public sealed class TemplateBuildWindow : EditorWindow
    {
        private static readonly string[] PlatformValues = { "auto", "editor", "wechat", "douyin" };
        private static readonly string[] PlatformLabels =
        {
            "自动（根据当前集成宏）", "编辑器", "微信小游戏", "抖音小游戏"
        };

        private TemplateBuildSettings _settings;
        private Vector2 _scrollPosition;
        private bool _upload;
        private bool _allowOverwrite;
        private bool _hasUnsavedChanges;
        private string _statusMessage;
        private MessageType _statusType;

        [MenuItem("JulyGF/构建/构建面板", false, 50)]
        public static void ShowWindow()
        {
            var window = GetWindow<TemplateBuildWindow>();
            window.titleContent = new GUIContent("JulyGF 构建");
            window.minSize = new Vector2(440f, 560f);
            window.Show();
        }

        private void OnEnable() => ReloadSettings();

        private void OnGUI()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox(
                    $"无法读取构建配置：{TemplateBuildSettings.RelativePath}", MessageType.Error);
                if (GUILayout.Button("重新加载")) ReloadSettings();
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("项目构建", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此面板负责模板工程的资源、HybridCLR、WebGL Player 和发布清单构建；" +
                "不包含小游戏盒子导出流程。",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            DrawTargetSettings();
            EditorGUILayout.Space(10f);
            DrawPipelineSettings();
            EditorGUILayout.Space(10f);
            DrawPublishSettings();
            EditorGUILayout.Space(12f);
            DrawActions();

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTargetSettings()
        {
            EditorGUILayout.LabelField("目标与版本", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            var platformIndex = Array.IndexOf(PlatformValues, _settings.platform);
            if (platformIndex < 0) platformIndex = 0;
            platformIndex = EditorGUILayout.Popup("目标平台", platformIndex, PlatformLabels);
            _settings.platform = PlatformValues[platformIndex];
            _settings.environment = EditorGUILayout.TextField("发布环境", _settings.environment);
            _settings.coreVersion = EditorGUILayout.TextField("核心版本", _settings.coreVersion);
            _settings.contentVersion = EditorGUILayout.TextField("内容版本", _settings.contentVersion);

            var activePlatform = PlatformProfileMenu.ResolveActivePlatform();
            EditorGUILayout.LabelField("当前集成宏", GetPlatformLabel(activePlatform),
                EditorStyles.miniLabel);

            MarkChangedIfNeeded();
        }

        private void DrawPipelineSettings()
        {
            EditorGUILayout.LabelField("构建步骤", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            _settings.generateLuban = EditorGUILayout.ToggleLeft("生成 Luban 配置表",
                _settings.generateLuban);
            _settings.generateHybridClr = EditorGUILayout.ToggleLeft("生成 HybridCLR 产物",
                _settings.generateHybridClr);
            _settings.buildPlayer = EditorGUILayout.ToggleLeft("全量构建时生成 WebGL Player",
                _settings.buildPlayer);
            _settings.developmentBuild = EditorGUILayout.ToggleLeft("Development Build",
                _settings.developmentBuild);

            MarkChangedIfNeeded();
        }

        private void DrawPublishSettings()
        {
            EditorGUILayout.LabelField("产物与发布", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            _settings.packageName = EditorGUILayout.TextField("YooAsset 包名", _settings.packageName);
            _settings.outputRoot = EditorGUILayout.TextField("产物根目录", _settings.outputRoot);
            _allowOverwrite = EditorGUILayout.ToggleLeft("允许覆盖同版本产物", _allowOverwrite);
            _upload = EditorGUILayout.ToggleLeft("构建完成后上传 COS", _upload);
            if (_upload)
            {
                EditorGUI.indentLevel++;
                _settings.coscliPath = EditorGUILayout.TextField("coscli 路径", _settings.coscliPath);
                _settings.cosBucket = EditorGUILayout.TextField("COS Bucket", _settings.cosBucket);
                _settings.cosPrefix = EditorGUILayout.TextField("COS 路径前缀", _settings.cosPrefix);
                EditorGUI.indentLevel--;
            }

            MarkChangedIfNeeded();
        }

        private void DrawActions()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isCompiling ||
                                                EditorApplication.isUpdating))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("保存设置", GUILayout.Height(28f))) SaveSettings();
                    if (GUILayout.Button("重新加载", GUILayout.Height(28f))) ReloadSettings();
                }

                if (_hasUnsavedChanges)
                    EditorGUILayout.HelpBox("设置尚未保存；执行构建时会自动保存。", MessageType.Warning);

                EditorGUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("执行全量构建", GUILayout.Height(42f))) RunBuild(false);
                    if (GUILayout.Button("执行热更构建", GUILayout.Height(42f))) RunBuild(true);
                }
            }

            EditorGUILayout.HelpBox(
                "全量构建：生成完整 HybridCLR/AOT、YooAsset 内容，并按设置生成 WebGL Player。\n" +
                "热更构建：复用对应核心版本的 AOT 基线，只更新热更 DLL 和 YooAsset 内容。",
                MessageType.None);
        }

        private void RunBuild(bool contentOnly)
        {
            SaveSettings();
            var request = new TemplateBuildRequest
            {
                Platform = _settings.platform,
                Environment = _settings.environment,
                CoreVersion = _settings.coreVersion,
                ContentVersion = _settings.contentVersion,
                ContentOnly = contentOnly,
                Upload = _upload,
                Interactive = true,
                AllowOverwrite = _allowOverwrite,
            };

            try
            {
                var result = TemplateBuildPipeline.Run(request);
                if (result.Succeeded)
                {
                    SetStatus(contentOnly ? "热更构建完成。" : "全量构建完成。", MessageType.Info);
                }
                else if (result.Outcome == July.Build.BuildOutcome.Cancelled)
                {
                    SetStatus("构建已取消。", MessageType.Warning);
                }
                else
                {
                    SetStatus($"构建失败：[{result.FailedStep}] {result.Error}", MessageType.Error);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus($"构建异常：{exception.Message}", MessageType.Error);
            }
        }

        private void ReloadSettings()
        {
            try
            {
                _settings = TemplateBuildSettings.Load();
                _hasUnsavedChanges = false;
                _statusMessage = null;
            }
            catch (Exception exception)
            {
                _settings = null;
                _statusMessage = exception.Message;
                _statusType = MessageType.Error;
            }
            Repaint();
        }

        private void SaveSettings()
        {
            if (_settings == null) return;
            _settings.Save();
            _hasUnsavedChanges = false;
            SetStatus($"设置已保存到 {TemplateBuildSettings.RelativePath}", MessageType.Info);
        }

        private void MarkChangedIfNeeded()
        {
            if (EditorGUI.EndChangeCheck()) _hasUnsavedChanges = true;
        }

        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
            Repaint();
        }

        private static string GetPlatformLabel(string platform)
        {
            switch (platform)
            {
                case "wechat": return "微信小游戏";
                case "douyin": return "抖音小游戏";
                default: return "编辑器";
            }
        }
    }
}
