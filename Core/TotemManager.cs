using Xabbo;
using Xabbo.Core;
using Xabbo.Core.Game;
using Xabbo.Core.GameData;
using Xabbo.Core.Messages.Incoming;
using Xabbo.Core.Messages.Outgoing;
using Xabbo.Core.Tasks;
using Xabbo.Messages.Flash;

namespace TotemEffects.Core;

public class TotemManager(Extension ext)
{
    private readonly Extension _ext = ext;
    private FurniInfo? JungleHeadClass { get; set; } = null;
    private FurniInfo? JungleCenterClass { get; set; } = null;
    private FurniInfo? JungleBottomClass { get; set; } = null;
    private FurniInfo? SantoriniHeadClass { get; set; } = null;
    private FurniInfo? SantoriniCenterClass { get; set; } = null;
    private FurniInfo? SantoriniBottomClass { get; set; } = null;
    private FurniInfo? WiredRepeatClass { get; set; } = null;
    private FurniInfo? WiredVariableClass { get; set; } = null;
    private TotemSession JungleSession { get; } = new();
    private TotemSession SantoriniSession { get; } = new();
    private TotemSession CurrentSession => IsSantorini ? SantoriniSession : JungleSession;
    private Id? UserId { get; set; } = null;
    public bool SelectMode { get; set; } = false;
    private int SelectedCombo { get; set; } = 1;
    private bool IsSantorini { get; set; } = false;
    private bool HasWiredEditPerm { get; set; } = false;
    private CancellationTokenSource? Cts { get; set; } = null;
    public int LoopDelay { get; set; } = 50;

    // test cubie
    /*private readonly Dictionary<int, (int Head, int Center, int Bottom)> JungleCombos = new()
    {
        { 1, (2, 2, 2) },
        { 2, (0, 0, 0) },
        { 3, (1, 1, 1) },
        { 4, (1, 2, 0) },
    };*/

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

    public async Task Initialize()
    {
        var profile = await _ext.profileManager.GetUserDataAsync();
        UserId = profile.Id;
        var furniData = _ext.gameData.Furni;
        if (furniData is null)
        {
            Console.WriteLine("Could not load furnidata");
            return;
        }

        FurniInfo? GetClass(string identifier) =>
            furniData.TryGetInfo(identifier, out var info) ? info : null;

        // test cubie
        /*JungleHeadClass = GetClass("cubie_shelf_0_p");
        JungleCenterClass = GetClass("cubie_shelf_1_b");
        JungleBottomClass = GetClass("cubie_shelf_1_p");*/

        JungleHeadClass = GetClass("lt_r26_totem3");
        JungleCenterClass = GetClass("lt_r26_totem2");
        JungleBottomClass = GetClass("lt_r26_totem1");

        SantoriniHeadClass = GetClass("totem_planet");
        SantoriniCenterClass = GetClass("totem_head");
        SantoriniBottomClass = GetClass("totem_leg");

        WiredRepeatClass = GetClass("wf_trg_period_short");
        WiredVariableClass = GetClass("wf_act_change_var_val");

        _ext.roomManager.Left += OnLeftRoom;
        _ext.Intercept(In.AvatarEffectAdded, OnEffectAdded);
        _ext.Intercept(In.NotificationDialog, OnNotificationDialog);
        _ext.Intercept(In.WiredPermissions, OnWiredPermissions);
    }

    public void SetCombo(int n, bool santorini)
    {
        SelectedCombo = n;
        IsSantorini = santorini;
    }

    public async Task Start()
    {
        ResetState();
        try
        {
            if (_ext.gameData.Furni is null)
                await _ext.gameData.WaitForLoadAsync(Cts!.Token);

            await RunSequence();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Stopped?.Invoke();
        }
    }

    public void Stop() => Cts?.Cancel();

    public void ResetState()
    {
        Stop();
        Cts?.Dispose();
        Cts = new CancellationTokenSource();
    }

    public void ResetSession()
    {
        JungleSession.Reset();
        SantoriniSession.Reset();
    }

    private void NotifyDialog(string message) =>
    _ext.Send(In.NotificationDialog, "ambassador.alert.warning", 2,
        "message", message, "title", "Totem Effects");

