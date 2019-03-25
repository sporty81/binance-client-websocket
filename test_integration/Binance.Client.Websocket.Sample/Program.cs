﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Binance.Client.Websocket.Client;
using Binance.Client.Websocket.Requests;
using Binance.Client.Websocket.Websockets;
using Serilog;
using Serilog.Events;

namespace Binance.Client.Websocket.Sample
{
    class Program
    {
        private static readonly ManualResetEvent ExitEvent = new ManualResetEvent(false);

        private static readonly string API_KEY = "your api key";
        private static readonly string API_SECRET = "";

        static void Main(string[] args)
        {
            InitLogging();

            AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;
            AssemblyLoadContext.Default.Unloading += DefaultOnUnloading;
            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            Console.WriteLine("|=======================|");
            Console.WriteLine("|     BINANCE CLIENT    |");
            Console.WriteLine("|=======================|");
            Console.WriteLine();

            Log.Debug("====================================");
            Log.Debug("              STARTING              ");
            Log.Debug("====================================");
           


            var url = BinanceValues.ApiWebsocketUrl;
            using (var communicator = new BinanceWebsocketCommunicator(url))
            {
                communicator.Name = "Binance-1";
                communicator.ReconnectTimeoutMs = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
                communicator.ReconnectionHappened.Subscribe(type =>
                    Log.Information($"Reconnection happened, type: {type}"));

                communicator.MessageReceived.Subscribe(msg =>
                {
                    var t = msg.Binary;
                    var d = t != null ? Encoding.UTF8.GetString(t) : null;
                    if(d != null)
                        Log.Information($"Received binary: {d}");
                });

                using (var client = new BinanceWebsocketClient(communicator))
                {

                    client.Streams.InfoStream.Subscribe(info =>
                    {
                        Log.Information($"Reconnection happened, Message: {info.Info}, Version: {info.Version:D}");
                        SendSubscriptionRequests(client).Wait();
                    });   

                    SubscribeToStreams(client);

                    communicator.Start();

                    ExitEvent.WaitOne();
                }
            }

            Log.Debug("====================================");
            Log.Debug("              STOPPING              ");
            Log.Debug("====================================");
            Log.CloseAndFlush();
        }

        private static async Task SendSubscriptionRequests(BinanceWebsocketClient client)
        {
            await client.Send(new PingRequest());
            await client.Send(new BookSubscribeRequest("XBTUSD"));
            await client.Send(new TradesSubscribeRequest("XBTUSD"));
            //await client.Send(new TradeBinSubscribeRequest("1m", "XBTUSD"));
            //await client.Send(new TradeBinSubscribeRequest("5m", "XBTUSD"));
            //await client.Send(new QuoteSubscribeRequest("XBTUSD"));
            //await client.Send(new LiquidationSubscribeRequest());
            //await client.Send(new InstrumentSubscribeRequest("XBTUSD"));

            if (!string.IsNullOrWhiteSpace(API_SECRET))
                await client.Send(new AuthenticationRequest(API_KEY, API_SECRET));
        }

        private static void SubscribeToStreams(BinanceWebsocketClient client)
        {
            client.Streams.ErrorStream.Subscribe(x =>
                Log.Warning($"Error received, message: {x.Error}, status: {x.Status}"));

            client.Streams.AuthenticationStream.Subscribe(x =>
            {
                Log.Information($"Authentication happened, success: {x.Success}");
                client.Send(new WalletSubscribeRequest()).Wait();
                client.Send(new OrderSubscribeRequest()).Wait();
                client.Send(new PositionSubscribeRequest()).Wait();
            });


            client.Streams.SubscribeStream.Subscribe(x =>
            {
                Log.Information(x.IsSubscription
                    ? $"Subscribed ({x.Success}) to {x.Subscribe}"
                    : $"Unsubscribed ({x.Success}) from {x.Unsubscribe}");
            });

            client.Streams.PongStream.Subscribe(x =>
                Log.Information($"Pong received ({x.Message})"));


            client.Streams.WalletStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                    Log.Information($"Wallet {x.Account}, {x.Currency} amount: {x.BalanceBtc}"))
            );

