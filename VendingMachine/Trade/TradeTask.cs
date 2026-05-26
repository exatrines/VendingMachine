using ECommons.Automation;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendingMachine.Trade;

internal static unsafe class TradeTask
{
    private const int NumericWaitMs = 3_000;
    private const int TradeSlotWaitMs = 400;

    private static long numericWaitDeadlineMs;
    private static long tradeSlotWaitDeadlineMs;

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

    internal static bool? WaitForTradeSlots(int targetCount)
    {
        if (GetMyTradeSlotCount() >= targetCount)
        {
            tradeSlotWaitDeadlineMs = 0;
            return true;
        }

        if (tradeSlotWaitDeadlineMs == 0)
            tradeSlotWaitDeadlineMs = Environment.TickCount64 + TradeSlotWaitMs;

        if (Environment.TickCount64 >= tradeSlotWaitDeadlineMs)
        {
            tradeSlotWaitDeadlineMs = 0;
            PluginLog.Debug(
                $"Vending Machine: trade slot wait gave up (expected >={targetCount}, detected {GetMyTradeSlotCount()}).");
            return true;
        }

        return false;
    }

    internal static bool IsNumericOpen() =>
        TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) && IsAddonReady(addon);

    internal static void BeginNumericWait() =>
        numericWaitDeadlineMs = Environment.TickCount64 + NumericWaitMs;

    internal static bool? WaitForNumericPrompt()
    {
        if (IsNumericOpen())
            return true;

        if (Environment.TickCount64 >= numericWaitDeadlineMs)
        {
            PluginLog.Warning("Vending Machine: InputNumeric did not appear while setting gil.");
            return false;
        }

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

        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("InputNumeric", 1).Address;
        if (!IsAddonReady(addon))
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

    internal static bool? SetNumericInput(int num) => ApplyNumericQuantity(num);
}
