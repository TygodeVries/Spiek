using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Spiek.Devices
{
    public class QuestDiscover
    {
        private UdpClient? broadcastClient;
        private const int Port = 9999;

        public void Start()
        {
            broadcastClient = new UdpClient();

            // Enable reuse address
            broadcastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            broadcastClient.Client.Bind(new IPEndPoint(IPAddress.Any, Port));

            broadcastClient.EnableBroadcast = true;

            _ = OnStartRead();
        }

        public void Broadcast(string message)
        {
            if (broadcastClient == null)
                throw new Exception("No broadcast available.");

            // Force UFT8
            byte[] payload = Encoding.UTF8.GetBytes(message);

            IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, Port);

            broadcastClient.Send(payload, payload.Length, endpoint);
        }

        public event Action<(string ip, string message)>? OnMessage;

        private async Task OnStartRead()
        {
            if (broadcastClient == null)
                throw new Exception("Broadcast is still null!");

            while (true)
            {
                UdpReceiveResult result = await broadcastClient.ReceiveAsync();

                string message = Encoding.UTF8.GetString(result.Buffer).Trim();
                string sender = result.RemoteEndPoint.Address.ToString();

                _ = Task.Run(() =>
                {
                    OnMessage?.Invoke((sender, message));
                });
            }

        }
    }
}
