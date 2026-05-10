using cfg;
using JulyCore;
using JulyCore.Data.UI;

namespace GameTemplate
{
    public class LubanUIWindowConfigProvider : IUIWindowConfigProvider
    {
        public UIOpenOptions GetUIOpenOptions(int uiWindowID)
        {
            var row = GF.Config.GetTable<TbUIWindow>().Get(uiWindowID);
            if (row == null)
            {
                GF.LogWarning($"[GF.UI] TbUIWindow 不存在配置: {uiWindowID}");
                return null;
            }

            return new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(row.Id, row.WindowName),
                Layer = (UILayer)row.UiLayer,
                ClickMaskToClose = row.IsClickBlankQuit,
                OpenAnimationType = (UIAnimationType)row.EnterAnimType,
                CloseAnimationType = (UIAnimationType)row.ExitAnimType,
                ShowMask = row.IsNeedBlackMask,
                IgnoreSafeArea = row.IsIgnoreSafeArea
            };
        }
    }
}
