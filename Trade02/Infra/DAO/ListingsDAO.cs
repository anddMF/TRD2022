using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Infra.DAO
{
    public class ListingsDAO
    {
        public ListingsStatusDAO status { get; set; }
        public List<ListingsDataDAO> data { get; set; }
    }

    public class ListingsDataDAO
    {
        public int id { get; set; }

        public string? name { get; set; }
        public string? symbol { get; set; }
        public string? slug { get; set; }
        public int? cmc_rank { get; set; }
        public int? num_market_pairs { get; set; }
        public double? circulating_supply { get; set; }
        public double? total_supply { get; set; }
        public double? market_cap_by_total_supply { get; set; }
        public double? max_supply { get; set; }
        public DateTime? last_updated { get; set; }
        public DateTime? date_added { get; set; }
        public List<string>? tags { get; set; }
        public PlatformDAO? platform { get; set; }
        public QuoteDAO? quote { get; set; }
    }

    public class ListingsStatusDAO
    {
        public DateTime? timestamp { get; set; }
        public int? error_code { get; set; }
        public string? error_message { get; set; }
        public int? elapsed { get; set; }
        public int? credit_count { get; set; }
        public string? notice { get; set; }
        public int? total_count { get; set; }
    }

    public class PlatformDAO 
    {
        public int? id { get; set; }
        public string? name { get; set; }
        public string? symbol { get; set; }
        public string? slug { get; set; }
        public string? token_address { get; set; }
    }

    public class QuoteDAO
    {
        public QuoteObject USD { get; set; }
    }

    public class QuoteObject
    {
        public double? price { get; set; }
        public double? volume_24h { get; set; }
        public double? volume_change_24h { get; set; }
        public double? volume_24h_reported { get; set; }
        public double? volume_7d { get; set; }
        public double? volume_7d_reported { get; set; }
        public double? volume_30d { get; set; }
        public double? volume_30d_reported { get; set; }
        public double? market_cap { get; set; }
        public double? market_cap_dominance { get; set; }
        public double? fully_diluted_market_cap { get; set; }
        public double? percent_change_1h { get; set; }
        public double? percent_change_24h { get; set; }
        public double? percent_change_7d { get; set; }
        public DateTime? last_updated { get; set; }
    }

}
