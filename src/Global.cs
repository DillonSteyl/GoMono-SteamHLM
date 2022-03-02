using Godot;
using System;
using System.Collections.Generic;
using Steamworks;

public class Global : Node
{
    private bool _playingAsHost = false;
    public bool playingAsHost
    {
        get { return _playingAsHost; }
        set
        {
            setPlayingAsHost(value);
        }
    }
    public HSteamNetConnection? connectionToHost = null;
    public Dictionary<CSteamID, HSteamNetConnection> connectionBySteamID = new Dictionary<CSteamID, HSteamNetConnection>();
    public CSteamID currentLobbyID;
    public CSteamID hostID;

    public const float GravityAcceleration = 400.0F;

    private static Dictionary<String, bool> ErrorCollection = new Dictionary<String, bool>();

    public void setPlayingAsHost(bool p_playingAsHost)
    {
        _playingAsHost = p_playingAsHost;
        Server serverNode = GetNode("/root/Main/ServerNode") as Server;
        serverNode.SetActive(_playingAsHost);
    }

    public void PushErrorOnce(String errorString)
    {
        if (ErrorCollection.ContainsKey(errorString))
        {
            return;
        }
        GD.PushError(errorString);
        ErrorCollection[errorString] = true;
    }

    public void PushWarningOnce(String warningString)
    {
        if (ErrorCollection.ContainsKey(warningString))
        {
            return;
        }
        GD.PushWarning(warningString);
        ErrorCollection[warningString] = true;
    }
}
