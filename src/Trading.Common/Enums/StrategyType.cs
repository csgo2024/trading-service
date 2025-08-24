using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Trading.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StrategyType
{
    [Description("DCA")]
    DCA,

    [Description("OpenSell")]
    OpenSell,

    [Description("OpenBuy")]
    OpenBuy,

    [Description("CloseBuy")]
    CloseBuy,

    [Description("CloseSell")]
    CloseSell,

}
