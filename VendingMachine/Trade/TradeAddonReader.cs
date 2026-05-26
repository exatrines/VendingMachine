using Dalamud.Memory;
using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendingMachine.Trade;

public static unsafe class TradeAddonReader
{
    public static bool TryGetTradeAddon(out AtkUnitBase* addon)
    {
        addon = null;
        return TryGetAddonByName("Trade", out addon) && IsAddonReady(addon) && IsTradeReady(addon);
    }

    /// <summary>True when a player trade window is open (condition flag or Trade addon visible).</summary>
    public static bool IsTradeSessionActive() =>
        Svc.Condition[ConditionFlag.TradeOpen] || IsTradeAddonVisible();

    /// <summary>True when the Trade addon exists and is visible.</summary>
    public static bool IsTradeAddonVisible()
    {
        if (!TryGetAddonByName("Trade", out AtkUnitBase* addon) || addon == null)
            return false;

        return addon->IsVisible;
    }

    /// <summary>Trade addon for debug UI when visible (does not require ready/complete state).</summary>
    public static bool TryGetTradeAddonForDebug(out AtkUnitBase* addon)
    {
        addon = null;
        if (!TryGetAddonByName("Trade", out addon) || addon == null)
            return false;

        return addon->IsVisible;
    }

    public static bool IsTradeReady(AtkUnitBase* addon)
    {
        if (addon == null)
            return false;

        const int readyProbeNodeId = 31;
        if (readyProbeNodeId < 0 || readyProbeNodeId >= addon->UldManager.NodeListCount)
            return true;

        var node = addon->UldManager.NodeList[readyProbeNodeId];
        if (node == null)
            return true;

        var componentNode = node->GetAsAtkComponentNode();
        if (componentNode == null || componentNode->Component == null)
            return true;

        var uld = componentNode->Component->UldManager;
        if (uld.NodeListCount == 0)
            return true;

        var child = uld.NodeList[0];
        if (child == null)
            return true;

        var imageNode = child->GetAsAtkImageNode();
        if (imageNode == null)
            return true;

        return imageNode->AtkResNode.Color.A == 0xFF;
    }

    public static TradeState GetLocalTradeState() => InventoryManager.Instance()->TradeLocalState;

    public static TradeState GetRemoteTradeState() => InventoryManager.Instance()->TradeRemoteState;

    public static bool IsOpponentTradeLocked() =>
        GetRemoteTradeState() is TradeState.LockedIn or TradeState.WaitingForConfirmation or TradeState.Confirmed;

    public static bool IsLocalTradeLocked() =>
        GetLocalTradeState() is TradeState.LockedIn or TradeState.WaitingForConfirmation or TradeState.Confirmed;

    public static int CountMyTradeSlotsFromAddon(AtkUnitBase* addon) =>
        CountVisibleTradeSlots(addon, mySlots: true);

    public static int CountOpponentTradeSlotsFromAddon(AtkUnitBase* addon) =>
        CountVisibleTradeSlots(addon, mySlots: false);

    private static int CountVisibleTradeSlots(AtkUnitBase* addon, bool mySlots)
    {
        var count = 0;
        var baseIndex = mySlots ? 10 : 15;
        for (var i = 0; i < 5; i++)
        {
            var slotNode = addon->UldManager.NodeList[baseIndex + i];
            if (slotNode == null)
                continue;

            var componentNode = slotNode->GetAsAtkComponentNode();
            if (componentNode == null || componentNode->Component == null)
                continue;

            var uld = componentNode->Component->UldManager;
            if (uld.NodeListCount == 0)
                continue;

            var iconNode = uld.NodeList[0];
            if (iconNode != null && iconNode->IsVisible())
                count++;
        }

        return count;
    }

    public static int CountMyTradeSlotsFromInventory()
    {
        var count = 0;
        var tradeItems = InventoryManager.Instance()->TradeItemsLocal;
        for (var i = 0; i < 5; i++)
        {
            if (tradeItems[i].GetItemId() != 0)
                count++;
        }

        return count;
    }

