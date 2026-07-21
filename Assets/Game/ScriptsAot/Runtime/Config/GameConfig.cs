using UnityEngine;
using July.Audio;
using July.UI;

namespace GameTemplate.Aot
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "JulyGF/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("UI")]
        public UIConfig UI = UIConfig.Default;

        [Header("音频")]
        public AudioConfig Audio = AudioConfig.Default;

        [Header("Tip")]
        public TipConfig Tip = TipConfig.Default;
    }
}
