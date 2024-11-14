﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Yafc.UI;

namespace Yafc.Model;

/// <summary>
/// Define "dependency tree" as trees of the type "Recipe.engine-unit requires Item.iron-gear-wheel, Item.steel-plate, Item.pipe,
/// (Entity.assembly-machine-1, Entity.assembly-machine-2, OR Entity.assembly-machine-3), AND Technology.engine".<br/>
/// (Contrast this with "accessibility graph", which is the complete graph created by merging all the dependency trees and adding
/// <see cref="Database.rootAccessible"/>.)<br/>
/// This represents one node in a dependency tree, which may be the root node of the tree or the child of another node.
/// </summary>
public abstract class DependencyNode {
    private DependencyNode() { } // All derived classes should be nested classes

    /// <summary>
    /// Creates a <see cref="DependencyNode"/> from a <see cref="DependencyList"/>. <paramref name="dependencies"/> contains the require-any/-all
    /// behavior and information about how the dependencies should be described. (e.g. "Crafter", "Ingredient", etc.)
    /// </summary>
    public static DependencyNode Create(DependencyList dependencies) => new ListNode(dependencies);

    /// <summary>
    /// Creates a <see cref="DependencyNode"/> that is satisfied if all of its child nodes are satisfied.
    /// This matches the old behavior for <see cref="DependencyList"/>[].
    /// </summary>
    public static DependencyNode RequireAll(IEnumerable<DependencyList> dependencies) => AndNode.Create(dependencies.Select(Create));
    /// <summary>
    /// Creates a <see cref="DependencyNode"/> that is satisfied if all of its child nodes are satisfied.
    /// This matches the old behavior for <see cref="DependencyList"/>[].
    /// </summary>
    public static DependencyNode RequireAll(IEnumerable<DependencyNode> dependencies) => AndNode.Create(dependencies);
    /// <summary>
    /// Creates a <see cref="DependencyNode"/> that is satisfied if all of its child nodes are satisfied.
    /// This matches the old behavior for <see cref="DependencyList"/>[].
    /// </summary>
    public static DependencyNode RequireAll(params DependencyNode[] dependencies) => AndNode.Create(dependencies);

    /// <summary>
    /// Creates a <see cref="DependencyNode"/> that is satisfied if any of its child nodes are satisfied. This behavior was only accessible
    /// within a single <see cref="DependencyList"/>, by not setting <see cref="DependencyList.Flags.RequireEverything"/>.
    /// </summary>
    public static DependencyNode RequireAny(IEnumerable<DependencyList> dependencies) => OrNode.Create(dependencies.Select(Create));
    /// <summary>
    /// Creates a <see cref="DependencyNode"/> that is satisfied if any of its child nodes are satisfied. This behavior was only accessible
    /// within a single <see cref="DependencyList"/>, by not setting <see cref="DependencyList.Flags.RequireEverything"/>.
    /// </summary>
    public static DependencyNode RequireAny(params DependencyNode[] dependencies) => OrNode.Create(dependencies);
    /// <summary>
    /// Creates a <see cref="DependencyNode"/> that is satisfied if any of its child nodes are satisfied. This behavior was only accessible
    /// within a single <see cref="DependencyList"/>, by not setting <see cref="DependencyList.Flags.RequireEverything"/>.
    /// </summary>
    public static DependencyNode RequireAny(IEnumerable<DependencyNode> dependencies) => OrNode.Create(dependencies);

    /// <summary>
    /// Gets the sequence of all <see cref="FactorioId"/>s that exist anywhere in this dependency tree.
    /// </summary>
    internal abstract IEnumerable<FactorioId> Flatten();

    /// <summary>
    /// Determines whether the object that owns this dependency tree is accessible, based on the accessibility <paramref name="isAccessible"/>
    /// returns for dependent objects, and the types of this node and its children.
    /// </summary>
    /// <param name="isAccessible">A delegate that returns <see langword="true"/> if the given <see cref="FactorioId"/> is known to be
    /// accessible, or <see langword="false"/> otherwise.</param>
    /// <returns><see langword="true"/> if the known-accessible dependent items are adequate to make the owning item accessible,
    /// or <see langword="false"/> otherwise.</returns>
    internal abstract bool IsAccessible(Func<FactorioId, bool> isAccessible);

    /// <summary>
    /// Gets the additional <see cref="Bits"/> that should be added to this tree's owner, based on the bits <paramref name="getBits"/> returns
    /// for dependent objects, and the types of this node and its children.
    /// </summary>
    /// <param name="getBits">A delegate that returns the <see cref="Bits"/> for a given <see cref="FactorioId"/>.</param>
    /// <returns>The <see cref="Bits"/> for all the dependent items, ORed together appropriately.</returns>
    internal abstract Bits AggregateBits(Func<FactorioId, Bits> getBits);

