using ECommons.GameFunctions;

namespace VendingMachine.Trade;

public enum BuyPhase
{
    Idle,
    Negotiating,
    SyncingGil,
    AwaitingSelfLock,
    WaitingOpponentConfirm,
    ReadyToConfirm,
    AwaitingNextTrade,
}

/// <summary>
/// Buy: watch opponent 5 slots, sync gil when offer changes, lock when opponent locks.
/// Split payments across trades (max 1M gil per trade) use <see cref="BuyPaymentSession"/>.
/// </summary>
public sealed unsafe class BuyTradeLogic
{
    private const int TaskTimeoutMs = 15_000;

    private readonly BuyPaymentSession session = new();

    // --- Session (persists across trades with same partner) ---
    private BuyPhase phase = BuyPhase.Idle;
    private long nextTradeRequestMs;

    // --- Current trade window ---
    private bool opponentWasLocked;
    private int payTargetGil;
    private int gilPaidThisTrade;
    private bool gilSyncPending;
    private bool lockSelfAfterGilSync;
    private int gilSyncGeneration;
    private int gilSyncTargetGil;
    private bool gilAppliedInCurrentChain;
    private string offerSnapshot = "";
    private string lastSyncedOfferSnapshot = "";
    private int lastSyncedPayTarget = -1;
    private bool offerLockClicked;
    private bool resetOfferLockClick;
    private bool loggedUnreadableOpponentSlots;
    private List<TradeAddonReader.OpponentItemStack> offerItems = [];
    private int offerGil;
    private BuyTradeAmounts lastAmounts;

    public bool HasActiveBuySession => session.Active;
    public bool IsPaymentSessionActive => session.Active && session.UnpaidGil > 0;

    public void Reset() => ResetSession();

    public bool ConsumeTradeOfferClickReset()
    {
        if (!resetOfferLockClick)
            return false;

        resetOfferLockClick = false;
        return true;
    }

    public void Update(bool tradeOpen)
    {
        if (!tradeOpen)
        {
            HandleTradeClosed();
            return;
        }

        if (phase is BuyPhase.Idle or BuyPhase.AwaitingNextTrade)
            BeginTradeWindow();

        var opponentItems = TradeAddonReader.GetOpponentItems();
        WarnIfOpponentSlotsVisibleButUnreadable(opponentItems);

        var snapshot = TradeAddonReader.SerializeOpponentTradeSlots();
        var amounts = ComputeAmounts();
        lastAmounts = amounts;
        ApplyAmountsToSession(opponentItems, amounts);

        if (snapshot != offerSnapshot)
        {
            offerSnapshot = snapshot;
            PluginLog.Debug($"Vending Machine: opponent offer changed → {snapshot}");
        }

        var opponentLocked = TradeAddonReader.IsOpponentTradeLocked();
        var selfLocked = TradeAddonReader.IsLocalTradeLocked();
        HandleOpponentLockTransitions(opponentItems, opponentLocked, selfLocked);

        FinishGilSyncIfIdle();

        if (!opponentLocked && !selfLocked && ShouldStartGilSync(amounts))
            StartGilSync(amounts.PayTargetGil, lockAfter: false);

        if (!TradeTask.IsActive)
            UpdateReadyToConfirm(opponentLocked);
    }

    // --- Trade window lifecycle ---

    private void BeginTradeWindow()
    {
        EnsureSession();
        ResetTradeWindowState();

        session.CurrentTotalGil = session.TotalGil;
        offerSnapshot = "";
        lastSyncedOfferSnapshot = "";
        lastSyncedPayTarget = -1;
        phase = BuyPhase.Negotiating;

        PluginLog.Information(
            $"Vending Machine: buy trade opened — total {session.TotalGil:N0}, paid {session.PaidGil:N0}, prior unpaid {Math.Max(0, session.TotalGil - session.PaidGil):N0}.");
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
        PluginLog.Information($"Vending Machine: buy payment session started with {session.PartnerName}.");
    }

    // --- Amounts (per-slot pricing via PriceCalculator) ---

    private readonly record struct BuyTradeAmounts(int OfferGil, int PriorUnpaidGil, int CombinedGil, int PayTargetGil);

    private BuyTradeAmounts ComputeAmounts()
    {
        var offer = PriceCalculator.CalculateBuyTotal(C.BuyEntries);
        var priorUnpaid = Math.Max(0, session.TotalGil - session.PaidGil);
        var combined = offer + priorUnpaid;
        var payTarget = combined <= 0 ? 0 : Math.Min(combined, TradeLimits.MaxGilPerTrade);
        return new BuyTradeAmounts(offer, priorUnpaid, combined, payTarget);
    }

