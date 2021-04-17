using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace FileExchanger.Hubs
{
    public class MessageHub : Hub
    {
        private static string _hostId;

        public Task SendMessageToAll(string message)
        {
            return Clients.All.SendAsync("ReceiveSendMessageToAll", message);
        }

        public Task CheckIfImHost()
        {
            return Clients.Caller.SendAsync("ReceiveCheckIfImHost", Context.ConnectionId == _hostId);
        }

        public Task ConnectAsHost()
        {
            _hostId = Context.ConnectionId;
            return Clients.Caller.SendAsync("ReceiveConnectAsHost", true);
        }
    }
}
