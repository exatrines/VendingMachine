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
public sealed unsafe partial class BuyTradeLogic
{
    private const int TaskTimeoutMs = 15_000;

    private readonly BuyPaymentSession session = new();

    private BuyPhase phase = BuyPhase.Idle;
    private long nextTradeRequestMs;

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
    private bool resetOfferLockClick;
    private bool loggedUnreadableOpponentSlots;
    private List<TradeItemStack> offerItems = [];
    private int offerGil;
    private int peakRunningTotalGil;
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
        var amounts = BuyTradeAmounts.Compute(session, C.BuyEntries);
        lastAmounts = amounts;
        ApplyAmountsToSession(opponentItems, amounts);

        if (snapshot != offerSnapshot)
        {
            offerSnapshot = snapshot;
            VmLog.Debug($"opponent offer changed → {snapshot}");
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
}