    private void NotifyDialogEvent(string message, string linkEventMsg, string linkEventUrl) =>
        _ext.Send(In.NotificationDialog, "builders_club.membership_in_grace", 4,
            "linkTitle", linkEventMsg, "linkUrl", linkEventUrl,
            "message", message, "title", "Totem Effects");

    private void NotifyChat(string message, int bubble) =>
        _ext.Send(new AvatarTalkMsg(message, BubbleStyle: bubble));

    public void ResetAll()
    {
        ResetSession();
        ResetState();
        JungleHeadClass = null;
        JungleCenterClass = null;
        JungleBottomClass = null;
        SantoriniHeadClass = null;
        SantoriniCenterClass = null;
        SantoriniBottomClass = null;
        WiredRepeatClass = null;
        WiredVariableClass = null;
        UserId = null;
    }

    private void OnLeftRoom() => ResetSession();

    public event Action? Started;
    public event Action? Stopped;
    public event Action? Unfocus;
    public event Action? EffectReceived;

    private void OnNotificationDialog(Intercept e)
    {
        if (Cts is null || Cts.IsCancellationRequested) return;

        string message = e.Packet.Read<string>();
        int paramCount = e.Packet.Read<int>();
        var parameters = new Dictionary<string, string>();
        for (int i = 0; i < paramCount; i++)
            parameters[e.Packet.Read<string>()] = e.Packet.Read<string>();

        // stop if the user can't place bc furni
        if (message == "furni_placement_error" &&
            parameters.TryGetValue("message", out var msg) &&
            (msg == "${room.error.cant_set_not_owner}" || msg == "${room.error.cant_set_item}"))
        {
            NotifyDialog("Seems like you don't have Builders Club or someone is in your room\n\n" +
                "- If you are in your room, you need to be alone to use furni from Builders Club\n" +
                "- If you are on someone else room you need a Builders Club subscription");

            Stop();
        }
    }

    private void OnWiredPermissions(Intercept e)
    {
        HasWiredEditPerm = e.Packet.Read<bool>();
        e.Packet.Read<bool>(); // canInspectWired
    }

    private void OnEffectAdded(Intercept e)
    {
        if (Cts is null || Cts.IsCancellationRequested) return;
        e.Block(); // prevent lag
        EffectReceived?.Invoke();
    }

    // helpers
    private FurniInfo? HeadClass => IsSantorini ? SantoriniHeadClass : JungleHeadClass;
    private FurniInfo? CenterClass => IsSantorini ? SantoriniCenterClass : JungleCenterClass;
    private FurniInfo? BottomClass => IsSantorini ? SantoriniBottomClass : JungleBottomClass;

    private void SendNotFound(string name, bool withInventoryLink = true)
    {
        if (withInventoryLink)
            NotifyDialogEvent($"Could not find <b>{name}</b> in your inventory", "Open inventory", "event:inventory/open");
        else
            NotifyDialog($"Could not find <b>{name}</b> in the room");
    }

    // receive ObjectAdd to know the wireds Id to be able to configure them
    private async Task<IFloorItem?> ReceiveObjectAdded(int? kind)
    {
        try
        {
            var msg = await _ext.ReceiveAsync<FloorItemAddedMsg>(timeout: 2000);
            return msg.Item.Kind == kind ? msg.Item : null;
        }
        catch { return null; }
    }

    // gets wireds from BC catalog
    private async Task<(ICatalogPage Page, ICatalogOffer Offer)?> FindCatalogOffer(
        ICatalog catalog,
        string pageName,
        string furniLine,
        CancellationToken token = default)
    {
        var node = catalog.FindNode(name: pageName);
        if (node == null) return null;
        var page = await new GetCatalogPageTask(_ext, node.Id, "BUILDERS_CLUB").ExecuteAsync(cancellationToken: token);
        if (page == null) return null;
        var offer = page.Offers.FirstOrDefault(x => x.FurniLine == furniLine);
        if (offer == null) return null;
        return (page, offer);
    }

