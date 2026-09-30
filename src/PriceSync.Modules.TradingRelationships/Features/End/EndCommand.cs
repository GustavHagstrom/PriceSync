using PriceSync.Modules.TradingRelationships.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace PriceSync.Modules.TradingRelationships.Features.End;

internal record EndCommand(TradingRelationshipId TradingRelationshipId);