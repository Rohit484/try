﻿using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNetTry.Protocol.ClientApi
{
    public abstract class FeatureContainerConverter<T> : JsonConverter where T : FeatureContainer
    {
        protected abstract void AddProperties(T container, JObject o);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is T result)
            {
                var o = new JObject();

                AddProperties(result, o);

                foreach (var feature in result.Features.Values.OfType<IFeature>())
                {
                    feature.Apply(result);
                }

                foreach (var property in result.FeatureProperties.OrderBy(p => p.Name))
                {
                    var jToken = JToken.FromObject(property.Value, serializer);
                    o.Add(new JProperty(property.Name, jToken));
                }


                o.WriteTo(writer);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) =>
            throw new NotImplementedException();

        public override bool CanRead { get; } = false;

        public override bool CanWrite { get; } = true;

        public override bool CanConvert(Type objectType) => objectType == typeof(T);
    }
}