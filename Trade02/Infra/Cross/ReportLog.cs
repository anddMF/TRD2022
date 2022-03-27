using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Trade02.Models.Trade;

namespace Trade02.Infra.Cross
{
    public class ReportLog
    {
        public enum logType { COMPRA, VENDA, POSICAO }

        public static void WriteReport(logType typeLog, Position position)
        {
            try
            {
                #region Folder ops
                string folderPath = string.Format("{0}{1}", Directory.GetCurrentDirectory(), "\\REPORTS");
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);
                #endregion

                string filepath = string.Format("{0}\\{1}.csv", folderPath, "REPORTS-"+ DateTime.Now.Date.ToString("yyyyMMdd"));

                if (!File.Exists(filepath))
                {
                    #region Create File
                    CreateFileReport(typeLog, position, filepath);
                    #endregion
                }
                else
                {
                    #region Append
                    using (StreamWriter sw = File.AppendText(filepath))
                    {
                        sw.WriteLine($"{DateTime.Now};[{typeLog}]; {position.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Type};");
                    }
                    #endregion
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nERRO NO REPORT");
                Console.WriteLine(ex.Message);
            }

        }

        private static void CreateFileReport(logType typeLog, Position position, string filepath)
        {
            using (StreamWriter sw = File.CreateText(filepath))
            {
                sw.WriteLine($"DATE;TYPE;ASSET;INITIAL PRICE;FINAL PRICE;INITIAL TOTAL; FINAL TOTAL;VALORIZATION;REC TYPE;");
                sw.WriteLine($"{DateTime.Now};[{typeLog}]; {position.Symbol}; {position.InitialPrice}; {position.LastPrice}; {position.InitialValue}; {position.LastValue}; {position.Valorization}; {position.Type};");
            }
        }

        private static double ConvertBytesToMegabytes(long bytes)
        {
            return (bytes / 1024f) / 1024f;
        }
    }
}
