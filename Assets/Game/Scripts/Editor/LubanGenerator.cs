using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CozyYard.Editor
{
    /// <summary>
    /// Luban 配置表 Editor 工具 —— 通用版。
    /// 自动发现 DataTables/Datas 下的模块结构（扁平或分模块），
    /// 解析 macOS/Windows 上的 dotnet 路径，一键生成 C# + JSON。
    /// 
    /// 约定：
    ///   Tools/Luban/Luban/Luban.dll          — Luban 可执行 DLL
    ///   Tools/Luban/DataTables/Datas/         — 扁平模式：__tables__.xlsx + 数据 xlsx 直接放此目录
    ///   Tools/Luban/DataTables/Datas/ModuleA/ — 分模块模式：各子模块平等，无特殊目录
    ///   Tools/Luban/DataTables/Datas/ModuleB/
    ///
    /// 输出：
    ///   Assets/Game/Res/Configs/              — JSON 数据（扁平模式或各模块统一输出）
    ///   Assets/Game/Scripts/Runtime/Generated/Configs/ — C# 代码 + TablesExt.cs
    /// </summary>
    public class LubanGeneratorWindow : EditorWindow
    {
        private const string LubanDll = "Tools/Luban/Luban/Luban.dll";
        private const string DataTablesRoot = "Tools/Luban/DataTables";
        private const string DatasDir = "Datas";

        private List<string> _modules = new();
        private bool _isFlatLayout;
        private Vector2 _scrollPos;

        private static GUIStyle _headerStyle;
        private static GUIStyle _sectionLabelStyle;
        private static GUIStyle _moduleButtonStyle;
        private static GUIStyle _statusBarStyle;

        private static bool? _prerequisitesValid;

        [MenuItem("JulyGF/配置表/生成窗口", priority = 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<LubanGeneratorWindow>("Luban 配置表");
            window.minSize = new Vector2(300, 200);
        }

        [MenuItem("JulyGF/配置表/生成全部", priority = 11)]
        public static void MenuGenerateAll() => GenerateAll();

        private void OnEnable() => RefreshModules();

        private void RefreshModules()
        {
            _isFlatLayout = DetectFlatLayout();
            _modules = _isFlatLayout ? new List<string>() : DiscoverModules();
        }

        private static void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 6, 6),
            };

            _sectionLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11,
                padding = new RectOffset(2, 0, 2, 2),
            };

            _moduleButtonStyle = new GUIStyle("Button")
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 8, 4, 4),
                fixedHeight = 24,
            };

            _statusBarStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 10,
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            const float pad = 8f;

            EditorGUILayout.Space(4);
            GUILayout.Label("Luban 配置表生成", _headerStyle);
            DrawSeparator();
            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(pad);
                using (new EditorGUILayout.VerticalScope())
                {
                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.35f, 0.75f, 0.35f, 1f);
                    if (GUILayout.Button("生成全部", GUILayout.Height(32)))
                        GenerateAll();
                    GUI.backgroundColor = prevBg;
                }
                GUILayout.Space(pad);
            }

            if (!_isFlatLayout && _modules.Count > 0)
            {
                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(pad);
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label($"模块（{_modules.Count}）", _sectionLabelStyle);

                        using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPos, EditorStyles.helpBox))
                        {
                            _scrollPos = scroll.scrollPosition;
                            for (var i = 0; i < _modules.Count; i++)
                            {
                                if (i % 2 == 1) DrawRowBackground();
                                if (GUILayout.Button(_modules[i], _moduleButtonStyle))
                                    GenerateSingle(_modules[i]);
                            }
                        }
                    }
                    GUILayout.Space(pad);
                }
            }

            GUILayout.FlexibleSpace();
            DrawSeparator();
            var layoutDesc = _isFlatLayout ? "扁平模式" : $"{_modules.Count} 个模块";
            GUILayout.Label($"  {layoutDesc}  |  配置表路径: {DataTablesRoot}", _statusBarStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新", GUILayout.Width(50), GUILayout.Height(20)))
                {
                    _prerequisitesValid = null;
                    RefreshModules();
                }
                GUILayout.Space(4);
            }
            EditorGUILayout.Space(2);
        }

        #region Public API

        public static bool GenerateAll()
        {
            if (!CheckPrerequisitesCached()) return false;

            try
            {
                var confPath = Path.Combine(ProjectRoot, DataTablesRoot, "luban.conf");
                if (File.Exists(confPath))
                {
                    EditorUtility.DisplayProgressBar("Luban", "使用 luban.conf 生成...", 0.5f);
                    if (!GenerateWithConf(confPath)) return false;
                }
                else
                {
                    var isFlat = DetectFlatLayout();
                    if (isFlat)
                    {
                        EditorUtility.DisplayProgressBar("Luban", "生成中...", 0.5f);
                        if (!GenerateFlat()) return false;
                    }
                    else
                    {
                        var modules = DiscoverModules();
                        for (var i = 0; i < modules.Count; i++)
                        {
                            var module = modules[i];
                            EditorUtility.DisplayProgressBar("Luban", $"生成 {module}... ({i + 1}/{modules.Count})",
                                (float)i / modules.Count);
                            if (!GenerateModule(module)) return false;
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            Debug.Log("[Luban] 生成完成");
            return true;
        }

        private static bool GenerateWithConf(string confPath)
        {
            const string jsonOut = "Assets/Game/Res/Configs";
            const string codeOut = "Assets/Game/Scripts/Runtime/Generated/Configs";
            const string topModule = "cfg";

            var success = RunLuban(confPath, jsonOut, codeOut, useConfDirectly: true);
            if (success)
            {
                var codeOutAbs = Path.GetFullPath(Path.Combine(ProjectRoot, codeOut));
                GenerateTablesExt(codeOutAbs, topModule);
            }
            return success;
        }

        public static bool ValidatePrerequisites()
        {
            var dllPath = Path.Combine(ProjectRoot, LubanDll);
            if (!File.Exists(dllPath))
            {
                Debug.LogError($"[Luban] 找不到 Luban.dll: {dllPath}");
                return false;
            }

            var dotnet = ResolveDotnet();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = dotnet,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                ApplyDotnetEnvironment(psi);

                using var p = Process.Start(psi);
                var stderrTask = p?.StandardError.ReadToEndAsync();
                var stdout = p?.StandardOutput.ReadToEnd()?.Trim();
                p?.WaitForExit();
                var stderr = stderrTask?.Result?.Trim();
                if (p == null || p.ExitCode != 0)
                {
                    var detail = !string.IsNullOrEmpty(stderr) ? $"\n{stderr}" : "";
                    Debug.LogError($"[Luban] dotnet 运行时不可用 (路径: {dotnet})，请安装 .NET SDK{detail}");
                    return false;
                }

                Debug.Log($"[Luban] dotnet {stdout} 已就绪 ({dotnet})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Luban] 无法启动 dotnet (路径: {dotnet})，请安装 .NET SDK\n{ex.Message}");
                return false;
            }

            return true;
        }

        #endregion

        #region Generation

        private static void GenerateSingle(string module)
        {
            if (!CheckPrerequisitesCached()) return;
            try
            {
                EditorUtility.DisplayProgressBar("Luban", $"生成 {module}...", 0.5f);
                if (GenerateModule(module)) AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool GenerateFlat()
        {
            const string jsonOut = "Assets/Game/Res/Configs";
            const string codeOut = "Assets/Game/Scripts/Runtime/Generated/Configs";
            const string topModule = "cfg";

            var schemaFiles = BuildSchemaFilesFlat();
            if (schemaFiles == null) return false;

            var confPath = WriteConf(topModule, schemaFiles, DatasDir);
            var success = RunLuban(confPath, jsonOut, codeOut);

            if (success)
            {
                var codeOutAbs = Path.GetFullPath(Path.Combine(ProjectRoot, codeOut));
                GenerateTablesExt(codeOutAbs, topModule);
                Debug.Log("[Luban] 生成完成");
            }
            return success;
        }

        private static bool GenerateModule(string module)
        {
            const string jsonOut = "Assets/Game/Res/Configs";
            const string codeOut = "Assets/Game/Scripts/Runtime/Generated/Configs";
            const string topModule = "cfg";
            var dataDir = $"{DatasDir}/{module}";

            var schemaFiles = BuildSchemaFilesModular(module);
            if (schemaFiles == null) return false;

            var confPath = WriteConf(topModule, schemaFiles, dataDir);
            var success = RunLuban(confPath, jsonOut, codeOut);

            if (success)
            {
                var codeOutAbs = Path.GetFullPath(Path.Combine(ProjectRoot, codeOut));
                GenerateTablesExt(codeOutAbs, topModule);
                Debug.Log($"[Luban] {module} 生成完成");
            }
            return success;
        }

        private static bool RunLuban(string confPath, string jsonOutRelative, string codeOutRelative, bool useConfDirectly = false)
        {
            var dllPath = Path.Combine(ProjectRoot, LubanDll);
            var jsonOutAbs = Path.GetFullPath(Path.Combine(ProjectRoot, jsonOutRelative));
            var codeOutAbs = Path.GetFullPath(Path.Combine(ProjectRoot, codeOutRelative));

            EnsureDirectory(jsonOutAbs);
            EnsureDirectory(codeOutAbs);

            var lubanArgs = $"-t all -d json " +
                            $"--conf \"{confPath}\" " +
                            $"-x outputDataDir=\"{jsonOutAbs}\" " +
                            $"-x outputCodeDir=\"{codeOutAbs}\" " +
                            $"-c cs-simple-json";

            var psi = new ProcessStartInfo
            {
                FileName = ResolveDotnet(),
                Arguments = $"\"{dllPath}\" {lubanArgs}",
                WorkingDirectory = Path.Combine(ProjectRoot, DataTablesRoot),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            ApplyDotnetEnvironment(psi);

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    Debug.LogError("[Luban] 无法启动 dotnet 进程");
                    if (!useConfDirectly) CleanTempConf(confPath);
                    return false;
                }

                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                var stderr = stderrTask.Result;

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log($"[Luban] {stdout.TrimEnd()}");

                if (process.ExitCode != 0)
                {
                    Debug.LogError($"[Luban] 生成失败 (exit {process.ExitCode}):\n{stderr}");
                    return false;
                }

                if (!string.IsNullOrEmpty(stderr))
                    Debug.LogWarning($"[Luban] {stderr.TrimEnd()}");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Luban] 无法启动进程: {ex.Message}");
                return false;
            }
            finally
            {
                if (!useConfDirectly) CleanTempConf(confPath);
            }
        }

        #endregion

        #region Layout Detection & Module Discovery

        /// <summary>
        /// 扁平模式：__tables__.xlsx 直接在 Datas/ 下。
        /// 分模块模式：__tables__.xlsx 在 Datas/ 的各子目录下。
        /// </summary>
        private static bool DetectFlatLayout()
        {
            var flatTableFile = Path.Combine(ProjectRoot, DataTablesRoot, DatasDir, "__tables__.xlsx");
            return File.Exists(flatTableFile);
        }

        private static List<string> DiscoverModules()
        {
            var result = new List<string>();
            var datasPath = Path.Combine(ProjectRoot, DataTablesRoot, DatasDir);
            if (!Directory.Exists(datasPath)) return result;

            foreach (var dir in Directory.GetDirectories(datasPath).OrderBy(d => d))
            {
                var name = Path.GetFileName(dir);
                if (File.Exists(Path.Combine(dir, "__tables__.xlsx")))
                    result.Add(name);
            }
            return result;
        }

        #endregion

        #region Schema Files

        private static List<string> BuildSchemaFilesFlat()
        {
            var basePath = Path.Combine(ProjectRoot, DataTablesRoot, DatasDir);
            var tablesFile = Path.Combine(basePath, "__tables__.xlsx");
            if (!File.Exists(tablesFile))
            {
                Debug.LogError("[Luban] 缺少 Datas/__tables__.xlsx");
                return null;
            }

            var files = new List<string> { $"{DatasDir}/__tables__.xlsx" };

            var beansFile = Path.Combine(basePath, "__beans__.xlsx");
            if (File.Exists(beansFile))
                files.Add($"{DatasDir}/__beans__.xlsx");

            var enumsFile = Path.Combine(basePath, "__enums__.xlsx");
            if (File.Exists(enumsFile))
                files.Add($"{DatasDir}/__enums__.xlsx");

            return files;
        }

        private static List<string> BuildSchemaFilesModular(string module)
        {
            var basePath = Path.Combine(ProjectRoot, DataTablesRoot, DatasDir, module);
            var tablesFile = Path.Combine(basePath, "__tables__.xlsx");
            if (!File.Exists(tablesFile))
            {
                Debug.LogError($"[Luban] {module} 缺少 __tables__.xlsx");
                return null;
            }

            var files = new List<string> { $"{DatasDir}/{module}/__tables__.xlsx" };

            var beansFile = Path.Combine(basePath, "__beans__.xlsx");
            if (File.Exists(beansFile))
                files.Add($"{DatasDir}/{module}/__beans__.xlsx");

            var enumsFile = Path.Combine(basePath, "__enums__.xlsx");
            if (File.Exists(enumsFile))
                files.Add($"{DatasDir}/{module}/__enums__.xlsx");

            return files;
        }

        private static string WriteConf(string topModule, List<string> schemaFiles, string dataDir)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"groups\": [");
            sb.AppendLine("    {\"names\":[\"c\"], \"default\":true},");
            sb.AppendLine("    {\"names\":[\"s\"], \"default\":true},");
            sb.AppendLine("    {\"names\":[\"e\"], \"default\":true}");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"schemaFiles\": [");
            for (var i = 0; i < schemaFiles.Count; i++)
            {
                var f = schemaFiles[i];
                var type = f.EndsWith("__tables__.xlsx") ? "table"
                    : f.EndsWith("__beans__.xlsx") ? "bean"
                    : f.EndsWith("__enums__.xlsx") ? "enum"
                    : "";
                var comma = i < schemaFiles.Count - 1 ? "," : "";
                sb.AppendLine($"    {{\"fileName\":\"{f}\", \"type\":\"{type}\"}}{comma}");
            }
            sb.AppendLine("  ],");
            sb.AppendLine($"  \"dataDir\": \"{dataDir}\",");
            sb.AppendLine("  \"targets\": [");
            sb.AppendLine(
                $"    {{\"name\":\"all\", \"manager\":\"Tables\", \"groups\":[\"c\",\"s\",\"e\"], \"topModule\":\"{topModule}\"}}");
            sb.AppendLine("  ]");
            sb.Append("}");

            var confPath = Path.Combine(ProjectRoot, DataTablesRoot, $".luban_temp_{topModule.Replace(".", "_")}.conf");
            File.WriteAllText(confPath, sb.ToString());
            return confPath;
        }

        #endregion

        #region TablesExt Generation

        private struct TablePropInfo
        {
            public string TypeName;
            public string PropName;
            public string LoadKey;
        }

        private static List<TablePropInfo> ParseTablesCs(string codeOutAbs)
        {
            var tablesPath = Path.Combine(codeOutAbs, "Tables.cs");
            if (!File.Exists(tablesPath)) return null;

            var content = File.ReadAllText(tablesPath);
            var props = new List<TablePropInfo>();

            var propMatches = Regex.Matches(content, @"public\s+(\w+)\s+(\w+)\s+\{get;");
            var keyMatches = Regex.Matches(content, @"loader\(""(\w+)""\)");

            for (var i = 0; i < propMatches.Count; i++)
            {
                props.Add(new TablePropInfo
                {
                    TypeName = propMatches[i].Groups[1].Value,
                    PropName = propMatches[i].Groups[2].Value,
                    LoadKey = i < keyMatches.Count
                        ? keyMatches[i].Groups[1].Value
                        : propMatches[i].Groups[2].Value.ToLower()
                });
            }
            return props;
        }

        private static void GenerateTablesExt(string codeOutAbs, string topModule)
        {
            var props = ParseTablesCs(codeOutAbs);
            if (props == null || props.Count == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine();
            sb.AppendLine($"namespace {topModule}");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class Tables");
            sb.AppendLine("    {");

            var names = string.Join(", ", props.Select(p => $"\"{p.LoadKey}\""));
            sb.AppendLine($"        public static readonly string[] TableNames = {{ {names} }};");
            sb.AppendLine();

            sb.AppendLine("        public void RegisterTo(Dictionary<Type, object> registry)");
            sb.AppendLine("        {");
            foreach (var p in props)
                sb.AppendLine($"            registry[typeof({p.TypeName})] = {p.PropName};");
            sb.AppendLine("        }");

            sb.AppendLine("    }");
            sb.AppendLine("}");

            var outPath = Path.Combine(codeOutAbs, "TablesExt.cs");
            File.WriteAllText(outPath, sb.ToString());
        }

        #endregion

        #region dotnet Resolution (macOS + Windows)

        private static string _resolvedDotnet;

        private static string ResolveDotnet()
        {
            if (_resolvedDotnet != null) return _resolvedDotnet;

#if UNITY_EDITOR_OSX
            string[] candidates =
            {
                "/usr/local/share/dotnet/dotnet",
                "/opt/homebrew/bin/dotnet",
                "/usr/local/bin/dotnet",
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".dotnet/dotnet"),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    _resolvedDotnet = c;
                    return c;
                }
            }

            try
            {
                var shell = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/zsh";
                var psi = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = "-lc \"which dotnet\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                var output = p?.StandardOutput.ReadToEnd()?.Trim();
                p?.WaitForExit();
                if (p?.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    _resolvedDotnet = output;
                    return output;
                }
            }
            catch { /* fallback below */ }
