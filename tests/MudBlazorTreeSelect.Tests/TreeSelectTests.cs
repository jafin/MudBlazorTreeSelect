using AngleSharp.Dom;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MudBlazorTreeSelect.Tests.Components;

namespace MudBlazorTreeSelect.Tests
{
    [TestFixture]
    public class TreeSelectTests : TreeSelectTestBase
    {
        /// <summary>
        /// Whether the popup list is open, read from the combobox the way a screen reader would.
        /// </summary>
        private static bool IsOpen<T>(IRenderedComponent<TreeSelect<T>> select)
            => select.Find("[role=combobox]").GetAttribute("aria-expanded") == "true";

        /// <summary>
        /// Sends a key press to the select the way the browser interceptor would.
        /// </summary>
        private Task PressAsync<T>(IRenderedComponent<TreeSelect<T>> select, string key)
        {
            var keys = Context.Services.GetRequiredService<IKeyInterceptorService>();
            return select.InvokeAsync(() => keys.DispatchAsync(select.Instance.ElementId, KeyEventKind.Down, new KeyboardEventArgs { Key = key, Type = "keydown" }));
        }

        /// <summary>
        /// Reads the visible rows of a tree select in display order.
        /// </summary>
        private static string[] TreeRows(IRenderedComponent<TreeSelectNestedTest> comp)
            => comp.FindAll("[role=treeitem].treeselect-item-tree").Select(x => x.TextContent.Trim()).ToArray();

        private static IElement TreeRow(IRenderedComponent<TreeSelectNestedTest> comp, string text)
            => comp.FindAll("[role=treeitem].treeselect-item-tree").Single(x => x.TextContent.Trim() == text);

        private static string? ActiveRow(IRenderedComponent<TreeSelectNestedTest> comp)
        {
            var id = comp.Find("[role=combobox]").GetAttribute("aria-activedescendant");
            return comp.FindAll("[role=treeitem].treeselect-item-tree").SingleOrDefault(x => x.Id == id)?.TextContent.Trim();
        }

        /// <summary>
        /// Nested items stay hidden until their parent is expanded with the chevron, and appear directly under it.
        /// </summary>
        [Test]
        public async Task SelectNested_Chevron_ExpandsAndCollapsesBranch()
        {
            var comp = Context.Render<TreeSelectNestedTest>();
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            TreeRows(comp).Should().Equal("Electronics", "Books");
            TreeRow(comp, "Electronics").GetAttribute("aria-expanded").Should().Be("false");

            await TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClickAsync();

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
            TreeRow(comp, "Electronics").GetAttribute("aria-expanded").Should().Be("true");

            await TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClickAsync();

            TreeRows(comp).Should().Equal("Electronics", "Books");
        }

        /// <summary>
        /// Branches expanded after their siblings registered still appear in display order.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandingLaterBranch_KeepsDisplayOrder()
        {
            var comp = Context.Render<TreeSelectNestedTest>();
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            await TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClickAsync();
            await TreeRow(comp, "Phones").QuerySelector(".treeselect-item-toggle")!.ClickAsync();

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Smartphones", "Feature phones", "Laptops", "Books");
            comp.FindComponent<TreeSelect<string>>().Instance.Items.Select(x => x.Value)
                .Should().Equal("electronics", "phones", "smartphones", "feature-phones", "laptops", "books");
        }

        /// <summary>
        /// A select with nested items exposes tree semantics instead of list semantics.
        /// </summary>
        [Test]
        public async Task SelectNested_UsesTreeRolesAndLevels()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.InitiallyExpanded, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            comp.Find("[role=combobox]").GetAttribute("aria-haspopup").Should().Be("tree");
            comp.Find("[role=tree]").Should().NotBeNull();
            comp.FindAll("[role=listbox]").Should().BeEmpty();
            TreeRow(comp, "Electronics").GetAttribute("aria-level").Should().Be("1");
            TreeRow(comp, "Phones").GetAttribute("aria-level").Should().Be("2");
            TreeRow(comp, "Smartphones").GetAttribute("aria-level").Should().Be("3");
            TreeRow(comp, "Smartphones").HasAttribute("aria-expanded").Should().BeFalse("leaves have no expanded state");
        }

