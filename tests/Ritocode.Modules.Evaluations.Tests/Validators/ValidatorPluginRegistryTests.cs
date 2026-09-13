using Ritocode.Modules.Evaluations.Validators;

namespace Ritocode.Modules.Evaluations.Tests.Validators;

public sealed class ValidatorPluginRegistryTests
{
    [Fact]
    public void Find_ReturnsThePluginRegisteredForAType()
    {
        var compile = new NamedPlugin("compile");
        var tests = new NamedPlugin("test");

        var registry = new ValidatorPluginRegistry([compile, tests]);

        Assert.Same(compile, registry.Find("compile"));
        Assert.Same(tests, registry.Find("test"));
    }

    [Fact]
    public void Find_ComparesOrdinally_SoATypeInAnotherCaseIsNotFound()
    {
        // The manifest pattern allows lower case only; a lookup that folded case would accept a type
        // the loader never could.
        var registry = new ValidatorPluginRegistry([new NamedPlugin("compile")]);

        Assert.Null(registry.Find("Compile"));
    }

    [Fact]
    public void Find_OfATypeNoPluginAnswers_IsNull()
    {
        var registry = new ValidatorPluginRegistry([new NamedPlugin("compile")]);

        Assert.Null(registry.Find("lint"));
    }

    [Fact]
    public void Types_AreOrderedOrdinally_WhateverOrderThePluginsWereRegisteredIn()
    {
        var registry = new ValidatorPluginRegistry([new NamedPlugin("test"), new NamedPlugin("compile"), new NamedPlugin("patch-scope")]);

        Assert.Equal(["compile", "patch-scope", "test"], registry.Types);
    }

    [Fact]
    public void AnEmptyRegistry_IsValid_AndFindsNothing()
    {
        // The state of the host until #19 registers the first plugin.
        var registry = new ValidatorPluginRegistry([]);

        Assert.Empty(registry.Types);
        Assert.Null(registry.Find("compile"));
    }

    [Fact]
    public void TwoPluginsAnsweringOneType_AreRefused_NamingBoth()
    {
        var failure = Assert.Throws<InvalidOperationException>(
            () => new ValidatorPluginRegistry([new NamedPlugin("compile"), new ExitCodeValidator(), new NamedPlugin("compile")]));

        Assert.Contains("'compile'", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Compile")]
    [InlineData("compile tests")]
    [InlineData("-compile")]
    [InlineData("compile--tests")]
    [InlineData("a-type-name-that-is-longer-than-32")]
    public void APluginAnsweringATypeNoManifestCanName_IsRefused(string type)
    {
        Assert.Throws<InvalidOperationException>(() => new ValidatorPluginRegistry([new NamedPlugin(type)]));
    }
}
