﻿using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Networking.Factorys;
using Basis.Scripts.Networking.NetworkedPlayer;
using Basis.Scripts.Player;
using DarkRift;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using static SerializableDarkRift;
namespace Basis.Scripts.Networking
{
    public static class BasisNetworkHandleRemote
    {
        public static async Task HandleCreateRemotePlayer(DarkRiftReader reader,Transform Parent)
        {
            reader.Read(out ServerReadyMessage SRM);
            await CreateRemotePlayer(SRM, Parent);
        }
        public static async Task HandleCreateAllRemoteClients(DarkRiftReader reader, Transform Parent)
        {
            reader.Read(out CreateAllRemoteMessage allRemote);
            int RemoteLength = allRemote.serverSidePlayer.Length;
            for (int PlayerIndex = 0; PlayerIndex < RemoteLength; PlayerIndex++)
            {
                await CreateRemotePlayer(allRemote.serverSidePlayer[PlayerIndex], Parent);
            }
        }
        public static async Task<BasisNetworkedPlayer> CreateRemotePlayer(ServerReadyMessage ServerReadyMessage, InstantiationParameters instantiationParameters)
        {
            ClientAvatarChangeMessage avatarID = ServerReadyMessage.localReadyMessage.clientAvatarChangeMessage;

            if (avatarID.byteArray != null)
            {
                BasisRemotePlayer remote = await BasisPlayerFactory.CreateRemotePlayer(instantiationParameters, avatarID, ServerReadyMessage.localReadyMessage.playerMetaDataMessage);
                BasisNetworkedPlayer networkedPlayer = await BasisPlayerFactoryNetworked.CreateNetworkedPlayer(instantiationParameters);
                networkedPlayer.ReInitialize(remote, ServerReadyMessage.playerIdMessage.playerID, ServerReadyMessage.localReadyMessage.localAvatarSyncMessage);
                if (BasisNetworkManagement.AddPlayer(networkedPlayer))
                {
                    Debug.Log("Added Player " + ServerReadyMessage.playerIdMessage.playerID);
                    BasisNetworkManagement.OnRemotePlayerJoined?.Invoke(networkedPlayer, remote);
                }
                return networkedPlayer;
            }
            else
            {
                Debug.LogError("Empty Avatar ID for Player fatal error! " + ServerReadyMessage.playerIdMessage.playerID);
                return null;
            }
        }
        public static async Task<BasisNetworkedPlayer> CreateRemotePlayer(ServerReadyMessage ServerReadyMessage, Transform Parent)
        {
            InstantiationParameters instantiationParameters = new InstantiationParameters(Vector3.zero, Quaternion.identity, Parent);
            return await CreateRemotePlayer(ServerReadyMessage, instantiationParameters);
        }
    }
}