    // generic helper to resolve a piece from room by clicking
    private async Task<TotemPiece?> ResolveFromRoomByClick(
        Id? lastId,
        Action<Id> saveId,
        int? classId,
        string className,
        IEnumerable<IFloorItem> roomFloorItems,
        CancellationToken token)
    {
        if (lastId.HasValue)
        {
            var inRoom = roomFloorItems.FirstOrDefault(x => x.Id == lastId.Value && x.Kind == classId);
            if (inRoom is not null) return new(inRoom.Id, true);
        }

        Unfocus?.Invoke();
        NotifyChat($"Click on the [b]{className}[/b] you want to use...", 202);
        while (true)
        {
            var clicked = await _ext.ReceiveAsync<ClickFurniMsg>(timeout: -1, block: true, cancellationToken: token);
            var inRoom = roomFloorItems.FirstOrDefault(x => x.Id == clicked.Id && x.Kind == classId && x.Type == ItemType.Floor);
            if (inRoom is not null)
            {
                saveId(inRoom.Id);
                return new(inRoom.Id, true);
            }
            NotifyChat($"That is not a [b]{className}[/b], try again...", 220);
        }
    }

    // generic helper to resolve a piece from inventory or room
    private TotemPiece? ResolveFromInventory(
        Id? lastId,
        Action<Id> saveId,
        int? classId,
        string className,
        IInventory inventory,
        IEnumerable<IFloorItem> roomFloorItems)
    {
        if (lastId.HasValue)
        {
            var inRoom = roomFloorItems.FirstOrDefault(x => x.Id == lastId.Value);
            if (inRoom is not null) return new(inRoom.Id, true);

            var inInventory = inventory.FirstOrDefault(x => x.Id == lastId.Value);
            if (inInventory is not null) return new(inInventory.Id, false);
        }

        var item = inventory.FirstOrDefault(x => x.Kind == classId && x.Type == ItemType.Floor);
        if (item is null)
        {
            SendNotFound(className);
            Stopped?.Invoke();
            return null;
        }

        saveId(item.Id);
        return new(item.Id, false);
    }

    // resolve head piece in select mode, asks to click the head in room
    private async Task<TotemPiece?> ResolveHeadPieceManual(
        TotemSession session,
        IEnumerable<IFloorItem> roomFloorItems,
        CancellationToken token)
    {
        // reuse last known used head
        if (session.HeadId.HasValue)
        {
            var inRoom = roomFloorItems.FirstOrDefault(x => x.Id == session.HeadId.Value);
            if (inRoom is not null)
                return new(session.HeadId.Value, true);

            return new(session.HeadId.Value, false);
        }

        Unfocus?.Invoke();
        NotifyChat($"Click on the [b]{HeadClass?.Name}[/b] you want to use...", 202);
        while (true)
        {
            var clicked = await _ext.ReceiveAsync<ClickFurniMsg>(timeout: -1, block: true, cancellationToken: token);
            var inRoom = roomFloorItems.FirstOrDefault(x => x.Id == clicked.Id && x.Kind == HeadClass?.Kind && x.Type == ItemType.Floor);
            if (inRoom is not null)
            {
                if (inRoom.OwnerId != UserId)
                {
                    NotifyChat($"You need to own the [b]{HeadClass?.Name}[/b] to farm effects", 220);
                    Stopped?.Invoke();
                    return null;
                }
                session.HeadId = inRoom.Id;
                return new(inRoom.Id, true);
            }
            NotifyChat($"That is not a [b]{HeadClass?.Name}[/b], try again...", 220);
        }
    }

    private TotemPiece? ResolveHeadPieceAuto(
        TotemSession session,
        IInventory inventory,
        IEnumerable<IFloorItem> roomFloorItems) =>
        ResolveFromInventory(session.HeadId, id => session.HeadId = id, HeadClass?.Kind, HeadClass?.Name ?? "Totem Head", inventory, roomFloorItems);

    private Task<TotemPiece?> ResolveBottomPieceManual(
        TotemSession session,
        IEnumerable<IFloorItem> roomFloorItems,
        CancellationToken token) =>
        ResolveFromRoomByClick(session.BottomId, id => session.BottomId = id, BottomClass?.Kind, BottomClass?.Name ?? "Totem Bottom", roomFloorItems, token);