    public static uint GetMyTradeSlotItemId(int slotIndex)
    {
        GetTradeSlotFromMemory(local: true, slotIndex, out var itemId, out _, out _);
        return itemId;
    }

    public static uint GetOpponentTradeSlotItemId(int slotIndex)
    {
        GetTradeSlotFromMemory(local: false, slotIndex, out var itemId, out _, out _);
        return itemId;
    }

    public static void GetTradeSlotFromMemory(bool local, int slotIndex, out uint itemId, out uint quantity, out bool isHq)
    {
        itemId = 0;
        quantity = 0;
        isHq = false;
        if (slotIndex is < 0 or > 4)
            return;

        var items = local
            ? InventoryManager.Instance()->TradeItemsLocal
            : InventoryManager.Instance()->TradeItemsRemote;

        var rawId = items[slotIndex].GetItemId();
        if (rawId == 0)
            return;

        isHq = rawId >= 1_000_000;
        itemId = rawId % 1_000_000;
        quantity = items[slotIndex].GetQuantity();
    }

    public static bool CanConfirmTrade(AtkUnitBase* addon) =>
        IsTradeReady(addon) || TradeTask.ConfirmAllowed;

    public static bool IsAwaitingFinalTradeConfirm() =>
        GetLocalTradeState() is TradeState.WaitingForConfirmation or TradeState.Confirmed
        && GetRemoteTradeState() is TradeState.WaitingForConfirmation or TradeState.Confirmed;

    public static bool TryClickTradeOfferButton(AtkUnitBase* addon)
    {
        foreach (var nodeIndex in new[] { 3, 4, 2, 5, 6 })
        {
            if (TryClickTradeOfferButtonAtNode(addon, nodeIndex))
            {
                PluginLog.Debug($"Vending Machine: clicked trade offer button (node {nodeIndex}).");
                return true;
            }
        }

        PluginLog.Warning("Vending Machine: could not click trade offer button (条件提示).");
        return false;
    }

    private static bool TryClickTradeOfferButtonAtNode(AtkUnitBase* addon, int nodeIndex)
    {
        var node = addon->UldManager.NodeList[nodeIndex];
        if (node == null)
            return false;

        var componentNode = node->GetAsAtkComponentNode();
        if (componentNode == null)
            return false;

        var button = (AtkComponentButton*)componentNode->Component;
        if (button == null || !button->AtkResNode->IsVisible())
            return false;

        if (!button->IsEnabled)
        {
            var flagsPtr = (ushort*)&button->AtkComponentBase.OwnerNode->AtkResNode.NodeFlags;
            *flagsPtr ^= 1 << 5;
        }

        button->ClickAddonButton(addon);
        return true;
    }

    /// <summary>Local gil input button (Atk Inspector: Trade node 13).</summary>
    private const int MyGilButtonNodeId = 13;

    /// <summary>Remote player's offered gil (Trade addon node 6; may be a component tree).</summary>
    public static uint GetOpponentGil(AtkUnitBase* addon) => ReadOpponentGilFromTradeUi(addon);

    /// <summary>Local player's offered gil shown on the Trade addon (not wallet total).</summary>
    public static uint GetMyGil(AtkUnitBase* addon) => ReadMyOfferedGilFromTrade(addon);

    /// <summary>TradeItemsLocal slot 5 only — never HandIn (that can reflect wallet gil).</summary>
    public static uint GetMyTradeGilFromInventory()
    {
        var tradeItems = InventoryManager.Instance()->TradeItemsLocal;
        if (tradeItems.Length < 6)
            return 0;

        var gilSlot = tradeItems[5];
        if (gilSlot.GetItemId() == 0)
            return 0;

        var qty = gilSlot.GetQuantity();
        return qty <= TradeLimits.MaxGilPerTrade ? qty : 0;
    }

    /// <summary>Read offered gil from Trade UI; falls back to trade memory when UI is empty.</summary>
    public static uint ReadMyOfferedGilFromTrade(AtkUnitBase* addon)
    {
        var fromGilInput = ReadMyGilFromGilInputOnly(addon);
        if (fromGilInput > 0)
            return fromGilInput;

        return GetMyTradeGilFromInventory();
    }

