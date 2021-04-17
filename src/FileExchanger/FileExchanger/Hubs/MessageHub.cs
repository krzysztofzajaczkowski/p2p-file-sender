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
        private IClientProxy Host => Clients.Client(_hostId);
        private IClientProxy Caller => Clients.Caller;

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

        public Task SendMessage(string message)
        {
            return Clients.Client(_hostId).SendAsync("ReceiveSendMessage", message);
        }

        public Task StartSending(int numberOfPackages, string fileName)
        {
            return Host.SendAsync("ReceiveStartSending", numberOfPackages, fileName);
        }

        public async Task SendPackage(int packageNumber, byte[] data)
        {
            await Caller.SendAsync("ReceiveProgress", packageNumber);
            await Host.SendAsync("ReceiveSendPackage", packageNumber, data);
        }

        public Task StopSending()
        {
            return Host.SendAsync("ReceiveStopSending");
        }
    }
}
