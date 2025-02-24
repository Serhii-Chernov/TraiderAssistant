using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TechnicalAnalysis.Shared
{
    public class TechnicalAnalysisNameValueActionStruct
    {
        public string Name { get; set; }
        public decimal Value { get; set; }
        public string Action { get;  set; }  
        public TechnicalAnalysisNameValueActionStruct() { }
    }
}
