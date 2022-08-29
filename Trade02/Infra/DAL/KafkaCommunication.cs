using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Trade02.Models.CrossCutting;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Trade02.Infra.DAL.Interfaces;
using Trade02.Models.Trade;
using Trade02.Infra.DAO;

namespace Trade02.Infra.DAL
{
    public class KafkaCommunication : IEventExtCommunication
    {
        private readonly ILogger _logger;

        private readonly string bootstrapServer = AppSettings.KafkaConfiguration.BootstrapServer;
        private readonly string topic = AppSettings.KafkaConfiguration.Topic;
        public KafkaCommunication(ILogger<KafkaCommunication> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Send a message for a Kafka Topic.
        /// </summary>
        /// <param name="message">string message for the topic</param>
        /// <returns>true if the message was persisted, otherwise, returns false</returns>
        public async Task<bool> SendMessage(TradeEvent payload)
        {
            try
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = bootstrapServer
                };

                using (var producer = new ProducerBuilder<string, TradeEventDAO>(config).Build())
                {
                    int tries = 3;
                    bool delivered = false;

                    var record = new Message<string, TradeEventDAO> { Value = payload.GenerateRecord() };

                    while (tries > 0 || delivered == false)
                    {
                        var result = await producer.ProduceAsync(topic, record);

                        _logger.LogInformation($"Message: {payload.Payload} | Status: {result.Status.ToString()}");

                        delivered = result.Status == PersistenceStatus.Persisted || result.Status == PersistenceStatus.PossiblyPersisted ? true : false;
                        tries = delivered ? -1 : tries - 1;
                    }

                    return delivered;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: KafkaCommunication.SendMessage(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Send messages for the topic but ignore the ones that were not persisted on the topic.
        /// </summary>
        /// <param name="messages">list of string messages for the topic</param>
        /// <returns></returns>
        public async Task<bool> SendMessages(List<string> messages)
        {
            try
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = bootstrapServer
                };

                using (var producer = new ProducerBuilder<Null, string>(config).Build())
                {
                    for (int i = 0; i < messages.Count; i++)
                    {
                        string message = messages[i];
                        int tries = 3;
                        bool delivered = false;

                        while (tries > 0 || delivered == false)
                        {
                            var result = await producer.ProduceAsync(topic, new Message<Null, string> { Value = message });

                            _logger.LogInformation($"Message: {message} | Status: {result.Status.ToString()}");

                            delivered = result.Status == PersistenceStatus.Persisted || result.Status == PersistenceStatus.PossiblyPersisted ? true : false;
                            tries = delivered ? -1 : tries - 1;
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR: {DateTime.Now}, metodo: KafkaCommunication.SendMessage(), message: {ex.Message}, \n stack: {ex.StackTrace}");
                return false;
            }
        }
    }
}
