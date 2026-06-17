using Xabbo;
using Xabbo.Core;
using Xabbo.Core.Messages.Incoming;
using Xabbo.Core.Tasks;
using Xabbo.Messages.Flash;

namespace TotemEffects.Core;

public class TotemManager(Extension ext)
{
    private readonly Extension _ext = ext;
    private int JungleHeadClassId { get; set; } = -1;
    private int JungleCenterClassId { get; set; } = -1;
    private int JungleBottomClassId { get; set; } = -1;
    private int SantoriniHeadClassId { get; set; } = -1;
    private int SantoriniCenterClassId { get; set; } = -1;
    private int SantoriniBottomClassId { get; set; } = -1;
    private int WiredRepeatClassId { get; set; } = -1;
    private int WiredVariableClassId { get; set; } = -1;

    private string CurrentCenterState { get; set; } = "-1";
    private string CurrentBottomState { get; set; } = "-1";

    private FloorItem? WiredRepeatItem { get; set; } = null;
    private FloorItem? WiredVariableItem { get; set; } = null;

    private IInventoryItem? HeadItem { get; set; } = null;
    private IInventoryItem? CenterItem { get; set; } = null;
    private IInventoryItem? BottomItem { get; set; } = null;

    private int SelectedCombo { get; set; } = 1;
    private bool IsSantorini { get; set; } = false;
    private CancellationTokenSource? cts { get; set; } = null;
    public int LoopDelay { get; set; } = 50;

    private readonly Dictionary<int, (int Head, int Center, int Bottom)> JungleCombos = new()
    {
        { 1, (2, 1, 10) }, // Duck
        { 2, (0, 0, 1) }, // Mystic
        { 3, (2, 2, 11) }, // Leaves
        { 4, (1, 1, 6) }, // Lightning
    };

    private readonly Dictionary<int, (int Head, int Center, int Bottom)> SantoriniCombos = new()
    {
        { 1, (2, 0, 2) }, // Levitation
        { 2, (0, 1, 7) }, // Rain
        { 3, (1, 2, 9) }, // Fire
        { 4, (2, 1, 10) }, // Stick
    };

    private TaskCompletionSource<bool> ReceivedObjectsData = new();

    public void Initialize()
    {
        var furniData = _ext.gameData.Furni;
        if (furniData is null) return;

        int GetClassId(string identifier) =>
            furniData.TryGetInfo(identifier, out var info) ? info.Kind : -1;

        JungleHeadClassId = GetClassId("lt_r26_totem3");
        JungleCenterClassId = GetClassId("lt_r26_totem2");
        JungleBottomClassId = GetClassId("lt_r26_totem1");

        SantoriniHeadClassId = GetClassId("totem_planet");
        SantoriniCenterClassId = GetClassId("totem_head");
        SantoriniBottomClassId = GetClassId("totem_leg");
        WiredRepeatClassId = GetClassId("wf_trg_period_short");
        WiredVariableClassId = GetClassId("wf_act_change_var_val");

        _ext.Intercept(In.ObjectAdd, OnObjectAdd);
        _ext.Intercept(In.AvatarEffectAdded, OnEffectAdded);
    }

    public void SetCombo(int n, bool santorini)
    {
        SelectedCombo = n;
        IsSantorini = santorini;
    }

    public void Start() => _ = RunSequence();
    public void Stop()
    {
        cts?.Cancel();
        Stopped?.Invoke();
    }
    public event Action? Started;
    public event Action? Stopped;
    public event Action? EffectReceived;

    private void OnObjectAdd(Intercept e)
    {
        if (cts is null || cts.IsCancellationRequested) return;
        var msg = e.Packet.Read<FloorItemAddedMsg>();
        var item = msg.Item;

        int headId = IsSantorini ? SantoriniHeadClassId : JungleHeadClassId;
        int centerId = IsSantorini ? SantoriniCenterClassId : JungleCenterClassId;
        int bottomId = IsSantorini ? SantoriniBottomClassId : JungleBottomClassId;

        if (item.Kind == bottomId)
            CurrentBottomState = item.Data.Value;
        else if (item.Kind == centerId)
            CurrentCenterState = item.Data.Value;
        else if (item.Kind == headId)
            ReceivedObjectsData.TrySetResult(true);
        else if (item.Kind == WiredRepeatClassId)
            WiredRepeatItem = item;
        else if (item.Kind == WiredVariableClassId)
            WiredVariableItem = item;
    }

    private void OnEffectAdded(Intercept e)
    {
        if (cts is null || cts.IsCancellationRequested) return;
        e.Block();
        EffectReceived?.Invoke();
    }

