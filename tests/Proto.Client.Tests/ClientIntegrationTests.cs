using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Proto.Remote;
using Proto.Remote.Tests;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Client.Tests
{

    
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class ClientIntegrationTests
    {
        
       
        private readonly RemoteManager _remoteManager;
        

        public ClientIntegrationTests(RemoteManager remoteManager, ITestOutputHelper testOutputHelper)
        {
           
            _remoteManager = remoteManager;
            
            Log.SetLoggerFactory(new LoggerFactory().AddConsole(LogLevel.Debug));
           
        }
        
       
        
        
        [Fact, DisplayTestMethodName]
        public async void CanSendJsonAndReceiveToClient()
        {
            
                
            using(var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig(), 10000))
            {
                var clientHostActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
                var ct = new CancellationTokenSource(60000);
                var tcs = new TaskCompletionSource<bool>();
                ct.Token.Register(() =>
                {
                    tcs.TrySetCanceled();
                });
            
                //This needs to spawn with a connection ID
                var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
                {
                    if (ctx.Message is Pong)
                    {
                        tcs.SetResult(true);
                        ctx.Self.Stop();
                    }
    
                    return Actor.Done;
                }));
    
                
                var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
                var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
                
                client.SendMessage(clientHostActor, envelope, 1);
                await tcs.Task;
            };
        }

//        [Fact, DisplayTestMethodName]
        public async void CanRequestAsyncToRemoteFromClientActor()
        {

            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
            {
                var remoteActor = new PID(_remoteManager.RemoteNode.Address, "EchoActorInstance");
            
                var ct = new CancellationTokenSource(30000);
                var tcs = new TaskCompletionSource<bool>();
                ct.Token.Register(() =>
                {
                    tcs.TrySetCanceled();
                });
            
                //This needs to spawn with a connection ID
                var proxyActor = RootContext.Empty.Spawn(Props.FromFunc(async ctx =>
                {
                    if (ctx.Message is Started)
                    {
                   
                        var result = await ctx.RequestAsync<Pong>(remoteActor, new Ping()
                        {
                            Message = "Hello to remote from inside"
                        });
                        tcs.SetResult(true);
                        ctx.Self.Stop();
                    }
                
                
                }));
            
           
                await tcs.Task;
            }

           
            
        }

      
        
        
        [Fact, DisplayTestMethodName]
        public async void CanSpawnRemoteActor()
        {
            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
            {
                var remoteActorName = Guid.NewGuid().ToString();
                var remoteActorResp = await client.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName,
                    "EchoActor", TimeSpan.FromSeconds(5));
                var remoteActor = remoteActorResp.Pid;
                var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(50000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
            }

        }
        
        [Fact, DisplayTestMethodName]
        public async void CanGetClientHostAddress()
        {
            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
            {
                var pid = await client.GetClientHostPID(TimeSpan.FromSeconds(5));
                Assert.Equal("127.0.0.1:12000", pid.Address);
            }

        }
        
        [Fact, DisplayTestMethodName]
        public async void CanSpawnClientHostActor()
        {
            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
            {
                var remoteActorName = Guid.NewGuid().ToString();
                var remoteActorResp =
                    await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
                var remoteActor = remoteActorResp.Pid;
                var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
            }

        }
        
        
        [Fact, DisplayTestMethodName]
        public async void CanConnectMultipleTimes()
        {
            PID remoteActor;
            string firstAddress;
            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
            {
                var remoteActorName = "EchoActor_" + Guid.NewGuid().ToString();
                var remoteActorResp =
                    await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
                remoteActor = remoteActorResp.Pid;
                var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
                firstAddress = ProcessRegistry.Instance.Address;
            }

            
            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
            {

                var pong2 = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong2.Message);
                Assert.Equal(firstAddress, ProcessRegistry.Instance.Address);
            }
        }
        
        [Fact, DisplayTestMethodName]
        public async void CanOverlapConnections()
        {

            var client1 = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig());
            var firstAddress = ProcessRegistry.Instance.Address;
            
            var remoteActorName = "EchoActor_" + Guid.NewGuid().ToString();
            var remoteActorResp =
                    await client1.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var remoteActor = remoteActorResp.Pid;
            var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
            
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
            
            var client2 = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig());   
            


            var remoteActor2Name = "EchoActor_" + Guid.NewGuid().ToString();
            var remoteActor2Resp =
                await client2.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
            var remoteActor2 = remoteActorResp.Pid;
            var pong2 = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                TimeSpan.FromMilliseconds(5000));
            Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong2.Message);
            Assert.Equal(firstAddress, ProcessRegistry.Instance.Address);
            
            client1.Dispose();
            client2.Dispose();
            
        }
        //TODO Write a test to make sure we can watch actors through the client connection

//        [Fact, DisplayTestMethodName]
//        public async void CanHandleUnknownHosts()
//        {
//            using (var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig()))
//            {
//                var remoteActorName = Guid.NewGuid().ToString();
//               
//                var testPID = new PID("127.0.0.1:1234", "$1");
//                RootContext.Empty.Send(testPID, new Ping {Message = "Hello"});
//                await Task.Delay(TimeSpan.FromSeconds(240));
//            }
//
//        }


    }
}