    /// <summary>
    /// Gets the new <see cref="AutomationStatus"/> for this tree's owner, based on the current value and the statuses returned by
    /// <paramref name="isAutomatable"/>.
    /// </summary>
    /// <param name="isAutomatable">A delegate that returns the <see cref="AutomationStatus"/> for a given <see cref="FactorioId"/>.</param>
    /// <param name="automationState">The initial <see cref="AutomationStatus"/>, which will not be exceeded regardless of the values returned
    /// by <paramref name="isAutomatable"/>.</param>
    /// <returns>The highest feasible <paramref name="automationState"/>, as constrained by <paramref name="isAutomatable"/> and
    /// <paramref name="automationState"/>.</returns>
    internal abstract AutomationStatus IsAutomatable(Func<FactorioId, AutomationStatus> isAutomatable, AutomationStatus automationState);

    /// <summary>
    /// Instructs this dependency tree to draw itself on the specified <see cref="ImGui"/>.
    /// </summary>
    /// <param name="gui">The drawing destination.</param>
    /// <param name="builder">A delegate that will draw the passed <see cref="DependencyList"/> onto the passed <see cref="ImGui"/>.</param>
    public abstract void Draw(ImGui gui, Action<ImGui, DependencyList> builder);

    /// <summary>
    /// A <see cref="DependencyNode"/> that requires all of its children.
    /// </summary>
    private sealed class AndNode : DependencyNode {
        private readonly DependencyNode[] dependencies;

        private AndNode(DependencyNode[] dependencies) => this.dependencies = dependencies; // Use Create

        /// <summary>
        /// Returns a <see cref="DependencyNode"/> that requires all of the children specified in <paramref name="dependencies"/>.
        /// </summary>
        /// <param name="dependencies">The children that should all be required by the returned value.</param>
        internal static DependencyNode Create(IEnumerable<DependencyNode> dependencies) {
            List<DependencyNode> realDependencies = [];
            foreach (DependencyNode item in dependencies) {
                if (item is AndNode and) {
                    realDependencies.AddRange(and.dependencies);
                }
                else {
                    realDependencies.Add(item);
                }
            }
            realDependencies = realDependencies.Distinct().ToList();

            // Prevent single-child nodes, so the drawing and preceding unpacking code doesn't have to handle that.
            if (realDependencies.Count == 1) {
                return realDependencies[0];
            }
            return new AndNode([.. realDependencies]);
        }

        internal override IEnumerable<FactorioId> Flatten() => dependencies.SelectMany(d => d.Flatten());

        internal override bool IsAccessible(Func<FactorioId, bool> isAccessible) => dependencies.All(d => d.IsAccessible(isAccessible));
        internal override Bits AggregateBits(Func<FactorioId, Bits> getBits) {
            Bits result = default;
            foreach (DependencyNode item in dependencies) {
                result |= item.AggregateBits(getBits);
            }
            return result;
        }
        internal override AutomationStatus IsAutomatable(Func<FactorioId, AutomationStatus> isAutomatable, AutomationStatus automationState)
            => dependencies.Min(d => d.IsAutomatable(isAutomatable, automationState));

        public override void Draw(ImGui gui, Action<ImGui, DependencyList> builder) {
            bool previousChildWasOr = false;
            foreach (DependencyNode dependency in dependencies) {
                if (dependency is OrNode && previousChildWasOr) {
                    gui.AllocateSpacing(.5f);
                }
                dependency.Draw(gui, builder);
                previousChildWasOr = dependency is OrNode;
            }
        }
    }

    /// <summary>
    /// A <see cref="DependencyNode"/> that requires at least one of its children.
    /// </summary>
    private sealed class OrNode : DependencyNode {
        private readonly DependencyNode[] dependencies;

        private OrNode(DependencyNode[] dependencies) => this.dependencies = dependencies; // Use Create

        /// <summary>
        /// Returns a <see cref="DependencyNode"/> that requires at least one of the children specified in <paramref name="dependencies"/>.
        /// </summary>
        /// <param name="dependencies">The children that will satisfy the returned value if at least one of them is satisfied.</param>
        internal static DependencyNode Create(IEnumerable<DependencyNode> dependencies) {
            List<DependencyNode> realDependencies = [];
            foreach (DependencyNode item in dependencies) {
                if (item is OrNode or) {
                    realDependencies.AddRange(or.dependencies);
                }
                else {
                    realDependencies.Add(item);
                }
            }
            realDependencies = realDependencies.Distinct().ToList();

            // Prevent single-child nodes, so the drawing and preceding unpacking code doesn't have to handle that.
            if (realDependencies.Count == 1) {
                return realDependencies[0];
            }
            return new OrNode([.. realDependencies]);
        }


