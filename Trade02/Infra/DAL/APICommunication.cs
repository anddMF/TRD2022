using Binance.Net;
using Binance.Net.Interfaces;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
//using System.Text;
//using System.Text.Json;
using System.Threading.Tasks;

namespace Trade02.Infra.DAL
{
    public class APICommunication
    {
        private static string API_KEY = "";
        private static string BIN_KEY = "";
        private static string BIN_SECRET = "";

        private static IHttpClientFactory _clientFactory;
        private static BinanceClient _binanceClient;

        public APICommunication(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _binanceClient = new BinanceClient(new BinanceClientOptions()
            {
                BaseAddress = "https://api.binance.com",
                ApiCredentials = new ApiCredentials(BIN_KEY, BIN_SECRET)
            });
        }

        public async Task<List<IBinanceTick>> GetTickers()
        {
            var response = await _binanceClient.Spot.Market.GetTickersAsync();
            if (response.Success)
                return response.Data.ToList();
            else
                throw new Exception("ERRO CARAI CARAI");

                //throw new Exception(response.Error.Message);

        }

        /// <summary>
        /// Generic method for different APIs
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
