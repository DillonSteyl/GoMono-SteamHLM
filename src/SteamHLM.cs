using Godot;
using System;
using Steamworks;
using Godot.Collections;

public class SteamHLM : Node
{
    public enum PacketType
    {
        RPC,
    }
    public enum RemoteCallType
    {
        /// <summary>Execute RPC regardless of the owner of the node.</summary>
        Remote,
        /// <summary>Execute RPC on both the calling peer and the recipient.</summary>
        RemoteSync,
        /// <summary>Execute RPC only if the recipient is not the owner of the node.</summary>
        Puppet,
        /// <summary>Execute RPC only if the recipient is the owner of the node</summary>     
        Master
    }

    public static Dictionary<String, RemoteCallType> RPCMap = new Dictionary<String, RemoteCallType>();
    private static Global global;

    public override void _Ready()
    {
        global = GetNode<Global>("/root/Global");
    }

    public override void _Process(float delta)
    {
        if (global.ConnectionToHost == null)
        {
            foreach (var item in global.ConnectionBySteamID)
            {
                CSteamID peerSteamID = item.Key;
                HSteamNetConnection connectionToPeer = item.Value;
                byte[][] newMessages = SteamManager.ReceiveMessages(connectionToPeer);
                for (int i = 0; i < newMessages.Length; i++)
                {
                    UnpackMessage(newMessages[i]);
                }
            }
        }
        else
        {
            byte[][] newMessages = SteamManager.ReceiveMessages((HSteamNetConnection)global.ConnectionToHost);
            for (int i = 0; i < newMessages.Length; i++)
            {
                UnpackMessage(newMessages[i]);
            }
        }
    }

    // ================================================================================
    // Registering RPC
    // ================================================================================

    /// <summary>
    /// Registers the provided method (on the given node) as an RPC with a given call type.
    /// </summary>
    public static void RegisterRPC(
        Node node,
        String methodName,
        RemoteCallType remoteCallType
    )
    {
        GD.Print("Registered RPC " + GetRPCHash(node.GetPath(), methodName));
        RPCMap[GetRPCHash(node.GetPath(), methodName)] = remoteCallType;
    }

    private static String GetRPCHash(NodePath nodePath, String methodName) => nodePath.ToString() + "::" + methodName;

    // ================================================================================
    // Sending RPCs
    // ================================================================================

    /// <summary>
    /// Sends an RPC to the server.
    /// </summary>
    public static void SendRPCServer(
        Node node,
        String method,
        object[] arguments,
        int sendFlags = Constants.k_nSteamNetworkingSend_Reliable
    )
    {
        TrySendRPC(global.ConnectionToHost, global.HostID, node, method, arguments);
    }

    /// <summary>
    /// Send an RPC to a specified client connection.
    /// </summary>
    public static void SendRPCClient(
        CSteamID clientSteamID,
        Node node,
        String method,
        object[] arguments,
        int sendFlags = Constants.k_nSteamNetworkingSend_Reliable
    )
    {
        HSteamNetConnection connectionToClient = global.ConnectionBySteamID[clientSteamID];
        TrySendRPC(connectionToClient, clientSteamID, node, method, arguments, sendFlags);
    }

    /// <summary>
    /// Send an RPC to all clients. Will only work if the calling peer is the host.
    /// </summary>
    public static void SendRPCAllClients(
        Node node,
        String method,
        object[] arguments,
        int sendFlags = Constants.k_nSteamNetworkingSend_Reliable
    )
    {
        // foreach (HSteamNetConnection connection in global.connectionBySteamID.Values)
        foreach (var item in global.ConnectionBySteamID)
        {
            CSteamID recipientSteamID = item.Key;
            HSteamNetConnection connection = item.Value;
            TrySendRPC(connection, recipientSteamID, node, method, arguments, sendFlags);
        }
    }

