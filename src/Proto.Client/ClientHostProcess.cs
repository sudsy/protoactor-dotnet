
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientHostProcess : Process
    {
        private static readonly ILogger Logger = Log.CreateLogger<ClientHostProcess>();
        private readonly PID _pid;
        private Client _client;


        public ClientHostProcess(PID pid, Client client)
        {
            _pid = pid;
            Logger.LogDebug($"Constructor for {pid} called");
            _client = client;
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
            //TODO: Make sure the client isn't disposed
            _client.SendMessage(_pid, msg, Serialization.DefaultSerializerId);
            
        }
    }
}