    private TotemPiece? ResolveBottomPieceAuto(
        TotemSession session,
        IInventory inventory,
        IEnumerable<IFloorItem> roomFloorItems) =>
        ResolveFromInventory(session.BottomId, id => session.BottomId = id, BottomClass?.Kind, BottomClass?.Name ?? "Totem Bottom", inventory, roomFloorItems);

    private Task<TotemPiece?> ResolveCenterPieceManual(
        TotemSession session,
        IEnumerable<IFloorItem> roomFloorItems,
        CancellationToken token) =>
        ResolveFromRoomByClick(session.CenterId, id => session.CenterId = id, CenterClass?.Kind, CenterClass?.Name ?? "Totem Center", roomFloorItems, token);

    private TotemPiece? ResolveCenterPieceAuto(
        TotemSession session,
        IInventory inventory,
        IEnumerable<IFloorItem> roomFloorItems) =>
        ResolveFromInventory(session.CenterId, id => session.CenterId = id, CenterClass?.Kind, CenterClass?.Name ?? "Totem Center", inventory, roomFloorItems);

    // ensures the wired configuration is in the room.
    // if already placed from last used, reuses them. Otherwise gets from BC and places them.
    // returns the tile where wireds are placed, or null on failure.
    private async Task<Point?> EnsureWiredsInRoom(
        TotemSession session,
        IEnumerable<IFloorItem> roomFloorItems,
        IEnumerable<Point> placeablePoints,
        CancellationToken token)
    {
        // check if wireds are already in room at the same tile from the last use
        if (session.WiredRepeatItem != null && session.WiredVariableItem != null)
        {
            var repeatInRoom = roomFloorItems.FirstOrDefault(x => x.Id == session.WiredRepeatItem.Id);
            var variableInRoom = roomFloorItems.FirstOrDefault(x => x.Id == session.WiredVariableItem.Id);

            if (repeatInRoom != null && variableInRoom != null && repeatInRoom.XY == variableInRoom.XY)
                return repeatInRoom.XY;
        }

        // gets wireds from BC catalog
        var catalog = await new GetCatalogTask(_ext, "BUILDERS_CLUB").ExecuteAsync(cancellationToken: token);
        var repeatResult = await FindCatalogOffer(catalog, "wired_triggers", "wf_trg_period_short", token);
        var variableResult = await FindCatalogOffer(catalog, "wired_variables_wired", "wf_act_change_var_val", token);
        if (repeatResult == null || variableResult == null)
        {
            Console.WriteLine("Could not get catalog offer of a wired");
            Stopped?.Invoke();
            return null;
        }

        // ask user to click a tile for wireds
        Unfocus?.Invoke();
        NotifyChat("Click where you want to place the wired configuration...", 202);
        Point tileForWireds;
        while (true)
        {
            var walk = await _ext.ReceiveAsync<WalkMsg>(
                timeout: -1,
                block: true,
                shouldCapture: walk => placeablePoints.Contains(walk.Point),
                cancellationToken: token);
            tileForWireds = walk.Point;
            break;
        }

        // place wired short repeat
        _ext.Send(Out.BuildersClubPlaceRoomItem, repeatResult.Value.Page.Id, repeatResult.Value.Offer.Id, "", tileForWireds, 0, true);
        session.WiredRepeatItem = await ReceiveObjectAdded(WiredRepeatClass?.Kind);
        if (session.WiredRepeatItem is null)
        {
            Console.WriteLine("Wired repeat item was not received after placement");
            Stopped?.Invoke();
            return null;
        }

        await Task.Delay(100, token);

        // place wired change variable
        _ext.Send(Out.BuildersClubPlaceRoomItem, variableResult.Value.Page.Id, variableResult.Value.Offer.Id, "", tileForWireds, 0, true);
        session.WiredVariableItem = await ReceiveObjectAdded(WiredVariableClass?.Kind);
        if (session.WiredVariableItem is null)
        {
            Console.WriteLine("Wired variable item was not received after placement");
            Stopped?.Invoke();
            return null;
        }

        await Task.Delay(100, token);
        return tileForWireds;
    }

