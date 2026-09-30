using Ritocode.Modules.Content.Ingest;

namespace Ritocode.Modules.Content.Tests.Ingest;

public sealed class ShortlistTests
{
    private static readonly string[] Catalogue = [.. Enumerable.Range(1, 60).Select(number => $"card-{number:00}")];

    [Fact]
    public void AShortlist_HoldsTheFindings_PlusTwentyOthers()
    {
        var shortlist = Shortlist.For("some-task", ["card-07", "card-42"], Catalogue);

        Assert.Equal(22, shortlist.Count);
        Assert.Contains("card-07", shortlist);
        Assert.Contains("card-42", shortlist);
        Assert.Equal(shortlist.Order(StringComparer.Ordinal), shortlist);
    }

    [Fact]
    public void TheSameTask_AlwaysGetsTheSameShortlist_WhateverTheCatalogueOrder()
    {
        var once = Shortlist.For("some-task", ["card-07"], Catalogue);
        var again = Shortlist.For("some-task", ["card-07"], Catalogue.Reverse());

        Assert.Equal(once, again);
    }

    [Fact]
    public void DifferentTasks_GetDifferentShortlists()
    {
        var one = Shortlist.For("task-one", ["card-07"], Catalogue);
        var other = Shortlist.For("task-two", ["card-07"], Catalogue);

        Assert.NotEqual(one, other);
    }

    [Fact]
    public void ASmallCatalogue_GivesEverythingItHas()
    {
        var shortlist = Shortlist.For("some-task", ["card-01"], Catalogue[..5]);

        Assert.Equal(Catalogue[..5], shortlist);
    }
}
