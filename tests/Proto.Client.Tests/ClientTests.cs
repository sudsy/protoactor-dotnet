using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Moq;
using Proto.Remote;
using Proto.Remote.Tests;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Client.Tests
{

    public class ClientTests
    {
        private ITestOutputHelper _testOutputHelper;

        public ClientTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        // [Fact, DisplayTestMethodName]
        // public async Task CanStopRemoteActor(){
        //     var remoteManager = new RemoteManager();

        //      using(var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig(), 10000))
        //     {
        //         var clientHostActor = new PID(remoteManager.DefaultNode.Address, "EchoActorInstance");
        //         var ct = new CancellationTokenSource(60000);
        //         var tcs = new TaskCompletionSource<bool>();
        //         ct.Token.Register(() =>
        //         {
        //             tcs.TrySetCanceled();
        //         });
            
        //         //This needs to spawn with a connection ID
        //         var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
        //         {
        //             switch(ctx.Message){
        //                 case Started _:
        //                     ctx.Watch(clientHostActor);
        //                     break;
        //                 case Terminated terminated:
        //                     tcs.SetResult(true);
        //                     ctx.Stop(ctx.Self);
        //                     break;
        //                 case Pong _:   
        //                     ctx.Stop(clientHostActor);
                            
        //                     break;
        //             }
                    
        //             return Actor.Done;
        //         }));
    
                
        //         var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
        //         var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
                
        //         Client.SendMessage(clientHostActor, envelope, 1);
        //         await tcs.Task;
        //     };
        //     remoteManager.Dispose();
        // }
        // [Fact, DisplayTestMethodName]
        // public async Task TimeoutOnConnectFailure()
        // {

            
        //     await Assert.ThrowsAsync<TimeoutException>(async () => { await Client.CreateAsync("127.0.0.1", 12222, new RemoteConfig(), 1000); });

        // }

        
//        [Fact, DisplayTestMethodName]
//        public async Task ReconnectAfterServerDown()
//        {
//            var remoteManager = new RemoteManager();
//            
//            var remoteActorName = "EchoActor_" + Guid.NewGuid().ToString();
//            var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig(), 1000);
//            var remoteActorResp =
//                await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
//            var remoteActor = remoteActorResp.Pid;
//            var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
//                TimeSpan.FromMilliseconds(5000));
//            
//            Assert.Equal($"{remoteManager.DefaultNode.Address} Hello", pong.Message);
//            remoteManager.Dispose();
//            await Task.Delay(TimeSpan.FromSeconds(3));
//            remoteManager = new RemoteManager();
//            remoteActorResp =
//                await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
//            remoteActor = remoteActorResp.Pid;
//            var pong2 = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
//                TimeSpan.FromMilliseconds(5000));
//            Assert.Equal($"{remoteManager.DefaultNode.Address} Hello", pong2.Message);
//            remoteManager.Dispose();
//
//        }
//        
//        [Fact, DisplayTestMethodName]
//        public async Task ResumeAfterServerDown()
//        {
//            var remoteManager = new RemoteManager();
//            
//            var remoteActorName = "EchoActor_" + Guid.NewGuid().ToString();
//            var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig(), 1000);
//            var remoteActorResp =
//                await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
//            var remoteActor = remoteActorResp.Pid;
//            var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
//                TimeSpan.FromMilliseconds(5000));
//            
//            Assert.Equal($"{remoteManager.DefaultNode.Address} Hello", pong.Message);
//            remoteManager.Dispose();
//            //Give the remote time to shut down
//            await Task.Delay(TimeSpan.FromSeconds(3));
//            //Try sending while we are down - start the remote after a few seconds
//            Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ => { remoteManager = new RemoteManager(); });
//            remoteActorResp =
//                await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(20));
//           
//            
//            
//            remoteActor = remoteActorResp.Pid;
//            var pong2 = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
//                TimeSpan.FromMilliseconds(5000));
//            Assert.Equal($"{remoteManager.DefaultNode.Address} Hello", pong2.Message);
//            remoteManager.Dispose();
//
//        }
//
//        [Fact, DisplayTestMethodName]
//        public async Task ResumeAfterServerDownAndClientDispose()
//        {
//            var remoteManager = new RemoteManager();
//            
//            var remoteActorName = "EchoActor_" + Guid.NewGuid().ToString();
//            var client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig(), 1000);
//            var remoteActorResp =
//                await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(5));
//            var remoteActor = remoteActorResp.Pid;
//            var pong = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
//                TimeSpan.FromMilliseconds(5000));
//            
//            Assert.Equal($"{remoteManager.DefaultNode.Address} Hello", pong.Message);
//            remoteManager.Dispose();
//            //Give the remote time to shut down
//            await Task.Delay(TimeSpan.FromSeconds(3));
//            
//            Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ => { remoteManager = new RemoteManager(); });
////          
//            //Try dispose while we are down - start the remote after a few seconds
//            client.Dispose();
//            
//            client = await Client.CreateAsync("127.0.0.1", 12000, new RemoteConfig(), 1000);
//            remoteActorResp =
//                await client.SpawnOnClientHostAsync(remoteActorName, "EchoActor", TimeSpan.FromSeconds(20));
//           
//            
//            
//            remoteActor = remoteActorResp.Pid;
//            var pong2 = await RootContext.Empty.RequestAsync<Pong>(remoteActor, new Ping {Message = "Hello"},
//                TimeSpan.FromMilliseconds(5000));
//            Assert.Equal($"{remoteManager.DefaultNode.Address} Hello", pong2.Message);
//            remoteManager.Dispose();
//
//        }
       

       
  
    }
}