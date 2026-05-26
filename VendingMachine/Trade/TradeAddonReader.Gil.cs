using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendingMachine.Trade;

public static unsafe partial class TradeAddonReader
{
    /// <summary>Local gil input button (Atk Inspector: Trade node 13).</summary>
    private const int MyGilButtonNodeId = 13;

    public static uint GetOpponentGil(AtkUnitBase* addon) => ReadOpponentGilFromTradeUi(addon);

    public static uint GetMyGil(AtkUnitBase* addon) => ReadMyOfferedGilFromTrade(addon);

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

    public static uint ReadMyOfferedGilFromTrade(AtkUnitBase* addon)
    {
        var fromGilInput = ReadMyGilFromGilInputOnly(addon);
        if (fromGilInput > 0)
            return fromGilInput;

        return GetMyTradeGilFromInventory();
    }

    public static uint ReadMyGilFromGilInputOnly(AtkUnitBase* addon) => ReadMyGilFromGilInputUi(addon);

    public static bool IsMyOfferedGilInTrade(AtkUnitBase* addon, int payTarget)
    {
        if (payTarget <= 0)
            return true;

        return ReadMyOfferedGilBestEffort(addon) == (uint)payTarget;
    }

    public static uint ReadMyOfferedGilBestEffort(AtkUnitBase* addon)
    {
        var fromInv = GetMyTradeGilFromInventory();
        if (fromInv > 0)
            return fromInv;

        return addon != null ? ReadMyGilFromGilInputOnly(addon) : 0;
    }

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

    public static uint GetOpponentTradeGilFromInventory()
    {
        var tradeItems = InventoryManager.Instance()->TradeItemsRemote;
        if (tradeItems.Length < 6)
            return 0;

        var gilSlot = tradeItems[5];
        return gilSlot.GetItemId() == 0 ? 0 : gilSlot.GetQuantity();
    }

    public static uint TryParseGilFromAddonNode(AtkUnitBase* addon, int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= addon->UldManager.NodeListCount)
            return 0;

        var node = addon->UldManager.NodeList[nodeIndex];
        return node == null ? 0 : TryParseGilFromResNode(node);
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
}
