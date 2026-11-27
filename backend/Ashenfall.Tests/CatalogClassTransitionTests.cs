using Ashenfall.Api.Catalog.Domain;
using Ashenfall.Api.Characters;
using Xunit;

namespace Ashenfall.Tests;

public sealed class CatalogClassTransitionTests
{
    [Fact]
    public void TransitionRequiresExplicitMatchingSuccessorInActiveRelease()
    {
        var oldRelease = new CatalogRelease { Id = Guid.NewGuid(), Status = "retired" };
        var active = new CatalogRelease { Id = Guid.NewGuid(), Status = "published" };
        var oldClass = new CharacterClass { Id = Guid.NewGuid(), CatalogReleaseId = oldRelease.Id, Code = "dwarf_warrior" };
        var replacement = new CharacterClass { Id = Guid.NewGuid(), CatalogReleaseId = active.Id, Code = oldClass.Code };
        Assert.Throws<InvalidOperationException>(() => CatalogClassTransition.Resolve(oldClass, oldRelease, active, [replacement], []));
        Assert.Same(replacement, CatalogClassTransition.Resolve(oldClass, oldRelease, active, [replacement], [(oldClass.Id, replacement.Id)]));
        replacement.Code = "other_class";
        Assert.Throws<InvalidOperationException>(() => CatalogClassTransition.Resolve(oldClass, oldRelease, active, [replacement], [(oldClass.Id, replacement.Id)]));
    }
}
