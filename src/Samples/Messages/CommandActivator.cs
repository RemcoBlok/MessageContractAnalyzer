using System;
using MassTransit.Metadata;
using MassTransit.Serialization;
using Newtonsoft.Json.Linq;

namespace Messages
{
    public static class CommandActivator
    {
        public static T Create<T>() where T : ICommand
        {
            var type = TypeMetadataCache<T>.ImplementationType;
            var command = (T)Activator.CreateInstance(type);
            type.GetProperty(nameof(ICommand.CommandId)).SetValue(command, Guid.NewGuid());
            return command;
        }

        [Activator]
        public static T Create<T>(object values) where T : ICommand
        {
            var jObject = JObject.FromObject(values, JsonMessageSerializer.Serializer);
            jObject[nameof(ICommand.CommandId)] = Guid.NewGuid();
            return jObject.ToObject<T>(JsonMessageSerializer.Deserializer);
        }
    }
}