        /// <summary>
        /// Clicking a parent row selects it, while clicking its chevron only expands it.
        /// </summary>
        [Test]
        public async Task SelectNested_ParentRowSelectsAndChevronDoesNot()
        {
            var comp = Context.Render<TreeSelectNestedTest>();
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClickAsync();
            select.Instance.Value.Should().BeNull();
            IsOpen(select).Should().BeTrue("expanding must not close the popup");

            await TreeRow(comp, "Electronics").ClickAsync();
            select.Instance.Value.Should().Be("electronics");
        }

        /// <summary>
        /// Right expands a collapsed branch then enters it, and Left leaves a child then collapses the branch.
        /// </summary>
        [Test]
        public async Task SelectNested_ArrowKeys_ExpandCollapseAndMoveBetweenLevels()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.MultiSelection, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            await comp.WaitForAssertionAsync(() => ActiveRow(comp).Should().Be("Electronics"));

            await PressAsync(select, "ArrowRight");
            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
            ActiveRow(comp).Should().Be("Electronics");

            await PressAsync(select, "ArrowRight");
            ActiveRow(comp).Should().Be("Phones");

            await PressAsync(select, "ArrowLeft");
            ActiveRow(comp).Should().Be("Electronics", "Left on a collapsed child moves to its parent");

