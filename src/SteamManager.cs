using Godot;
using System;
using Steamworks;
using System.Runtime.InteropServices;

public class SteamManager : Node
{
    public const int AppID = 480;
    public const int MaximumReceivedMessages = 16;

    public static string SteamName;
    public static CSteamID SteamID;

    private static IntPtr _sendBuffer = Marshal.AllocHGlobal(1024);

    public override void _Ready()
    {
        SteamworksInit();
        SteamNetworkingUtils.InitRelayNetworkAccess();
    }

    public override void _Process(float delta)
    {
        SteamAPI.RunCallbacks();
    }

    // Initialises steamworks, quitting the application if any sanity checks fail.
    private void SteamworksInit()
    {
        if (!Packsize.Test())
        {
            GD.PushError(
                "[Steamworks.NET] Packsize Test returned false, the wrong version of " +
                "Steamworks.NET is being run in this platform."
            );
        }
        if (!DllCheck.Test())
        {
            GD.PushError(
                "[Steamworks.NET] DllCheck Test returned false, One or more of the " +
                "Steamworks binaries seems to be the wrong version."
            );
        }

        try
        {
            if (SteamAPI.RestartAppIfNecessary((AppId_t)AppID))
            {
                GD.Print("Restarting through steam...");
                GetTree().Quit();
            }
        }
        catch (System.DllNotFoundException e)
        {
            GD.PushError(
                "[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in " +
                "the correct location. Refer to the README for more details.\n" + e
            );
            GetTree().Quit();
        }

        if (SteamAPI.Init())
        {
            GD.Print("Steam initialised: " + SteamFriends.GetPersonaName());
        }
        else
        {
            GD.PushError("Unable to initialise Steamworks API. Make sure steam is launched.");
            GetTree().Quit();
        }
        SteamName = SteamFriends.GetPersonaName();
        SteamID = SteamUser.GetSteamID();
    }

    // ================================================================================
    // Utils
    // ================================================================================

    public static void SendMessage(
        HSteamNetConnection connection, byte[] message, int nSendFlags = Constants.k_nSteamNetworkingSend_Unreliable
    )
    {
        Marshal.Copy(message, 0, _sendBuffer, message.Length);
        long messageNumber;
        SteamNetworkingSockets.SendMessageToConnection(
            connection, _sendBuffer, (uint)message.Length, nSendFlags, out messageNumber
        );
    }

    public static byte[][] ReceiveMessages(HSteamNetConnection connection, int numMessages = MaximumReceivedMessages)
    {
        IntPtr[] receiveBuffers = new IntPtr[numMessages];
        int messageCount = SteamNetworkingSockets.ReceiveMessagesOnConnection(connection, receiveBuffers, receiveBuffers.Length);
        if (messageCount < 0)
        {
            return new byte[0][];
        }
        byte[][] resultingMessages = new byte[messageCount][];
        for (int i = 0; i < messageCount; i++)
        {
            SteamNetworkingMessage_t netMessage = Marshal.PtrToStructure<SteamNetworkingMessage_t>(receiveBuffers[i]);
            byte[] messageData = new byte[netMessage.m_cbSize];
            Marshal.Copy(netMessage.m_pData, messageData, 0, messageData.Length);
            resultingMessages[i] = messageData;
            Marshal.DestroyStructure<SteamNetworkingMessage_t>(receiveBuffers[i]);
        }
        return resultingMessages;
    }
}
