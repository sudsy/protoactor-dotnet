using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public static  class Client
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Client).FullName);
        
        private static string _hostname;
        private static int _port;
        private static PID _endpointWriter;
        private static readonly ConcurrentDictionary<string, PID> _remoteProxyTable = new ConcurrentDictionary<string, PID>();
        private static string _clientHostAddress;
        private static Channel _channel;
        private static AsyncDuplexStreamingCall<ClientMessageBatch, MessageBatch> _clientStreams;


        static Client()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }
        
        public static async Task Connect(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _hostname = hostname;
            _port = port;
            
            var tcs = new TaskCompletionSource<bool>();
            
            
            Logger.LogDebug("Connecting to client host");
            
            ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientProxyProcess(pid));
            
            _channel = new Channel(hostname, port, config.ChannelCredentials, config.ChannelOptions);

            await _channel.ConnectAsync(DateTime.UtcNow.AddMilliseconds(connectionTimeoutMs));
            
            var client = new ClientRemoting.ClientRemotingClient(_channel);
            
           
            
            _clientStreams = client.ConnectClient();
            
            
            
            
            Logger.LogDebug("Connected to Client Host");

            _endpointWriter =
                RootContext.Empty.Spawn(Props.FromProducer(() =>
                    new ClientEndpointWriter(_clientStreams.RequestStream)));
            
            
           
            //Setup listener for incoming stream
            var streamListenerTask = Task.Factory.StartNew(async () =>
            {
                var responseStream = _clientStreams.ResponseStream;
                while (await responseStream.MoveNext(CancellationToken.None)
                ) //Need to work out how this might be cancelled
                {
                    Logger.LogDebug("Received Message Batch");
                    var messageBatch = responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,messageBatch.TargetNames[envelope.Target]);
                        
                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId], envelope.MessageData, envelope.SerializerId);

                        if (message is ClientConnectionStarted)
                        {
                            _clientHostAddress = envelope.Sender.Address;
                            ProcessRegistry.Instance.Address = envelope.Sender.Address + "#" + envelope.Sender.Id;
                            tcs.TrySetResult(true);
                            continue;
                        }
                          
                        
                        Logger.LogDebug($"Opened Envelope from {envelope.Sender} for {target} containing message {message}");
                        //todo: Need to convert the headers here
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, null);
                        
                        RootContext.Empty.Send(target, localEnvelope);
                    }
                   
                }
            });



            await tcs.Task;


        }

        public static async Task Disconnect()
        {
            _clientStreams.Dispose();
            await _channel.ShutdownAsync();
        }

        public static async Task<PID> GetProxyPID(PID localPID)
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

  
 

        public static void SendMessage(PID target, object envelope, int serializerId)
        {
           
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);

            var remoteProxyID = sender;
            
            if (!(message is ProxyPidRequest) && sender != null)
            {
                remoteProxyID = GetProxyPID(sender).Result;
            }
            
            var env = new RemoteDeliver(header, message, target, remoteProxyID, serializerId);
            
            RootContext.Empty.Send(_endpointWriter, env);

        }


        public static  Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            return Remote.Remote.SpawnNamedAsync(address, name, kind, timeout);
        }
        
        public static  Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return Remote.Remote.SpawnAsync(address, kind, timeout);
        }

        public static Task<string> GetClientHostAddress()
        {
            
             return Task.FromResult(_clientHostAddress);
           
        }

        public static async Task<ActorPidResponse> SpawnOnClientHostAsync(string name, string kind, TimeSpan timeout)
        {
            var hostAddress = await GetClientHostAddress();
            return await SpawnNamedAsync(hostAddress, name, kind, timeout);
            
        }
        
        public static async Task<ActorPidResponse> SpawnOnClientHostAsync(string kind, TimeSpan timeout)
        {
            var hostAddress = await GetClientHostAddress();
            return await SpawnAsync(hostAddress, kind, timeout);
        }
    }
}