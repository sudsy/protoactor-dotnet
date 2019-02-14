using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointManager: ClientRemoting.ClientRemotingBase
    {
        
        private static readonly ILogger Logger = Log.CreateLogger(typeof(ClientEndpointManager).FullName);
        
        public ClientEndpointManager(string clientHostAddress)
        {
            _clientHostAddress = clientHostAddress;
          
            
        }
        
        
        private readonly string _clientHostAddress;

        public override async Task ConnectClient(IAsyncStreamReader<ClientMessageBatch> requestStream,
            IServerStreamWriter<MessageBatch> responseStream, ServerCallContext context)
        {
            
            Logger.LogDebug($"Spawning Client EndpointWriter");
            
            var clientEndpointWriter = SpawnClientEndpointWriter(responseStream);
            
           
            
            try
            {
                while (await requestStream.MoveNext())
                {
                    var clientMessageBatch = requestStream.Current;

                    var targetAddress = clientMessageBatch.Address;

                    Logger.LogDebug($"Received Batch for {targetAddress}");

                    foreach (var envelope in clientMessageBatch.Batch.Envelopes)
                    {

                        var message = Serialization.Deserialize(clientMessageBatch.Batch.TypeNames[envelope.TypeId],
                            envelope.MessageData, envelope.SerializerId);

                        Logger.LogDebug($"Batch Message {message}");

                        var target = new PID(targetAddress, clientMessageBatch.Batch.TargetNames[envelope.Target]);

                      
                        Logger.LogDebug($"Target is {target}");

                       

                        //Forward the message to the correct endpoint
                        Proto.MessageHeader header = null;
                        if (envelope.MessageHeader != null)
                        {
                            header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
                        }

                        var forwardingEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);

                        Logger.LogDebug($"Sending message {message} to target {target} from {envelope.Sender}");
                        
                        RootContext.Empty.Send(target, forwardingEnvelope);



                    }
                }

                Logger.LogDebug("Finished Request Stream - stopping connection manager");
                clientEndpointWriter.Stop();
                
            }
                
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Exception on Client Host");
                throw ex;
            }

            

              
        }

      

        private PID SpawnClientHostAddressResponder()
        {
            return RootContext.Empty.Spawn(Props.FromFunc(context =>
            {
//                if (context.Message is ClientHostAddressRequest)
//                {
//                    context.Respond(new ClientHostAddressResponse(){Address = ProcessRegistry.Instance.Address});
//                }

                return Actor.Done;
            }));
        }


        private static PID SpawnClientEndpointWriter(IServerStreamWriter<MessageBatch> responseStream)
        {
            
            //TOD, make one of these supervise the other so we can shutdown the whole tree if the connection dies
            var endpointWriter = RootContext.Empty.SpawnNamed(Props.FromProducer(() => new ClientHostEndpointWriter(responseStream)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy), Guid.NewGuid().ToString());
            
            return endpointWriter;

//            var props = Props.FromProducer(() => new ProxyActivator(endpointWriter)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
//            return RootContext.Empty.Spawn(props);
        }

        
    }
}