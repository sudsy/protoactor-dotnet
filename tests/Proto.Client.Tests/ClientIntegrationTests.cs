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
            
            using(var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
            {
                var clientHostActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
                var ct = new CancellationTokenSource(30000);
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
                
                Client.SendMessage(clientHostActor, envelope, 1);
                await tcs.Task;
            };
        }

        [Fact, DisplayTestMethodName]
        public async void CanRequestAsyncToRemoteFromClientActor()
        {

            using (var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
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
            using (var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
            {
                var remoteActorName = Guid.NewGuid().ToString();
                var remoteActorResp = await client.SpawnNamedAsync(_remoteManager.DefaultNode.Address, remoteActorName,
                    "EchoActor", TimeSpan.FromSeconds(5));
                var remoteActor = remoteActorResp.Pid;
                var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
            }

        }
        
        [Fact, DisplayTestMethodName]
        public async void CanGetClientHostAddress()
        {
            using (var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
            {
                var address = await client.GetClientHostAddress();
                Assert.Equal("127.0.0.1:12000", address);
            }

        }
        
        [Fact, DisplayTestMethodName]
        public async void CanSpawnClientHostActor()
        {
            using (var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
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
            using (var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
            {
                var remoteActorName = "EchoActor_" + Guid.NewGuid().ToString();
                var remoteActorResp =
                    await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
                remoteActor = remoteActorResp.Pid;
                var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong.Message);
                
            }
//            await Client.Disconnect();

            using (var client = new Client("127.0.0.1", 12000, new RemoteConfig()))
            {

                var pong2 = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
                    TimeSpan.FromMilliseconds(5000));
                Assert.Equal($"{_remoteManager.DefaultNode.Address} Hello", pong2.Message);
            }
        }
        //TODO Write a test to make sure we can watch actors through the client connection


//        [Fact, DisplayTestMethodName]
//        public async void CanCleanUpConnectionManagerOnDisconnect()
//        {
//            var watchcs = new TaskCompletionSource<bool>();
//            var tcs = new TaskCompletionSource<bool>();
//            
//            await Client.Connect("127.0.0.1", 12000, new RemoteConfig());
//            var address = ProcessRegistry.Instance.Address;
//            var clientAddress = new Uri(address);
//            Assert.Equal("127.0.0.1:12000", clientAddress.Authority);
//            Assert.Equal("client", clientAddress.Scheme);
//
//            var endpointManagerId = clientAddress.AbsolutePath.Substring(1);
//            var endpointPid = new PID(clientAddress.Authority, endpointManagerId);
//
//            Remote.Remote.Start("127.0.0.1", 12222);
//            RootContext.Empty.Spawn(Props.FromFunc(context =>
//            {
//                switch (context.Message)
//                {
//                    case Started _:
//                        context.Watch(endpointPid);
//                        watchcs.TrySetResult(true);
//                        break;
//                    case Terminated terminated:
//                        if (terminated.Who.Id == endpointManagerId)
//                        {
//                            tcs.SetResult(true);
//                        }
//
//                        break;
//                }
//
//                return Actor.Done;
//
//            }));
//
//            await Task.Delay(TimeSpan.FromSeconds(1));
////            endpointPID.Stop();
//            await Client.Disconnect();
//
//            await tcs.Task;
//
//
//        }

    }
}