    /// <summary>My gil from the gil input field only (node 13 / ButtonTextNode), never item slot quantities.</summary>
    public static uint ReadMyGilFromGilInputOnly(AtkUnitBase* addon) => ReadMyGilFromGilInputUi(addon);

    public static bool IsMyOfferedGilInTrade(AtkUnitBase* addon, int payTarget)
    {
        if (payTarget <= 0)
            return true;

        return ReadMyOfferedGilBestEffort(addon) == (uint)payTarget;
    }

    /// <summary>Trade memory first (reliable after InputNumeric), then gil button UI.</summary>
    public static uint ReadMyOfferedGilBestEffort(AtkUnitBase* addon)
    {
        var fromInv = GetMyTradeGilFromInventory();
        if (fromInv > 0)
            return fromInv;

        return addon != null ? ReadMyGilFromGilInputOnly(addon) : 0;
    }

    private static uint ReadMyGilFromGilInputUi(AtkUnitBase* addon)
    {
        var fromChild = TryParseGilFromGilInputChild(addon, MyGilButtonNodeId, 2);
        if (fromChild > 0)
            return fromChild;

        var button = addon->GetComponentButtonById(MyGilButtonNodeId);
        if (button != null)
        {
            var fromButton = TryParseGilFromButton(button);
            if (fromButton > 0)
                return fromButton;
        }

        return 0;
    }

    /// <summary>Opponent gil from gil display nodes (31, 6) or trade memory — not item quantities.</summary>
    public static uint ReadOpponentGilFromGilInputOnly(AtkUnitBase* addon)
    {
        var inv = GetOpponentTradeGilFromInventory();
        if (inv > 0)
            return inv;

        var node31 = TryParseGilFromDirectTextNode(addon, 31);
        if (node31 > 0 && node31 <= TradeLimits.MaxGilPerTrade)
            return node31;

        var node6 = TryParseGilFromDirectTextNode(addon, 6);
        if (node6 > 0 && node6 <= TradeLimits.MaxGilPerTrade)
            return node6;

        return 0;
    }

    private static uint ReadOpponentGilFromTradeUi(AtkUnitBase* addon) => ReadOpponentGilFromGilInputOnly(addon);

    private static uint TryParseGilFromGilInputChild(AtkUnitBase* addon, int parentNodeId, int childIndex)
    {
        if (parentNodeId < 0 || parentNodeId >= addon->UldManager.NodeListCount)
            return 0;

        var parent = addon->UldManager.NodeList[parentNodeId];
        if (parent == null)
            return 0;

        var componentNode = parent->GetAsAtkComponentNode();
        if (componentNode == null || componentNode->Component == null)
            return 0;

        var uld = componentNode->Component->UldManager;
        if (childIndex < 0 || childIndex >= uld.NodeListCount)
            return 0;

        var child = uld.NodeList[childIndex];
        if (child == null)
            return 0;

        var textNode = child->GetAsAtkTextNode();
        return textNode == null ? 0 : ParseGilFromTextNode(textNode);
    }

    private static uint TryParseGilFromDirectTextNode(AtkUnitBase* addon, int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= addon->UldManager.NodeListCount)
            return 0;

        var node = addon->UldManager.NodeList[nodeIndex];
        if (node == null)
            return 0;

