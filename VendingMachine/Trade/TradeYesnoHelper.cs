using Dalamud.Memory;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.Throttlers;
using ECommons.UIHelpers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace VendingMachine.Trade;

internal static unsafe class TradeYesnoHelper
{
    private static string? cachedTradeYesnoText;

    public static bool AnyTradeExecuteYesnoVisible()
    {
        var found = false;
        ForEachTradeExecuteYesnoAddon(_ =>
        {
            found = true;
            return true;
        });
        return found;
    }

    public static bool TryAcceptTradeExecuteYesno()
    {
        if (!EzThrottler.Throttle("VmSelectYes", 200))
            return false;

        var clicked = false;
        ForEachTradeExecuteYesnoAddon(addon =>
        {
            VmLog.Information($"confirming SelectYesno ({addon.Text})");
            clicked = TryClickYes(addon);
            return clicked;
        });
        return clicked;
    }

    private static unsafe void ForEachTradeExecuteYesnoAddon(Func<AddonMaster.SelectYesno, bool> action)
    {
        for (var i = 1; i < 100; i++)
        {
            var ptr = Svc.GameGui.GetAddonByName("SelectYesno", i);
            if (ptr == 0)
                break;

            var master = new AddonMaster.SelectYesno(ptr);
            if (!master.Base->IsVisible)
                continue;

            var text = ReadPromptText((AddonSelectYesno*)master.Base);
            if (!IsTradeExecuteYesnoText(text))
                continue;

            if (action(master))
                break;
        }
    }

    private static string ReadPromptText(AddonSelectYesno* addon)
    {
        if (addon->PromptText != null)
            return GenericHelpers.ReadSeString(&addon->PromptText->NodeText).ExtractText();

        var baseAddon = (AtkUnitBase*)addon;
        var textNode = baseAddon->UldManager.NodeList[15]->GetAsAtkTextNode();
        return textNode == null
            ? string.Empty
            : MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
    }

    private static bool TryClickYes(AddonMaster.SelectYesno yesno)
    {
        var addon = (AddonSelectYesno*)yesno.Base;
        var yesButton = addon->YesButton;
        if (yesButton == null)
        {
            VmLog.Warning("SelectYesno YesButton is null.");
            return TryClickYesViaCallback(yesno.Base);
        }

        ForceEnableButton(yesButton);

        if (yesButton->IsEnabled && yesButton->AtkResNode->IsVisible())
        {
            yesButton->ClickAddonButton(yesno.Base);
            if (!yesno.Base->IsVisible)
                return true;
        }

        if (TryClickYesViaCallback(yesno.Base))
            return !yesno.Base->IsVisible;

        // Last resort: click even when the game reports disabled.
        yesButton->ClickAddonButton(yesno.Base);
        return !yesno.Base->IsVisible;
    }

    private static bool TryClickYesViaCallback(AtkUnitBase* addon)
    {
        try
        {
            Callback.Fire(addon, true, 0);
            return true;
        }
        catch (Exception ex)
        {
            VmLog.Debug($"SelectYesno Callback.Fire(0) failed: {ex.Message}");
            return false;
        }
    }

    private static void ForceEnableButton(AtkComponentButton* button)
    {
        if (button == null)
            return;

        if (button->IsEnabled)
            return;

        var flagsPtr = (ushort*)&button->AtkComponentBase.OwnerNode->AtkResNode.NodeFlags;
        *flagsPtr ^= 1 << 5;
    }

    public static bool IsTradeExecuteYesnoText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        cachedTradeYesnoText ??= Svc.Data.GetExcelSheet<Addon>()!.GetRow(102223).Text.ExtractText();
        if (text == cachedTradeYesnoText)
            return true;

        if (text.Contains("トレードを実行", StringComparison.Ordinal))
            return true;

        if (text.Contains("この内容でトレード", StringComparison.Ordinal))
            return true;

        return text.Contains("execute the trade", StringComparison.OrdinalIgnoreCase)
               || text.Contains("proceed with the trade", StringComparison.OrdinalIgnoreCase);
    }
}
