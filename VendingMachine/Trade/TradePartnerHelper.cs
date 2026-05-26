using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace VendingMachine.Trade;

public static unsafe class TradePartnerHelper
{
    public static IPlayerCharacter? GetTradePartner() => TradeDetectionManager.GetTradePartner();

    public static string? TryResolvePartnerName(ulong contentId, uint entityId)
    {
        if (contentId == 0 && entityId == 0)
            return null;

        return Svc.Objects.OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.Struct()->ContentId == contentId || p.EntityId == entityId)
            ?.Name.ToString();
    }

    public static void CancelTrade()
    {
        if (!Svc.Condition[ConditionFlag.TradeOpen])
            return;

        InventoryManager.Instance()->RefuseTrade();
        PluginLog.Information("Vending Machine: canceled trade (gil over 1,000,000 limit).");
    }

    public static bool TryRequestTradeWithPartner(ulong contentId, uint entityId)
    {
        var player = Svc.Objects.OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.Struct()->ContentId == contentId || p.EntityId == entityId);

        if (player == null)
        {
            PluginLog.Warning("Vending Machine: split payment partner is not nearby; waiting.");
            return false;
        }

        if (Svc.Condition[ConditionFlag.TradeOpen])
            return false;

        Svc.Targets.Target = player;
        InventoryManager.Instance()->SendTradeRequest(player.EntityId);
        PluginLog.Information($"Vending Machine: sent trade request to {player.Name} for remaining gil payment.");
        return true;
    }
}
