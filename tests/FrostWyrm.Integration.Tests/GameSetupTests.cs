using CryptidCompanion;
using FrostWyrm.Archive.DependencyInjection;
using FrostWyrm.Archive.Prototypes;
using FrostWyrm.Archive.Serialization;

namespace FrostWyrm.Integration.Tests;

public sealed class GameSetupTests
{
    [Theory]
    [InlineData(3, "Alpha", 4)]
    [InlineData(4, "Delta", 1)]
    [InlineData(5, "Epsilon", 55)]
    public void ShippedCardLoadsAndSelectsCluesForEachPlayerCount(int players, string book, int clue)
    {
        var game = CreateGame();

        game.SetupStart(4, players);

        Assert.Equal(4, game.CardNumber);
        Assert.Equal(players, game.PlayerCount);
        Assert.NotNull(game.CurrentSetup);
        Assert.Equal(players, game.CurrentSetup.Clues.Count);
        Assert.Equal(clue, game.CurrentSetup.Clues[book]);
    }

    [Theory]
    [InlineData(4, 2)]
    [InlineData(4, 6)]
    [InlineData(0, 3)]
    public void InvalidInputsAreRejectedWithoutChangingTheSelectedSetup(int card, int players)
    {
        var game = CreateGame();
        game.SetupStart(4, 3);
        var previous = game.CurrentSetup;

        Assert.Throws<ArgumentOutOfRangeException>(() => game.SetupStart(card, players));
        Assert.Same(previous, game.CurrentSetup);
        Assert.Equal(4, game.CardNumber);
        Assert.Equal(3, game.PlayerCount);
    }

    [Fact]
    public void UnknownCardDoesNotReplaceTheSelectedSetup()
    {
        var game = CreateGame();
        game.SetupStart(4, 3);
        var previous = game.CurrentSetup;

        Assert.Throws<InvalidOperationException>(() => game.SetupStart(99, 5));
        Assert.Same(previous, game.CurrentSetup);
        Assert.Equal(4, game.CardNumber);
        Assert.Equal(3, game.PlayerCount);
    }

    [Fact]
    public void SelectingAnotherPlayerCountReplacesTheSetup()
    {
        var game = CreateGame();
        game.SetupStart(4, 3);

        game.SetupStart(4, 5);

        Assert.Equal(5, game.PlayerCount);
        Assert.Equal(5, game.CurrentSetup!.Clues.Count);
    }

    private static GameManager CreateGame()
    {
        var dependencies = new DependencyManager();
        dependencies.Register(new CryptidCompanion.Domain.ContentCatalog());
        dependencies.Register(new SerializationManager());
        var prototypes = new PrototypeManager();
        dependencies.Register(prototypes);
        dependencies.InjectDependencies(prototypes);
        prototypes.RegisterAssemblyPrototypes(typeof(SetupCardPrototype).Assembly);
        prototypes.LoadPrototypes(Path.Combine(AppContext.BaseDirectory, "Resources", "setup-cards.yml"));

        var game = new GameManager();
        dependencies.InjectDependencies(game);
        return game;
    }
}
