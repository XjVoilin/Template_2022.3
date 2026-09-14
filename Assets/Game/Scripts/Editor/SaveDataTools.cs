using System.IO;
using UnityEditor;
using UnityEngine;

namespace CozyYard.Editor
{
    public static class SaveDataTools
    {
        [MenuItem("JulyGF/存档/打开本地缓存路径")]
        private static void OpenPersistentDataPath()
        {
            var path = Application.persistentDataPath;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("JulyGF/存档/打开旧版文件存档目录")]
        private static void OpenSaveDataPath()
        {
            var path = Path.Combine(Application.persistentDataPath, "Save");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("JulyGF/存档/清除所有存档")]
        private static void DeleteAllSaveData()
        {
            var path = Path.Combine(Application.persistentDataPath, "Save");
            if (!EditorUtility.DisplayDialog("清除存档", "确定要清除当前项目的全部 PlayerPrefs（含存档和偏好设置）及旧版文件存档吗？此操作不可撤销。", "确定", "取消"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Debug.Log("[SaveDataTools] 已清除当前项目的 PlayerPrefs 与旧版文件存档。");
        }
    }
}