        var textNode = node->GetAsAtkTextNode();
        return textNode == null ? 0 : ParseGilFromTextNode(textNode);
    }

    private static bool IsPlausibleTradeOfferGil(uint value, uint opponentUiGil) =>
        value > 0
        && value <= TradeLimits.MaxGilPerTrade
        && value != opponentUiGil;

    public static uint GetOpponentTradeGilFromInventory()
    {
        var tradeItems = InventoryManager.Instance()->TradeItemsRemote;
        if (tradeItems.Length < 6)
            return 0;

        var gilSlot = tradeItems[5];
        return gilSlot.GetItemId() == 0 ? 0 : gilSlot.GetQuantity();
    }

    /// <summary>Debug breakdown for gil-in-trade waits.</summary>
    public static (uint Offered, uint Inventory, uint UiNode5, uint UiNode6, uint UiNode13, uint OpponentUiNode6, uint OpponentInventory) ReadMyGilDiagnostics(
        AtkUnitBase* addon) =>
    (
        ReadMyOfferedGilFromTrade(addon),
        GetMyTradeGilFromInventory(),
        TryParseGilFromAddonNode(addon, 5),
        TryParseGilFromAddonNode(addon, 6),
        TryParseGilFromAddonNode(addon, MyGilButtonNodeId),
        GetOpponentGil(addon),
        GetOpponentTradeGilFromInventory());

    public static uint TryParseGilFromAddonNode(AtkUnitBase* addon, int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= addon->UldManager.NodeListCount)
            return 0;

        var node = addon->UldManager.NodeList[nodeIndex];
        return node == null ? 0 : TryParseGilFromResNode(node);
    }

    private static uint TryParseGilFromResNode(AtkResNode* node, int depth = 0)
    {
        if (node == null || depth > 6)
            return 0;

        var textNode = node->GetAsAtkTextNode();
        if (textNode != null)
            return ParseGilFromTextNode(textNode);

        var componentNode = node->GetAsAtkComponentNode();
        if (componentNode == null || componentNode->Component == null)
            return 0;

        if (componentNode->Component->GetComponentType() == ComponentType.Button)
        {
            var fromButton = TryParseGilFromButton((AtkComponentButton*)componentNode->Component);
            if (fromButton > 0)
                return fromButton;
        }

        var uld = componentNode->Component->UldManager;
        for (var i = 0; i < uld.NodeListCount; i++)
        {
            var child = uld.NodeList[i];
            var gil = TryParseGilFromResNode(child, depth + 1);
            if (gil > 0)
                return gil;
        }

        return 0;
    }

    private static uint TryParseGilFromButton(AtkComponentButton* button)
    {
        if (button == null)
            return 0;

        var textNode = button->ButtonTextNode;
        return textNode == null ? 0 : ParseGilFromTextNode(textNode);
    }

    private static uint ParseGilFromTextNode(AtkTextNode* textNode)
    {
        var text = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
        return ParseGilText(text);
    }

    private static uint ParseGilText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        Span<char> digits = stackalloc char[text.Length];
        var count = 0;
        foreach (var c in text)
        {
            if (c is >= '0' and <= '9')
                digits[count++] = c;
            else if (c is >= '\uFF10' and <= '\uFF19')
                digits[count++] = (char)('0' + (c - '\uFF10'));
        }

        return count == 0 || !uint.TryParse(digits[..count], out var gil) ? 0 : gil;
    }

    public sealed class OpponentItemStack
    {
        public uint ItemId { get; init; }
        public int Quantity { get; init; }
    }

    /// <summary>Opponent offer aggregated from the five trade slots (TradeItemsRemote).</summary>
    public static List<OpponentItemStack> GetOpponentItems() => AggregateOpponentTradeSlots();

    /// <summary>Snapshot of all five opponent trade slots (for change detection).</summary>
    public static string SerializeOpponentTradeSlots()
    {
        var parts = new string[5];
        for (var i = 0; i < 5; i++)
        {
            GetTradeSlotFromMemory(local: false, i, out var itemId, out var quantity, out var isHq);
            parts[i] = $"{i}:{itemId}:{quantity}:{(isHq ? 1 : 0)}";
        }

        return string.Join("|", parts);
    }

    private static List<OpponentItemStack> AggregateOpponentTradeSlots()
    {
        var merged = new Dictionary<uint, int>();
        for (var i = 0; i < 5; i++)
        {
            GetTradeSlotFromMemory(local: false, i, out var itemId, out var quantity, out _);
            if (itemId == 0 || quantity == 0)
                continue;

            merged[itemId] = merged.GetValueOrDefault(itemId) + (int)quantity;
        }

        return ToStacks(merged);
    }

    private static List<OpponentItemStack> ToStacks(Dictionary<uint, int> merged) =>
        merged.Select(kv => new OpponentItemStack { ItemId = kv.Key, Quantity = kv.Value }).ToList();
}
