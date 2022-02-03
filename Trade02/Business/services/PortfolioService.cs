using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Trade02.Infra.DAL;
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

        public async Task<List<Position>> ManageOpenPositions(List<Position> openPositions)
        {
            // verificar os dados da moeda, comparar o valor atual com o valor de quando comprou (que está na lista)
            // tirar a porcentagem dessa diferença de valor, se for prejuízo de X%, executar a venda. 
            // Se for lucro, atualizar propriedade do último valor mais alto e fazer essa comparação com esse novo valor
            List<Position> result = new List<Position>();
            try
            {
                for (int i = 0; i < openPositions.Count; i++)
                {
                    Position current = openPositions[i];
                    var market = await _marketSvc.GetSingleTicker(current.Data.Symbol);

                    decimal currentChange = ((market.AskPrice - current.CurrentlPrice) / current.CurrentlPrice) * 100;
                    if (currentChange > 0)
                    {
                        // entra na area de lucro ATUAL, não o total
                        openPositions[i].CurrentlPrice = market.AskPrice;
                        openPositions[i].Valorization += currentChange;
                        result.Add(current);
                    }
                    else
                    {
                        // validação para a venda
                        decimal totalChange = ((market.AskPrice - current.InitialPrice) / current.InitialPrice) * 100;

                        if (totalChange < (decimal)-1.4)
                        {
                            // venda porque já ta no prejuízo
                            // mas retorna essa moeda para continuar acompanhando
                            break;
                        }
                        if (currentChange < (decimal)-1.4)
                        {
                            // venda porque pode ser a tendencia de queda
                            // mas retorna essa moeda para continuar acompanhando
                            break;
                        }
                        result.Add(current);
                    }
                }
                return null;

            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
