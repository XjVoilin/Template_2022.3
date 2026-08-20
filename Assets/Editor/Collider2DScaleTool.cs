using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Collider2D 统一缩放工具。
    /// 支持 PolygonCollider2D / BoxCollider2D / CircleCollider2D / CapsuleCollider2D / EdgeCollider2D。
    /// 右键任意 Collider2D 组件 → 缩放碰撞体 打开。
    /// </summary>
    public sealed class Collider2DScaleTool : EditorWindow
    {
        private Collider2D _target;
        private float _scale = 1f;

        // 各类型原始数据
        private Vector2[][] _polyPaths;
        private Vector2 _boxSize;
        private float _circleRadius;
        private Vector2 _capsuleSize;
        private Vector2[] _edgePoints;

        [MenuItem("CONTEXT/PolygonCollider2D/缩放碰撞体...")]
        [MenuItem("CONTEXT/BoxCollider2D/缩放碰撞体...")]
        [MenuItem("CONTEXT/CircleCollider2D/缩放碰撞体...")]
        [MenuItem("CONTEXT/CapsuleCollider2D/缩放碰撞体...")]
        [MenuItem("CONTEXT/EdgeCollider2D/缩放碰撞体...")]
        private static void OpenFromContext(MenuCommand cmd)
        {
            var collider = cmd.context as Collider2D;
            if (collider == null) return;

            var window = GetWindow<Collider2DScaleTool>("Collider Scale");
            window.minSize = new Vector2(280, 120);
            window.maxSize = new Vector2(400, 120);
            window.Init(collider);
        }

        private void Init(Collider2D collider)
        {
            _target = collider;
            _scale = 1f;

            switch (collider)
            {
                case PolygonCollider2D poly:
                    _polyPaths = new Vector2[poly.pathCount][];
                    for (int i = 0; i < poly.pathCount; i++)
                        _polyPaths[i] = poly.GetPath(i);
                    break;
                case BoxCollider2D box:
                    _boxSize = box.size;
                    break;
                case CircleCollider2D circle:
                    _circleRadius = circle.radius;
                    break;
                case CapsuleCollider2D capsule:
                    _capsuleSize = capsule.size;
                    break;
                case EdgeCollider2D edge:
                    _edgePoints = edge.points;
                    break;
            }
        }

        private void OnGUI()
        {
            if (_target == null)
            {
                EditorGUILayout.HelpBox("请通过右键 Collider2D → 缩放碰撞体 打开", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{_target.gameObject.name}  ({_target.GetType().Name})",
                EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _scale = EditorGUILayout.Slider("缩放比例", _scale, 0.5f, 1.5f);
            if (EditorGUI.EndChangeCheck())
                ApplyScale();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("重置", GUILayout.Height(24)))
            {
                _scale = 1f;
                ApplyScale();
            }

            if (GUILayout.Button("确认并关闭", GUILayout.Height(24)))
            {
                SaveToPrefab();
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyScale()
        {
            if (_target == null) return;
            Undo.RecordObject(_target, "Scale Collider2D");

            switch (_target)
            {
                case PolygonCollider2D poly:
                    if (_polyPaths == null) break;
                    for (int i = 0; i < _polyPaths.Length; i++)
                    {
                        var src = _polyPaths[i];
                        var dst = new Vector2[src.Length];
                        for (int j = 0; j < src.Length; j++)
                            dst[j] = src[j] * _scale;
                        poly.SetPath(i, dst);
                    }
                    break;
                case BoxCollider2D box:
                    box.size = _boxSize * _scale;
                    break;
                case CircleCollider2D circle:
                    circle.radius = _circleRadius * _scale;
                    break;
                case CapsuleCollider2D capsule:
                    capsule.size = _capsuleSize * _scale;
                    break;
                case EdgeCollider2D edge:
                    if (_edgePoints == null) break;
                    var scaledEdge = new Vector2[_edgePoints.Length];
                    for (int j = 0; j < _edgePoints.Length; j++)
                        scaledEdge[j] = _edgePoints[j] * _scale;
                    edge.points = scaledEdge;
                    break;
            }

            EditorUtility.SetDirty(_target);
        }

        private void SaveToPrefab()
        {
            if (_target == null) return;

            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(_target.gameObject);
            if (prefabRoot != null)
            {
                PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.UserAction);
                Debug.Log($"[ColliderScale] 已保存到 Prefab: {_target.gameObject.name}, scale={_scale:F2}");
            }
            else
            {
                EditorUtility.SetDirty(_target);
                Debug.Log($"[ColliderScale] 已应用: {_target.gameObject.name}, scale={_scale:F2}");
            }
        }
    }
}
