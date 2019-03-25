﻿using System.Reactive.Subjects;
using Binance.Client.Websocket.Json;
using Binance.Client.Websocket.Messages;
using Newtonsoft.Json.Linq;

namespace Binance.Client.Websocket.Responses.Trades
{
    /// <summary>
    /// Trades response
    /// </summary>
    public class TradeResponse : ResponseBase
    {
        /// <summary>
        /// Operation type
        /// </summary>
        public override MessageType Op => MessageType.Trade;

        /// <summary>
        /// All latest trades
        /// </summary>
        public Trade[] Data { get; set; }


        internal static bool TryHandle(JObject response, ISubject<TradeResponse> subject)
        {
            if (response?["table"]?.Value<string>() != "trade")
                return false;

            var parsed = response.ToObject<TradeResponse>(BinanceJsonSerializer.Serializer);
            subject.OnNext(parsed);

            return true;
        }
    }
}
