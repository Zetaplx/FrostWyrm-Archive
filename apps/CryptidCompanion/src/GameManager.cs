using System.Globalization;

using FrostWyrm.Archive;
using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;

namespace CryptidCompanion;

public sealed class GameManager : IManager
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public int CardNumber { get; private set; }
    public int PlayerCount { get; private set; }
    public PlayerSetup? CurrentSetup { get; private set; }

    public void SetupStart(int cardNumber, int playerCount)
    {
        if (cardNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(cardNumber), "Choose a positive card number.");
        if (playerCount is < 3 or > 5)
            throw new ArgumentOutOfRangeException(nameof(playerCount), "Choose between 3 and 5 players.");

        var card = _prototypes.EnumeratePrototypes<SetupCardPrototype>()
            .SingleOrDefault(card => card.ID == cardNumber.ToString(CultureInfo.InvariantCulture))
            ?? throw new InvalidOperationException($"Setup card {cardNumber} is not available.");
        var setup = card.Setups.SingleOrDefault(setup => setup.PlayerCount == playerCount)
            ?? throw new InvalidOperationException($"Card {cardNumber} has no setup for {playerCount} players.");

        CardNumber = cardNumber;
        PlayerCount = playerCount;
        CurrentSetup = setup;
    }
}
