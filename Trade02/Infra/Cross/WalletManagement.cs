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
        private static readonly string filePath = $"{pathFolder}\\positions.csv";
        public static bool AddPositionToFile(Position position, decimal currentProfit, decimal currentUSDTProfit)
        {
            try
            {
                if (!Directory.Exists(pathFolder))
                    Directory.CreateDirectory(pathFolder);

                if (!File.Exists(filePath))
                {
                    CreateFile(position, currentProfit, currentUSDTProfit, filePath);
                }
                else
                {
                    using (StreamWriter sw = File.AppendText(filePath))
                    {
                        sw.WriteLine($"{DateTime.Now}; {position.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Quantity}; {position.Type}; {currentProfit}; {currentUSDTProfit}");
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

        public static bool RemovePositionFromFile(string symbol, decimal currentProfit, decimal currentUSDTProfit)
        {
            var filePositions = GetPositionFromFile();
            filePositions = filePositions.FindAll(x => x.Symbol != symbol);

            File.Delete(filePath);

            if (filePositions.Count > 0)
            {
                for(int i = 0; i < filePositions.Count; i++)
                {
                    Position pos = filePositions[i];
                    AddPositionToFile(pos, currentProfit, currentUSDTProfit);
                }
            }

            return false;
        }

        private static void CreateFile(Position position, decimal currentProfit, decimal currentUSDTProfit, string filepath)
        {
            using (StreamWriter sw = File.CreateText(filepath))
            {
                sw.WriteLine($"DATE;ASSET;INITIAL PRICE;FINAL PRICE;INITIAL TOTAL; FINAL TOTAL;VALORIZATION;QUANTITY;REC TYPE;CURRENT VAL; CURRENT USDT VAL");
                sw.WriteLine($"{DateTime.Now}; {position.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Quantity}; {position.Type}; {currentProfit}; {currentUSDTProfit}");
            }
        }

        public static List<Position> GetPositionFromFile()
        {
            try
            {
                List<Position> positions = new List<Position>();

                if (!Directory.Exists(pathFolder))
                    return null;

                if (!File.Exists(filePath))
                    return null;

                positions = File.ReadAllLines(filePath).Skip(1).Select(x => TransformLineIntoPosition(x)).ToList();

                return positions;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO ao restaurar posicoes: " + ex.Message);
                throw ex;
            }
        }

        private static Position TransformLineIntoPosition(string line)
        {
            string[] values = line.Split(';');
            var position = new Position(values[1], Convert.ToDecimal(values[2]), Convert.ToDecimal(values[7]), ConvertRecType(values[8]));
            position.Valorization = Convert.ToDecimal(values[6]);
            position.LastPrice = Convert.ToDecimal(values[3]);

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
