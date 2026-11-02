# MudBlazorTreeSelect

> [!IMPORTANT]
> **This library is temporary.** It only exists while our pull request to MudBlazor, [#14121](https://github.com/MudBlazor/MudBlazor/pull/14121), is not merged and released.
> It is a stand-in, not a separate product: it will not gain features of its own, and it is not a long-term alternative to `MudSelect`.
>
> **When the pull request is merged and shipped in a MudBlazor release:**
> 1. Move to that MudBlazor version and switch back to `MudSelect` and `MudSelectItem` (see [Migrating back to MudSelect](#migrating-back-to-mudselect)).
> 2. This repository will be archived and the NuGet package marked deprecated.
>
> **If the pull request is closed without merging**, or merged in a different shape, this library stays as it is and is the only supported way to use the feature. In that case this notice will be updated.
>
> Check the pull request for its current status before adopting this.

A tree-style `MudSelect` with expandable nested items, for use with the **normal MudBlazor NuGet package** (9.12.0 or later).

It is a renamed copy of the nested-items feature from MudBlazor pull request [#14121](https://github.com/MudBlazor/MudBlazor/pull/14121) (issues [#11869](https://github.com/MudBlazor/MudBlazor/issues/11869) and [#8542](https://github.com/MudBlazor/MudBlazor/issues/8542)). It exists so the control can be used before that work ships in MudBlazor, and it should be removed once it does (see [Migrating back to MudSelect](#migrating-back-to-mudselect)).

| This library | MudBlazor equivalent after the PR ships |
| --- | --- |
| `TreeSelect<T>` | `MudSelect<T>` |
| `TreeSelectItem<T>` | `MudSelectItem<T>` |
| `MudBlazorTreeSelect.ExpandIconPlacement` | `MudBlazor.ExpandIconPlacement` |
| `MudBlazorTreeSelect.CheckedStrategy` | `MudBlazor.CheckedStrategy` |

All parameters keep their names, defaults and behavior. `TreeSelect` inherits `MudBaseInput<T>`, so it works in a `MudForm`, with `Label`, `Variant`, `Validation` and the other input parameters, and it uses MudBlazor's own `mud-select` styling.

## Setup

1. Install the package, or reference the project:

   ```
   dotnet add package MudBlazorTreeSelect
   ```

2. Add the stylesheet once, after MudBlazor's own CSS (in `App.razor`, `index.html` or `_Host.cshtml`):

   ```html
   <link href="_content/MudBlazorTreeSelect/treeselect.css" rel="stylesheet" />
   ```

3. Import the namespace (in `_Imports.razor`):

   ```razor
   @using MudBlazorTreeSelect
   ```

MudBlazor's providers (`MudPopoverProvider` and the rest) are needed as usual.

## Usage

Put child items in the `NestedList` of a `TreeSelectItem`. When `NestedList` is set, wrap the item's own content in `ChildContent`.

```razor
<TreeSelect T="string" @bind-Value="_category" Label="Category" Variant="Variant.Outlined">
    <TreeSelectItem Value="@("Electronics")">
        <ChildContent>Electronics</ChildContent>
        <NestedList>
            <TreeSelectItem Value="@("Phones")">
                <ChildContent>Phones</ChildContent>
                <NestedList>
                    <TreeSelectItem Value="@("Smartphones")">Smartphones</TreeSelectItem>
                    <TreeSelectItem Value="@("Feature phones")">Feature phones</TreeSelectItem>
                </NestedList>
            </TreeSelectItem>
            <TreeSelectItem Value="@("Laptops")">Laptops</TreeSelectItem>
        </NestedList>
    </TreeSelectItem>
    <TreeSelectItem Value="@("Books")">Books</TreeSelectItem>
</TreeSelect>
```

A select becomes a tree only when at least one item has a `NestedList`. Without nested items it behaves like a normal `MudSelect`.

### Behavior

- A parent is a normal, selectable item. Its chevron only expands or collapses it.
- Expansion is remembered when the popup closes and reopens. When the popup opens, the ancestors of the selected item are expanded.
- A disabled item with a `NestedList` still expands and collapses, but it is never selected.
- Keyboard follows the WAI-ARIA tree pattern, and the keys act on the highlighted item, so press Down to highlight one first when nothing is selected: Right expands a collapsed item or moves to its first child, Left collapses an expanded item or moves to its parent (swapped in right-to-left layouts), and Up, Down, Home and End move through the visible rows. In single selection, Left and Right change the value with the highlight like Up and Down, unless `SelectionOnEnter` is set.
- The popup uses `role="tree"` and `role="treeitem"` with `aria-level` and `aria-expanded`, and the combobox gets `aria-haspopup="tree"`.

## Parameters

On `TreeSelect<T>`, in addition to everything `MudSelect<T>` has:

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `InitiallyExpanded` | `bool` | `false` | Expands every item with a `NestedList` the first time it is shown. |
| `ExpandIconPlacement` | `ExpandIconPlacement` | `Start` | Side of the row where the chevron is shown (`Start` or `End`). |
| `TreeLines` | `bool` | `true` | Draws connectors between nested items and their parents. |
| `ExpandOnClick` | `bool` | `false` | Also acts on a parent when its row is clicked. Single selection: a click only expands or collapses it, so only items without children can be chosen. `MultiSelection`: a click toggles its checkbox and expands it. |
| `CheckedStrategy` | `CheckedStrategy` | `All` | Which checked items `SelectedValues` reports, with `MultiSelection`. See below. |

On `TreeSelectItem<T>`:

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `NestedList` | `RenderFragment?` | `null` | Child items shown when the item is expanded. |
| `Expanded` / `ExpandedChanged` | `bool` / `EventCallback<bool>` | `false` | Two-way bindable expanded state. |
| `ExpandLessIcon` | `string` | `Icons.Material.Filled.ExpandLess` | Icon while expanded. |
| `ExpandMoreIcon` | `string` | `Icons.Material.Filled.ExpandMore` | Icon while collapsed. |
| `ExpandIconColor` | `Color` | `Color.Default` | Color of the chevron. |

### Checked strategy

- `All` (default): every item is checked independently. Checking a parent checks nothing else.
- `Child`: checking a parent checks its enabled descendants. `SelectedValues` holds only items without children.
- `Parent`: checking a parent checks its enabled descendants. `SelectedValues` holds the topmost parents whose enabled descendants are all checked, plus checked items that have no such parent.

With `Child` and `Parent`, a parent shows as indeterminate while only some of its enabled descendants are checked, and Select All follows the strategy. Disabled items are never checked by a parent. Either form is accepted when you supply `SelectedValues`.

## Differences from the MudBlazor version

This is a copy that can only use MudBlazor's public API, so a few internal details had to be replaced. None of them change the public parameters.

- **External `SelectedValues` changes and form state.** MudBlazor's `MudSelect` suppresses its "touched" and form-change bookkeeping while it applies a value that came from the parent, using an internal method of its base class. The copy suppresses only its own part. A programmatic change to `SelectedValues` may therefore mark the field as touched or trigger validation a little differently from `MudSelect`.
- **Maintenance.** The component is a copy of about 3,400 lines. Fixes made to `MudSelect` in future MudBlazor releases do not reach it.
- **Not supported:** filtering a tree, and the virtualization-style features that `MudSelect` does not have either.

The "Select all" text is still localized through MudBlazor's localization (`ILocalizationInterceptor`).

## Migrating back to MudSelect

When MudBlazor ships nested items in `MudSelect`:

1. Update `MudBlazor` to that release.
2. Replace `TreeSelect` with `MudSelect` and `TreeSelectItem` with `MudSelectItem` in your markup. Parameter names are the same. Remove `@using MudBlazorTreeSelect` or switch to MudBlazor's `ExpandIconPlacement` and `CheckedStrategy`.
3. Remove the `treeselect.css` link and this package.

## Demo app

`samples/TreeSelectDemo` is a Blazor Server app that uses `TreeSelect` the way a consumer would: it installs the normal `MudBlazor` 9.12.0 package and a locally packed `MudBlazorTreeSelect`. It has a page with every feature, plus switches for dark mode and right-to-left.

```
dotnet pack src/MudBlazorTreeSelect/MudBlazorTreeSelect.csproj -c Release -o artifacts
dotnet run --project samples/TreeSelectDemo --urls http://localhost:5093
```

`nuget.config` adds the `artifacts` folder as a package source for `MudBlazorTreeSelect` only. Repack after changing the library, and clear the cached copy (`~/.nuget/packages/mudblazortreeselect`) or bump the version so the demo picks it up.

## Development

```
dotnet build MudBlazorTreeSelect.slnx
dotnet test --project tests/MudBlazorTreeSelect.Tests/MudBlazorTreeSelect.Tests.csproj
dotnet pack src/MudBlazorTreeSelect/MudBlazorTreeSelect.csproj -c Release
```

The library targets net8.0, net9.0 and net10.0. The tests (bUnit and NUnit, run on net10.0) are the nested-select tests from the MudBlazor pull request, ported to this library.

The sources under `src/MudBlazorTreeSelect` were generated from MudBlazor's `Components/Select` folder on the `feature/select-nested-items` branch (commit `2a370d593`), and the later fixes up to commit `2b363ac47` were applied to them. They carry the renames above and these patches:

| MudBlazor internal | Replacement |
| --- | --- |
| `InternalMudLocalizer` | the public `ILocalizationInterceptor` |
| `SuppressInteractionEffectsWhileAsync` and its field | a local version that covers only this component's own state |
| `MudBaseInput.ReadValue` read from the context | `TreeSelect.CurrentValue`, which reads it inside the component |
| `FocusAsync(bool preventScroll)` override | removed |
| `IReadOnlyList.FindIndex` and `SequenceComparer` helpers | copied into `Internal/` |
| new rules in MudBlazor's `_list.scss` | `wwwroot/treeselect.css`, with classes renamed to `treeselect-item-*` |

## License

MIT, see [LICENSE](LICENSE). This is derived from MudBlazor, so MudBlazor's copyright notice is kept alongside the author's.
