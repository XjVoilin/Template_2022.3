using System.Threading;
using Cysharp.Threading.Tasks;
using July.Bootstrap;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Aot
{
    /// <summary>种子工程的最小启动画面；具体项目可以替换表现，保留启动视图契约。</summary>
    public sealed class LaunchView : MonoBehaviour, IBootstrapView
    {
        private TMP_Text _message;
        private TMP_Text _progressText;
        private TMP_Text _actionText;
        private RectTransform _progressFill;
        private Button _actionButton;
        private UniTaskCompletionSource<LaunchFailureAction> _pendingFailure;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasObject.transform, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f);

            _message = CreateText("Message", background.transform, 100, "Starting...", 42);
            _progressText = CreateText("Progress", background.transform, -30, "0%", 30);
            var bar = CreateRect("ProgressBar", background.transform, new Vector2(640, 10), -90);
            bar.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.23f, 0.3f);
            _progressFill = CreateRect("Fill", bar, Vector2.zero, 0);
            _progressFill.anchorMin = Vector2.zero;
            _progressFill.anchorMax = new Vector2(0, 1);
            _progressFill.offsetMin = _progressFill.offsetMax = Vector2.zero;
            _progressFill.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.75f, 0.9f);

            var button = CreateRect("Action", background.transform, new Vector2(400, 100), -240);
            var image = button.gameObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.36f, 0.5f);
            _actionButton = button.gameObject.AddComponent<Button>();
            _actionButton.targetGraphic = image;
            _actionText = CreateText("Label", button, 0, "Retry", 36);
            _actionButton.gameObject.SetActive(false);

            // 启动失败可能发生在 UISystem 初始化前；随后 UISystem 会接管这个事件系统。
            var events = new GameObject("LaunchEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }

        public void SetStepInfo(int completed, int total)
        {
            var progress = (float)completed / total;
            _progressFill.anchorMax = new Vector2(progress, 1);
            _progressText.text = $"{progress:P0}";
        }

        public async UniTask<LaunchFailureAction> ShowFailureAsync(LaunchFailure failure, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var action = failure.CanRetry ? LaunchFailureAction.Retry : LaunchFailureAction.Restart;
            var completion = new UniTaskCompletionSource<LaunchFailureAction>();
            _pendingFailure = completion;
            _message.text = failure.CanRetry ? "Connection failed. Please check your network." : "Unable to start. Please restart the game.";
            _actionText.text = failure.CanRetry ? "Retry" : "Restart";
            _actionButton.onClick.AddListener(OnClick);
            _actionButton.gameObject.SetActive(true);
            using var cancellation = ct.Register(() => completion.TrySetCanceled(ct));
            try { return await completion.Task; }
            finally
            {
                _pendingFailure = null;
                // 应用退出可能先销毁视图，再完成等待的取消。
                if (_actionButton != null)
                {
                    _actionButton.onClick.RemoveListener(OnClick);
                    _actionButton.gameObject.SetActive(false);
                    _message.text = "Starting...";
                }
            }
            void OnClick() => completion.TrySetResult(action);
        }

        public void PrepareForGame() { } // 画面已在 Awake 中保留，跨越首个业务场景切换。
        public void Complete()
        {
            SetStepInfo(1, 1);
            Destroy(gameObject);
        }

        private void OnDestroy() => _pendingFailure?.TrySetCanceled();

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, float y)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0, y);
            return rect;
        }

        private static TMP_Text CreateText(string name, Transform parent, float y, string value, int size)
        {
            var text = CreateRect(name, parent, new Vector2(880, 100), y).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }
    }
}
