using Binance.Net.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL;
using Trade02.Models.CrossCutting;
using Trade02.Models.Trade;

namespace Trade02.Business.services
{
    /// <summary>
    /// Responsável por manipular dados da carteira de investimentos.
    /// </summary>
    public class PortfolioService
    {
        private static APICommunication _clientSvc;
        private static MarketService _marketSvc;
        private readonly ILogger<Worker> _logger;

        public PortfolioService(IHttpClientFactory clientFactory, ILogger<Worker> logger)
        {
            _logger = logger;
            _clientSvc = new APICommunication(clientFactory);
            _marketSvc = new MarketService(clientFactory, logger);
        }

        public async Task<PortfolioResponse> ManageOpenPositions(List<Position> openPositions, List<IBinanceTick> previousData)
        {
            // verificar os dados da moeda, comparar o valor atual com o valor de quando comprou (que está na lista)
            // tirar a porcentagem dessa diferença de valor, se for prejuízo de X%, executar a venda. 
            // Se for lucro, atualizar propriedade do último valor mais alto e fazer essa comparação com esse novo valor
            List<Position> result = new List<Position>();
            try
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    Position currentPosition = openPositions[i];
                    var marketPosition = await _marketSvc.GetSingleTicker(currentPosition.Data.Symbol);

                    decimal currentChange = ((marketPosition.AskPrice - currentPosition.CurrentPrice) / currentPosition.CurrentPrice) * 100;
                    if (currentChange > 0)
                    {
                        // entra na area de lucro ATUAL, não o total

                        openPositions[i].CurrentPrice = marketPosition.AskPrice;
                        openPositions[i].Valorization += currentChange;
                        result.Add(currentPosition);
                    }
                    else
                    {
                        // validação para a venda
                        decimal totalChange = ((marketPosition.AskPrice - currentPosition.InitialPrice) / currentPosition.InitialPrice) * 100;

                        if (totalChange < (decimal)-1.4)
                        {
                            // venda porque já ta no prejuízo
                            // também salva no arquivo a linha de venda

                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.LastValue);
                            // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                            previousData.Add(marketPosition);
                            break;
                        }
                        if (currentChange < (decimal)-1.4)
                        {
                            // venda porque pode ser a tendencia de queda
                            // também salva no arquivo a linha de venda

                            var order = await _marketSvc.PlaceSellOrder(currentPosition.Data.Symbol, currentPosition.LastValue);
                            // retorna a moeda para o previous para continuar acompanhando caso seja uma queda de mercado
                            previousData.Add(marketPosition);
                            //order.Price;
                            break;
                        }
                        result.Add(currentPosition);
                    }
                }

                return new PortfolioResponse(previousData, result);

            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
