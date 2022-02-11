using Binance.Net;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using Binance.Net.Objects.Spot.MarketData;
using Binance.Net.Objects.Spot.SpotData;
using CryptoExchange.Net.Authentication;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
//using System.Text;
//using System.Text.Json;
using System.Threading.Tasks;
using Trade02.Models.CrossCutting;

namespace Trade02.Infra.DAL
{
    public class APICommunication
    {
        private static string API_KEY = "";

        private static IHttpClientFactory _clientFactory;
        private static BinanceClient _binanceClient;

        public APICommunication(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _binanceClient = new BinanceClient(new BinanceClientOptions()
            {
                BaseAddress = AppSettings.ApiConfiguration.Address,
                ApiCredentials = new ApiCredentials(AppSettings.ApiConfiguration.Key, AppSettings.ApiConfiguration.Secret)
            });
        }

        /// <summary>
        /// Get dos dados de criptomoedas nas últimas 24h.
        /// </summary>
        /// <returns></returns>
        public async Task<List<IBinanceTick>> GetTickers()
        {
            // await _binanceClient.General.GetDailySpotAccountSnapshotAsync();
            var response = await _binanceClient.Spot.Market.GetTickersAsync();

            if (response.Success)
                return response.Data.ToList();
            else
                throw new Exception(response.Error.Message);

        }

        /// <summary>
        /// Get dos dados de uma única moeda nas últimas 24h.
        /// </summary>
        /// <param name="symbol">símbolo que será retornado</param>
        /// <returns></returns>
        public async Task<IBinanceTick> GetTicker(string symbol)
        {
            var response = await _binanceClient.Spot.Market.GetTickerAsync(symbol);

            if (response.Success)
                return response.Data;
            else
                throw new Exception(response.Error.Message);

        }

        /// <summary>
        /// Get do preço médio do símbolo de input
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public async Task<BinanceAveragePrice> GetAvgPrice(string symbol)
        {
            var response = await _binanceClient.Spot.Market.GetCurrentAvgPriceAsync(symbol);

            if (response.Success)
                return response.Data;
            else
                throw new Exception(response.Error.Message);
        }

        public async Task<BinanceOrderBook> GetOrderBook(string symbol, int limit)
        {
            var res = await _binanceClient.Spot.Market.GetOrderBookAsync(symbol, limit);

            if (res.Success)
                return res.Data;
            else
                throw new Exception(res.Error.Message);

        }

        public async Task<List<IBinanceKline>> GetKlines(string symbol, KlineInterval interval)
        {
            var res = await _binanceClient.Spot.Market.GetKlinesAsync(symbol, interval);

            if (res.Success)
                return res.Data.ToList();
            else
                throw new Exception(res.Error.Message);
        }

        /// <summary>
        /// Método para enviar uma ordem na Binance
        /// </summary>
        /// <param name="symbol">símbolo da ordem</param>
        /// <param name="quantity">quantidade a ser executada</param>
        /// <param name="operation">Buy ou Sell</param>
        /// <returns></returns>
        public async Task<BinancePlacedOrder> PlaceOrder(string symbol, decimal quantity, OrderSide operation)
        {
            // "BTCUSDT" vai comprar BTC com USDT, coloca o quoteOrderQuantity que vai setar quantos USDT vai gastar para comprar BTC
            if(operation == OrderSide.Buy)
            {
                var response = await _binanceClient.Spot.Order.PlaceOrderAsync(symbol, operation, OrderType.Market, quoteOrderQuantity: Math.Truncate(quantity));

                if (response.Success)
                    return response.Data;
                else
                    throw new Exception(response.Error.Message);
            } else
            {
                // para venda o quantity é utilizado, pois ele recebe a quantidade da moeda possuída que será vendida
                var response = await _binanceClient.Spot.Order.PlaceOrderAsync(symbol, operation, OrderType.Market, quantity: quantity);

                if (response.Success)
                    return response.Data;
                else
                    throw new Exception(response.Error.Message);
            }
            //var open = await _binanceClient.Spot.Order.GetOpenOrdersAsync("MANAUSDT");
        }

        /// <summary>
        /// Get das informações da conta. Faz um filtro nos balances pois o endpoint retorna todas as moedas da binance, incluindo as que o user não possui saldo.
        /// Portanto, o linq separa somente as com saldo.
        /// </summary>
        /// <returns></returns>
        public async Task<BinanceAccountInfo> GetAccountInfo()
        {
            BinanceAccountInfo data = new BinanceAccountInfo();
            int tryCounter = 0;
            while(tryCounter < 3)
            {
                var response = await _binanceClient.General.GetAccountInfoAsync();
                if (response.Success)
                {
                    data = response.Data;
                    data.Balances = from obj in data.Balances
                                    where obj.Total > 0
                                    select obj;
                    tryCounter = 4;
                }
                else
                {
                    tryCounter++;

                    if(tryCounter >= 3)
                        throw new Exception(response.Error.Message);
                }
            }
            
            
            return data;
        }

        /// <summary>
        /// Método genérico para APIs diferentes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="site"></param>
        /// <param name="endpoint"></param>
        /// <returns>Task with the result</returns>
        public async Task<List<T>> GetStuff<T>(string site, string endpoint)
        {

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            
            var client = _clientFactory.CreateClient(site);
            
            client.DefaultRequestHeaders.Add("X-CMC_PRO_API_KEY", API_KEY);
            client.DefaultRequestHeaders.Add("Accepts", "application/json");

            try
            {
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string resString = await response.Content.ReadAsStringAsync();
                    using var responseStream = await response.Content.ReadAsStreamAsync();
                    var result = JsonConvert.DeserializeObject<T>(resString);
                    //var result = await JsonSerializer.DeserializeAsync<T[]>(responseStream);
                    var list = new List<T>();
                    list.Add(result);
                    return list;
                }
                else
                {
                    return new List<T>();
                }
            } catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }

        }

        /// <summary>
        /// Cria uma url com query params
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string CreateUrlQuery(string endpoint, Dictionary<string, string> param)
        {
            string result = endpoint + "?";

            foreach (var item in param)
            {
                result = result + "&" + item.Key + "=" + item.Value;
            }

            return result;
        }
    }
}
