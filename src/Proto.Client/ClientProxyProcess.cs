using Proto.Remote;

namespace Proto.Client
{
    public class ClientProxyProcess : Process
    {
        private readonly PID _pid;
        private Client _client;

        public ClientProxyProcess(Client client, PID pid)
        {
            _client = client;
            _pid = pid;
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);

        private void Send(object msg)
        {
           
            _client.SendMessage(_pid, msg, Serialization.DefaultSerializerId);
            
        }
    }
}