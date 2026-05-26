using ECommons.GameFunctions;

namespace VendingMachine.Trade;

public sealed unsafe partial class BuyTradeLogic
{
    private void BeginTradeWindow()
    {
        EnsureSession();
        ResetTradeWindowState();

        peakRunningTotalGil = session.TotalGil;
        session.CurrentTotalGil = session.TotalGil;
        offerSnapshot = "";
        lastSyncedOfferSnapshot = "";
        lastSyncedPayTarget = -1;
        phase = BuyPhase.Negotiating;

        VmLog.Information(
            $"buy trade opened — total {session.TotalGil:N0}, paid {session.PaidGil:N0}, prior unpaid {Math.Max(0, session.TotalGil - session.PaidGil):N0}.");
    }

    private void HandleTradeClosed()
    {
        if (phase == BuyPhase.AwaitingNextTrade || (session.Active && session.UnpaidGil > 0))
        {
            TryRequestNextTrade();
            return;
        }

        if (phase != BuyPhase.Idle)
            ResetTradeWindowState();
    }

    private void EnsureSession()
    {
        if (session.Active)
            return;

        var partner = TradePartnerHelper.GetTradePartner();
        if (partner == null)
            return;

        session.Active = true;
        session.PartnerName = partner.Name.ToString();
        session.PartnerContentId = partner.Struct()->ContentId;
        session.PartnerEntityId = partner.EntityId;
        VmLog.Information($"buy payment session started with {session.PartnerName}.");
    }

    private void ResetSession()
    {
        phase = BuyPhase.Idle;
        session.Reset();
        ResetTradeWindowState();
        TradeTask.ConfirmAllowed = false;
    }

    private void ResetTradeWindowState()
    {
        opponentWasLocked = false;
        payTargetGil = 0;
        gilPaidThisTrade = 0;
        gilSyncPending = false;
        lockSelfAfterGilSync = false;
        resetOfferLockClick = false;
        loggedUnreadableOpponentSlots = false;
        offerSnapshot = "";
        lastSyncedOfferSnapshot = "";
        lastSyncedPayTarget = -1;
        gilSyncGeneration = 0;
        gilSyncTargetGil = 0;
        gilAppliedInCurrentChain = false;
        offerItems = [];
        offerGil = 0;
        peakRunningTotalGil = 0;
        TradeTask.ConfirmAllowed = false;
    }

    private void WarnIfOpponentSlotsVisibleButUnreadable(List<TradeItemStack> opponentItems)
    {
        if (loggedUnreadableOpponentSlots || opponentItems.Count > 0)
            return;

        if (TradeAddonReader.TryGetTradeAddon(out var addon)
            && TradeAddonReader.CountOpponentTradeSlotsFromAddon(addon) > 0)
        {
            VmLog.Warning("opponent trade slots are visible but item data could not be read.");
        }

        loggedUnreadableOpponentSlots = true;
    }
}
