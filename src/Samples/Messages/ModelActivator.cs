using MassTransit.Metadata;
using MassTransit.Serialization;
using Newtonsoft.Json.Linq;
using System;

namespace Messages
{
    public static class ModelActivator
    {
        public static T Create<T>()
        {
            return (T)Activator.CreateInstance(TypeMetadataCache<T>.ImplementationType);
        }

        [Activator]
        public static T Create<T>(object values)
        {
            var jToken = JToken.FromObject(values, JsonMessageSerializer.Serializer);
            return jToken.ToObject<T>(JsonMessageSerializer.Deserializer);
        }
    }
}
