using System.Text.Json.Serialization;

namespace DaCards;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Card))]
[JsonSerializable(typeof(List<Card>))]
[JsonSerializable(typeof(Move))]
[JsonSerializable(typeof(List<Move>))]
[JsonSerializable(typeof(StockPile))]
[JsonSerializable(typeof(WastePile))]
[JsonSerializable(typeof(FoundationPile))]
[JsonSerializable(typeof(List<FoundationPile>))]
[JsonSerializable(typeof(TableauPile))]
[JsonSerializable(typeof(List<TableauPile>))]
[JsonSerializable(typeof(SolitaireGame))]
[JsonSerializable(typeof(SpiderMove))]
[JsonSerializable(typeof(List<SpiderMove>))]
[JsonSerializable(typeof(SpiderTableauPile))]
[JsonSerializable(typeof(List<SpiderTableauPile>))]
[JsonSerializable(typeof(SpiderSolitaireGame))]
[JsonSerializable(typeof(FreeCellMove))]
[JsonSerializable(typeof(List<FreeCellMove>))]
[JsonSerializable(typeof(FreeCellTableauPile))]
[JsonSerializable(typeof(List<FreeCellTableauPile>))]
[JsonSerializable(typeof(List<Card?>))]
[JsonSerializable(typeof(FreeCellGame))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
