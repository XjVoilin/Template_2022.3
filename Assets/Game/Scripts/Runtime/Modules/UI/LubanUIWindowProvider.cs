using July.Arch;
using July.Config;
using July.UI;
using cfg;
namespace Game
{
    public class LubanUIWindowProvider : IUIWindowProvider
    {
        public bool TryResolve(int windowId, out UIOpenOptions options)
        {
            var configSystem = ArchContext.Current.GetSystem<IConfigSystem>();
            if (configSystem == null || !configSystem.TryGetTable<TbUIWindow>(out var table))
            {
                options = null;
                return false;
            }

            var row = table.GetOrDefault(windowId);
            if (row == null)
            {
                options = null;
                return false;
            }

            options = new UIOpenOptions
            {
                WindowIdentifier = new WindowIdentifier(row.Id, row.WindowName),
                Layer = (UILayer)row.UiLayer,
                OpenAnimationType = (UIAnimationType)row.EnterAnimType,
                CloseAnimationType = (UIAnimationType)row.ExitAnimType,
                ShowMask = row.IsNeedBlackMask,
                ClickMaskToClose = row.IsClickBlankQuit,
                IgnoreSafeArea = row.IsIgnoreSafeArea,
            };
            return true;
        }
    }
}
