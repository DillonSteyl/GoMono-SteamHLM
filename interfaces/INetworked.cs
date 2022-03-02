using Steamworks;

/// <summary>
/// Networked nodes should implement the <c>INetworked</c> interface. They are given an owner (referenced by steam ID). 
/// </summary>
public interface INetworked
{
    CSteamID OwnerSteamID { get; set; }
    bool IsMaster()
    {
        return SteamUser.GetSteamID() == OwnerSteamID;
    }
}