using GameBox;
using IngameDebugConsole;

public static class DebugCommands
{
    [ConsoleMethod("currency", "查看货币余额")]
    public static void ShowCurrency()
    {
        // var q = JulyArch.GameContext.Instance?.Query<ICurrencyQueries>();
        // UnityEngine.Debug.Log($"余额: {q?.Balance ?? 0}");
    }
}