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
            
            ProcessRegistry.Instance.RegisterHostResolver(pid =>
            {
                Logger.LogDebug($"Testing if {pid} is a client");
                return isClientAddress(pid.Address) ? new ClientProcess(pid) : null;
            });
            
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
                        if (target.Address.Equals(ProcessRegistry.Instance.Address))
                        {
                            Logger.LogDebug(
                                $"Sending message {message} to local target {target} from {envelope.Sender}");
                            RootContext.Empty.Send(target, forwardingEnvelope);
                        }
                        else
                        {
                            Logger.LogDebug($"Sending message to remote target {target}");
                            //todo: We could have forwarded this batch without deserializing contents if we had access to SendEnvelopesAsync from EndPointWriter
                            //todo: If we use a naming prefix for proxies, we could tell if we are sending to another client proxy and avoid deserialization there too
                            Remote.Remote.SendMessage(target, forwardingEnvelope, Serialization.DefaultSerializerId);
                        }


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

        private bool isClientAddress(string argAddress)
        {
            return argAddress.StartsWith("client:");
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
            var endpointWriter = RootContext.Empty.Spawn(Props.FromProducer(() => new ClientHostEndpointWriter(responseStream)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy));
            
            return endpointWriter;

//            var props = Props.FromProducer(() => new ProxyActivator(endpointWriter)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
//            return RootContext.Empty.Spawn(props);
        }

        
    }
}