using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL;
using Trade02.Infra.DAO;

namespace Trade02.Business.services
{
    public class ListingsService
    {
        private static string API_KEY = "16cc45b6-9f40-47d2-85b4-dcf96debd52f";
        private static IHttpClientFactory _clientFactory;
        private static APICommunication _clientSvc;
        public ListingsService(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _clientSvc = new APICommunication(clientFactory);
        }

        public async Task<List<ListingsDataDAO>> GetTopPercentages(int limit)
        {
            string endpoint = "https://pro-api.coinmarketcap.com/";
            Dictionary<string, string> param = new Dictionary<string, string>();
            param.Add("percent_change_24h_min", "3");
            param.Add("limit", limit.ToString());

            string finalEndpoint = _clientSvc.CreateUrlQuery("v1/cryptocurrency/listings/latest", param);

            var res = await _clientSvc.GetStuff<ListingsDAO>("coinMarket", finalEndpoint);
            return ConvertResponse(res);
        }

        // A lista de resposta vem dentro do objeto data
        public List<ListingsDataDAO> ConvertResponse(List<ListingsDAO> response)
        {
            List<ListingsDataDAO> data = new List<ListingsDataDAO>();
            if (response.Count > 0)
                data = response[0].data;

            return data;
        }
    }
}