    // ensures a tile is selected for the totem
    // if bottom and center are already there, reuses the last tile
    // otherwise asks the user to click a tile
    // returns the tile, or null on failure
    private async Task<Point?> EnsureTileForTotem(
        TotemSession session,
        IEnumerable<IFloorItem> roomFloorItems,
        IEnumerable<Point> placeablePoints,
        Point tileForWireds,
        TotemPiece bottomPiece,
        TotemPiece centerPiece,
        CancellationToken token)
    {
        // check if bottom and center are already in the room from last use
        if (session.TileForTotem.HasValue)
        {
            var bottomInRoom = roomFloorItems.FirstOrDefault(x => x.Id == bottomPiece.Id);
            var centerInRoom = roomFloorItems.FirstOrDefault(x => x.Id == centerPiece.Id);

            if (bottomInRoom != null && centerInRoom != null)
                return session.TileForTotem.Value;
        }

        // ask user to click a tile for the totem
        Unfocus?.Invoke();
        NotifyChat("Click where you want to place the totem...", 202);
        while (true)
        {
            var walk = await _ext.ReceiveAsync<WalkMsg>(timeout: -1, block: true, cancellationToken: token);
            if (placeablePoints.Contains(walk.Point) && walk.Point != tileForWireds)
            {
                session.TileForTotem = walk.Point;
                return walk.Point;
            }
        }
    }

    // moves pieces to setup tiles, sets the combo states, then moves everything to the final totem tile
    private async Task SetupCombo(
        TotemSession session,
        (int Head, int Center, int Bottom) combo,
        TotemPiece headPiece,
        TotemPiece bottomPiece,
        TotemPiece centerPiece,
        IEnumerable<Point> placeablePoints,
        Point tileForWireds,
        Point tileForTotem)
    {
        // pick 3 free tiles for temporary setup
        var availablePoints = placeablePoints
            .Where(p => p != tileForTotem && p != tileForWireds)
            .Take(3)
            .ToList();

        if (availablePoints.Count < 3)
        {
            NotifyDialog("There are not enough free tiles in the room");
            Stop();
            return;
        }

        var tileSetupBottom = availablePoints[0];
        var tileSetupCenter = availablePoints[1];
        var tileSetupHead = availablePoints[2];

        // move/place pieces to setup tiles
        MovePiece(bottomPiece, tileSetupBottom);
        await Task.Delay(100);
        MovePiece(centerPiece, tileSetupCenter);
        await Task.Delay(100);
        MovePiece(headPiece, tileSetupHead);

        // configure wired repeat trigger
        if (session.WiredRepeatItem != null)
            _ext.Send(Out.UpdateTrigger, session.WiredRepeatItem.Id, 1, 1, 0, 0, 0, 0, 0, "");

        // configure wired variable to control head state
        if (session.WiredVariableItem != null)
            _ext.Send(Out.UpdateAction, session.WiredVariableItem.Id, 6, 0, 0, 0, 0,
                combo.Head, -10, "", 1, headPiece.Id, 0, 2, 100, 200, 2, 0, 200, 2, "-110", "n", 0);

        // set bottom and center states via wired variable value
        _ext.Send(Out.WiredSetObjectVariableValue, 0, bottomPiece.Id, "-110", combo.Bottom);
        await Task.Delay(300);
        _ext.Send(Out.WiredSetObjectVariableValue, 0, centerPiece.Id, "-110", combo.Center);
        await Task.Delay(300);

        // move all pieces to the final totem tile
        _ext.Send(Out.MoveObject, bottomPiece.Id, tileForTotem, 0);
        await Task.Delay(100);
        _ext.Send(Out.MoveObject, centerPiece.Id, tileForTotem, 0);
        await Task.Delay(100);
        _ext.Send(Out.MoveObject, headPiece.Id, tileForTotem, 0);

        await Task.Delay(100);

        // get 1 effect the head before starting the head loop
        _ext.Send(Out.UseFurniture, headPiece.Id, 0);
    }

    // move or place a piece depending on whether it is already in the room or in inventory.
    private void MovePiece(TotemPiece piece, Point tile)
    {
        if (piece.IsFromRoom)
            _ext.Send(Out.MoveObject, piece.Id, tile, 0);
        else
            _ext.Send(Out.PlaceObject, piece.PlaceString(tile.X, tile.Y));
    }