    /// <summary>
    /// Attempts to send an RPC to the specified connection. Will only send successfully if the method is registered
    /// as an RPC. If <param name="connection">connection</param> is null and this peer is the server, the method
    /// will execute locally.
    /// </summary>
    private static void TrySendRPC(
        HSteamNetConnection? connection,
        CSteamID recipientSteamID,
        Node node,
        String method,
        object[] arguments,
        int sendFlags = Constants.k_nSteamNetworkingSend_Reliable
    )
    {
        // Attempt to get the connection and call type.
        RemoteCallType remoteCallType;
        if (!RPCMap.TryGetValue(GetRPCHash(node.GetPath(), method), out remoteCallType))
        {
            global.PushErrorOnce("Method not registered as RPC: " + node.GetPath() + "::" + method);
            return;
        }
        if (
            (remoteCallType == RemoteCallType.Puppet || remoteCallType == RemoteCallType.Master) &&
            !(node is INetworked)
        )
        {
            global.PushErrorOnce("Node " + node.Name + " does not implement INetworked interface.");
            return;
        }

        INetworked networkedNode = node as INetworked;
        bool send = false;
        bool executeLocally = false;
        switch (remoteCallType)
        {
            case RemoteCallType.Remote:
                send = true;
                break;
            case RemoteCallType.RemoteSync:
                send = true;
                executeLocally = true;
                break;
            case RemoteCallType.Puppet:
                send = (networkedNode.OwnerSteamID != recipientSteamID);
                break;
            case RemoteCallType.Master:
                send = (networkedNode.OwnerSteamID == recipientSteamID);
                break;
            default:
                break;
        }
        if (send)
        {
            if (connection == null && global.PlayingAsHost)
            {
                // if we sent this from the server to itself, call locally
                node.Callv(method, new Godot.Collections.Array(arguments));
                // make sure we don't execute twice
                executeLocally = false;
            }
            else
            {
                SendRPC((HSteamNetConnection)connection, node, method, arguments, sendFlags);
            }
        }
        if (executeLocally)
        {
            node.Callv(method, new Godot.Collections.Array(arguments));
        }
    }

    /// <summary>
    /// Sends a remote procedure call to the specified connection. Does not perform any validation.
    /// </summary>
    private static void SendRPC(
        HSteamNetConnection connection,
        Node node,
        String method,
        object[] arguments,
        int sendFlags = Constants.k_nSteamNetworkingSend_Reliable
    )
    {
        // Send RPC to the connection.
        String nodePath = node.GetPath();
        object[] payload = new object[] { PacketType.RPC, nodePath, method, arguments };
        byte[] messageData = GD.Var2Bytes(payload);
        SteamManager.SendMessage((HSteamNetConnection)connection, messageData);
    }

    // ================================================================================
    // Receiving & handling messages
    // ================================================================================

    /// <summary>
    /// Unpacks and handles the specified message.
    /// </summary>
    private void UnpackMessage(byte[] message)
    {
        Godot.Collections.Array messageData = (Godot.Collections.Array)GD.Bytes2Var(message);
        switch (messageData[0])
        {
            case (int)PacketType.RPC:
                NodePath nodePath = (String)messageData[1];
                String methodName = (String)messageData[2];
                Godot.Collections.Array argumentsArray = (Godot.Collections.Array)messageData[3];
                HandleMethodCallFromRPC(nodePath, methodName, argumentsArray);
                break;
            default:
                global.PushErrorOnce("Received unknown packet type.");
                break;
        }
    }

    /// <summary>
    /// Handles a method call from an RPC message. This includes checking that the RPC is registered and checking
    /// node ownership against the remote call type.
    /// </summary>
    private void HandleMethodCallFromRPC(
        NodePath nodePath, String methodName, Godot.Collections.Array argumentsArray
    )
    {
        RemoteCallType remoteCallType;
        String RPCHash = GetRPCHash(nodePath, methodName);
        Node node = GetNodeOrNull(nodePath);
        if (node == null)
        {
            GD.PushWarning("RPC call to missing node: " + nodePath.ToString());
            return;
        }
        if (!RPCMap.TryGetValue(RPCHash, out remoteCallType))
        {
            GD.PushWarning("RPC call to unregistered method.");
            return;
        }
        if (
            (remoteCallType == RemoteCallType.Puppet || remoteCallType == RemoteCallType.Master) &&
            !(node is INetworked)
        )
        {
            global.PushErrorOnce("Node " + node.Name + " does not implement INetworked interface.");
            return;
        }

        INetworked networkedNode = node as INetworked;
        bool execute = false;
        switch (remoteCallType)
        {
            case RemoteCallType.Remote:
            case RemoteCallType.RemoteSync:
                execute = true;
                break;
            case RemoteCallType.Puppet:
                execute = !networkedNode.IsMaster();
                break;
            case RemoteCallType.Master:
                execute = networkedNode.IsMaster();
                break;
            default:
                break;
        }
        if (execute)
        {
            GetNode(nodePath).Callv(methodName, argumentsArray);
        }
    }
}