    private void ApplyAmountsToSession(List<TradeAddonReader.OpponentItemStack> opponentItems, BuyTradeAmounts amounts)
    {
        session.CurrentTotalGil = session.TotalGil + amounts.OfferGil;
        payTargetGil = amounts.PayTargetGil;

        if (opponentItems.Count > 0)
        {
            offerItems = opponentItems
                .Select(s => new TradeAddonReader.OpponentItemStack { ItemId = s.ItemId, Quantity = s.Quantity })
                .ToList();
            offerGil = amounts.OfferGil;
        }
    }

    // --- Opponent lock / unlock ---

    private void HandleOpponentLockTransitions(
        List<TradeAddonReader.OpponentItemStack> opponentItems,
        bool opponentLocked,
        bool selfLocked)
    {
        if (!opponentLocked && opponentWasLocked)
        {
            opponentWasLocked = false;
            OnOpponentUnlocked(selfLocked);
            return;
        }

        if (!opponentLocked || opponentWasLocked)
            return;

        opponentWasLocked = true;
        PluginLog.Information("Vending Machine: opponent locked — preparing to lock.");
        OnOpponentLocked(opponentItems);
    }

    private void OnOpponentUnlocked(bool selfLocked)
    {
        if (phase is BuyPhase.WaitingOpponentConfirm or BuyPhase.ReadyToConfirm)
        {
            payTargetGil = 0;
            gilPaidThisTrade = 0;
            phase = BuyPhase.Negotiating;
            TradeTask.ConfirmAllowed = false;
            resetOfferLockClick = true;
            offerLockClicked = false;
            PluginLog.Debug("Vending Machine: opponent unlocked — back to negotiating.");
            return;
        }

        if (!selfLocked)
        {
            phase = BuyPhase.Negotiating;
            lastSyncedOfferSnapshot = "";
            lastSyncedPayTarget = -1;
            PluginLog.Debug("Vending Machine: opponent unlocked — negotiating.");
        }
    }

    private void OnOpponentLocked(List<TradeAddonReader.OpponentItemStack> opponentItems)
    {
        var amounts = ComputeAmounts();
        ApplyAmountsToSession(opponentItems, amounts);
        gilPaidThisTrade = amounts.PayTargetGil;

        if (amounts.PayTargetGil <= 0)
        {
            phase = BuyPhase.AwaitingSelfLock;
            return;
        }

        if (IsMyGilAtTarget(amounts.PayTargetGil)
            && lastSyncedPayTarget == amounts.PayTargetGil
            && lastSyncedOfferSnapshot == offerSnapshot)
        {
            phase = BuyPhase.AwaitingSelfLock;
            return;
        }

        StartGilSync(amounts.PayTargetGil, lockAfter: true);
    }

    // --- Gil sync (TaskManager) ---

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
            PluginLog.Information($"Vending Machine: superseding gil sync {gilSyncTargetGil:N0} → {targetGil:N0}.");
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

