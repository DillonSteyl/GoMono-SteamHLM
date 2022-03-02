using Godot;
using System;
using System.Collections.Generic;
using Steamworks;

public class Global : Node
{
    private bool _playingAsHost = false;
    public bool PlayingAsHost
    {
        get { return _playingAsHost; }
        set
        {
            setPlayingAsHost(value);
        }
    }
    public HSteamNetConnection? ConnectionToHost = null;
    public Dictionary<CSteamID, HSteamNetConnection> ConnectionBySteamID = new Dictionary<CSteamID, HSteamNetConnection>();
    public CSteamID CurrentLobbyID;
    public CSteamID HostID;

    public const float GravityAcceleration = 400.0F;

    private static Dictionary<String, bool> _errorCollection = new Dictionary<String, bool>();

    public void setPlayingAsHost(bool p_playingAsHost)
    {
        _playingAsHost = p_playingAsHost;
        Server serverNode = GetNode("/root/Main/ServerNode") as Server;
        serverNode.SetActive(_playingAsHost);
    }

    public void PushErrorOnce(String errorString)
    {
        if (_errorCollection.ContainsKey(errorString))
        {
            return;
        }
        GD.PushError(errorString);
        _errorCollection[errorString] = true;
    }

    public void PushWarningOnce(String warningString)
    {
        if (_errorCollection.ContainsKey(warningString))
        {
            return;
        }
        GD.PushWarning(warningString);
        _errorCollection[warningString] = true;
    }
}
