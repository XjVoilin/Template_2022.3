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

        [MenuItem("JulyGF/存档/打开存档目录")]
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
            if (!Directory.Exists(path))
            {
                Debug.Log("[SaveDataTools] 存档目录不存在，无需清除");
                return;
            }

            if (!EditorUtility.DisplayDialog("清除存档", "确定要删除所有本地存档数据吗？此操作不可撤销。", "确定", "取消"))
                return;

            Directory.Delete(path, true);
            Debug.Log($"[SaveDataTools] 已清除存档目录: {path}");
        }
    }
}
