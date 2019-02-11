using System;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientProcess : Process
    {
        private PID _endpointWriterPID;
        private PID _clientTargetPID;

        public ClientProcess(PID pid)
        {
            _clientTargetPID = pid;
            
            var clientAddress = new Uri(pid.Address);
            _endpointWriterPID = new PID(clientAddress.Host + ":" + clientAddress.Port, clientAddress.AbsolutePath.Substring(1));
        }

        protected override void SendUserMessage(PID _, object message) => Send(message);

        protected override void SendSystemMessage(PID _, object message) => Send(message);
        
        private void Send(object envelope)
        {
           
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);
            
            var env = new RemoteDeliver(header, message, _clientTargetPID, sender, Serialization.DefaultSerializerId);
            
            RootContext.Empty.Send(_endpointWriterPID, env);
            
        }
    }
}