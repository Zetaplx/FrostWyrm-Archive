using FrostWyrm.Archive.DependencyInjection;

namespace FrostWyrm.Archive.Tests.DependencyInjection;

public sealed class DependencyManagerTests
{
    [Fact]
    public void RegisterAndResolveExactType()
    {
        var manager = new DependencyManager();
        var dependency = new TestDependency();

        manager.Register(dependency);

        Assert.Same(dependency, manager.Resolve<TestDependency>());
    }

    [Fact]
    public void ResolveUsesAssignableTypeFallback()
    {
        var manager = new DependencyManager();
        var dependency = new TestDependency();

        manager.Register(dependency);

        Assert.Same(dependency, manager.Resolve<ITestDependency>());
    }

    [Fact]
    public void TryResolveCanDisableInheritanceLookup()
    {
        var manager = new DependencyManager();
        manager.Register(new TestDependency());

        var result = manager.TryResolve<ITestDependency>(
            out _,
            allowInheritanceLookup: false);

        Assert.False(result);
    }

    [Fact]
    public void RegisterRejectsNonSealedDependencies()
    {
        var manager = new DependencyManager();

        Assert.Throws<InvalidOperationException>(
            () => manager.Register(new NonSealedDependency()));
    }

    [Fact]
    public void ResolveThrowsWhenMultipleDependenciesSatisfyType()
    {
        var manager = new DependencyManager();

        manager.Register(new SharedDependencyA());
        manager.Register(new SharedDependencyB());

        Assert.Throws<InvalidOperationException>(
            () => manager.Resolve<ISharedDependency>());
    }

    [Fact]
    public void InjectDependenciesResolvesFieldsAndProperties()
    {
        var manager = new DependencyManager();
        var dependency = new TestDependency();
        var target = new InjectionTarget();

        manager.Register(dependency);
        manager.InjectDependencies(target);

        Assert.Same(dependency, target.FieldDependency);
        Assert.Same(dependency, target.PropertyDependency);
    }

    [Fact]
    public void RegisterDefaultDependenciesAndAutoInjectDependencies()
    {
        var manager = new DependencyManager();

        manager.RegisterDefaultDependencies(typeof(DependencyManagerTests).Assembly);
        manager.AutoInjectDependencies();

        var consumer = manager.Resolve<AutoInjectedDependency>();
        var dependency = manager.Resolve<DefaultTestDependency>();

        Assert.Same(dependency, consumer.Dependency);
    }

    public interface ITestDependency;

    public sealed class TestDependency : ITestDependency;

    public class NonSealedDependency;

    public interface ISharedDependency;

    public sealed class SharedDependencyA : ISharedDependency;

    public sealed class SharedDependencyB : ISharedDependency;

    public sealed class InjectionTarget
    {
        [Dependency]
        private ITestDependency _fieldDependency = default!;

        [Dependency]
        public TestDependency PropertyDependency { get; private set; } = default!;

        public ITestDependency FieldDependency => _fieldDependency;
    }

    [DefaultDependency]
    public sealed class DefaultTestDependency;

    [DefaultDependency, AutoInjectDependencies]
    public sealed class AutoInjectedDependency
    {
        [Dependency]
        public DefaultTestDependency Dependency { get; private set; } = default!;
    }
}
