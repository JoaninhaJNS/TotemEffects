using Xabbo;
using Xabbo.Core.Game;
using Xabbo.Core.GameData;
using Xabbo.GEarth;

namespace TotemEffects.Core;

public class Extension : GEarthExtension
{
    public RoomManager roomManager { get; private set; }
    public TradeManager tradeManager { get; private set; }
    public InventoryManager inventoryManager { get; private set; }
    public GameDataManager gameData { get; private set; }
    public ProfileManager profileManager { get; private set; }
    public TotemManager totemManager { get; private set; }

    public Extension() : base(new GEarthOptions
    {
        Name = "Totem Effects",
        Description = "Automatically farm totem effects",
        Author = "JoaninhaJNS",
        Version = "1.0.0"
    })
    {
        gameData = new GameDataManager();
        roomManager = new RoomManager(this);
        profileManager = new ProfileManager(this);
        tradeManager = new TradeManager(this, profileManager, roomManager);
        inventoryManager = new InventoryManager(this, roomManager, tradeManager);
        totemManager = new TotemManager(this);
    }

    protected override async void OnConnected(ConnectedEventArgs e)
    {
        base.OnConnected(e);
        await gameData.LoadAsync(e.Session.Hotel, [GameDataType.FurniData]);
        totemManager.Initialize();       
    }

    protected override void OnDisconnected()
    {
        base.OnDisconnected();
        totemManager.ResetState();
    }
}