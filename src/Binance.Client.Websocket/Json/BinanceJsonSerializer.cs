﻿using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Binance.Client.Websocket.Json
{
    public static class BinanceJsonSerializer
    {
        public static JsonSerializerSettings Settings => new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.None,
            Converters = new List<JsonConverter>() { new BinanceStringEnumConverter { CamelCaseText = true} },
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static JsonSerializer Serializer => JsonSerializer.Create(Settings);

        public static T Deserialize<T>(string data)
        {
            return JsonConvert.DeserializeObject<T>(data, Settings);
        }

        public static string Serialize(object data)
        {
            return JsonConvert.SerializeObject(data, Settings);
        }
    }
}