    // repeatedly picks up, places and use the head totem to farm effects
    private async Task LoopHead(TotemPiece head, Point tile, CancellationToken token)
    {
        _ext.Send(Out.PlaceObject, head.PlaceString(tile.X, tile.Y));
        _ext.Send(Out.UseFurniture, head.Id, 0);
        await Task.Delay(LoopDelay, token);
        _ext.Send(Out.PickupObject, 2, head.Id, false);
        await Task.Delay(LoopDelay, token);
    }

    private async Task RunSequence()
    {
        Started?.Invoke();
        var token = Cts!.Token;
        var state = CurrentSession;
        var combo = IsSantorini ? SantoriniCombos[SelectedCombo] : JungleCombos[SelectedCombo];

        // validate if the user is in a room and returns room instance
        if (!_ext.roomManager.EnsureInRoom(out var room))
        {
            NotifyDialogEvent("You must be in a room to start!", "Open navigator", "event:navigator/search/");
            Stopped?.Invoke();
            return;
        }

        var roomUsers = room.Users;
        var roomFloorItems = room.FloorItems;
        var roomData = room.Data;
        if (roomUsers is null || roomFloorItems is null || roomData is null)
        {
            Console.WriteLine("RoomManager returned null data");
            Stopped?.Invoke();
            return;
        }

        var self = roomUsers.FirstOrDefault(x => x.Id == UserId);
        if (self is null)
        {
            Console.WriteLine("Could not find self in room users");
            Stopped?.Invoke();
            return;
        }

        // validate if the user is room owner or have group admin rights + wired edit permission
        if (roomData.OwnerId != UserId && !(self.RightsLevel == RightsLevel.GroupAdmin && HasWiredEditPerm))
        {
            NotifyDialog("You are not in your room or don't have enough rights");
            Stopped?.Invoke();
            return;
        }

        IInventory? inventory = null;
        if (!SelectMode)
        {
            inventory = await _ext.inventoryManager.LoadInventoryAsync(cancellationToken: token);
            if (inventory is null)
            {
                Console.WriteLine("Could not fetch inventory");
                Stopped?.Invoke();
                return;
            }
        }

        var headPiece = SelectMode
            ? await ResolveHeadPieceManual(state, roomFloorItems, token)
            : ResolveHeadPieceAuto(state, inventory!, roomFloorItems);
        if (headPiece is null) return;

        var bottomPiece = SelectMode
            ? await ResolveBottomPieceManual(state, roomFloorItems, token)
            : ResolveBottomPieceAuto(state, inventory!, roomFloorItems);
        if (bottomPiece is null) return;

        var centerPiece = SelectMode
            ? await ResolveCenterPieceManual(state, roomFloorItems, token)
            : ResolveCenterPieceAuto(state, inventory!, roomFloorItems);
        if (centerPiece is null) return;

        // gets possible placeable points in the current room
        var placeablePoints = room.FindPlaceablePoints(room.Heightmap.Area, (1, 1), allowEntryTile: false);

        // ensure wireds are placed
        var tileForWireds = await EnsureWiredsInRoom(state, roomFloorItems, placeablePoints, token);
        if (tileForWireds is null)
        {
            Console.WriteLine("tileForWired returned null");
            Stopped?.Invoke();
            return;
        }
        // ensure totem tile is selected
        var tileForTotem = await EnsureTileForTotem(state, roomFloorItems, placeablePoints, tileForWireds.Value, bottomPiece, centerPiece, token);
        if (tileForTotem is null)
        {
            Console.WriteLine("tileForTotem returned null");
            Stopped?.Invoke();
            return;
        }

        // setup combo states and move totem to final tile
        await SetupCombo(state, combo, headPiece, bottomPiece, centerPiece, placeablePoints, tileForWireds.Value, tileForTotem.Value);

        // check if head piece is in the room before pickup
        if (roomFloorItems.Any(x => x.Id == headPiece.Id))
        {
            _ext.Send(Out.PickupObject, 2, headPiece.Id, false);
            await Task.Delay(100);
        }

        // farm effects loop
        try
        {
            while (!token.IsCancellationRequested && _ext.roomManager.IsInRoom)
                await LoopHead(headPiece, tileForTotem.Value, token);
        }
        catch (OperationCanceledException) { }
    }
}