using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public class WalletManagement
    {
        public static bool AddPositionToFile(Position position, decimal currentProfit)
        {
            try
            {
                string pathFolder = string.Format("{0}{1}", Directory.GetCurrentDirectory(), "\\WALLET");

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
                Console.WriteLine("ERRO AddPositionToFile: " + ex.Message);
                return false;
            }
        }
        private static void CreateFile(Position position, decimal currentProfit, string filepath)
        {
            using (StreamWriter sw = File.CreateText(filepath))
            {
                sw.WriteLine($"DATE;ASSET;INITIAL PRICE;FINAL PRICE;INITIAL TOTAL; FINAL TOTAL;VALORIZATION;QUANTITY;REC TYPE;");
                sw.WriteLine($"{DateTime.Now}; {position.Data.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Quantity}; {position.Type}; {currentProfit};");
            }
        }

        public static List<Position> GetPositionFromFile()
        {
            return null;
        }
    }
}