            client.Streams.OrderStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                    Log.Information(
                        $"Order {x.Symbol} updated. Time: {x.Timestamp:HH:mm:ss.fff}, Amount: {x.OrderQty}, " +
                        $"Price: {x.Price}, Direction: {x.Side}, Working: {x.WorkingIndicator}, Status: {x.OrdStatus}"))
            );

            client.Streams.PositionStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                    Log.Information(
                        $"Position {x.Symbol}, {x.Currency} updated. Time: {x.Timestamp:HH:mm:ss.fff}, Amount: {x.CurrentQty}, " +
                        $"Price: {x.LastPrice}, PNL: {x.UnrealisedPnl}"))
            );

            client.Streams.TradesStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                    Log.Information($"Trade {x.Symbol} executed. Time: {x.Timestamp:mm:ss.fff}, [{x.Side}] Amount: {x.Size}, " +
                                    $"Price: {x.Price}"))
            );

            client.Streams.BookStream.Subscribe(book =>
                book.Data.Take(100).ToList().ForEach(x => Log.Information(
                    $"Book | {book.Action} pair: {x.Symbol}, price: {x.Price}, amount {x.Size}, side: {x.Side}"))
            );

            client.Streams.QuoteStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                    Log.Information($"Quote {x.Symbol}. Bid: {x.BidPrice} - {x.BidSize} Ask: {x.AskPrice} - {x.AskSize}"))
            );

            client.Streams.LiquidationStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                    Log.Information(
                        $"Liquadation Action: {y.Action}, OrderID: {x.OrderID}, Symbol: {x.Symbol}, Side: {x.Side}, Price: {x.Price}, LeavesQty: {x.leavesQty}"))
            );

            client.Streams.TradeBinStream.Subscribe(y =>
                y.Data.ToList().ForEach(x =>
                Log.Information($"TradeBin table:{y.Table} {x.Symbol} executed. Time: {x.Timestamp:mm:ss.fff}, Open: {x.Open}, " +
                        $"Close: {x.Close}, Volume: {x.Volume}, Trades: {x.Trades}"))
            );

            client.Streams.InstrumentStream.Subscribe(x =>
            {
                x.Data.ToList().ForEach(y =>
                {
                    Log.Information($"Instrument, {y.Symbol}, " +
                                    $"price: {y.MarkPrice}, last: {y.LastPrice}, " +
                                    $"mark: {y.MarkMethod}, fair: {y.FairMethod}, direction: {y.LastTickDirection}, " +
                                    $"funding: {y.FundingRate} i: {y.IndicativeFundingRate} s: {y.FundingQuoteSymbol}");
                });
            });


            // example of unsubscribe requests
            //Task.Run(async () =>
            //{
            //    await Task.Delay(5000);
            //    await client.Send(new BookSubscribeRequest("XBTUSD") {IsUnsubscribe = true});
            //    await Task.Delay(5000);
            //    await client.Send(new TradesSubscribeRequest() {IsUnsubscribe = true});
            //});
        }

        private static void InitLogging()
        {
            var executingDir = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var logPath = Path.Combine(executingDir, "logs", "verbose.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                .WriteTo.ColoredConsole(LogEventLevel.Verbose)
                .CreateLogger();
        }

        private static void CurrentDomainOnProcessExit(object sender, EventArgs eventArgs)
        {
            Log.Warning("Exiting process");
            ExitEvent.Set();
        }

        private static void DefaultOnUnloading(AssemblyLoadContext assemblyLoadContext)
        {
            Log.Warning("Unloading process");
            ExitEvent.Set();
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Log.Warning("Canceling process");
            e.Cancel = true;
            ExitEvent.Set();
        }
    }
}
