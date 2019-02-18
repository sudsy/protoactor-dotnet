using System;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientProcess : Process
    {
        private PID _endpointWriterPID;
        private PID _clientTargetPID;
        private static readonly ILogger Logger = Log.CreateLogger<ClientProcess>();

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
            
            Logger.LogDebug($"Sending Client Message {message} to {_clientTargetPID}");
            
            var env = new RemoteDeliver(header, message, _clientTargetPID, sender, Serialization.DefaultSerializerId);

            if (_endpointWriterPID.Address == ProcessRegistry.Instance.Address)
            {
                RootContext.Empty.Send(_endpointWriterPID, env);
            }
            else
            {
                var messageBatch = new ClientMessageBatch
                {
                    Address = _endpointWriterPID.Address,
                    Batch = env.getMessageBatch()
                };
                RootContext.Empty.Send(_endpointWriterPID, messageBatch);
            }
            
            
            
        }
    }
}