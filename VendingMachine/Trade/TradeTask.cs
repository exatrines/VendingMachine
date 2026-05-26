using ECommons.Automation;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendingMachine.Trade;

internal static unsafe class TradeTask
{
    private const int NumericWaitMs = 5_000;
    private const int TradeSlotWaitMs = 5_000;

    private static long numericWaitDeadlineMs;
    private static long tradeSlotWaitDeadlineMs;
    private static int pendingOfferSlotsBefore;

    internal static bool IsActive => Tasks.IsBusy;

    internal static volatile bool ConfirmAllowed;

    public static int MaxGil => TradeLimits.MaxGilPerTrade;

    internal static bool GenericThrottle(bool rethrottle = false) =>
        FrameThrottler.Throttle("VmTaskThrottle", 4, rethrottle);

    internal static bool? WaitUntilTradeOpen() => Svc.Condition[ConditionFlag.TradeOpen];

    internal static bool? WaitUntilTradeNotOpen() => !Svc.Condition[ConditionFlag.TradeOpen];

    internal static int GetMyTradeSlotCount()
    {
        var invCount = TradeAddonReader.CountMyTradeSlotsFromInventory();
        if (invCount > 0)
            return invCount;

        if (TradeAddonReader.TryGetTradeAddon(out var addon))
            return TradeAddonReader.CountMyTradeSlotsFromAddon(addon);

        return 0;
    }

    internal static void ResetTradeSlotWait() => tradeSlotWaitDeadlineMs = 0;

    internal static bool? WaitForSlotCountAbove(int slotsBeforeOffer)
    {
        var count = GetMyTradeSlotCount();
        if (count > slotsBeforeOffer)
        {
            tradeSlotWaitDeadlineMs = 0;
            return true;
        }

        if (tradeSlotWaitDeadlineMs == 0)
            tradeSlotWaitDeadlineMs = Environment.TickCount64 + TradeSlotWaitMs;

        if (Environment.TickCount64 >= tradeSlotWaitDeadlineMs)
        {
            tradeSlotWaitDeadlineMs = 0;
            VmLog.Warning(
                $"trade slot wait timed out (before={slotsBeforeOffer}, now={count}, need +1 slot).");
            return false;
        }

        return false;
    }

    internal static bool IsNumericOpen() =>
        TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) && IsAddonReady(addon);

    internal static void BeginNumericWait() =>
        numericWaitDeadlineMs = Environment.TickCount64 + NumericWaitMs;

    internal static void SetPendingOfferSlotsBefore(int slotsBefore) =>
        pendingOfferSlotsBefore = slotsBefore;

    internal static int GetPendingOfferSlotsBefore() => pendingOfferSlotsBefore;

    internal static bool? WaitForNumericPrompt()
    {
        if (IsNumericOpen())
            return true;

        if (Environment.TickCount64 >= numericWaitDeadlineMs)
        {
            VmLog.Warning("InputNumeric did not appear.");
            return false;
        }

        return false;
    }

    internal static bool? WaitForNumericOrItemPlaced(int slotsBeforeOffer)
    {
        if (IsNumericOpen())
            return true;

        if (GetMyTradeSlotCount() > slotsBeforeOffer)
            return true;

        if (Environment.TickCount64 >= numericWaitDeadlineMs)
        {
            VmLog.Warning(
                $"no InputNumeric and no new trade slot after offer (slots {slotsBeforeOffer} -> {GetMyTradeSlotCount()}).");
            return false;
        }

        return false;
    }

    internal static bool? ApplyItemQuantityIfNeeded(int quantity, int slotsBeforeOffer)
    {
        if (IsNumericOpen())
            return ApplyNumericQuantity(quantity);

        if (GetMyTradeSlotCount() > slotsBeforeOffer)
            return true;

        return false;
    }

    internal static bool? ApplyNumericQuantity(int num)
    {
        if (num < 0 || num > MaxGil)
            throw new ArgumentOutOfRangeException(nameof(num));

        if (!IsNumericOpen())
            return false;

        if (!GenericThrottle() || !EzThrottler.Throttle("VmApplyNumeric", 100))
            return false;

        if (!TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) || !IsAddonReady(addon))
            return false;

        var input = new AddonMaster.InputNumeric((nint)addon);
        input.Ok(num);
        input.Ok();
        return true;
    }

    internal static bool? WaitUntilNumericClosed()
    {
        if (IsNumericOpen())
            return false;

        return true;
    }

    internal static bool? PauseBetweenOffers()
    {
        if (IsNumericOpen())
            return false;

        return EzThrottler.Throttle("VmPostOfferPause", 400);
    }

    internal static bool? OpenGilInput()
    {
        if (TryGetAddonByName<AtkUnitBase>("Trade", out var addon) && IsAddonReady(addon))
        {
            if (GenericThrottle())
            {
                Callback.Fire(addon, true, 2, Callback.ZeroAtkValue);
                return true;
            }
        }
        else
        {
            GenericThrottle(true);
        }

        return false;
    }
}
