using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public class WalletManagement
    {
        private static readonly string pathFolder = string.Format("{0}{1}", Directory.GetCurrentDirectory(), "\\WALLET");
        public static bool AddPositionToFile(Position position, decimal currentProfit)
        {
            try
            {
                if (!Directory.Exists(pathFolder))
                    Directory.CreateDirectory(pathFolder);

                string filePath = $"{pathFolder}\\positions.csv";

                if (!File.Exists(filePath))
                {
                    CreateFile(position, currentProfit, filePath);
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filePath))
                    {
                        sw.WriteLine($"{DateTime.Now}; {position.Data.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Quantity}; {position.Type}; {currentProfit};");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO ao adicionar posicoes no arquivo positions: " + ex.Message);
                return false;
            }
        }

        private static void CreateFile(Position position, decimal currentProfit, string filepath)
        {
            using (StreamWriter sw = File.CreateText(filepath))
            {
                sw.WriteLine($"DATE;ASSET;INITIAL PRICE;FINAL PRICE;INITIAL TOTAL; FINAL TOTAL;VALORIZATION;QUANTITY;REC TYPE;CURRENT VALORIZATION;");
                sw.WriteLine($"{DateTime.Now}; {position.Data.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Quantity}; {position.Type}; {currentProfit};");
            }
        }

        public static List<Position> GetPositionFromFile()
        {
            try
            {
                if (!Directory.Exists(pathFolder))
                    return null;

                List<Position> positions = File.ReadAllLines(pathFolder).Skip(1).Select(x => TransformLineIntoPosition(x)).ToList();

                return positions;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO ao restaurar posicoes: " + ex.Message);
                return null;
            }
        }

        private static Position TransformLineIntoPosition(string line)
        {
            string[] values = line.Split(';');
            var position = new Position(values[1], Convert.ToDecimal(values[2]), Convert.ToDecimal(values[7]));
            position.Valorization = Convert.ToDecimal(values[6]);
            position.LastPrice = Convert.ToDecimal(values[3]);
            position.Type = ConvertRecType(values[8]);

            return position;
        }

        private static RecommendationType ConvertRecType(string value)
        {
            switch (value.ToLower())
            {
                case "day":
                    return RecommendationType.Day;
                    
                case "hour":
                    return RecommendationType.Hour;
                    
                case "minute":
                    return RecommendationType.Minute;

                default:
                    return RecommendationType.Day;
            }
        }
    }
}