        internal override IEnumerable<FactorioId> Flatten() => dependencies.SelectMany(d => d.Flatten());

        internal override bool IsAccessible(Func<FactorioId, bool> isAccessible) => dependencies.Any(d => d.IsAccessible(isAccessible));
        internal override Bits AggregateBits(Func<FactorioId, Bits> getBits) => dependencies.Select(d => d.AggregateBits(getBits)).Min();
        internal override AutomationStatus IsAutomatable(Func<FactorioId, AutomationStatus> isAutomatable, AutomationStatus automationState)
            => dependencies.Max(d => d.IsAutomatable(isAutomatable, automationState));

        public override void Draw(ImGui gui, Action<ImGui, DependencyList> builder) {
            Vector2 offset = new(.4f, 0);
            using (gui.EnterGroup(new(1f, 0, 0, 0))) {
                bool isFirst = true;
                foreach (var dependency in dependencies) {
                    if (!isFirst) {
                        using (gui.EnterGroup(new(1, .25f))) {
                            gui.BuildText("-- OR --", Font.productionTableHeader);
                        }
                        gui.DrawRectangle(gui.lastRect - offset, SchemeColor.GreyAlt);
                    }
                    isFirst = false;
                    dependency.Draw(gui, builder);
                }
            }
            gui.DrawRectangle(gui.lastRect.LeftPart(.2f) + offset, SchemeColor.GreyAlt);
        }
    }

    /// <summary>
    /// A <see cref="DependencyNode"/> that matches the behavior of a legacy <see cref="DependencyList"/>.
    /// </summary>
    /// <param name="dependencies">The <see cref="DependencyList"/> whose behavior should be matched by this <see cref="ListNode"/>.</param>
    private sealed class ListNode(DependencyList dependencies) : DependencyNode {
        private readonly DependencyList dependencies = dependencies;

        internal override IEnumerable<FactorioId> Flatten() => dependencies.elements;

        internal override bool IsAccessible(Func<FactorioId, bool> isAccessible) {
            if (dependencies.flags.HasFlag(DependencyList.Flags.RequireEverything)) {
                return dependencies.elements.All(isAccessible);
            }
            return dependencies.elements.Any(isAccessible);
        }

        internal override Bits AggregateBits(Func<FactorioId, Bits> getBits) {
            Bits bits = new();
            if (dependencies.flags.HasFlag(DependencyList.Flags.RequireEverything)) {
                foreach (FactorioId item in dependencies.elements) {
                    bits |= getBits(item);
                }
                return bits;
            }
            else if (dependencies.elements.Length > 0) {
                return bits | dependencies.elements.Select(getBits).Min();
            }
            return bits;
        }

        internal override AutomationStatus IsAutomatable(Func<FactorioId, AutomationStatus> getAutomation, AutomationStatus automationState) {
            // Copied from AutomationAnalysis.cs.
            if (!dependencies.flags.HasFlags(DependencyList.Flags.OneTimeInvestment)) {
                if (dependencies.flags.HasFlags(DependencyList.Flags.RequireEverything)) {
                    foreach (FactorioId element in dependencies.elements) {
                        if (getAutomation(element) < automationState) {
                            automationState = getAutomation(element);
                        }
                    }
                }
                else {
                    AutomationStatus localHighest = AutomationStatus.NotAutomatable;

                    foreach (FactorioId element in dependencies.elements) {
                        if (getAutomation(element) > localHighest) {
                            localHighest = getAutomation(element);
                        }
                    }

                    if (localHighest < automationState) {
                        automationState = localHighest;
                    }
                }
            }
            else if (automationState == AutomationStatus.AutomatableNow && dependencies.flags == DependencyList.Flags.CraftingEntity) {
                // If only character is accessible at current milestones as a crafting entity, don't count the object as currently automatable
                bool hasMachine = false;

                foreach (FactorioId element in dependencies.elements) {
                    if (element != Database.character?.id && Milestones.Instance.IsAccessibleWithCurrentMilestones(element)) {
                        hasMachine = true;
                        break;
                    }
                }

                if (!hasMachine) {
                    automationState = AutomationStatus.AutomatableLater;
                }
            }
            return automationState;
        }

        public override void Draw(ImGui gui, Action<ImGui, DependencyList> builder) => builder(gui, dependencies);
    }

    public static implicit operator DependencyNode(DependencyList list) => Create(list);
}
