using System.Collections.Generic;
using Proto.Remote;

namespace Proto.Client
{
    public static class RemoteDeliverExtensions
    {
        internal static MessageBatch getMessageBatch(this RemoteDeliver rd)
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