        PluginLog.Information($"Vending Machine: syncing gil to {targetGil:N0} (gen {gen}).");

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
            PluginLog.Information($"Vending Machine: gil sync complete — {targetGil:N0}.");
        }
        else if (EzThrottler.Throttle("VmGilSyncNoNumeric", 5000))
        {
            PluginLog.Warning(
                $"Vending Machine: gil sync failed (InputNumeric did not apply {targetGil:N0}). Will retry when offer changes.");
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

    // --- Confirm (TradeController clicks Yesno) ---

    private void UpdateReadyToConfirm(bool opponentLocked)
    {
        if (!TradeAddonReader.IsLocalTradeLocked() || !opponentLocked)
            return;

        if (payTargetGil > 0 && !IsMyGilAtTarget(payTargetGil))
            return;

        if (!TradeAddonReader.IsAwaitingFinalTradeConfirm()
            && !TradeYesnoHelper.AnyTradeExecuteYesnoVisible())
            return;

        if (phase != BuyPhase.ReadyToConfirm)
            PluginLog.Information("Vending Machine: both locked — ready for trade execute SelectYesno.");

        phase = BuyPhase.ReadyToConfirm;
        TradeTask.ConfirmAllowed = true;
    }

    public bool ShouldAcceptFinalConfirm() =>
        phase == BuyPhase.ReadyToConfirm
        && TradeTask.ConfirmAllowed
        && TradeAddonReader.IsLocalTradeLocked()
        && TradeAddonReader.IsOpponentTradeLocked()
        && (TradeYesnoHelper.AnyTradeExecuteYesnoVisible()
            || TradeAddonReader.IsAwaitingFinalTradeConfirm());

    public bool ShouldClickTradeOfferLock()
    {
        if (offerLockClicked || phase != BuyPhase.AwaitingSelfLock)
            return false;

        if (TradeTask.IsActive || gilSyncPending || TradeAddonReader.IsLocalTradeLocked())
            return false;

        if (!TradeAddonReader.IsOpponentTradeLocked())
            return false;

        return payTargetGil <= 0 || IsMyGilAtTarget(payTargetGil);
    }

    public void OnSelfOfferLocked()
    {
        phase = phase is BuyPhase.ReadyToConfirm ? BuyPhase.ReadyToConfirm : BuyPhase.WaitingOpponentConfirm;
        PluginLog.Information("Vending Machine: self locked — waiting for opponent.");
    }

    // --- Trade complete / next trade ---

    public bool TryCompleteTradeAndContinue()
    {
        if (!session.Active)
            return false;

        var paid = gilPaidThisTrade > 0 ? gilPaidThisTrade : payTargetGil;
        if (paid <= 0)
            return false;

        var commitDelta = Math.Max(0, session.CurrentTotalGil - session.TotalGil);
        if (commitDelta > 0)
            session.TotalGil += commitDelta;

        if (offerGil > 0 && offerItems.Count > 0)
            session.OrderItems = PriceCalculator.MergeBuyOrderItems(session.OrderItems, offerItems);

        session.PaidGil += paid;
        ResetTradeWindowState();

        PluginLog.Information(
            $"Vending Machine: trade complete — total {session.TotalGil:N0}, paid {session.PaidGil:N0}, remaining {session.UnpaidGil:N0}.");

        if (session.UnpaidGil > 0)
        {
            phase = BuyPhase.AwaitingNextTrade;
            nextTradeRequestMs = Environment.TickCount64 + 1500;
            PluginLog.Information("Vending Machine: unpaid remains — requesting next trade.");
            return true;
        }

        PluginLog.Information("Vending Machine: buy payment session complete.");
        return false;
    }

    private void TryRequestNextTrade()
    {
        if (!session.Active || session.UnpaidGil <= 0)
            return;

        if (Environment.TickCount64 < nextTradeRequestMs)
            return;

        if (!EzThrottler.Throttle("VmBuyTradeRequest", 2000))
            return;

        if (session.PartnerContentId == 0 && session.PartnerEntityId == 0)
        {
            PluginLog.Warning("Vending Machine: payment partner unknown; ending session.");
            ResetSession();
            return;
        }

        TradePartnerHelper.TryRequestTradeWithPartner(session.PartnerContentId, session.PartnerEntityId);
        nextTradeRequestMs = Environment.TickCount64 + 3000;
    }

    public TradeSession? BuildCompletedSession()
    {
        if (!session.Active || session.OrderItems.Count == 0)
            return null;

        return new TradeSession
        {
            Mode = TradeMode.Buy,
            Lines = PriceCalculator.BuildBuyResultLines(session.OrderItems, C.BuyEntries),
            TotalGil = session.TotalGil,
        };
    }

    public BuySplitPaymentDebugInfo GetSplitPaymentDebugInfo()
    {
        if (!session.Active)
            return new BuySplitPaymentDebugInfo { Active = false };

        var partnerName = session.PartnerName;
        if (string.IsNullOrEmpty(partnerName))
        {
            partnerName = TradePartnerHelper.TryResolvePartnerName(session.PartnerContentId, session.PartnerEntityId)
                ?? TradePartnerHelper.GetTradePartner()?.Name.ToString();
        }

        var a = lastAmounts;
        return new BuySplitPaymentDebugInfo
        {
            Active = true,
            PartnerName = partnerName,
            TotalGil = session.TotalGil,
            CurrentTotalGil = session.CurrentTotalGil,
            PaidGil = session.PaidGil,
            RemainingGil = session.UnpaidGil,
            OfferGil = a.OfferGil,
            PriorUnpaidGil = a.PriorUnpaidGil,
            CombinedGil = a.CombinedGil,
            ThisTradeTargetGil = a.PayTargetGil,
            Phase = phase,
        };
    }

    // --- Reset ---

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
        offerLockClicked = false;
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
        TradeTask.ConfirmAllowed = false;
    }

    private void WarnIfOpponentSlotsVisibleButUnreadable(List<TradeAddonReader.OpponentItemStack> opponentItems)
    {
        if (loggedUnreadableOpponentSlots || opponentItems.Count > 0)
            return;

        if (TradeAddonReader.TryGetTradeAddon(out var addon)
            && TradeAddonReader.CountOpponentTradeSlotsFromAddon(addon) > 0)
        {
            PluginLog.Warning("Vending Machine: opponent trade slots are visible but item data could not be read.");
        }

        loggedUnreadableOpponentSlots = true;
    }
}
