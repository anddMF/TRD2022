using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL;

namespace Trade02.Business.services
{
    public class MarketService
    {
        private static APICommunication _clientSvc;
        private readonly ILogger<Worker> _logger;

        public MarketService(IHttpClientFactory clientFactory, ILogger<Worker> logger)
        {
            _logger = logger;
            _clientSvc = new APICommunication(clientFactory);
        }

        /// <summary>
        /// Retorna os símbolos com a maior valorização positiva ordenados na decrescente.
        /// </summary>
        /// <param name="numberOfSymbols">quantidade de símbolos que serão retornados</param>
        /// <param name="currencySymbol">símbola da moeda que será utilizada para a compra</param>
        /// <param name="maxPercentage">porcentagem máxima da variação de preço</param>
        /// <returns></returns>
        public async Task<List<IBinanceTick>> GetTopPercentages(int numberOfSymbols, string currencySymbol, decimal maxPercentage, List<string> ownedSymbols = null)
        {
            try
            {
                List<IBinanceTick> allSymbols = await _clientSvc.GetTickers();
                List<IBinanceTick> filteredResult = allSymbols.OrderByDescending(x => x.PriceChangePercent).ToList();
                filteredResult.RemoveAll(x => !x.Symbol.EndsWith(currencySymbol));

                filteredResult = RemoveOwnedCoins(filteredResult, ownedSymbols);

                // maxPercentage=10; faz sentido ter esse limite porque as chances de pegar uma moeda que já cresceu o que tinha que crescer depois
                // de 10% é maior do que uma moeda que valorizou menos de 10%
                filteredResult.RemoveAll(x => x.PriceChangePercent > maxPercentage);
                filteredResult = filteredResult.Take(numberOfSymbols).ToList();
                return filteredResult;
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR at: {DateTimeOffset.Now}, message: {ex.Message}");
                // throw aqui não faz cair na exception da camada que chama esse método
                throw;
            }
        }

        /// <summary>
        /// Remove da lista de símbolos as moedas que já possuem posição em aberto.
        /// </summary>
        /// <param name="allSymbols"></param>
        /// <param name="ownedSymbols"></param>
        /// <returns>lista atualizada</returns>
        private List<IBinanceTick> RemoveOwnedCoins(List<IBinanceTick> allSymbols, List<string> ownedSymbols)
        {
            for (int i = 0; i < ownedSymbols.Count; i++)
            {
                string current = ownedSymbols[i];

                allSymbols.RemoveAll(x => x.Symbol.StartsWith(current));
            }

            return allSymbols;
        }
    }
}