            await PressAsync(select, "ArrowLeft");
            TreeRows(comp).Should().Equal("Electronics", "Books");
        }

        /// <summary>
        /// Collapsing a branch that contains the highlighted item moves the highlight to the branch.
        /// </summary>
        [Test]
        public async Task SelectNested_CollapsingAncestorOfActiveItem_HighlightsAncestor()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.MultiSelection, true)
                .Add(x => x.InitiallyExpanded, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            await comp.WaitForAssertionAsync(() => ActiveRow(comp).Should().Be("Electronics"));
            await PressAsync(select, "ArrowDown");
            await PressAsync(select, "ArrowDown");
            ActiveRow(comp).Should().Be("Smartphones");

            await TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClickAsync();

            TreeRows(comp).Should().Equal("Electronics", "Books");
            ActiveRow(comp).Should().Be("Electronics");
        }

        /// <summary>
        /// Opening with a selected value inside a collapsed branch expands its ancestors.
        /// </summary>
        [Test]
        public async Task SelectNested_OpeningWithNestedSelection_RevealsSelectedItem()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.Value, "smartphones"));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Smartphones", "Feature phones", "Laptops", "Books");
            TreeRow(comp, "Smartphones").GetAttribute("aria-selected").Should().Be("true");
        }

        /// <summary>
        /// A branch expanded by the user stays expanded when the popup is reopened.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpansionSurvivesReopening()
        {
            var comp = Context.Render<TreeSelectNestedTest>();
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            await TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClickAsync();
            await comp.InvokeAsync(() => select.Instance.CloseMenu());

            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
        }

        /// <summary>
        /// Select all covers every enabled node, including those under collapsed branches.
        /// </summary>
        [Test]
        public async Task SelectNested_SelectAll_SelectsCollapsedDescendants()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.MultiSelection, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await comp.FindAll(".mud-list-item").First().ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics", "phones", "smartphones", "laptops", "books"]);
        }

        /// <summary>
        /// Describes the connector segments of a tree row as <c>column:kind</c>, where kind is through, elbow or last.
        /// </summary>
        private static string[] Guides(IRenderedComponent<TreeSelectNestedTest> comp, string text)
            => TreeRow(comp, text).QuerySelectorAll(".treeselect-item-guide").Select(x =>
            {
                var column = System.Text.RegularExpressions.Regex.Match(x.GetAttribute("style")!, @"--treeselect-guide-column:\s*(\d+)").Groups[1].Value;
                var kind = x.ClassList.Contains("treeselect-item-guide-last") ? "last" : x.ClassList.Contains("treeselect-item-guide-elbow") ? "elbow" : "through";
                return $"{column}:{kind}";
            }).ToArray();

        /// <summary>
        /// Tree lines join each row to its parent, stop at the last sibling and carry an ancestor's line past its descendants.
        /// </summary>
        [Test]
        public async Task SelectNested_TreeLines_JoinRowsToParents()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.InitiallyExpanded, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            Guides(comp, "Electronics").Should().BeEmpty();
            Guides(comp, "Phones").Should().Equal("0:elbow");
            Guides(comp, "Smartphones").Should().Equal("0:through", "1:elbow");
            Guides(comp, "Feature phones").Should().Equal("0:through", "1:last");
            Guides(comp, "Laptops").Should().Equal("0:last");
            Guides(comp, "Books").Should().BeEmpty();
        }

        /// <summary>
        /// Turning tree lines off removes every connector segment.
        /// </summary>
        [Test]
        public async Task SelectNested_TreeLinesOff_DrawsNoGuides()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.InitiallyExpanded, true)
                .Add(x => x.TreeLines, false));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            comp.FindAll(".treeselect-item-guide").Should().BeEmpty();
        }

        /// <summary>
        /// The expand icon comes before the text by default and can be moved to the end of the row.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandIconPlacement_MovesToggle()
        {
            var comp = Context.Render<TreeSelectNestedTest>();
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClassList.Should().Contain("treeselect-item-toggle-start");
            TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-branch").Should().BeNull();

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ExpandIconPlacement, ExpandIconPlacement.End));

            TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-toggle")!.ClassList.Should().NotContain("treeselect-item-toggle-start");
            TreeRow(comp, "Electronics").QuerySelector(".treeselect-item-branch").Should().NotBeNull();
        }

        /// <summary>
        /// With ExpandOnClick and single selection, clicking a parent only toggles it, and only leaves can be chosen.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandOnClick_SingleSelectionOnlyExpandsParents()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.ExpandOnClick, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await TreeRow(comp, "Electronics").ClickAsync();

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
            select.Instance.Value.Should().BeNull("a parent is never selected by clicking it");
            IsOpen(select).Should().BeTrue();

            await TreeRow(comp, "Electronics").ClickAsync();
            TreeRows(comp).Should().Equal("Electronics", "Books");

            await TreeRow(comp, "Electronics").ClickAsync();
            await TreeRow(comp, "Laptops").ClickAsync();
            select.Instance.Value.Should().Be("laptops");
            IsOpen(select).Should().BeFalse("choosing a leaf closes the popup");
        }

        /// <summary>
        /// With ExpandOnClick and multi-selection, clicking a parent checks it and expands it without collapsing it later.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandOnClick_MultiSelectionChecksAndExpandsParents()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.MultiSelection, true)
                .Add(x => x.ExpandOnClick, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics"]);
            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEmpty();
            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
        }

        /// <summary>
        /// With ExpandOnClick and single selection, Enter and arrow navigation expand a parent but never select it.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandOnClick_KeysNeverSelectParent()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.ExpandOnClick, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await PressAsync(select, "ArrowDown");
            ActiveRow(comp).Should().Be("Electronics");
            select.Instance.Value.Should().BeNull("arrowing onto a parent must not select it");

            await PressAsync(select, "Enter");

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
            select.Instance.Value.Should().BeNull();
            IsOpen(select).Should().BeTrue();

            await PressAsync(select, "Enter");
            TreeRows(comp).Should().Equal("Electronics", "Books");
        }

        /// <summary>
        /// Without ExpandOnClick a parent row selects and only its chevron toggles it.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandOnClickOff_ClickSelectsWithoutExpanding()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.MultiSelection, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics"]);
            TreeRows(comp).Should().Equal("Electronics", "Books");
        }

        /// <summary>
        /// Names the checkbox state of a tree row, ignoring the chevron that can sit in the same row.
        /// </summary>
        private static string TreeCheckbox(IRenderedComponent<TreeSelectNestedTest> comp, string text)
            => CheckboxState(TreeRow(comp, text).QuerySelector("svg")!);

        private static async Task<IRenderedComponent<TreeSelectNestedTest>> OpenCheckedTreeAsync(BunitContext context, CheckedStrategy strategy, IReadOnlyCollection<string?>? selected = null)
        {
            var comp = context.Render<TreeSelectNestedTest>(parameters =>
            {
                parameters.Add(x => x.MultiSelection, true).Add(x => x.InitiallyExpanded, true).Add(x => x.CheckedStrategy, strategy);
                if (selected is not null)
                {
                    parameters.Add(x => x.SelectedValues, selected);
                }
            });
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            return comp;
        }

        /// <summary>
        /// With the Child strategy, checking a parent checks its enabled descendants and reports only those, never the parent.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyChild_ParentChecksEnabledDescendants()
        {
            var comp = await OpenCheckedTreeAsync(Context, CheckedStrategy.Child);

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["smartphones", "laptops"]);
            TreeCheckbox(comp, "Electronics").Should().Be("checked");
            TreeCheckbox(comp, "Phones").Should().Be("checked");
            TreeCheckbox(comp, "Smartphones").Should().Be("checked");
            TreeCheckbox(comp, "Feature phones").Should().Be("unchecked", "a disabled item is never checked by its parent");
            TreeCheckbox(comp, "Books").Should().Be("unchecked");

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEmpty();
            TreeCheckbox(comp, "Electronics").Should().Be("unchecked");
        }

        /// <summary>
        /// A parent shows as indeterminate while only some of its enabled descendants are checked, and as checked once all are.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyChild_ParentIsIndeterminateWhenPartlyChecked()
        {
            var comp = await OpenCheckedTreeAsync(Context, CheckedStrategy.Child);

            await TreeRow(comp, "Smartphones").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["smartphones"]);
            TreeCheckbox(comp, "Phones").Should().Be("checked", "its only enabled descendant is checked");
            TreeCheckbox(comp, "Electronics").Should().Be("indeterminate");

            await TreeRow(comp, "Laptops").ClickAsync();

            TreeCheckbox(comp, "Electronics").Should().Be("checked");
            comp.Instance.SelectedValues.Should().BeEquivalentTo(["smartphones", "laptops"]);
        }

        /// <summary>
        /// With the Parent strategy, fully checked descendants collapse into their topmost parent, and unchecking one splits it again.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyParent_ReportsTopmostFullyCheckedParents()
        {
            var comp = await OpenCheckedTreeAsync(Context, CheckedStrategy.Parent);

            await TreeRow(comp, "Electronics").ClickAsync();
            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics"]);
            TreeCheckbox(comp, "Smartphones").Should().Be("checked", "descendants of a reported parent are checked too");

            await TreeRow(comp, "Laptops").ClickAsync();
            comp.Instance.SelectedValues.Should().BeEquivalentTo(["phones"]);
            TreeCheckbox(comp, "Electronics").Should().Be("indeterminate");

            await TreeRow(comp, "Laptops").ClickAsync();
            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics"]);
        }

        /// <summary>
        /// Select all follows the strategy: leaves for Child, and the topmost parents for Parent.
        /// </summary>
        [TestCase(CheckedStrategy.Child, new[] { "smartphones", "laptops", "books" })]
        [TestCase(CheckedStrategy.Parent, new[] { "electronics", "books" })]
        public async Task SelectNested_CheckedStrategy_SelectAllFollowsStrategy(CheckedStrategy strategy, string[] expected)
        {
            var comp = await OpenCheckedTreeAsync(Context, strategy);

            await comp.FindAll(".mud-list-item").First().ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(expected);
            CheckboxState(comp.FindAll(".mud-list-item").First()).Should().Be("checked");

            await comp.FindAll(".mud-list-item").First().ClickAsync();

            comp.Instance.SelectedValues.Should().BeEmpty();
        }

        /// <summary>
        /// A selection supplied by the caller in either form is understood: a reported parent checks its descendants.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyParent_SuppliedParentChecksDescendants()
        {
            var comp = await OpenCheckedTreeAsync(Context, CheckedStrategy.Parent, ["electronics"]);

            TreeCheckbox(comp, "Electronics").Should().Be("checked");
            TreeCheckbox(comp, "Phones").Should().Be("checked");
            TreeCheckbox(comp, "Smartphones").Should().Be("checked");
            TreeCheckbox(comp, "Laptops").Should().Be("checked");
            TreeCheckbox(comp, "Books").Should().Be("unchecked");
        }

        /// <summary>
        /// With the Parent strategy, a selected value that has no matching item survives toggling a parent.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyParent_KeepsValuesWithoutItems()
        {
            var comp = await OpenCheckedTreeAsync(Context, CheckedStrategy.Parent, ["unknown"]);

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics", "unknown"]);
        }

        /// <summary>
        /// An item added after the first render is counted in a parent's checked state, even though the selected values did not change.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyParent_ItemAddedLater_UpdatesCheckedState()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.MultiSelection, true)
                .Add(x => x.InitiallyExpanded, true)
                .Add(x => x.ShowLaptops, false)
                .Add(x => x.CheckedStrategy, CheckedStrategy.Parent)
                .Add(x => x.SelectedValues, ["electronics"]));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            TreeCheckbox(comp, "Electronics").Should().Be("checked");

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ShowLaptops, true));

            TreeCheckbox(comp, "Laptops").Should().Be("checked", "the selected parent covers the new item");
            TreeCheckbox(comp, "Electronics").Should().Be("checked");

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEmpty("clicking a fully checked parent clears its branch");
            TreeCheckbox(comp, "Electronics").Should().Be("unchecked");
        }

        /// <summary>
        /// Disabling an item after the first render changes what a checked parent covers, so the parent's state follows.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyParent_ItemDisabledLater_UpdatesCheckedState()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.MultiSelection, true)
                .Add(x => x.InitiallyExpanded, true)
                .Add(x => x.CheckedStrategy, CheckedStrategy.Parent)
                .Add(x => x.SelectedValues, ["smartphones"]));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            TreeCheckbox(comp, "Electronics").Should().Be("indeterminate", "only one of its two enabled leaves is checked");

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.DisableLaptops, true));

            TreeCheckbox(comp, "Electronics").Should().Be("checked", "a disabled item is not counted, so every enabled leaf is checked");
        }

        /// <summary>
        /// The default strategy keeps items independent: checking a parent checks nothing else.
        /// </summary>
        [Test]
        public async Task SelectNested_CheckedStrategyAll_KeepsItemsIndependent()
        {
            var comp = await OpenCheckedTreeAsync(Context, CheckedStrategy.All);

            await TreeRow(comp, "Electronics").ClickAsync();

            comp.Instance.SelectedValues.Should().BeEquivalentTo(["electronics"]);
            TreeCheckbox(comp, "Phones").Should().Be("unchecked");
        }

        /// <summary>
        /// Removing the last sibling makes the previous one the last, so its connector closes instead of continuing.
        /// </summary>
        [Test]
        public async Task SelectNested_RemovingLastSibling_ClosesPreviousConnector()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.InitiallyExpanded, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            Guides(comp, "Phones").Should().Equal("0:elbow");

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ShowLaptops, false));

            TreeRows(comp).Should().Equal("Electronics", "Phones", "Smartphones", "Feature phones", "Books");
            Guides(comp, "Phones").Should().Equal("0:last");
            Guides(comp, "Smartphones").Should().Equal("1:elbow");
        }

        /// <summary>
        /// Home and End move the highlight to the first and last visible rows of the tree.
        /// </summary>
        [Test]
        public async Task SelectNested_HomeAndEnd_MoveToFirstAndLastVisibleRow()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.MultiSelection, true)
                .Add(x => x.InitiallyExpanded, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await PressAsync(select, "End");
            ActiveRow(comp).Should().Be("Books");

            await PressAsync(select, "Home");
            ActiveRow(comp).Should().Be("Electronics");
        }

        /// <summary>
        /// In right-to-left layouts the arrow keys swap: Left opens a branch and Right closes it.
        /// </summary>
        [Test]
        public async Task SelectNested_RightToLeft_SwapsExpandAndCollapseKeys()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.MultiSelection, true)
                .AddCascadingValue("RightToLeft", true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            await comp.WaitForAssertionAsync(() => ActiveRow(comp).Should().Be("Electronics"));

            await PressAsync(select, "ArrowRight");
            // The key that expands in left-to-right layouts must do nothing here.
            TreeRows(comp).Should().Equal("Electronics", "Books");

            await PressAsync(select, "ArrowLeft");
            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");

            await PressAsync(select, "ArrowRight");
            TreeRows(comp).Should().Equal("Electronics", "Books");
        }

        /// <summary>
        /// A bound Expanded value opens the branch, follows later changes from the parent and reports user toggles.
        /// </summary>
        [Test]
        public async Task SelectNested_BoundExpanded_FollowsParentAndReportsUserChanges()
        {
            var comp = Context.Render<TreeSelectExpandedTest>(parameters => parameters.Add(x => x.ElectronicsExpanded, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            string[] Rows() => comp.FindAll("[role=treeitem]").Select(x => x.TextContent.Trim()).ToArray();
            IElement Toggle() => comp.FindAll("[role=treeitem]").First().QuerySelector(".treeselect-item-toggle")!;
            Rows().Should().Equal("Electronics", "Phones", "Laptops", "Books");

            await Toggle().ClickAsync();

            Rows().Should().Equal("Electronics", "Books");
            comp.Instance.ElectronicsExpanded.Should().BeFalse();
            comp.Instance.ChangeCount.Should().Be(1);

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ElectronicsExpanded, true));

            Rows().Should().Equal("Electronics", "Phones", "Laptops", "Books");
        }

        /// <summary>
        /// With SelectAll the tree still owns only tree items, so the Select All row is one instead of an option.
        /// </summary>
        [Test]
        public async Task SelectNested_SelectAllRow_IsATreeItemInsideTheTree()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.MultiSelection, true));
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            var tree = comp.Find("[role=tree]");
            tree.QuerySelectorAll("[role=option]").Should().BeEmpty("an option is not allowed inside a tree");
            var rows = tree.QuerySelectorAll(".mud-list-item");
            rows.Should().OnlyContain(x => x.GetAttribute("role") == "treeitem");
            var selectAll = rows.First();
            selectAll.TextContent.Trim().Should().Be("Select all");
            selectAll.GetAttribute("aria-level").Should().Be("1");
        }

        /// <summary>
        /// A flat multi-select keeps the Select All row as an option.
        /// </summary>
        [Test]
        public async Task SelectNested_FlatMultiSelectAll_KeepsOptionRole()
        {
            var comp = Context.Render<TreeSelectFlatMultiTest>();
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            comp.FindAll("[role=tree]").Should().BeEmpty();
            comp.FindAll("[role=treeitem]").Should().BeEmpty();
            comp.FindAll(".mud-list-item").First().GetAttribute("role").Should().Be("option");
        }

        /// <summary>
        /// In single selection Left and Right change the value with the highlight, like Up and Down, so the value never lags the highlighted row.
        /// </summary>
        [Test]
        public async Task SelectNested_SingleSelection_ExpandAndCollapseKeysChangeTheValue()
        {
            var comp = Context.Render<TreeSelectNestedTest>();
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await PressAsync(select, "ArrowDown");
            select.Instance.Value.Should().Be("electronics");

            await PressAsync(select, "ArrowRight");
            TreeRows(comp).Should().Equal("Electronics", "Phones", "Laptops", "Books");
            select.Instance.Value.Should().Be("electronics", "expanding does not move the highlight");

            await PressAsync(select, "ArrowRight");
            ActiveRow(comp).Should().Be("Phones");
            select.Instance.Value.Should().Be("phones");

            await PressAsync(select, "ArrowLeft");
            ActiveRow(comp).Should().Be("Electronics");
            select.Instance.Value.Should().Be("electronics");
        }

        /// <summary>
        /// With SelectionOnEnter the expand and collapse keys only move the highlight, as Up and Down do.
        /// </summary>
        [Test]
        public async Task SelectNested_SelectionOnEnter_ExpandAndCollapseKeysLeaveTheValue()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters.Add(x => x.SelectionOnEnter, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await PressAsync(select, "ArrowDown");
            await PressAsync(select, "ArrowRight");
            await PressAsync(select, "ArrowRight");

            ActiveRow(comp).Should().Be("Phones");
            select.Instance.Value.Should().BeNull();

            await PressAsync(select, "Enter");
            select.Instance.Value.Should().Be("phones");
        }

        /// <summary>
        /// With ExpandOnClick the expand and collapse keys never select a parent, because a parent only expands.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpandOnClick_ExpandKeyDoesNotSelectParentChild()
        {
            var comp = Context.Render<TreeSelectNestedTest>(parameters => parameters
                .Add(x => x.ExpandOnClick, true)
                .Add(x => x.InitiallyExpanded, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            await PressAsync(select, "ArrowDown");
            await PressAsync(select, "ArrowRight");

            ActiveRow(comp).Should().Be("Phones");
            select.Instance.Value.Should().BeNull("Phones has children, so it only expands");
        }

        /// <summary>
        /// A bound Expanded changed by the parent while the popup is closed wins when the popup opens again, without a spurious change event.
        /// </summary>
        [Test]
        public async Task SelectNested_BoundExpandedChangedWhileClosed_IsNotRevertedOnReopen()
        {
            var comp = Context.Render<TreeSelectExpandedTest>(parameters => parameters.Add(x => x.ElectronicsExpanded, true));
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            await comp.InvokeAsync(() => select.Instance.CloseMenu());

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ElectronicsExpanded, false));
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            comp.FindAll("[role=treeitem]").Select(x => x.TextContent.Trim()).Should().Equal("Electronics", "Books");
            comp.Instance.ElectronicsExpanded.Should().BeFalse();
            comp.Instance.ChangeCount.Should().Be(0, "reopening must not report a change the parent made itself");
        }

        /// <summary>
        /// Remembered expansion follows the select's comparer, so a value that differs only by what the comparer ignores keeps its state.
        /// </summary>
        [Test]
        public async Task SelectNested_ExpansionMemory_UsesSelectComparer()
        {
            var comp = Context.Render<TreeSelectExpandedTest>();
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            await comp.FindAll("[role=treeitem]").First().QuerySelector(".treeselect-item-toggle")!.ClickAsync();
            await comp.InvokeAsync(() => select.Instance.CloseMenu());

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ElectronicsValue, "ELECTRONICS"));
            await comp.InvokeAsync(() => select.Instance.OpenMenu());

            comp.FindAll("[role=treeitem]").Select(x => x.TextContent.Trim()).Should().Equal("Electronics", "Phones", "Laptops", "Books");
        }

        /// <summary>
        /// A select becomes a tree when an item gains a nested list after it first rendered, and a plain list again when it loses it.
        /// </summary>
        [Test]
        public async Task SelectNested_NestedListAddedLater_SwitchesBetweenListAndTree()
        {
            var comp = Context.Render<TreeSelectLateNestedTest>();
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            comp.Find("[role=combobox]").GetAttribute("aria-haspopup").Should().Be("listbox");
            comp.FindAll("[role=tree]").Should().BeEmpty();

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ShowChildren, true));

            comp.Find("[role=combobox]").GetAttribute("aria-haspopup").Should().Be("tree");
            comp.FindAll(".treeselect-item-toggle").Should().HaveCount(1);

            await comp.SetParametersAndRenderAsync(parameters => parameters.Add(x => x.ShowChildren, false));

            comp.Find("[role=combobox]").GetAttribute("aria-haspopup").Should().Be("listbox");
            comp.FindAll(".treeselect-item-toggle").Should().BeEmpty();
        }

        /// <summary>
        /// The expand and collapse icons can be replaced per item and follow the expanded state.
        /// </summary>
        [Test]
        public async Task SelectNested_CustomExpandIcons_FollowExpandedState()
        {
            var comp = Context.Render<TreeSelectExpandedTest>();
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());
            string ToggleHtml() => comp.FindAll("[role=treeitem]").First().QuerySelector(".treeselect-item-toggle")!.InnerHtml;

            // Compare the drawing paths, since the icon markup is serialized differently from the constants.
            const string addPath = "M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z";
            const string removePath = "M19 13H5v-2h14v2z";
            ToggleHtml().Should().Contain(addPath).And.NotContain(removePath);
            ToggleHtml().Should().Contain("mud-primary-text");

            await comp.FindAll("[role=treeitem]").First().QuerySelector(".treeselect-item-toggle")!.ClickAsync();

            ToggleHtml().Should().Contain(removePath).And.NotContain(addPath);
        }

        /// <summary>
        /// A select without nested items keeps list semantics and gets none of the tree markup.
        /// </summary>
        [Test]
        public async Task SelectNested_FlatSelect_StaysAList()
        {
            var comp = Context.Render<TreeSelectFlatTest>();
            await comp.InvokeAsync(() => comp.FindComponent<TreeSelect<string>>().Instance.OpenMenu());

            comp.Find("[role=combobox]").GetAttribute("aria-haspopup").Should().Be("listbox");
            comp.FindAll("[role=tree]").Should().BeEmpty();
            comp.FindAll("[role=treeitem]").Should().BeEmpty();
            comp.FindAll(".treeselect-item-tree, .treeselect-item-toggle, .treeselect-item-guide").Should().BeEmpty();
        }

        /// <summary>
        /// A disabled branch can be highlighted and opened with the arrow keys, but navigating onto it or pressing Enter never selects it.
        /// </summary>
        [Test]
        public async Task SelectNested_DisabledBranch_OpensWithKeysWithoutSelecting()
        {
            var comp = Context.Render<TreeSelectDisabledBranchTest>();
            var select = comp.FindComponent<TreeSelect<string>>();
            await comp.InvokeAsync(() => select.Instance.OpenMenu());
            string?[] Rows() => comp.FindAll("[role=treeitem]").Select(x => x.TextContent.Trim()).ToArray();
            string? Active() => comp.FindAll("[role=treeitem]").SingleOrDefault(x => x.Id == comp.Find("[role=combobox]").GetAttribute("aria-activedescendant"))?.TextContent.Trim();

            await PressAsync(select, "ArrowDown");
            Active().Should().Be("Archive");
            select.Instance.Value.Should().BeNull("navigating onto a disabled branch must not select it");

            await PressAsync(select, "Enter");
            select.Instance.Value.Should().BeNull();
            IsOpen(select).Should().BeTrue("Enter does nothing on a disabled row");

            await PressAsync(select, "ArrowRight");
            Rows().Should().Equal("Archive", "Old docs", "Old photos", "Current");

            await PressAsync(select, "ArrowRight");
            Active().Should().Be("Old docs");
        }

        /// <summary>
        /// Names the checkbox icon a multi-select option renders: checked, unchecked or indeterminate.
        /// </summary>
        private static string CheckboxState(IElement option)
        {
            var path = $"d=\"{option.QuerySelectorAll("path").Last().GetAttribute("d")}\"";

            if (Icons.Material.Filled.CheckBox.Contains(path))
            {
                return "checked";
            }

            if (Icons.Material.Filled.CheckBoxOutlineBlank.Contains(path))
            {
                return "unchecked";
            }

            return Icons.Material.Filled.IndeterminateCheckBox.Contains(path) ? "indeterminate" : $"unknown icon {path}";
        }
    }
}
