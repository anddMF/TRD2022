using System;
using System.Collections.Generic;
using System.Text;

namespace Trade02.Models.CrossCutting
{
    public class AppSettings
    {
        public ApiConfiguration ApiConfiguration { get; set; }
    }

    public class ApiConfiguration
    {
        public string Key { get; set; }
        public string Secret { get; set; }
        public string Address { get; set; }
    }
}
