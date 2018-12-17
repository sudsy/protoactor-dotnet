using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class Client
    {
        
        
        private readonly string _hostname;
        private readonly int _port;
        private readonly PID _endpointWriter;
        private readonly ConcurrentDictionary<string, PID> _remoteProxyTable = new ConcurrentDictionary<string, PID>();


        static Client()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }
        
        public Client(string hostname, int port, RemoteConfig config)
        {
            _hostname = hostname;
            _port = port;
            
           
            
            ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientProxyProcess(this, pid));
            
            Channel channel = new Channel(hostname, port, config.ChannelCredentials, config.ChannelOptions);
            var client = new ClientRemoting.ClientRemotingClient(channel);
            
            
            var clientStreams = client.ConnectClient();

            _endpointWriter =
                RootContext.Empty.Spawn(Props.FromProducer(() =>
                    new ClientEndpointWriter(clientStreams.RequestStream)));
            

            //Setup listener for incoming stream
            Task.Factory.StartNew(async () =>
            {
                var responseStream = clientStreams.ResponseStream;
                while (await responseStream.MoveNext(CancellationToken.None)
                ) //Need to work out how this might be cancelled
                {
                    var messageBatch = responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,messageBatch.TargetNames[envelope.Target]);
                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId], envelope.MessageData, envelope.SerializerId);
                        //todo: Need to convert the headers here
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, null);
                        
                        RootContext.Empty.Send(target, localEnvelope);
                    }
                   
                }
            });
            
            
        }

        public async Task<PID> GetProxyPID(PID localPID)
        {
            //This needs a cache
            var localPidString = localPID.ToString();

            _remoteProxyTable.TryGetValue(localPidString, out var remoteProxyPid);
            
            if (remoteProxyPid != null)
            {
                return remoteProxyPid;
            }
            
            var activator = new PID($"{_hostname}:{_port}", "proxy_activator");
            
            var proxyResponseMessage = await RootContext.Empty.RequestAsync<ProxyPidResponse>(activator, new ProxyPidRequest()
            {
                ClientPID = localPID
            });
            
            
            remoteProxyPid =  proxyResponseMessage.ProxyPID;
            
            _remoteProxyTable.TryAdd(localPidString, remoteProxyPid);

            return remoteProxyPid;
        }

  
 

        public void SendMessage(PID target, object envelope, int serializerId)
        {
           
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);

            var remoteProxyID = sender;
            
            if (!(message is ProxyPidRequest))
            {
                remoteProxyID = GetProxyPID(sender).Result;
            }
            
            var env = new RemoteDeliver(header, message, target, remoteProxyID, serializerId);
            
            RootContext.Empty.Send(_endpointWriter, env);

        }
        
        internal static MessageBatch getMessageBatch(RemoteDeliver rd)
        {
            var envelopes = new List<Remote.MessageEnvelope>();
            var typeNames = new Dictionary<string,int>();
            var targetNames = new Dictionary<string,int>();
            var typeNameList = new List<string>();
            var targetNameList = new List<string>();
                
            var targetName = rd.Target.Id;
            var serializerId = rd.SerializerId == -1 ? Serialization.DefaultSerializerId : rd.SerializerId;

            if (!targetNames.TryGetValue(targetName, out var targetId))
            {
                targetId = targetNames[targetName] = targetNames.Count;
                targetNameList.Add(targetName);
            }

            var typeName = Serialization.GetTypeName(rd.Message, serializerId);
            if (!typeNames.TryGetValue(typeName, out var typeId))
            {
                typeId = typeNames[typeName] = typeNames.Count;
                typeNameList.Add(typeName);
            }

            Remote.MessageHeader header = null;
            if (rd.Header != null && rd.Header.Count > 0)
            {
                header = new Remote.MessageHeader();
                header.HeaderData.Add(rd.Header.ToDictionary());
            }

            var bytes = Serialization.Serialize(rd.Message, serializerId);
            var envelope = new Remote.MessageEnvelope
            {
                MessageData = bytes,
                Sender = rd.Sender,
                Target = targetId,
                TypeId = typeId,
                SerializerId = serializerId,
                MessageHeader = header,
            };

            envelopes.Add(envelope);
                
            var batch = new MessageBatch();
            batch.TargetNames.AddRange(targetNameList);
            batch.TypeNames.AddRange(typeNameList);
            batch.Envelopes.AddRange(envelopes);

            return batch;
        }

    }
}