#endif
            _resolvedDotnet = "dotnet";
            return "dotnet";
        }

        private static void ApplyDotnetEnvironment(ProcessStartInfo psi)
        {
#if UNITY_EDITOR_OSX
            var dotnetPath = ResolveDotnet();
            if (dotnetPath == "dotnet") return;

            var dotnetRoot = Path.GetDirectoryName(dotnetPath);
            psi.Environment["DOTNET_ROOT"] = dotnetRoot;

            var currentPath = psi.Environment.ContainsKey("PATH") ? psi.Environment["PATH"] : "";
            string[] extraPaths = { dotnetRoot, "/usr/local/bin", "/opt/homebrew/bin" };
            foreach (var ep in extraPaths)
            {
                if (!string.IsNullOrEmpty(ep) && !currentPath.Contains(ep))
                    currentPath = ep + ":" + currentPath;
            }
            psi.Environment["PATH"] = currentPath;
#endif
        }

        #endregion

        #region Helpers

        private static bool CheckPrerequisitesCached()
        {
            if (_prerequisitesValid.HasValue)
                return _prerequisitesValid.Value;
            _prerequisitesValid = ValidatePrerequisites();
            return _prerequisitesValid.Value;
        }

        private static void DrawSeparator()
        {
            var rect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            rect.height = 1f;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f, 0.6f));
        }

        private static void DrawRowBackground()
        {
            var rect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true));
            rect.height = 24f;
            rect.y -= 1f;
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.06f));
        }

        private static void CleanTempConf(string path)
        {
            if (File.Exists(path) && path.Contains("_temp"))
                File.Delete(path);
        }

        private static void EnsureDirectory(string fullPath)
        {
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);
        }

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        #endregion
    }
}
