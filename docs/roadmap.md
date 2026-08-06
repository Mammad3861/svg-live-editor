# SvgLiveEditor Roadmap

This roadmap records product direction. Post-v1 entries are ideas, not promises, deadlines, or a fixed delivery schedule. Priorities may change as accessibility, security, reliability, and user feedback are evaluated.

## Shipped

- **v0.1 through v0.7:** application foundations, the security-restricted preview, Inspector, sharing, templates, persistence, visual selection and movement, resizing, same-parent Arrange, and opacity.

### v0.7.1 — Editor UX stabilization (shipped)

- Source editor context menu.
- Current selection-overlay improvement.
- Compact Layer feedback.
- Visible Property help.
- Properties Undo/Redo routing.
- Editor UX stabilization.

### v0.8 — Layers and groups (shipped)

- Real Layers/Groups architecture and UI.
- Clear SVG hierarchy.
- Safe group-aware ordering.
- Visibility and lock controls.
- Address the same-parent layer limitation without unsafe implicit reparenting by exposing group boundaries.

## Pre-v1 core work

### v0.9.0 — Visual Authoring (current standalone release)

- Insert bounded basic SVG elements and empty groups.
- Duplicate exact subtrees with safe deterministic ID/reference remapping.
- Delete exact subtrees with reference and non-empty-group safeguards.
- Explicit conservative move into, out of, and between existing groups.
- Preserve one-operation Undo, source selection, expansion state, Properties, and Preview synchronization.

### v0.10.0 — Visual Composition (planned)

- Multi-selection.
- Moving multiple selected elements.
- Group and Ungroup commands.
- Alignment and distribution.
- Basic snapping.
- Evaluate safe basic path bounding-box resize after the core composition architecture is stable.

### v1.0.0 — Stable Release / stabilization

- Accessibility.
- Reliability and data-loss review.
- Persistence and recovery validation.
- Complete documentation.
- Packaging and distribution review.
- Final pre-v1 UX consistency pass.

## Post-v1 and non-blocking enhancements

These entries are optional product-direction ideas, not promises or deadlines.

1. **Advanced selection visual redesign**
   - Bring selection, handles, and interaction polish closer to modern Microsoft/Adobe-class desktop UX.
   - Richer hover, selection, move, and resize states.
   - Optional future appearance customization.
2. **Visual color editing**
   - Fill/stroke color swatches and a visual color picker.
   - HEX, RGB, and other useful representations.
   - Alpha and recent colors.
   - Safely preserve non-color SVG paint values such as `none`, `currentColor`, and `url(#gradient)`.
3. **Expanded Templates**
   - A substantially larger template library with more categories and visual variety.
   - Improved thumbnails and discovery.
   - Personal/user templates and future template extensibility.
4. **Keyboard customization**
   - Shortcut reference UI, customizable shortcuts/keymap, and conflict detection.
5. **Appearance and Theme system**
   - System (default), Light, and Dark modes.
   - System mode follows the Windows appearance preference; users may override it with Light or Dark.
   - Persist the choice per user and eventually react dynamically to Windows theme changes.
   - Consistently theme WPF chrome, menus, dialogs, AvalonEdit, Inspector, Properties, and other app-owned UI.
   - Never modify SVG artwork merely because the application theme changes.
6. **Rulers, Guides & Smart Placement**
   - Horizontal and vertical rulers with document-coordinate display.
   - Draggable guides with guide locking and hiding.
   - Snap indicators and optional smart snapping that can be disabled.
   - Smart alignment guides, equal-spacing suggestions, and center/edge alignment suggestions.
   - Placement feedback similar to modern Office, Figma, and design applications.
   - Visual property and position hints where useful, without hidden source rewrites.
7. **Advanced vector editing**
   - Path-node editing, advanced gradients, masks/effects editing, and richer transforms.
8. **Advanced exports and distribution**
   - Additional carefully audited export formats and distribution options after the stable baseline.
9. **Future integrations**
   - External design-tool workflows and advanced AI-assisted workflows.

