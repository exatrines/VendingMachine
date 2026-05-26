namespace VendingMachine.Trade;

public sealed unsafe partial class BuyTradeLogic
{
    private bool ShouldStartGilSync(BuyTradeAmounts amounts)
    {
        if (phase is BuyPhase.WaitingOpponentConfirm or BuyPhase.ReadyToConfirm or BuyPhase.AwaitingNextTrade)
            return false;

        if (amounts.PayTargetGil <= 0 || gilSyncPending || TradeTask.IsActive)
            return false;

        if (lastSyncedOfferSnapshot == offerSnapshot && lastSyncedPayTarget == amounts.PayTargetGil)
            return false;

        return !IsMyGilAtTarget(amounts.PayTargetGil);
    }

    private void StartGilSync(int targetGil, bool lockAfter)
    {
        if (targetGil <= 0)
            return;

        if ((TradeTask.IsActive || gilSyncPending) && gilSyncTargetGil == targetGil && lockSelfAfterGilSync == lockAfter)
            return;

        if (TradeTask.IsActive || gilSyncPending)
        {
            VmLog.Information($"superseding gil sync {gilSyncTargetGil:N0} → {targetGil:N0}.");
            Tasks.Abort();
            gilSyncPending = false;
        }

        gilSyncGeneration++;
        var gen = gilSyncGeneration;
        gilSyncTargetGil = targetGil;
        gilAppliedInCurrentChain = false;
        lockSelfAfterGilSync = lockAfter;
        payTargetGil = targetGil;
        gilPaidThisTrade = targetGil;
        gilSyncPending = true;
        phase = BuyPhase.SyncingGil;
        TradeTask.ConfirmAllowed = false;

        VmLog.Information($"syncing gil to {targetGil:N0} (gen {gen}).");

        Tasks.Enqueue(() => GilStep(gen, TradeTask.WaitUntilTradeOpen), TaskTimeoutMs);
        Tasks.Enqueue(() => GilStep(gen, TradeTask.OpenGilInput), TaskTimeoutMs);
        Tasks.Enqueue(() => { if (!IsGilSyncCurrent(gen)) return true; TradeTask.BeginNumericWait(); return true; }, TaskTimeoutMs);
        Tasks.Enqueue(() => GilStep(gen, TradeTask.WaitForNumericPrompt), TaskTimeoutMs);
        Tasks.Enqueue(() => GilStep(gen, () => ApplyGil(targetGil)), TaskTimeoutMs, $"ApplyGil {targetGil}");
        Tasks.Enqueue(() => GilStep(gen, TradeTask.WaitUntilNumericClosed), TaskTimeoutMs);
        Tasks.Enqueue(() => FinishGilSync(gen, targetGil), TaskTimeoutMs, "GilSyncDone");
    }

    private bool IsGilSyncCurrent(int gen) => gen == gilSyncGeneration;

    private bool? GilStep(int gen, Func<bool?> step) => IsGilSyncCurrent(gen) ? step() : true;

    private bool? ApplyGil(int targetGil)
    {
        var ok = TradeTask.ApplyNumericQuantity(targetGil);
        if (ok == true)
            gilAppliedInCurrentChain = true;

        return ok;
    }

    private bool? FinishGilSync(int gen, int targetGil)
    {
        if (!IsGilSyncCurrent(gen))
            return true;

        gilSyncPending = false;

        if (gilAppliedInCurrentChain)
        {
            lastSyncedOfferSnapshot = offerSnapshot;
            lastSyncedPayTarget = targetGil;
            VmLog.Information($"gil sync complete — {targetGil:N0}.");
        }
        else if (EzThrottler.Throttle("VmGilSyncNoNumeric", 5000))
        {
            VmLog.Warning(
                $"gil sync failed (InputNumeric did not apply {targetGil:N0}). Will retry when offer changes.");
        }

        if (lockSelfAfterGilSync && TradeAddonReader.IsOpponentTradeLocked()
            && (gilAppliedInCurrentChain || IsMyGilAtTarget(targetGil)))
            phase = BuyPhase.AwaitingSelfLock;
        else if (phase == BuyPhase.SyncingGil)
            phase = BuyPhase.Negotiating;

        lockSelfAfterGilSync = false;
        return true;
    }

    private void FinishGilSyncIfIdle()
    {
        if (!gilSyncPending || TradeTask.IsActive)
            return;

        gilSyncPending = false;
        if (phase == BuyPhase.SyncingGil)
            phase = BuyPhase.Negotiating;
    }

    private static bool IsMyGilAtTarget(int targetGil)
    {
        if (targetGil <= 0)
            return true;

        TradeAddonReader.TryGetTradeAddon(out var addon);
        return TradeAddonReader.IsMyOfferedGilInTrade(addon, targetGil);
    }
}
