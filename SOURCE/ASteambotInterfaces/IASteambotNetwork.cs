using System;
using System.Collections.Generic;

namespace ASteambotIntefaces
{
    public interface IASteambotNetwork
    {
        /// <summary>
        /// This methode will receive all the necessary data to send messages through ASteambot
        /// </summary>
        /// <param name="partenar">The steamID of the partenar wich wrote the message</param>
        void GetASteambotData(Func<List<GameServer>> GetAllGameServers, Func<int, int, NetworkCode.ASteambotCode, string, bool> SendToGameServer);
    }
}
