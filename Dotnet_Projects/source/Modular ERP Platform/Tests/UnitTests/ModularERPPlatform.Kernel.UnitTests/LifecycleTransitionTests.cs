using System;
using System.Collections.Generic;
using System.Text;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel.UnitTests;

[TestFixture]
public sealed class LifecycleTransitionTests
{
    [TestCase(ModuleState.Discovered, ModuleState.Loaded)]
    [TestCase(ModuleState.Discovered, ModuleState.Blocked)]
    [TestCase(ModuleState.Loaded, ModuleState.Initialized)]
    [TestCase(ModuleState.Initialized, ModuleState.Active)]
    [TestCase(ModuleState.Active, ModuleState.Deactivated)]
    [TestCase(ModuleState.Deactivated, ModuleState.Active)]
    [TestCase(ModuleState.Deactivated, ModuleState.Unloaded)]
    [TestCase(ModuleState.Faulted, ModuleState.Unloaded)]
    public void Legal_transitions_are_permitted(ModuleState from, ModuleState to)
    {
        LifecycleManager.IsLegalTransition(from, to).Should().BeTrue();
    }

    [TestCase(ModuleState.Discovered, ModuleState.Active)]
    [TestCase(ModuleState.Loaded, ModuleState.Active)]
    [TestCase(ModuleState.Unloaded, ModuleState.Loaded)]
    [TestCase(ModuleState.Unloaded, ModuleState.Active)]
    [TestCase(ModuleState.Active, ModuleState.Initialized)]
    [TestCase(ModuleState.Blocked, ModuleState.Active)]
    public void Illegal_transitions_are_refused(ModuleState from, ModuleState to)
    {
        LifecycleManager.IsLegalTransition(from, to).Should().BeFalse();
    }

    [Test]
    public void Every_state_can_reach_Faulted_except_Unloaded_and_Faulted()
    {
        foreach (ModuleState state in Enum.GetValues<ModuleState>())
        {
            bool expected = state is not (ModuleState.Unloaded or ModuleState.Faulted or ModuleState.Blocked);

            LifecycleManager.IsLegalTransition(state, ModuleState.Faulted)
                .Should().Be(expected, "state {0} -> Faulted", state);
        }
    }

    [Test]
    public void Unloaded_is_terminal()
    {
        foreach (ModuleState state in Enum.GetValues<ModuleState>())
        {
            LifecycleManager.IsLegalTransition(ModuleState.Unloaded, state)
                .Should().BeFalse("Unloaded -> {0}", state);
        }
    }
}