    public void ResetState()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = new CancellationTokenSource();
        WiredRepeatItem = null;
        WiredVariableItem = null;
        HeadItem = null;
        CenterItem = null;
        BottomItem = null;
        ReceivedObjectsData = new();
        CurrentCenterState = "-1";
        CurrentBottomState = "-1";
    }

    private async Task CycleToState(IInventoryItem item, string currentState, int targetState, int maxStates)
    {
        if (!int.TryParse(currentState, out int current)) return;
        int steps = (targetState - current + maxStates) % maxStates;
        for (int i = 0; i < steps; i++)
        {
            _ext.Send(Out.UseFurniture, item.Id, 0);
            await Task.Delay(100);
        }
    }

    private async Task<(ICatalogPage Page, ICatalogOffer Offer)?> FindCatalogOffer(ICatalog catalog, string pageName, string furniLine)
    {
        var node = catalog.FindNode(name: pageName);
        if (node == null) return null;
        var page = await new GetCatalogPageTask(_ext, node.Id, "BUILDERS_CLUB").ExecuteAsync();
        if (page == null) return null;
        var offer = page.Offers.FirstOrDefault(x => x.FurniLine == furniLine);
        if (offer == null) return null;
        return (page, offer);
    }

    private async Task PlaceItem(IInventoryItem item, int x, int y, int delayAfter = 100)
    {
        _ext.Send(Out.PlaceObject, $"{-item.Id} {x} {y} 0");
        if (delayAfter > 0)
            await Task.Delay(delayAfter);
    }

    private async Task MoveItem(IInventoryItem item, int x, int y)
    {
        await Task.Delay(100);
        _ext.Send(Out.MoveObject, item.Id, x, y, 0);
    }

    private async Task LoopHead(IInventoryItem head, CancellationToken token)
    {
        _ext.Send(Out.PickupObject, 2, head.Id, false);
        await Task.Delay(LoopDelay, token);
        _ext.Send(Out.PlaceObject, $"{-head.Id} 5 5 0");
        _ext.Send(Out.UseFurniture, head.Id, 0);
        await Task.Delay(LoopDelay, token);
    }

    private async Task RoomLoaded()
    {
        try
        {
            await _ext.ReceiveAsync(In.RoomEntryInfo, timeout: 2000);
        }
        catch (TimeoutException)
        {
            Console.WriteLine("Timeout receiving RoomEntryInfo");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error receiving RoomEntryInfo: {ex.Message}");
        }
    }

    private async Task RunSequence()
    {
        ResetState();
        Started?.Invoke();
        var token = cts!.Token;

        var headClassId = IsSantorini ? SantoriniHeadClassId : JungleHeadClassId;
        var centerClassId = IsSantorini ? SantoriniCenterClassId : JungleCenterClassId;
        var bottomClassId = IsSantorini ? SantoriniBottomClassId : JungleBottomClassId;
        var combo = IsSantorini ? SantoriniCombos[SelectedCombo] : JungleCombos[SelectedCombo];

        _ext.Send(Out.CreateFlat, "My Room :)", "", "model_c", 14, 10, 0);
        await RoomLoaded();

        var inventory = await _ext.inventoryManager.LoadInventoryAsync();
        foreach (var item in inventory)
        {
            if (item.Kind == headClassId) HeadItem = item;
            else if (item.Kind == centerClassId) CenterItem = item;
            else if (item.Kind == bottomClassId) BottomItem = item;
            if (HeadItem != null && CenterItem != null && BottomItem != null) break;
        }

        if (HeadItem is null || CenterItem is null || BottomItem is null)
        {
            _ext.Send(In.NotificationDialog, "builders_club.membership_in_grace", 4, "linkTitle", "Open inventory", "linkUrl", "event:inventory/open", "message", $"You don't have all 3 parts of <b>{(IsSantorini ? "Santorini Totem" : "Jungle Totem")}</b> in your inventory...", "title", "Totem Effects");
            _ext.totemManager.Stop();
            return;
        }

        var catalog = await new GetCatalogTask(_ext, "BUILDERS_CLUB").ExecuteAsync();
        var wiredRepeatResult = await FindCatalogOffer(catalog, "wired_triggers", "wf_trg_period_short");
        var wiredVariableResult = await FindCatalogOffer(catalog, "wired_variables_wired", "wf_act_change_var_val");
        if (wiredRepeatResult == null || wiredVariableResult == null) return;

        _ext.Send(Out.BuildersClubPlaceRoomItem, wiredRepeatResult.Value.Page.Id, wiredRepeatResult.Value.Offer.Id, "", 10, 5, 0, true);
        await Task.Delay(100);
        _ext.Send(Out.BuildersClubPlaceRoomItem, wiredVariableResult.Value.Page.Id, wiredVariableResult.Value.Offer.Id, "", 10, 5, 0, true);
        await Task.Delay(100);
        await PlaceItem(BottomItem, 6, 7);
        await PlaceItem(CenterItem, 7, 7);
        await PlaceItem(HeadItem, 8, 7, 0);

        var completed = await Task.WhenAny(ReceivedObjectsData.Task, Task.Delay(2000));
        if (completed != ReceivedObjectsData.Task)
        {
            Console.WriteLine("Timeout waiting for ObjectAdd Head");
            return;
        }

        if (WiredRepeatItem != null)
            _ext.Send(Out.UpdateTrigger, WiredRepeatItem.Id, 1, 1, 0, 0, 0, 0, 0, "");

        if (WiredVariableItem != null)
            _ext.Send(Out.UpdateAction, WiredVariableItem.Id, 6, 0, 0, 0, 0, combo.Head, -10, "", 1, HeadItem.Id, 0, 2, 100, 200, 2, 0, 200, 2, "-110", "n", 0);

        await CycleToState(BottomItem, CurrentBottomState, combo.Bottom, 12);
        await CycleToState(CenterItem, CurrentCenterState, combo.Center, 3);

        await MoveItem(BottomItem, 5, 5);
        await MoveItem(CenterItem, 5, 5);
        await MoveItem(HeadItem, 5, 5);

        await Task.Delay(100);
        _ext.Send(Out.UseFurniture, HeadItem.Id, 0);

        try
        {
            while (!token.IsCancellationRequested && _ext.roomManager.IsInRoom)
                await LoopHead(HeadItem, token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            Stopped?.Invoke();
        }
    }
}