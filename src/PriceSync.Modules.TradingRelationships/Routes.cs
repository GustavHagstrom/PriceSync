using System;
using System.Collections.Generic;
using System.Text;

namespace PriceSync.Modules.TradingRelationships;

internal static class Routes
{
    public const string BaseV1 = "/api/v1/trading-relationships";
    public static class Names
    {
        public const string Create = "CreateTradingRelationship";
        public const string Get = "GetTradingRelationship";
    }
}
